using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RoslynMCP.Services;
using System.Text;

namespace RoslynMCP.Tools;

[McpServerToolType]
public partial class RoslynTool
{
    private readonly ILogger<RoslynTool> _logger;
    private readonly IRoslynWorkspaceService _workspaceService;

    public RoslynTool(ILogger<RoslynTool> logger, IRoslynWorkspaceService workspaceService)
    {
        _logger = logger;
        _workspaceService = workspaceService;
    }

    /// <summary>
    /// Parses a generic type name and returns the base type name with generic arity notation.
    /// For example: "AClass<int, float>" returns "AClass`2"
    /// Handles nested generics: "AClass<List<int>, float>" returns "AClass`2"
    /// </summary>
    /// <param name="typeName">The type name potentially containing generic parameters</param>
    /// <returns>The base type name with generic arity notation if applicable</returns>
    protected static string ParseGenericTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return typeName;

        var genericStartIndex = typeName.IndexOf('<');
        if (genericStartIndex == -1)
            return typeName; // Not a generic type

        var baseTypeName = typeName.Substring(0, genericStartIndex);
        
        // Count the number of generic arguments
        int genericArgCount = 0;
        int depth = 0;

        for (int i = genericStartIndex; i < typeName.Length; i++)
        {
            char c = typeName[i];
            
            if (c == '<')
            {
                depth++;
                if (depth == 1)
                {
                    genericArgCount = 1; // At least one argument
                }
            }
            else if (c == '>')
            {
                depth--;
                if (depth == 0)
                {
                    break; // End of generic arguments
                }
            }
            else if (c == ',' && depth == 1)
            {
                // Comma at the top level indicates another generic argument
                genericArgCount++;
            }
        }

