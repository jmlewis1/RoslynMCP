using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace RoslynMCP.Tools;

public partial class RoslynTool
{
    [McpServerTool, Description("Get type documentation for an unqualified type name by resolving it in the context of a specific file")]
    public async Task<string> GetTypeDocumentationFromContext(
        [Description("Absolute path to the solution file")] string solutionPath,
        [Description("Path to the file containing the context for type resolution")] string filePath,
        [Description("Unqualified type name (e.g., 'Person', 'List<string>', 'Dictionary<int, Person>')")] string unqualifiedTypeName)
    {
        try
        {
            _logger.LogInformation("Getting type documentation for unqualified type {TypeName} in context of {FilePath}", 
                unqualifiedTypeName, filePath);

            var solution = await _workspaceService.GetSolutionAsync(solutionPath);
            if (solution == null)
            {
                return $"Error: Could not load solution '{solutionPath}'";
            }

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

            // Get the compilation unit to find using directives
            var root = await syntaxTree.GetRootAsync();
            var compilationUnit = root as CompilationUnitSyntax;
            if (compilationUnit == null)
            {
                return "Error: Could not get compilation unit";
            }

            // Try to resolve the type name
            INamedTypeSymbol? resolvedType = await ResolveTypeInContext(
                unqualifiedTypeName, semanticModel, compilationUnit, solution);

            if (resolvedType == null)
            {
                return $"Error: Could not resolve type '{unqualifiedTypeName}' in the context of file '{filePath}'";
            }

            // Get the fully qualified name
            string fullyQualifiedName = resolvedType.ToDisplayString();
            
            _logger.LogInformation("Resolved '{UnqualifiedName}' to '{FullyQualifiedName}'", 
                unqualifiedTypeName, fullyQualifiedName);

            // For built-in types and types not in the solution, return direct documentation
            if (IsBuiltInOrExternalType(resolvedType))
            {
                return GetDirectTypeDocumentation(resolvedType);
            }

            // Call the existing GetTypeDocumentation method for types in the solution
            return await GetTypeDocumentation(solutionPath, fullyQualifiedName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get type documentation from context: {FilePath}, {TypeName}", 
                filePath, unqualifiedTypeName);
            return $"Error getting type documentation from context: {ex.Message}";
        }
    }

    private async Task<INamedTypeSymbol?> ResolveTypeInContext(
        string typeName, 
        SemanticModel semanticModel, 
        CompilationUnitSyntax compilationUnit,
        Solution solution)
    {
        var compilation = semanticModel.Compilation;
        
        // Remove any generic type parameters for initial lookup
        string baseTypeName = typeName;
        bool isGeneric = typeName.Contains('<');
        if (isGeneric)
        {
            int genericIndex = typeName.IndexOf('<');
            baseTypeName = typeName.Substring(0, genericIndex);
        }

        // 1. Check if it's a built-in type or in System namespace
        /*var systemType = ResolveSystemType(baseTypeName, compilation);
        if (systemType != null)
        {
            return HandleGenericType(systemType, typeName, semanticModel, compilation);
        }*/

        // 2. Get all using directives
        var usings = compilationUnit.Usings;
        var globalUsings = await GetGlobalUsings(solution);
        
        // 3. Check current namespace
        var namespaceDeclaration = compilationUnit.Members
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();
        
        var fileScopedNamespace = compilationUnit.Members
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        string? currentNamespace = namespaceDeclaration?.Name.ToString() 
            ?? fileScopedNamespace?.Name.ToString();

        if (!string.IsNullOrEmpty(currentNamespace))
        {
            var typeInCurrentNamespace = compilation.GetTypeByMetadataName($"{currentNamespace}.{baseTypeName}");
            if (typeInCurrentNamespace != null)
            {
                return HandleGenericType(typeInCurrentNamespace, typeName, semanticModel, compilation);
            }
        }

        // 4. Check each using directive
        foreach (var usingDirective in usings)
        {
            var namespaceName = usingDirective.Name?.ToString();
            if (!string.IsNullOrEmpty(namespaceName))
            {
                var qualifiedTypeName = $"{namespaceName}.{baseTypeName}";
                var type = compilation.GetTypeByMetadataName(qualifiedTypeName);
                if (type != null)
                {
                    return HandleGenericType(type, typeName, semanticModel, compilation);
                }
            }
        }

        // 5. Check global usings
        foreach (var globalUsing in globalUsings)
        {
            var qualifiedTypeName = $"{globalUsing}.{baseTypeName}";
            var type = compilation.GetTypeByMetadataName(qualifiedTypeName);
            if (type != null)
            {
                return HandleGenericType(type, typeName, semanticModel, compilation);
            }
        }

        // 6. Search all types in referenced assemblies
        // This handles cases where using directives reference namespaces that contain 
        // types which themselves use types from other namespaces
        var allTypes = compilation.GetSymbolsWithName(baseTypeName, SymbolFilter.Type);
        foreach (var symbol in allTypes)
        {
            if (symbol is INamedTypeSymbol namedType && 
                namedType.DeclaredAccessibility == Accessibility.Public)
            {
                return HandleGenericType(namedType, typeName, semanticModel, compilation);
            }
        }

        // 6. Check if it's a nested type in the current namespace
        if (!string.IsNullOrEmpty(currentNamespace))
        {
            foreach (var type in compilation.GetSymbolsWithName(baseTypeName, SymbolFilter.Type))
            {
                if (type is INamedTypeSymbol namedType && 
                    type.ContainingNamespace?.ToDisplayString() == currentNamespace)
                {
                    return HandleGenericType(namedType, typeName, semanticModel, compilation);
                }
            }
        }

        return null;
    }

