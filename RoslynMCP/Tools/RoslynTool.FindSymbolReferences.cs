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
    [McpServerTool, Description("Find all references to a symbol throughout the solution")]
    public async Task<string> FindSymbolReferences(
        [Description("Absolute path to the solution file")] string solutionPath,
        [Description("Path to the file containing the symbol")] string filePath,
        [Description("Line number (1-based)")] int line, 
        [Description("Token to find references for")] string tokenToFind)
    {
        try
        {
            _logger.LogInformation("Finding symbol references at {FilePath}:{Line}:{TokenToFind}", filePath, line, tokenToFind);

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
            
            // Find the token on the line using the same method as GetDetailedSymbolInfo
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
            
            // First try GetDeclaredSymbol for declarations (works for class, property, etc.)
            targetSymbol = semanticModel.GetDeclaredSymbol(node);
            
            if (targetSymbol == null)
            {
                // For references and other cases, try GetSymbolInfo
                var symbol = semanticModel.GetSymbolInfo(node);
                if (symbol.Symbol != null)
                {
                    targetSymbol = symbol.Symbol;
                }
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

            // Find all references to the symbol
            var references = await SymbolFinder.FindReferencesAsync(targetSymbol, solution);
            
            var result = new StringBuilder();
            result.AppendLine($"References to '{targetSymbol.Name}' ({targetSymbol.Kind}):");
            result.AppendLine($"Symbol: {targetSymbol.ToDisplayString()}");
            result.AppendLine();

            var totalReferences = 0;
            var fileGroups = new Dictionary<string, List<(int lineNumber, string lineText)>>();

            // Group references by file
            foreach (var referenceGroup in references)
            {
                foreach (var location in referenceGroup.Locations)
                {
                    if (location.Location.IsInSource)
                    {
                        var referenceDocument = solution.GetDocument(location.Location.SourceTree);
                        if (referenceDocument != null)
                        {
                            var referenceSourceText = await location.Location.SourceTree.GetTextAsync();
                            var referenceTextLine = referenceSourceText.Lines.GetLineFromPosition(location.Location.SourceSpan.Start);
                            var lineNumber = referenceTextLine.LineNumber + 1; // Convert to 1-based
                            var lineText = referenceTextLine.ToString().Trim();
                            
                            var fileName = Path.GetRelativePath(Path.GetDirectoryName(solutionPath) ?? "", referenceDocument.FilePath ?? referenceDocument.Name);
                            
                            if (!fileGroups.ContainsKey(fileName))
                            {
                                fileGroups[fileName] = new List<(int, string)>();
                            }
                            
                            fileGroups[fileName].Add((lineNumber, lineText));
                            totalReferences++;
                        }
                    }
                }
            }

            result.AppendLine($"Found {totalReferences} references across {fileGroups.Count} files:");
            result.AppendLine();

            // Output references grouped by file
            foreach (var fileGroup in fileGroups.OrderBy(kvp => kvp.Key))
            {
                result.AppendLine($"{fileGroup.Key}:");
                
                // Sort references by line number within each file
                foreach (var reference in fileGroup.Value.OrderBy(r => r.lineNumber))
                {
                    result.AppendLine($"    {reference.lineNumber}: {reference.lineText}");
                }
                result.AppendLine();
            }

            _logger.LogInformation("Found {TotalReferences} references across {FileCount} files for {Symbol}", 
                totalReferences, fileGroups.Count, targetSymbol.Name);
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find symbol references: {FilePath}:{Line}:{TokenToFind}", filePath, line, tokenToFind);
            return $"Error finding symbol references: {ex.Message}";
        }
    }

}