        return $"{baseTypeName}`{genericArgCount}";
    }

    /// <summary>
    /// Appends comprehensive type information including basic details, inheritance, and documentation
    /// </summary>
    /// <param name="result">StringBuilder to append to</param>
    /// <param name="targetType">The type symbol to analyze</param>
    /// <param name="includeFullDocumentation">Whether to include complete documentation and public interface</param>
    protected static void AppendTypeInformation(StringBuilder result, ITypeSymbol targetType, bool includeFullDocumentation = false)
    {
        result.AppendLine("=== TYPE INFORMATION ===");
        result.AppendLine($"Name: {targetType.Name}");
        
        // Construct the fully qualified name correctly
        var fullyQualifiedName = targetType.ContainingNamespace?.IsGlobalNamespace == false 
            ? $"{targetType.ContainingNamespace.ToDisplayString()}.{targetType.Name}"
            : targetType.Name;
        result.AppendLine($"Full Name: {fullyQualifiedName}");
        result.AppendLine($"Namespace: {targetType.ContainingNamespace?.ToDisplayString() ?? "None"}");
        result.AppendLine($"Type Kind: {targetType.TypeKind}");
        result.AppendLine($"Accessibility: {targetType.DeclaredAccessibility}");
        result.AppendLine($"Is Abstract: {targetType.IsAbstract}");
        result.AppendLine($"Is Sealed: {targetType.IsSealed}");
        result.AppendLine($"Is Static: {targetType.IsStatic}");
        result.AppendLine();

        // XML Documentation
        result.AppendLine("=== XML DOCUMENTATION ===");
        var xmlDocs = targetType.GetDocumentationCommentXml();
        if (!string.IsNullOrEmpty(xmlDocs))
        {
            result.AppendLine(xmlDocs);
        }
        else
        {
            result.AppendLine("No XML documentation found.");
        }
        result.AppendLine();

        // Inheritance tree
        AppendInheritanceInformation(result, targetType);

        if (includeFullDocumentation)
        {
            AppendPublicInterface(result, targetType);
        }
    }

    /// <summary>
    /// Appends inheritance tree information for a type
    /// </summary>
    /// <param name="result">StringBuilder to append to</param>
    /// <param name="targetType">The type symbol to analyze</param>
    protected static void AppendInheritanceInformation(StringBuilder result, ITypeSymbol targetType)
    {
        result.AppendLine("=== INHERITANCE TREE ===");
        result.AppendLine($"This Type: {targetType.ToDisplayString()}");
        
        // Build inheritance chain
        var inheritanceChain = new List<ITypeSymbol>();
        var current = targetType.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            inheritanceChain.Add(current);
            current = current.BaseType;
        }

        // Show base types
        foreach (var baseType in inheritanceChain)
        {
            result.AppendLine($"    ↑ {baseType.ToDisplayString()}");
        }

        // Show interfaces implemented directly by this type
        var directInterfaces = targetType.Interfaces;
        if (directInterfaces.Length > 0)
        {
            result.AppendLine("    Interfaces:");
            foreach (var iface in directInterfaces)
            {
                result.AppendLine($"        - {iface.ToDisplayString()}");
                
                // Show interface inheritance
                ShowInterfaceInheritance(iface, result, "            ");
            }
        }

        // Show all interfaces (including inherited ones)
        var allInterfaces = targetType.AllInterfaces;
        var inheritedInterfaces = allInterfaces.Except(directInterfaces).ToList();
        if (inheritedInterfaces.Any())
        {
            result.AppendLine("    Inherited Interfaces:");
            foreach (var iface in inheritedInterfaces)
            {
                result.AppendLine($"        - {iface.ToDisplayString()}");
            }
        }

        result.AppendLine();
    }

    /// <summary>
    /// Appends detailed public interface information for a type
    /// </summary>
    /// <param name="result">StringBuilder to append to</param>
    /// <param name="targetType">The type symbol to analyze</param>
    protected static void AppendPublicInterface(StringBuilder result, ITypeSymbol targetType)
    {
        result.AppendLine("=== PUBLIC INTERFACE ===");
        
        // Constructors
        var constructors = targetType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Constructor && m.DeclaredAccessibility == Accessibility.Public)
            .ToList();
        
        if (constructors.Any())
        {
            result.AppendLine("Constructors:");
            foreach (var ctor in constructors)
            {
                result.AppendLine($"  {ctor.ToDisplayString()}");
                var ctorDocs = ctor.GetDocumentationCommentXml();
                if (!string.IsNullOrEmpty(ctorDocs))
                {
                    AppendFormattedXmlDocs(result, ctorDocs, "    ");
                }
                result.AppendLine();
            }
        }

        // Properties
        var properties = targetType.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(p => p.Name)
            .ToList();

        if (properties.Any())
        {
            result.AppendLine("Properties:");
            foreach (var prop in properties)
            {
                result.AppendLine($"  {prop.ToDisplayString()}");
                var propDocs = prop.GetDocumentationCommentXml();
                if (!string.IsNullOrEmpty(propDocs))
                {
                    AppendFormattedXmlDocs(result, propDocs, "    ");
                }
                result.AppendLine();
            }
        }

        // Methods
        var methods = targetType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public && 
                       m.MethodKind == MethodKind.Ordinary)
            .OrderBy(m => m.Name)
            .ToList();

        if (methods.Any())
        {
            result.AppendLine("Methods:");
            foreach (var method in methods)
            {
                result.AppendLine($"  {method.ToDisplayString()}");
                var methodDocs = method.GetDocumentationCommentXml();
                if (!string.IsNullOrEmpty(methodDocs))
                {
                    AppendFormattedXmlDocs(result, methodDocs, "    ");
                }
                result.AppendLine();
            }
        }

        // Fields
        var fields = targetType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(f => f.Name)
            .ToList();

        if (fields.Any())
        {
            result.AppendLine("Fields:");
            foreach (var field in fields)
            {
                result.AppendLine($"  {field.ToDisplayString()}");
                var fieldDocs = field.GetDocumentationCommentXml();
                if (!string.IsNullOrEmpty(fieldDocs))
                {
                    AppendFormattedXmlDocs(result, fieldDocs, "    ");
                }
                result.AppendLine();
            }
        }

        // Events
        var events = targetType.GetMembers()
            .OfType<IEventSymbol>()
            .Where(e => e.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(e => e.Name)
            .ToList();

        if (events.Any())
        {
            result.AppendLine("Events:");
            foreach (var evt in events)
            {
                result.AppendLine($"  {evt.ToDisplayString()}");
                var eventDocs = evt.GetDocumentationCommentXml();
                if (!string.IsNullOrEmpty(eventDocs))
                {
                    AppendFormattedXmlDocs(result, eventDocs, "    ");
                }
                result.AppendLine();
            }
        }
    }

    private static void ShowInterfaceInheritance(ITypeSymbol interfaceType, StringBuilder result, string indent)
    {
        var baseInterfaces = interfaceType.Interfaces;
        foreach (var baseInterface in baseInterfaces)
        {
            result.AppendLine($"{indent}↑ {baseInterface.ToDisplayString()}");
            ShowInterfaceInheritance(baseInterface, result, indent + "    ");
        }
    }

    private static void AppendFormattedXmlDocs(StringBuilder result, string xmlDocs, string indent)
    {
        var lines = xmlDocs.Split('\n');
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                result.AppendLine($"{indent}/// {line.Trim()}");
            }
        }
    }
}