    private INamedTypeSymbol? HandleGenericType(
        INamedTypeSymbol baseType, 
        string originalTypeName, 
        SemanticModel semanticModel,
        Compilation compilation)
    {
        if (!originalTypeName.Contains('<'))
        {
            return baseType;
        }

        // For generic types, we need to construct the type with type arguments
        // For now, return the unbound generic type
        // A more complete implementation would parse the generic arguments
        return baseType;
    }

    private INamedTypeSymbol? ResolveSystemType(string typeName, Compilation compilation)
    {
        // Check common built-in type aliases
        var builtInType = typeName switch
        {
            "bool" => compilation.GetSpecialType(SpecialType.System_Boolean),
            "byte" => compilation.GetSpecialType(SpecialType.System_Byte),
            "sbyte" => compilation.GetSpecialType(SpecialType.System_SByte),
            "short" => compilation.GetSpecialType(SpecialType.System_Int16),
            "ushort" => compilation.GetSpecialType(SpecialType.System_UInt16),
            "int" => compilation.GetSpecialType(SpecialType.System_Int32),
            "uint" => compilation.GetSpecialType(SpecialType.System_UInt32),
            "long" => compilation.GetSpecialType(SpecialType.System_Int64),
            "ulong" => compilation.GetSpecialType(SpecialType.System_UInt64),
            "float" => compilation.GetSpecialType(SpecialType.System_Single),
            "double" => compilation.GetSpecialType(SpecialType.System_Double),
            "decimal" => compilation.GetSpecialType(SpecialType.System_Decimal),
            "char" => compilation.GetSpecialType(SpecialType.System_Char),
            "string" => compilation.GetSpecialType(SpecialType.System_String),
            "object" => compilation.GetSpecialType(SpecialType.System_Object),
            _ => null
        };

        if (builtInType != null)
        {
            return builtInType;
        }

        // Check System namespace types
        var systemType = compilation.GetTypeByMetadataName($"System.{typeName}");
        if (systemType != null)
        {
            return systemType;
        }

        // Check common generic collection types
        return typeName switch
        {
            "List" => compilation.GetTypeByMetadataName("System.Collections.Generic.List`1"),
            "Dictionary" => compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2"),
            "HashSet" => compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1"),
            "Queue" => compilation.GetTypeByMetadataName("System.Collections.Generic.Queue`1"),
            "Stack" => compilation.GetTypeByMetadataName("System.Collections.Generic.Stack`1"),
            "Task" => compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
            _ => null
        };
    }

