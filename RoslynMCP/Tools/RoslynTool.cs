using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace RoslynMCP.Tools;

[McpServerToolType]
public sealed class RoslynTool
{
    private readonly ILogger<RoslynTool> _logger;

    public RoslynTool(ILogger<RoslynTool> logger)
    {
        _logger = logger;
    }

    [McpServerTool, Description("Load a C# solution using Roslyn and return basic information about it")]
    public async Task<string> LoadSolution(string solutionPath)
    {
        try
        {
            _logger.LogInformation("Loading solution: {SolutionPath}", solutionPath);

            using var workspace = MSBuildWorkspace.Create();
            
            // Handle workspace diagnostic events to log any issues
            workspace.WorkspaceFailed += (sender, e) =>
            {
                _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}", e.Diagnostic.Kind, e.Diagnostic.Message);
            };
            
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            
            var projectCount = solution.Projects.Count();
            var projectNames = solution.Projects.Select(p => p.Name).ToList();
            
            // Log diagnostic information
            var diagnostics = workspace.Diagnostics.Where(d => d.Kind == WorkspaceDiagnosticKind.Failure).ToList();
            if (diagnostics.Any())
            {
                foreach (var diagnostic in diagnostics)
                {
                    _logger.LogWarning("Workspace failure: {Message}", diagnostic.Message);
                }
            }
            
            var result = $"Solution loaded successfully!\nPath: {solutionPath}\nProject Count: {projectCount}\nProjects: {string.Join(", ", projectNames)}";
            
            if (diagnostics.Any())
            {
                result += $"\nWarnings: {diagnostics.Count} diagnostic(s) reported";
            }
            
            _logger.LogInformation("Solution loaded successfully with {ProjectCount} projects", projectCount);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load solution: {SolutionPath}", solutionPath);
            return $"Error loading solution: {ex.Message}";
        }
    }
}