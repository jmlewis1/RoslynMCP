using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace RoslynMCP.Services;

/// <summary>
/// Interface for RoslynWorkspaceService to enable mocking in unit tests
/// </summary>
public interface IRoslynWorkspaceService
{
    /// <summary>
    /// Gets or creates a workspace for the specified solution path
    /// </summary>
    Task<Workspace> GetWorkspaceAsync(string solutionPath);
    
    /// <summary>
    /// Gets the Solution instance for the specified solution path
    /// </summary>
    Task<Solution> GetSolutionAsync(string solutionPath);
    
    /// <summary>
    /// Gets a document from a solution by its file path
    /// </summary>
    Task<Document?> GetDocumentAsync(string solutionPath, string filePath);
}