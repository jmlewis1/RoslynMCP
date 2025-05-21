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
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            
            var projectCount = solution.Projects.Count();
            var projectNames = solution.Projects.Select(p => p.Name).ToList();
            
            var result = $"Solution loaded successfully!\nPath: {solutionPath}\nProject Count: {projectCount}\nProjects: {string.Join(", ", projectNames)}";
            
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