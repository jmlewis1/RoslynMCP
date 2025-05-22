using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace RoslynMCP.Tools;

public partial class RoslynTool
{
    [McpServerTool, Description("Find where a symbol is declared and get its type documentation")]
    public async Task<string> FindSymbolDeclaration(
        [Description("Absolute path to the solution file")] string solutionPath,
        [Description("Path to the file containing the symbol")] string filePath,
        [Description("Line number (1-based)")] int line, 
        [Description("Token to find declaration for")] string tokenToFind)
    {
        try
        {
            _logger.LogInformation("Finding symbol declaration at {FilePath}:{Line}:{TokenToFind}", filePath, line, tokenToFind);

            // Get the solution
            var solution = await _workspaceService.GetSolutionAsync(solutionPath);
            if (solution == null)
            {
                return $"Error: Could not load solution '{solutionPath}'";
            }

            // Get the document
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
            
            // Find the token on the line using the same method as other tools
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

            // Get symbol information - try different methods for different syntax kinds
            ISymbol? targetSymbol = null;
            
            // First check if we're already on a declaration
            targetSymbol = semanticModel.GetDeclaredSymbol(node);
            
            if (targetSymbol == null)
            {
                // Try to get symbol from usage/reference
                var symbolInfo = semanticModel.GetSymbolInfo(node);
                targetSymbol = symbolInfo.Symbol;
            }
            
            // If still no symbol found, try parent nodes
            if (targetSymbol == null)
            {
                var current = node.Parent;
                while (current != null && targetSymbol == null)
                {
                    // Try GetDeclaredSymbol first
                    targetSymbol = semanticModel.GetDeclaredSymbol(current);
                    
                    if (targetSymbol == null)
                    {
                        // Then try GetSymbolInfo
                        var symbol = semanticModel.GetSymbolInfo(current);
                        if (symbol.Symbol != null)
                        {
                            targetSymbol = symbol.Symbol;
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                    
                    current = current.Parent;
                }
            }

            if (targetSymbol == null)
            {
                return "No symbol found at the specified location";
            }

            // Find the symbol's declaration location(s)
            var declarations = targetSymbol.DeclaringSyntaxReferences;
            
            if (declarations.Length == 0)
            {
                // For metadata symbols (like System types), we won't have source locations
                return BuildMetadataSymbolResult(targetSymbol, solution);
            }

            // Build the result
            var result = new StringBuilder();
            
            // Get the primary declaration (usually there's only one)
            var primaryDeclaration = declarations.First();
            var declarationTree = primaryDeclaration.SyntaxTree;
            var declarationSourceText = await declarationTree.GetTextAsync();
            var declarationSpan = primaryDeclaration.Span;
            var declarationLine = declarationSourceText.Lines.GetLineFromPosition(declarationSpan.Start);
            
            // Find which document contains this syntax tree
            var declarationDocument = solution.GetDocument(declarationTree);
            if (declarationDocument == null)
            {
                return "Error: Could not find document for declaration";
            }

            var declarationFilePath = declarationDocument.FilePath ?? declarationDocument.Name;
            var relativeFilePath = Path.GetRelativePath(Path.GetDirectoryName(solutionPath) ?? "", declarationFilePath);
            
            result.AppendLine($"Symbol: {targetSymbol.Name}");
            result.AppendLine($"Kind: {targetSymbol.Kind}");
            result.AppendLine();
            result.AppendLine("Declaration:");
            result.AppendLine($"  File: {relativeFilePath}");
            result.AppendLine($"  Line: {declarationLine.LineNumber + 1}");
            result.AppendLine();
            
            // Get type documentation using the existing GetTypeDocumentation method
            if (targetSymbol is INamedTypeSymbol namedType)
            {
                var fullyQualifiedName = namedType.ToDisplayString();
                var typeDocumentation = await GetTypeDocumentation(solutionPath, fullyQualifiedName);
                result.AppendLine("Type Documentation:");
                result.AppendLine(typeDocumentation);
            }
            else if (targetSymbol is ILocalSymbol local)
            {
                // For local variables - handle this case first before checking ContainingType
                result.AppendLine("Local Variable Information:");
                result.AppendLine($"  Name: {local.Name}");
                result.AppendLine($"  Type: {local.Type.ToDisplayString()}");
                
                // Get type documentation for the variable's type
                if (local.Type is INamedTypeSymbol variableType)
                {
                    result.AppendLine();
                    var fullyQualifiedName = variableType.ToDisplayString();
                    var typeDocumentation = await GetTypeDocumentation(solutionPath, fullyQualifiedName);
                    result.AppendLine("Variable Type Documentation:");
                    result.AppendLine(typeDocumentation);
                }
            }
            else if (targetSymbol.ContainingType != null)
            {
                // For members of a type, get the containing type's documentation and extract relevant member info
                var containingType = targetSymbol.ContainingType;
                result.AppendLine($"Member of: {containingType.ToDisplayString()}");
                result.AppendLine();
                
                // Get member-specific information
                result.AppendLine("Member Information:");
                result.AppendLine($"  Name: {targetSymbol.Name}");
                result.AppendLine($"  Kind: {targetSymbol.Kind}");
                result.AppendLine($"  Accessibility: {targetSymbol.DeclaredAccessibility}");
                
                if (targetSymbol is IPropertySymbol property)
                {
                    result.AppendLine($"  Type: {property.Type.ToDisplayString()}");
                    result.AppendLine($"  Is Read-Only: {property.IsReadOnly}");
                    result.AppendLine($"  Is Write-Only: {property.IsWriteOnly}");
                }
                else if (targetSymbol is IFieldSymbol field)
                {
                    result.AppendLine($"  Type: {field.Type.ToDisplayString()}");
                    result.AppendLine($"  Is Read-Only: {field.IsReadOnly}");
                    result.AppendLine($"  Is Static: {field.IsStatic}");
                }
                else if (targetSymbol is IMethodSymbol method)
                {
                    result.AppendLine($"  Return Type: {method.ReturnType.ToDisplayString()}");
                    result.AppendLine($"  Parameters: {string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))}");
                }
                
                // Get XML documentation if available
                var xmlDoc = targetSymbol.GetDocumentationCommentXml();
                if (!string.IsNullOrWhiteSpace(xmlDoc))
                {
                    result.AppendLine();
                    result.AppendLine("XML Documentation:");
                    result.AppendLine(xmlDoc);
                }
            }
            
            _logger.LogInformation("Found declaration for {Symbol} at {File}:{Line}", 
                targetSymbol.Name, relativeFilePath, declarationLine.LineNumber + 1);
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find symbol declaration: {FilePath}:{Line}:{TokenToFind}", filePath, line, tokenToFind);
            return $"Error finding symbol declaration: {ex.Message}";
        }
    }

    private string BuildMetadataSymbolResult(ISymbol symbol, Solution solution)
    {
        var result = new StringBuilder();
        
        result.AppendLine($"Symbol: {symbol.Name}");
        result.AppendLine($"Kind: {symbol.Kind}");
        result.AppendLine();
        result.AppendLine("Declaration:");
        result.AppendLine($"  Location: Metadata (external assembly)");
        result.AppendLine($"  Assembly: {symbol.ContainingAssembly?.Name ?? "Unknown"}");
        result.AppendLine($"  Namespace: {symbol.ContainingNamespace?.ToDisplayString() ?? "Global"}");
        result.AppendLine();
        
        // For metadata symbols, we can still provide type information
        if (symbol is INamedTypeSymbol namedType)
        {
            result.AppendLine("Type Information:");
            result.AppendLine($"  Full Name: {namedType.ToDisplayString()}");
            result.AppendLine($"  Base Type: {namedType.BaseType?.ToDisplayString() ?? "None"}");
            
            if (namedType.Interfaces.Length > 0)
            {
                result.AppendLine($"  Interfaces: {string.Join(", ", namedType.Interfaces.Select(i => i.ToDisplayString()))}");
            }
            
            // Get public members
            var publicMembers = namedType.GetMembers().Where(m => m.DeclaredAccessibility == Accessibility.Public);
            if (publicMembers.Any())
            {
                result.AppendLine();
                result.AppendLine("Public Members:");
                foreach (var member in publicMembers.Take(10)) // Limit to first 10 to avoid huge output
                {
                    result.AppendLine($"  - {member.Kind}: {member.Name}");
                }
                if (publicMembers.Count() > 10)
                {
                    result.AppendLine($"  ... and {publicMembers.Count() - 10} more");
                }
            }
        }
        
        // Get XML documentation if available
        var xmlDoc = symbol.GetDocumentationCommentXml();
        if (!string.IsNullOrWhiteSpace(xmlDoc))
        {
            result.AppendLine();
            result.AppendLine("XML Documentation:");
            result.AppendLine(xmlDoc);
        }
        
        return result.ToString();
    }
}