using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace RoslynMCP.Tools;

public partial class RoslynTool
{
    [McpServerTool, Description("Get comprehensive documentation and inheritance information for a fully qualified type name")]
    public async Task<string> GetTypeDocumentation(
        [Description("Absolute path to the solution file")] string solutionPath,
        [Description("Fully qualified type name (e.g., TestSln.TestProject.Person)")] string fullyQualifiedTypeName)
    {
        try
        {
            _logger.LogInformation("Getting type documentation for {TypeName}", fullyQualifiedTypeName);

            var solution = await _workspaceService.GetSolutionAsync(solutionPath);
            if (solution == null)
            {
                return $"Error: Could not load solution '{solutionPath}'";
            }

            // Search for the type across all projects in the solution
            INamedTypeSymbol? targetType = null;
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                targetType = compilation.GetTypeByMetadataName(fullyQualifiedTypeName);
                if (targetType != null) break;
            }

            if (targetType == null)
            {
                return $"Error: Type '{fullyQualifiedTypeName}' not found in solution";
            }

            var result = new StringBuilder();
            result.AppendLine($"Type Documentation for: {fullyQualifiedTypeName}");
            result.AppendLine("=".PadRight(80, '='));
            result.AppendLine();

            // Use shared type information formatting with full documentation
            AppendTypeInformation(result, targetType, includeFullDocumentation: true);

            _logger.LogInformation("Type documentation retrieved successfully for {TypeName}", fullyQualifiedTypeName);
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get type documentation for {TypeName}", fullyQualifiedTypeName);
            return $"Error getting type documentation: {ex.Message}";
        }
    }

}