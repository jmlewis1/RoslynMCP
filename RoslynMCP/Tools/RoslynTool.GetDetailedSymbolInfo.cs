using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace RoslynMCP.Tools;

public partial class RoslynTool
{
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
            
            // Find the token on the line using proper token enumeration
            var position = await FindTokenPositionOnLine(syntaxTree, textLine, tokenToFind);
            if (position < 0)
                return $"Error: Couldn't find token '{tokenToFind}' on line {line}";

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
            else if (node.IsKind(SyntaxKind.VariableDeclarator) || node.IsKind(SyntaxKind.Parameter))
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
            else if (targetSymbol is IParameterSymbol parameter)
            {
                typeSymbolToAnalyze = parameter.Type;
                result.AppendLine($"Parameter: {parameter.ToDisplayString()}");
            }
            else
            {
                typeSymbolToAnalyze = targetType;
                result.AppendLine($"Symbol: {targetSymbol.ToDisplayString()} ({targetSymbol.Kind})");
            }

            if (typeSymbolToAnalyze != null)
            {
                result.AppendLine();
                
                // Get the fully qualified type name and call GetTypeDocumentation
                var fullyQualifiedName = typeSymbolToAnalyze.ContainingNamespace?.IsGlobalNamespace == false 
                    ? $"{typeSymbolToAnalyze.ContainingNamespace.ToDisplayString()}.{typeSymbolToAnalyze.Name}"
                    : typeSymbolToAnalyze.Name;
                
                result.AppendLine($"=== DETAILED TYPE INFORMATION ===");
                result.AppendLine("Getting comprehensive type documentation...");
                result.AppendLine();
                
                // Call GetTypeDocumentation to get the full analysis
                var typeDocumentation = await GetTypeDocumentation(solutionPath, fullyQualifiedName);
                if (!typeDocumentation.StartsWith("Error:"))
                {
                    // Extract the content after the header to avoid duplication
                    var lines = typeDocumentation.Split('\n');
                    var startIndex = 0;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("=== BASIC INFORMATION ===") || 
                            lines[i].StartsWith("=== TYPE INFORMATION ==="))
                        {
                            startIndex = i;
                            break;
                        }
                    }
                    
                    for (int i = startIndex; i < lines.Length; i++)
                    {
                        result.AppendLine(lines[i]);
                    }
                }
                else
                {
                    // Fallback to the old method if GetTypeDocumentation fails
                    result.AppendLine("Failed to get detailed type documentation, using fallback:");
                    AppendTypeInformation(result, typeSymbolToAnalyze, includeFullDocumentation: true);
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

    /// <summary>
    /// Finds the position of a specific token on a given line using proper token enumeration
    /// </summary>
    /// <param name="syntaxTree">The syntax tree to search in</param>
    /// <param name="textLine">The text line to search on</param>
    /// <param name="tokenToFind">The token text to find (case sensitive)</param>
    /// <returns>The absolute position of the token, or -1 if not found</returns>
    private static async Task<int> FindTokenPositionOnLine(SyntaxTree syntaxTree, TextLine textLine, string tokenToFind)
    {
        try
        {
            var root = await syntaxTree.GetRootAsync();
            
            // Get all tokens that intersect with this line
            var lineSpan = textLine.Span;
            var tokensOnLine = root.DescendantTokens()
                .Where(token => token.Span.IntersectsWith(lineSpan) && 
                               !token.IsKind(SyntaxKind.None) &&
                               token.Span.Start >= lineSpan.Start &&
                               token.Span.End <= lineSpan.End)
                .OrderBy(token => token.Span.Start)
                .ToList();

            // Look for exact match (case sensitive)
            foreach (var token in tokensOnLine)
            {
                if (token.ValueText == tokenToFind || token.Text == tokenToFind)
                {
                    return token.Span.Start;
                }
            }

            // If no exact match found, try looking for identifier tokens that match
            foreach (var token in tokensOnLine)
            {
                if (token.IsKind(SyntaxKind.IdentifierToken) && 
                    (token.ValueText == tokenToFind || token.Text == tokenToFind))
                {
                    return token.Span.Start;
                }
            }

            return -1;
        }
        catch (Exception)
        {
            // Fall back to simple string search if token enumeration fails
            var sourceCodeFromLine = textLine.ToString();
            var stringPosition = sourceCodeFromLine.IndexOf(tokenToFind, StringComparison.Ordinal);
            if (stringPosition >= 0)
            {
                return textLine.Start + stringPosition;
            }
            return -1;
        }
    }
}