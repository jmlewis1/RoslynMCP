using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using RoslynMCP.Utils;
using System.Collections.Concurrent;
using System.IO;

namespace RoslynMCP.Services;

/// <summary>
/// Service responsible for creating, caching, and managing Roslyn MSBuildWorkspace instances
/// with robust file change detection and workspace updating.
/// </summary>
public class RoslynWorkspaceService : IRoslynWorkspaceService, IDisposable
{
    private readonly ILogger<RoslynWorkspaceService> _logger;
    private readonly ConcurrentDictionary<string, WorkspaceInfo> _workspaces = new();
    private readonly ConcurrentDictionary<string, DateTime> _fileOperationTracker = new();
    private readonly TimeSpan _fileOperationDebounceTime = TimeSpan.FromMilliseconds(500);
    private bool _disposed = false;
    
    private readonly HashSet<string> _watchedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj"
    };

    private readonly HashSet<string> _ignoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vs",
        "obj",
        "bin",
        ".git",
        ".claude",
        "node_modules"
    };

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
    public async Task<Workspace> GetWorkspaceAsync(string solutionPath)
    {
        var nativePath = PathConverter.ConvertToNativePath(solutionPath);
        var normalizedPath = Path.GetFullPath(nativePath);
        
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
        
        // Normalize the file path for comparison
        var normalizedFilePath = Path.GetFullPath(filePath);
        
        return solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(
                Path.GetFullPath(d.FilePath ?? ""), 
                normalizedFilePath, 
                StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<AdhocWorkspace> CloneSolutionToAdhocAsync(Solution sourceSolution)
    {
        var adhocWorkspace = new AdhocWorkspace();

        foreach (var project in sourceSolution.Projects)
        {
            var newProjectId = ProjectId.CreateNewId(debugName: project.Name);

            var projectInfo = ProjectInfo.Create(
                id: newProjectId,
                version: project.Version,
                name: project.Name,
                assemblyName: project.AssemblyName,
                language: project.Language,
                filePath: project.FilePath,
                outputFilePath: project.OutputFilePath,
                compilationOptions: project.CompilationOptions,
                parseOptions: project.ParseOptions,
                metadataReferences: project.MetadataReferences,
                analyzerReferences: project.AnalyzerReferences
            );

            adhocWorkspace.AddProject(projectInfo);

            foreach (var document in project.Documents)
            {
                var text = await document.GetTextAsync();
                adhocWorkspace.AddDocument(
                    DocumentInfo.Create(
                        DocumentId.CreateNewId(newProjectId),
                        name: document.Name,
                        loader: TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create())),
                        filePath: document.FilePath
                    )
                );
            }
        }

        return adhocWorkspace;
    }

    private async Task<Workspace> CreateAndCacheWorkspaceAsync(string solutionPath)
    {
        var msBuildWorkspace = MSBuildWorkspace.Create();
        
        // Handle workspace diagnostic events
        msBuildWorkspace.WorkspaceFailed += (sender, e) =>
        {
            _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}", e.Diagnostic.Kind, e.Diagnostic.Message);
        };

        try
        {
            Solution solution;
            // Load the solution
            try
            {
                solution = await msBuildWorkspace.OpenSolutionAsync(solutionPath);
            }
            catch(Exception ex)
            {
                // Could be an issue of WSL/Windows path mixing try the other type of path
                _logger.LogWarning(ex, "Failed to load solution with original path, trying alternative path format");
                solutionPath = PathConverter.ToOtherPath(solutionPath);
                solution = await msBuildWorkspace.OpenSolutionAsync(solutionPath);
            }

            // Log diagnostic information
            var diagnostics = msBuildWorkspace.Diagnostics.Where(d => d.Kind == WorkspaceDiagnosticKind.Failure).ToList();
            if (diagnostics.Any())
            {
                foreach (var diagnostic in diagnostics)
                {
                    _logger.LogWarning("Workspace failure: {Message}", diagnostic.Message);
                }
                _logger.LogWarning("{Count} diagnostic(s) reported when loading solution {SolutionPath}", 
                    diagnostics.Count, solutionPath);
            }

            // Create an AdhocWorkspace from the MSBuildWorkspace
            var workspace = await CloneSolutionToAdhocAsync(solution);

            var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
            var workspaceInfo = new WorkspaceInfo(workspace, solution, solutionPath);
            
            // Set up file watching
            SetupFileWatcher(workspaceInfo, solutionDirectory);
            
            // Cache the workspace
            _workspaces[solutionPath] = workspaceInfo;
            
            _logger.LogInformation("Solution loaded successfully with {ProjectCount} projects", solution.Projects.Count());
            
            return workspace;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load solution: {SolutionPath}", solutionPath);
            msBuildWorkspace.Dispose();
            throw;
        }
    }

    private void SetupFileWatcher(WorkspaceInfo workspaceInfo, string rootDirectory)
    {
        _logger.LogInformation("Setting up file watcher for directory: {Directory}", rootDirectory);
        
        var watcher = new FileSystemWatcher
        {
            Path = rootDirectory,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            Filter = "*.*",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        // Use a single handler for all events with debouncing
        watcher.Changed += (sender, e) => HandleFileSystemEvent(e.FullPath, FileOperation.Changed, workspaceInfo);
        watcher.Created += (sender, e) => HandleFileSystemEvent(e.FullPath, FileOperation.Created, workspaceInfo);
        watcher.Deleted += (sender, e) => HandleFileSystemEvent(e.FullPath, FileOperation.Deleted, workspaceInfo);
        watcher.Renamed += (sender, e) => HandleRenameEvent(e.OldFullPath, e.FullPath, workspaceInfo);
        
        // Handle errors
        watcher.Error += (sender, e) =>
        {
            _logger.LogError(e.GetException(), "FileSystemWatcher error");
        };
        
        workspaceInfo.FileWatcher = watcher;
        _logger.LogInformation("File watcher created for directory: {Directory}", rootDirectory);
    }

    private void HandleFileSystemEvent(string path, FileOperation operation, WorkspaceInfo workspaceInfo)
    {
        try
        {
            // Check if this is a directory
            bool isDirectory = false;
            try
            {
                isDirectory = Directory.Exists(path) && !File.Exists(path);
            }
            catch { }

            // Handle directory events
            if (isDirectory)
            {
                if (operation == FileOperation.Created)
                {
                    _logger.LogInformation("Directory created: {Path}", path);
                }
                else if (operation == FileOperation.Deleted)
                {
                    _logger.LogInformation("Directory deleted: {Path}", path);
                    // Remove all documents from this directory
                    Task.Run(async () => await RemoveDocumentsFromDirectoryAsync(path, workspaceInfo));
                }
                return;
            }

            // Ignore non-watched extensions
            var extension = Path.GetExtension(path);
            if (!_watchedExtensions.Contains(extension))
            {
                return;
            }

            // Ignore files in ignored directories
            if (IsInIgnoredDirectory(path))
            {
                return;
            }

            // Debounce file operations
            var key = $"{path}:{operation}";
            var now = DateTime.UtcNow;
            
            if (_fileOperationTracker.TryGetValue(key, out var lastOperation))
            {
                if (now - lastOperation < _fileOperationDebounceTime)
                {
                    _logger.LogDebug("Debouncing {Operation} for {Path}", operation, path);
                    _fileOperationTracker[key] = now;
                    return;
                }
            }
            
            _fileOperationTracker[key] = now;

            // Log the operation
            _logger.LogInformation("File {Operation}: {Path}", operation, path);

            // Handle the file operation
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(100); // Small delay to ensure file operations complete
                    
                    switch (operation)
                    {
                        case FileOperation.Created:
                            await HandleFileCreatedAsync(path, workspaceInfo);
                            break;
                        case FileOperation.Changed:
                            await HandleFileChangedAsync(path, workspaceInfo);
                            break;
                        case FileOperation.Deleted:
                            await HandleFileDeletedAsync(path, workspaceInfo);
                            break;
                    }
                    
                    // Clean up old entries from the tracker
                    CleanupFileOperationTracker();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling {Operation} for file: {Path}", operation, path);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleFileSystemEvent for {Path}", path);
        }
    }

    private void HandleRenameEvent(string oldPath, string newPath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            var extension = Path.GetExtension(newPath);
            if (!_watchedExtensions.Contains(extension))
            {
                return;
            }

            if (IsInIgnoredDirectory(newPath))
            {
                return;
            }

            _logger.LogInformation("File renamed from {OldPath} to {NewPath}", oldPath, newPath);

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(100);
                    // Remove old and add new
                    await HandleFileDeletedAsync(oldPath, workspaceInfo);
                    await HandleFileCreatedAsync(newPath, workspaceInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling rename from {OldPath} to {NewPath}", oldPath, newPath);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleRenameEvent");
        }
    }

    private async Task HandleFileCreatedAsync(string filePath, WorkspaceInfo workspaceInfo)
    {
        // For .cs files, add to workspace
        if (Path.GetExtension(filePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            await AddDocumentToWorkspaceAsync(filePath, workspaceInfo);
        }
    }

    private async Task HandleFileChangedAsync(string filePath, WorkspaceInfo workspaceInfo)
    {
        // For .cs files, update in workspace
        if (Path.GetExtension(filePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedPath = Path.GetFullPath(filePath);
            var document = await GetDocumentByFullPath(workspaceInfo.Workspace.CurrentSolution, normalizedPath);
            
            if (document != null)
            {
                await UpdateDocumentInWorkspaceAsync(filePath, document, workspaceInfo);
            }
            else
            {
                // If document doesn't exist, try to add it
                _logger.LogDebug("Document not found for changed file, attempting to add: {Path}", filePath);
                await AddDocumentToWorkspaceAsync(filePath, workspaceInfo);
            }
        }
    }

    private async Task HandleFileDeletedAsync(string filePath, WorkspaceInfo workspaceInfo)
    {
        // For .cs files, remove from workspace
        if (Path.GetExtension(filePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            await RemoveDocumentFromWorkspaceAsync(filePath, workspaceInfo);
        }
    }

    private async Task AddDocumentToWorkspaceAsync(string filePath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(filePath);
            var solution = workspaceInfo.Workspace.CurrentSolution;
            
            // Check if document already exists
            var existingDoc = await GetDocumentByFullPath(solution, normalizedPath);
            if (existingDoc != null)
            {
                _logger.LogDebug("Document already exists in workspace: {Path}", filePath);
                return;
            }

            // Find the appropriate project
            var project = FindBestMatchingProject(solution, Path.GetDirectoryName(normalizedPath)!);
            if (project == null)
            {
                _logger.LogWarning("No suitable project found for document: {Path}", filePath);
                return;
            }

            // Read file content with retry logic
            string fileContent = string.Empty;
            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    fileContent = await File.ReadAllTextAsync(filePath);
                    break;
                }
                catch (IOException ioEx) when (retryCount > 1)
                {
                    _logger.LogDebug(ioEx, "File locked, retrying read for {Path}", filePath);
                    retryCount--;
                    await Task.Delay(200);
                }
            }

            var fileName = Path.GetFileName(filePath);
            var documentId = DocumentId.CreateNewId(project.Id);
            var newSolution = solution.AddDocument(documentId, fileName, fileContent, filePath: normalizedPath);

            if (workspaceInfo.Workspace.TryApplyChanges(newSolution))
            {
                _logger.LogInformation("Document added successfully: {Path}", filePath);
            }
            else
            {
                _logger.LogWarning("Failed to add document to workspace: {Path}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding document to workspace: {Path}", filePath);
        }
    }

    private async Task UpdateDocumentInWorkspaceAsync(string filePath, Document document, WorkspaceInfo workspaceInfo)
    {
        try
        {
            // Read file content with retry logic
            string fileContent = string.Empty;
            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    fileContent = await File.ReadAllTextAsync(filePath);
                    break;
                }
                catch (IOException ioEx) when (retryCount > 1)
                {
                    _logger.LogDebug(ioEx, "File locked, retrying read for {Path}", filePath);
                    retryCount--;
                    await Task.Delay(200);
                }
            }

            var sourceText = Microsoft.CodeAnalysis.Text.SourceText.From(fileContent);
            var newSolution = document.Project.Solution.WithDocumentText(document.Id, sourceText);

            if (workspaceInfo.Workspace.TryApplyChanges(newSolution))
            {
                _logger.LogInformation("Document updated successfully: {Path}", filePath);
            }
            else
            {
                _logger.LogWarning("Failed to update document in workspace: {Path}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document in workspace: {Path}", filePath);
        }
    }

    private async Task RemoveDocumentFromWorkspaceAsync(string filePath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(filePath);
            var solution = workspaceInfo.Workspace.CurrentSolution;
            var document = await GetDocumentByFullPath(solution, normalizedPath);

            if (document != null)
            {
                var newSolution = solution.RemoveDocument(document.Id);
                
                if (workspaceInfo.Workspace.TryApplyChanges(newSolution))
                {
                    _logger.LogInformation("Document removed successfully: {Path}", filePath);
                }
                else
                {
                    _logger.LogWarning("Failed to remove document from workspace: {Path}", filePath);
                }
            }
            else
            {
                _logger.LogDebug("Document not found in workspace: {Path}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing document from workspace: {Path}", filePath);
        }
    }

    private async Task RemoveDocumentsFromDirectoryAsync(string directoryPath, WorkspaceInfo workspaceInfo)
    {
        try
        {
            var normalizedDirPath = Path.GetFullPath(directoryPath);
            var solution = workspaceInfo.Workspace.CurrentSolution;
            var documentsToRemove = new List<Document>();

            foreach (var project in solution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    if (document.FilePath != null && 
                        Path.GetFullPath(document.FilePath).StartsWith(normalizedDirPath, StringComparison.OrdinalIgnoreCase))
                    {
                        documentsToRemove.Add(document);
                    }
                }
            }

            foreach (var document in documentsToRemove)
            {
                _logger.LogDebug("Removing document from deleted directory: {Path}", document.FilePath);
                await RemoveDocumentFromWorkspaceAsync(document.FilePath!, workspaceInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing documents from directory: {Path}", directoryPath);
        }
    }

    private async Task<Document?> GetDocumentByFullPath(Solution solution, string fullPath)
    {
        return await Task.Run(() =>
        {
            return solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath != null && 
                    string.Equals(Path.GetFullPath(d.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));
        });
    }

    private Project? FindBestMatchingProject(Solution solution, string directoryPath)
    {
        var normalizedDirPath = Path.GetFullPath(directoryPath);
        Project? bestMatch = null;
        int bestMatchLength = 0;

        foreach (var project in solution.Projects)
        {
            if (project.FilePath != null)
            {
                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(project.FilePath))!;
                if (normalizedDirPath.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    if (projectDirectory.Length > bestMatchLength)
                    {
                        bestMatch = project;
                        bestMatchLength = projectDirectory.Length;
                    }
                }
            }
        }

        return bestMatch ?? solution.Projects.FirstOrDefault();
    }

    private bool IsInIgnoredDirectory(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        var parts = normalizedPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        return parts.Any(part => _ignoredDirectories.Contains(part));
    }

    private void CleanupFileOperationTracker()
    {
        var cutoffTime = DateTime.UtcNow - TimeSpan.FromMinutes(5);
        var keysToRemove = _fileOperationTracker
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _fileOperationTracker.TryRemove(key, out _);
        }
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
                    workspaceInfo.FileWatcher?.Dispose();
                    workspaceInfo.Workspace?.Dispose();
                }
                
                _workspaces.Clear();
                _fileOperationTracker.Clear();
            }

            _disposed = true;
        }
    }

    private enum FileOperation
    {
        Created,
        Changed,
        Deleted
    }

    private class WorkspaceInfo
    {
        public Workspace Workspace { get; }
        public Solution OriginalSolution { get; }
        public string SolutionFilePath { get; }
        public FileSystemWatcher? FileWatcher { get; set; }

        public WorkspaceInfo(Workspace workspace, Solution solution, string solutionFilePath)
        {
            Workspace = workspace;
            OriginalSolution = solution;
            SolutionFilePath = solutionFilePath;
        }
    }
}