    private async Task<List<string>> GetGlobalUsings(Solution solution)
    {
        var globalUsings = new HashSet<string>();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var root = await syntaxTree.GetRootAsync();
                if (root is CompilationUnitSyntax compilationUnit)
                {
                    var globalUsingDirectives = compilationUnit.Usings
                        .Where(u => u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword));

                    foreach (var globalUsing in globalUsingDirectives)
                    {
                        var namespaceName = globalUsing.Name?.ToString();
                        if (!string.IsNullOrEmpty(namespaceName))
                        {
                            globalUsings.Add(namespaceName);
                        }
                    }
                }
            }
        }

        return globalUsings.ToList();
    }

    private bool IsBuiltInOrExternalType(INamedTypeSymbol type)
    {
        // Check if it's a built-in type or from System namespace
        var namespaceName = type.ContainingNamespace?.ToDisplayString() ?? "";
        return namespaceName.StartsWith("System") || 
               namespaceName.StartsWith("Microsoft") ||
               namespaceName.StartsWith("Newtonsoft") ||
               type.SpecialType != SpecialType.None;
    }

    private string GetDirectTypeDocumentation(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Type Documentation for: {type.ToDisplayString()}");
        sb.AppendLine("=".PadRight(50, '='));
        sb.AppendLine();

        AppendTypeInformation(sb, type, includeFullDocumentation: true);
        return sb.ToString();

        // Add XML documentation if available
        var xmlComment = type.GetDocumentationCommentXml();
        if (!string.IsNullOrWhiteSpace(xmlComment))
        {
            sb.AppendLine("=== DOCUMENTATION ===");
            AppendFormattedXmlDocs(sb, xmlComment, "");
            sb.AppendLine();
        }

        sb.AppendLine("=== TYPE INFORMATION ===");
        sb.AppendLine($"Kind: {type.TypeKind}");
        sb.AppendLine($"Namespace: {type.ContainingNamespace?.ToDisplayString() ?? "<global>"}");
        
        if (type.IsGenericType)
        {
            sb.AppendLine($"Generic: Yes (Arity: {type.Arity})");
        }
        
        if (type.BaseType != null && type.BaseType.SpecialType != SpecialType.System_Object)
        {
            sb.AppendLine($"Base Type: {type.BaseType.ToDisplayString()}");
        }

        var interfaces = type.Interfaces;
        if (interfaces.Length > 0)
        {
            sb.AppendLine($"Implements: {string.Join(", ", interfaces.Select(i => i.ToDisplayString()))}");
        }

        sb.AppendLine();
        sb.AppendLine("=== PUBLIC INTERFACE ===");

        // Add constructors
        var publicConstructors = type.Constructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();
        if (publicConstructors.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Constructors:");
            foreach (var ctor in publicConstructors)
            {
                sb.AppendLine($"  {type.Name}({string.Join(", ", ctor.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))})");
            }
        }

        // Add properties
        var publicProperties = type.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(p => p.Name).ToList();
        
        if (publicProperties.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Properties:");
            foreach (var prop in publicProperties)
            {
                var accessors = new List<string>();
                if (prop.GetMethod != null) accessors.Add("get");
                if (prop.SetMethod != null) accessors.Add("set");
                sb.AppendLine($"  {prop.Type.ToDisplayString()} {prop.Name} {{ {string.Join("; ", accessors)}; }}");
            }
        }

        // Add methods
        var publicMethods = type.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public && 
                       m.MethodKind == MethodKind.Ordinary &&
                       !m.IsStatic)
            .OrderBy(m => m.Name).ToList();
        
        if (publicMethods.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Methods:");
            foreach (var method in publicMethods)
            {
                var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                sb.AppendLine($"  {method.ReturnType.ToDisplayString()} {method.Name}({parameters})");
            }
        }

        // Add static methods
        var publicStaticMethods = type.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public && 
                       m.MethodKind == MethodKind.Ordinary &&
                       m.IsStatic)
            .OrderBy(m => m.Name).ToList();
        
        if (publicStaticMethods.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Static Methods:");
            foreach (var method in publicStaticMethods)
            {
                var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                sb.AppendLine($"  {method.ReturnType.ToDisplayString()} {method.Name}({parameters})");
            }
        }

        return sb.ToString();
    }
}