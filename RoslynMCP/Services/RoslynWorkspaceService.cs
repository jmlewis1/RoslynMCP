using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;

namespace RoslynMCP.Services;

/// <summary>
/// Service responsible for creating, caching, and managing Roslyn MSBuildWorkspace instances
/// with automatic file change detection and workspace updating.
/// </summary>
public class RoslynWorkspaceService : IRoslynWorkspaceService, IDisposable
{
    private readonly ILogger<RoslynWorkspaceService> _logger;
    private readonly ConcurrentDictionary<string, WorkspaceInfo> _workspaces = new();
    private bool _disposed = false;

    public RoslynWorkspaceService(ILogger<RoslynWorkspaceService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates a workspace for the specified solution path. If the workspace
    /// already exists in the cache, it is returned. Otherwise, a new workspace is created.
    /// </summary>
    /// <param name="solutionPath">The absolute path to the solution file.</param>
    /// <returns>The MSBuildWorkspace instance for the solution.</returns>
    public async Task<MSBuildWorkspace> GetWorkspaceAsync(string solutionPath)
    {
        var normalizedPath = Path.GetFullPath(solutionPath);
        
        if (_workspaces.TryGetValue(normalizedPath, out var workspaceInfo))
        {
            _logger.LogInformation("Using cached workspace for solution: {SolutionPath}", normalizedPath);
            return workspaceInfo.Workspace;
        }

        _logger.LogInformation("Creating new workspace for solution: {SolutionPath}", normalizedPath);
        return await CreateAndCacheWorkspaceAsync(normalizedPath);
    }

    /// <summary>
    /// Gets the Solution instance for the specified solution path.
    /// </summary>
    /// <param name="solutionPath">The absolute path to the solution file.</param>
    /// <returns>The Solution instance.</returns>
    public async Task<Solution> GetSolutionAsync(string solutionPath)
    {
        var workspace = await GetWorkspaceAsync(solutionPath);
        return workspace.CurrentSolution;
    }

    /// <summary>
    /// Gets a document from a solution by its file path.
    /// </summary>
    /// <param name="solutionPath">The solution path containing the document.</param>
    /// <param name="filePath">The file path of the document to retrieve.</param>
    /// <returns>The Document instance if found, otherwise null.</returns>
    public async Task<Document?> GetDocumentAsync(string solutionPath, string filePath)
    {
        var solution = await GetSolutionAsync(solutionPath);
        var fileName = Path.GetFileName(filePath);
        
        return solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => Path.GetFileName(d.FilePath) == fileName);
    }

    private async Task<MSBuildWorkspace> CreateAndCacheWorkspaceAsync(string solutionPath)
    {
        var workspace = MSBuildWorkspace.Create();
        
        // Handle workspace diagnostic events
        workspace.WorkspaceFailed += (sender, e) =>
        {
            _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}", e.Diagnostic.Kind, e.Diagnostic.Message);
        };

