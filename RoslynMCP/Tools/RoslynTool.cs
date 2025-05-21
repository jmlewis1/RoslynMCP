using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RoslynMCP.Services;
using System.ComponentModel;
using System.Text;

namespace RoslynMCP.Tools;

[McpServerToolType]
public class RoslynTool
{
    private readonly ILogger<RoslynTool> _logger;
    private readonly IRoslynWorkspaceService _workspaceService;

    public RoslynTool(ILogger<RoslynTool> logger, IRoslynWorkspaceService workspaceService)
    {
        _logger = logger;
        _workspaceService = workspaceService;
    }

    [McpServerTool, Description("Load a C# solution using Roslyn and return basic information about it")]
    public string Test(string test)
    {
        return "hello world";
    }

    [McpServerTool, Description("Get detailed information including XML documentation and public interface for a variable, function, type, declaration, definition at a specific location")]
    public async Task<string> GetDetailedSymbolInfo(
        [Description("Absolute path to the solution file")] string solutionPath,
        [Description("Path to the file containing the symbol")] string filePath,
        [Description("Line number (1-based)")] int line, 
        [Description("Token to get information about")] string tokenToFind)
    {
        try
        {
            _logger.LogInformation("Getting detailed symbol info at {FilePath}:{Line}:{TokenToFind}", filePath, line, tokenToFind);

            var document = await _workspaceService.GetDocumentAsync(solutionPath, filePath);
            
            if (document == null)
            {
                return $"Error: Document '{filePath}' not found in solution";
            }

            var syntaxTree = await document.GetSyntaxTreeAsync();
            var semanticModel = await document.GetSemanticModelAsync();
            
            if (syntaxTree == null || semanticModel == null)
            {
                return "Error: Could not get syntax tree or semantic model";
            }

            // Convert line to text position
            var sourceText = await syntaxTree.GetTextAsync();
            var textLines = sourceText.Lines;
            
            if (line < 1 || line > textLines.Count)
            {
                return $"Error: Line {line} is out of range (1-{textLines.Count})";
            }

            var textLine = textLines[line - 1];
            var sourceCodeFromLine = textLine.ToString();
            var position = sourceCodeFromLine.IndexOf(tokenToFind);
            if (position < 0)
                return $"Error: Couldn't find token '{tokenToFind}' on line {line}";

            position = position + textLine.Start;

            // Get the node at the position
            var root = await syntaxTree.GetRootAsync();
            var token = root.FindToken(position);
            var node = token.Parent;
            
            if (node == null)
            {
                return "No syntax node found at the specified position";
            }

            // Get symbol information - try different approaches
            var symbol = semanticModel.GetSymbolInfo(node);
            var typeInfo = semanticModel.GetTypeInfo(node);
            
            // Initialize targetSymbol
            ISymbol? targetSymbol = null;
            if (symbol.Symbol != null)
            {
                targetSymbol = symbol.Symbol;
            }
            else if (node.IsKind(SyntaxKind.VariableDeclarator))
            {
                // For variable declarators, try GetDeclaredSymbol
                targetSymbol = semanticModel.GetDeclaredSymbol(node);
            }
            
            // If we still don't have a symbol, try parent nodes
            if (targetSymbol == null && typeInfo.Type == null)
            {
                var current = node.Parent;
                while (current != null && targetSymbol == null && typeInfo.Type == null)
                {
                    symbol = semanticModel.GetSymbolInfo(current);
                    typeInfo = semanticModel.GetTypeInfo(current);
                    if (symbol.Symbol != null)
                    {
                        targetSymbol = symbol.Symbol;
                        break;
                    }
                    current = current.Parent;
                }
            }
            
            var result = new StringBuilder();
            result.AppendLine($"Detailed Symbol Analysis at {Path.GetFileName(filePath)}:{line}:{tokenToFind}");
            result.AppendLine($"Token: '{token.ValueText}' (Kind: {token.Kind()})");
            result.AppendLine($"Node: {node.Kind()} - '{node.ToString().Trim()}'");
            result.AppendLine();

            // Type information (often more useful for getting the type details)
            ITypeSymbol? targetType = null;
            if (typeInfo.Type != null)
            {
                targetType = typeInfo.Type;
                result.AppendLine($"Type: {targetType.ToDisplayString()}");
            }

            // Show what we found
            if (targetSymbol != null)
            {
                result.AppendLine($"Direct Symbol: {targetSymbol.Name} ({targetSymbol.Kind})");
            }

            // If we don't have a direct symbol but we have a type, use the type
            if (targetSymbol == null && targetType != null)
            {
                targetSymbol = targetType;
            }

            if (targetSymbol == null)
            {
                return result.ToString() + "\nNo symbol or type information found at this location.";
            }

            result.AppendLine();
            result.AppendLine("=== SYMBOL DETAILS ===");
            
            // Get the actual type symbol if this is a variable/field/property
            ITypeSymbol? typeSymbolToAnalyze = null;
            if (targetSymbol is IFieldSymbol field)
            {
                typeSymbolToAnalyze = field.Type;
                result.AppendLine($"Variable '{field.Name}' of type: {field.Type.ToDisplayString()}");
            }
            else if (targetSymbol is ILocalSymbol local)
            {
                typeSymbolToAnalyze = local.Type;
                result.AppendLine($"Local variable '{local.Name}' of type: {local.Type.ToDisplayString()}");
            }
            else if (targetSymbol is IPropertySymbol property)
            {
                typeSymbolToAnalyze = property.Type;
                result.AppendLine($"Property '{property.Name}' of type: {property.Type.ToDisplayString()}");
            }
            else if (targetSymbol is ITypeSymbol type)
            {
                typeSymbolToAnalyze = type;
                result.AppendLine($"Type: {type.ToDisplayString()}");
            }
            else
            {
                typeSymbolToAnalyze = targetType;
                result.AppendLine($"Symbol: {targetSymbol.ToDisplayString()} ({targetSymbol.Kind})");
            }

            if (typeSymbolToAnalyze != null)
            {
                result.AppendLine();
                result.AppendLine("=== TYPE INFORMATION ===");
                
                // XML Documentation
                var xmlDocs = typeSymbolToAnalyze.GetDocumentationCommentXml();
                if (!string.IsNullOrEmpty(xmlDocs))
                {
                    result.AppendLine("XML Documentation:");
                    result.AppendLine(xmlDocs);
                }
                else
                {
                    result.AppendLine("No XML documentation found.");
                }

                result.AppendLine();
                result.AppendLine("=== PUBLIC INTERFACE ===");
                result.AppendLine($"Type: {typeSymbolToAnalyze.TypeKind} {typeSymbolToAnalyze.ToDisplayString()}");
                result.AppendLine($"Namespace: {typeSymbolToAnalyze.ContainingNamespace?.ToDisplayString() ?? "None"}");
                result.AppendLine($"Accessibility: {typeSymbolToAnalyze.DeclaredAccessibility}");

                // Public members
                var publicMembers = typeSymbolToAnalyze.GetMembers()
                    .Where(m => m.DeclaredAccessibility == Accessibility.Public)
                    .OrderBy(m => m.Kind)
                    .ThenBy(m => m.Name);

                if (publicMembers.Any())
                {
                    result.AppendLine();
                    result.AppendLine("Public Members:");
                    
                    foreach (var member in publicMembers)
                    {
                        result.AppendLine($"  {member.Kind}: {member.ToDisplayString()}");
                        
                        // Include XML docs for public members
                        var memberXmlDocs = member.GetDocumentationCommentXml();
                        if (!string.IsNullOrEmpty(memberXmlDocs))
                        {
                            var lines = memberXmlDocs.Split('\n');
                            foreach (var docLine in lines)
                            {
                                if (!string.IsNullOrWhiteSpace(docLine))
                                {
                                    result.AppendLine($"    /// {docLine.Trim()}");
                                }
                            }
                        }
                        result.AppendLine();
                    }
                }

                // Base type and interfaces
                if (typeSymbolToAnalyze.BaseType != null && 
                    typeSymbolToAnalyze.BaseType.SpecialType != SpecialType.System_Object)
                {
                    result.AppendLine($"Base Type: {typeSymbolToAnalyze.BaseType.ToDisplayString()}");
                }

                var interfaces = typeSymbolToAnalyze.Interfaces;
                if (interfaces.Length > 0)
                {
                    result.AppendLine("Implemented Interfaces:");
                    foreach (var iface in interfaces)
                    {
                        result.AppendLine($"  - {iface.ToDisplayString()}");
                    }
                }
            }

            _logger.LogInformation("Detailed symbol info retrieved successfully for {FilePath}:{Line}:{TokenToFind}", filePath, line, tokenToFind);
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get detailed symbol info: {FilePath}:{Line}:{TokenToFind}", filePath, line, tokenToFind);
            return $"Error getting detailed symbol information: {ex.Message}";
        }
    }
}