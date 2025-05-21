using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text;

namespace RoslynMCP.Tools;

[McpServerToolType]
public sealed class RoslynTool
{
    private readonly ILogger<RoslynTool> _logger;

    public RoslynTool(ILogger<RoslynTool> logger)
    {
        _logger = logger;
    }

    //[McpServerTool, Description("Load a C# solution using Roslyn and return basic information about it")]
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



    //[McpServerTool, Description("Get detailed symbol information at a specific file location (line and character position)")]
    public async Task<string> GetSymbolInfo(string solutionPath, string filePath, int line, int character)
    {
        try
        {
            _logger.LogInformation("Getting symbol info at {FilePath}:{Line}:{Character}", filePath, line, character);

            using var workspace = MSBuildWorkspace.Create();
            
            // Handle workspace diagnostic events
            workspace.WorkspaceFailed += (sender, e) =>
            {
                _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}", e.Diagnostic.Kind, e.Diagnostic.Message);
            };
            
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            
            // Find the document
            var fileName = Path.GetFileName(filePath);
            var document = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => Path.GetFileName(d.Name) == fileName);
            
            if (document == null)
            {
                return $"Error: Document '{filePath}' not found in solution";
            }

            // Get the syntax tree and semantic model
            var syntaxTree = await document.GetSyntaxTreeAsync();
            var semanticModel = await document.GetSemanticModelAsync();
            
            if (syntaxTree == null || semanticModel == null)
            {
                return "Error: Could not get syntax tree or semantic model";
            }

            // Convert line/character to position
            var sourceText = await syntaxTree.GetTextAsync();
            var textLines = sourceText.Lines;
            
            if (line < 1 || line > textLines.Count)
            {
                return $"Error: Line {line} is out of range (1-{textLines.Count})";
            }

            var textLine = textLines[line - 1]; // Convert to 0-based
            if (character < 1 || character > textLine.Span.Length + 1)
            {
                return $"Error: Character {character} is out of range for line {line} (1-{textLine.Span.Length + 1})";
            }

            var position = textLine.Start + (character - 1); // Convert to 0-based
            
            // Get the node at the position
            var root = await syntaxTree.GetRootAsync();
            var node = root.FindToken(position).Parent;
            
            if (node == null)
            {
                return "No syntax node found at the specified position";
            }

            // Get symbol information
            var symbol = semanticModel.GetSymbolInfo(node);
            var typeInfo = semanticModel.GetTypeInfo(node);
            
            var result = new StringBuilder();
            result.AppendLine($"Symbol Information at {Path.GetFileName(filePath)}:{line}:{character}");
            result.AppendLine($"Position: {position}");
            result.AppendLine($"Node Kind: {node.Kind()}");
            result.AppendLine($"Node Text: {node.ToString().Trim()}");
            result.AppendLine();

            // Symbol details
            if (symbol.Symbol != null)
            {
                result.AppendLine("Symbol Details:");
                result.AppendLine($"  Name: {symbol.Symbol.Name}");
                result.AppendLine($"  Kind: {symbol.Symbol.Kind}");
                result.AppendLine($"  Type: {symbol.Symbol.GetType().Name}");
                result.AppendLine($"  Containing Type: {symbol.Symbol.ContainingType?.Name ?? "None"}");
                result.AppendLine($"  Containing Namespace: {symbol.Symbol.ContainingNamespace?.ToDisplayString() ?? "None"}");
                result.AppendLine($"  Accessibility: {symbol.Symbol.DeclaredAccessibility}");
                result.AppendLine($"  Display String: {symbol.Symbol.ToDisplayString()}");
                
                if (symbol.Symbol.Locations.Any())
                {
                    result.AppendLine($"  Definition Location: {symbol.Symbol.Locations.First()}");
                }
            }
            else
            {
                result.AppendLine("No symbol found at this location");
            }

            // Type information
            if (typeInfo.Type != null)
            {
                result.AppendLine();
                result.AppendLine("Type Information:");
                result.AppendLine($"  Type: {typeInfo.Type.ToDisplayString()}");
                result.AppendLine($"  Type Kind: {typeInfo.Type.TypeKind}");
                result.AppendLine($"  Nullable: {typeInfo.Nullability}");
            }

            // Candidate symbols
            if (symbol.CandidateSymbols.Length > 0)
            {
                result.AppendLine();
                result.AppendLine("Candidate Symbols:");
                foreach (var candidate in symbol.CandidateSymbols)
                {
                    result.AppendLine($"  - {candidate.ToDisplayString()}");
                }
                result.AppendLine($"Candidate Reason: {symbol.CandidateReason}");
            }

            _logger.LogInformation("Symbol info retrieved successfully for {FilePath}:{Line}:{Character}", filePath, line, character);
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get symbol info: {FilePath}:{Line}:{Character}", filePath, line, character);
            return $"Error getting symbol information: {ex.Message}";
        }
    }


    /// <summary>
    /// Get detailed Symbol information
    /// </summary>
    /// <param name="solutionPath"></param>
    /// <param name="filePath"></param>
    /// <param name="line"></param>
    /// <param name="character"></param>
    /// <returns></returns>
    [McpServerTool, Description("Get detailed information including XML documentation and public interface for a variable, function, type, declaration, definition at a specific location")]
    public async Task<string> GetDetailedSymbolInfo(
        [Description("absolute path to the solution file")] string solutionPath,
        [Description("absolute path to the the source file the token appears in")] string filePath,
        [Description("The line the token appears on in the source code")] int line, 
        [Description("The token to get information about")] string tokenToFind)
    {
        try
        {
            _logger.LogInformation("Getting detailed symbol info at {FilePath}:{Line}:{TokenToFind}", filePath, line, tokenToFind);

            using var workspace = MSBuildWorkspace.Create();
            
            workspace.WorkspaceFailed += (sender, e) =>
            {
                _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}", e.Diagnostic.Kind, e.Diagnostic.Message);
            };
            
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            
            // Find the document
            var fileName = Path.GetFileName(filePath);
            var document = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => Path.GetFileName(d.Name) == fileName);
            
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

            // Convert line/character to position
            var sourceText = await syntaxTree.GetTextAsync();
            var textLines = sourceText.Lines;
            
            if (line < 1 || line > textLines.Count)
            {
                return $"Error: Line {line} is out of range (1-{textLines.Count})";
            }

            var textLine = textLines[line - 1];
            /*if (character < 1 || character > textLine.Span.Length + 1)
            {
                return $"Error: Character {character} is out of range for line {line} (1-{textLine.Span.Length + 1})";
            }
            
            var position = textLine.Start + (character - 1);
            */
            var sourceCodeFromLine = textLine.ToString();
            var position = sourceCodeFromLine.IndexOf(tokenToFind);
            if (position < 0)
                throw new Exception($"Couldn't find {tokenToFind} on line {line}");

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
            else if (node.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.VariableDeclarator))
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