        try
        {
            // Load the solution
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            
            // Log diagnostic information
            var diagnostics = workspace.Diagnostics.Where(d => d.Kind == WorkspaceDiagnosticKind.Failure).ToList();
            if (diagnostics.Any())
            {
                foreach (var diagnostic in diagnostics)
                {
                    _logger.LogWarning("Workspace failure: {Message}", diagnostic.Message);
                }
                _logger.LogWarning("{Count} diagnostic(s) reported when loading solution {SolutionPath}", 
                    diagnostics.Count, solutionPath);
            }

            var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
            var workspaceInfo = new WorkspaceInfo(workspace, solution);
            
            // Set up file watching
            SetupFileWatchers(workspaceInfo, solutionDirectory);
            
            // Cache the workspace
            _workspaces[solutionPath] = workspaceInfo;
            
            _logger.LogInformation("Solution loaded successfully with {ProjectCount} projects", solution.Projects.Count());
            
            return workspace;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load solution: {SolutionPath}", solutionPath);
            throw;
        }
    }

    private void SetupFileWatchers(WorkspaceInfo workspaceInfo, string rootDirectory)
    {
        _logger.LogInformation("Setting up file watchers for directory: {Directory}", rootDirectory);
        
        // Create watcher for the root directory
        var rootWatcher = CreateFileSystemWatcher(rootDirectory, workspaceInfo);
        workspaceInfo.FileWatchers.Add(rootWatcher);
        
        // Recursively add watchers for subdirectories
        foreach (var directory in Directory.GetDirectories(rootDirectory, "*", SearchOption.AllDirectories))
        {
            var watcher = CreateFileSystemWatcher(directory, workspaceInfo);
            workspaceInfo.FileWatchers.Add(watcher);
        }
    }

    private FileSystemWatcher CreateFileSystemWatcher(string directory, WorkspaceInfo workspaceInfo)
    {
        var watcher = new FileSystemWatcher
        {
            Path = directory,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = "*.cs", // Monitor C# files
            IncludeSubdirectories = false, // Each subdirectory gets its own watcher
            EnableRaisingEvents = true
        };

        // Handle file changes
        watcher.Changed += (sender, e) => OnFileChanged(e.FullPath, workspaceInfo);
        watcher.Created += (sender, e) => OnFileCreated(e.FullPath, workspaceInfo);
        watcher.Deleted += (sender, e) => OnFileDeleted(e.FullPath, workspaceInfo);
        watcher.Renamed += (sender, e) => OnFileRenamed(e.OldFullPath, e.FullPath, workspaceInfo);
        
        // Handle directory changes
        watcher.Created += (sender, e) => 
        {
            if (Directory.Exists(e.FullPath))
            {
                OnDirectoryCreated(e.FullPath, workspaceInfo);
            }
        };

        _logger.LogDebug("File watcher created for directory: {Directory}", directory);
        return watcher;
    }

    private void OnFileChanged(string filePath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            _logger.LogInformation("File changed: {FilePath}", filePath);
            
            // Queue file updates to avoid IO conflicts
            Task.Run(async () => 
            {
                try
                {
                    await UpdateDocumentInWorkspaceAsync(filePath, workspaceInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating document in workspace: {FilePath}", filePath);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file change event: {FilePath}", filePath);
        }
    }

    private void OnFileCreated(string filePath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            if (Directory.Exists(filePath))
            {
                OnDirectoryCreated(filePath, workspaceInfo);
            }
            else
            {
                _logger.LogInformation("File created: {FilePath}", filePath);
                
                // Queue file updates
                Task.Run(async () => 
                {
                    try
                    {
                        await AddDocumentToWorkspaceAsync(filePath, workspaceInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error adding document to workspace: {FilePath}", filePath);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file creation event: {FilePath}", filePath);
        }
    }

    private void OnFileDeleted(string filePath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            _logger.LogInformation("File deleted: {FilePath}", filePath);
            
            // Queue file updates
            Task.Run(async () => 
            {
                try
                {
                    await RemoveDocumentFromWorkspaceAsync(filePath, workspaceInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing document from workspace: {FilePath}", filePath);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file deletion event: {FilePath}", filePath);
        }
    }

    private void OnFileRenamed(string oldPath, string newPath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            _logger.LogInformation("File renamed from {OldPath} to {NewPath}", oldPath, newPath);
            
            // Queue file updates
            Task.Run(async () => 
            {
                try
                {
                    // Handle as a delete followed by a create
                    await RemoveDocumentFromWorkspaceAsync(oldPath, workspaceInfo);
                    await AddDocumentToWorkspaceAsync(newPath, workspaceInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling file rename from {OldPath} to {NewPath}", oldPath, newPath);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file rename event from {OldPath} to {NewPath}", oldPath, newPath);
        }
    }

    private void OnDirectoryCreated(string directoryPath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            _logger.LogInformation("Directory created: {DirectoryPath}", directoryPath);
            
            // Create a new watcher for this directory
            var watcher = CreateFileSystemWatcher(directoryPath, workspaceInfo);
            workspaceInfo.FileWatchers.Add(watcher);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling directory creation event: {DirectoryPath}", directoryPath);
        }
    }

    private async Task UpdateDocumentInWorkspaceAsync(string filePath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var solution = workspaceInfo.Workspace.CurrentSolution;
            
            // Find the document in the solution
            var document = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => Path.GetFileName(d.FilePath) == fileName);
            
            if (document != null)
            {
                _logger.LogDebug("Updating document in workspace: {FilePath}", filePath);
                
                // Read the updated content
                var fileContent = await File.ReadAllTextAsync(filePath);
                var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(fileContent);
                
                // Create a new document with the updated text
                var newDocument = document.WithText(sourceText);
                
                // Update the solution
                var newSolution = solution.WithDocumentText(document.Id, sourceText);
                
                // Apply the changes to the workspace
                if (workspaceInfo.Workspace.TryApplyChanges(newSolution))
                {
                    _logger.LogInformation("Document updated successfully: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("Failed to apply document changes to workspace: {FilePath}", filePath);
                }
            }
            else
            {
                _logger.LogWarning("Document not found in workspace: {FilePath}", filePath);
                await AddDocumentToWorkspaceAsync(filePath, workspaceInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document in workspace: {FilePath}", filePath);
        }
    }

    private async Task AddDocumentToWorkspaceAsync(string filePath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            // Get the file extension to determine document type
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".cs")
            {
                _logger.LogDebug("Ignoring non-C# file: {FilePath}", filePath);
                return;
            }

            var solution = workspaceInfo.Workspace.CurrentSolution;
            var fileName = Path.GetFileName(filePath);
            var directoryName = Path.GetDirectoryName(filePath)!;

            // Find the appropriate project for this file
            var project = FindBestMatchingProject(solution, directoryName);
            
            if (project != null)
            {
                _logger.LogDebug("Adding document to project {ProjectName}: {FilePath}", project.Name, filePath);
                
                // Read the file content
                var fileContent = await File.ReadAllTextAsync(filePath);
                var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(fileContent);
                
                // Add the document to the project
                var newProject = project.AddDocument(fileName, sourceText, filePath: filePath).Project;
                
                // Update the solution
                var newSolution = newProject.ParseOptions != null 
                    ? solution.WithProjectParseOptions(newProject.Id, newProject.ParseOptions)
                    : solution;
                
                // Apply the changes to the workspace
                if (workspaceInfo.Workspace.TryApplyChanges(newSolution))
                {
                    _logger.LogInformation("Document added successfully: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("Failed to add document to workspace: {FilePath}", filePath);
                }
            }
            else
            {
                _logger.LogWarning("No suitable project found for document: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding document to workspace: {FilePath}", filePath);
        }
    }

    private Task RemoveDocumentFromWorkspaceAsync(string filePath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            var solution = workspaceInfo.Workspace.CurrentSolution;
            var fileName = Path.GetFileName(filePath);
            
            // Find the document in the solution
            var document = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => Path.GetFileName(d.FilePath) == fileName);
            
            if (document != null)
            {
                _logger.LogDebug("Removing document from workspace: {FilePath}", filePath);
                
                // Remove the document from the solution
                var newSolution = solution.RemoveDocument(document.Id);
                
                // Apply the changes to the workspace
                if (workspaceInfo.Workspace.TryApplyChanges(newSolution))
                {
                    _logger.LogInformation("Document removed successfully: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("Failed to remove document from workspace: {FilePath}", filePath);
                }
            }
            else
            {
                _logger.LogDebug("Document not found in workspace: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing document from workspace: {FilePath}", filePath);
        }
        
        return Task.CompletedTask;
    }

    private Project? FindBestMatchingProject(Solution solution, string directoryPath)
    {
        // Try to find a project that has a matching directory structure
        foreach (var project in solution.Projects)
        {
            if (project.FilePath != null)
            {
                var projectDirectory = Path.GetDirectoryName(project.FilePath);
                if (directoryPath.StartsWith(projectDirectory!, StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }
        }

        // Fallback to the first project if no match is found
        return solution.Projects.FirstOrDefault();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var workspaceInfo in _workspaces.Values)
                {
                    foreach (var watcher in workspaceInfo.FileWatchers)
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                    
                    workspaceInfo.Workspace.Dispose();
                }
                
                _workspaces.Clear();
            }

            _disposed = true;
        }
    }

    private class WorkspaceInfo
    {
        public MSBuildWorkspace Workspace { get; }
        public Solution OriginalSolution { get; }
        public List<FileSystemWatcher> FileWatchers { get; } = new();

        public WorkspaceInfo(MSBuildWorkspace workspace, Solution solution)
        {
            Workspace = workspace;
            OriginalSolution = solution;
        }
    }
}