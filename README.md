# RoslynMCP

RoslynMCP is a Model Context Protocol (MCP) server that provides powerful code analysis tools for C# projects using the Microsoft Roslyn compiler platform. It enables AI assistants and other MCP clients to analyze, navigate, and understand C# codebases with deep semantic understanding.

## Features

- **Semantic Code Analysis**: Leverage Roslyn's powerful semantic analysis capabilities
- **Type Documentation**: Get comprehensive documentation for any type in your solution
- **Symbol Navigation**: Find symbol declarations and references across your codebase
- **Context-Aware Type Resolution**: Resolve unqualified type names based on file context
- **Generic Type Support**: Full support for generic types including nested generics
- **Cross-Platform**: Works on Windows, Linux, and macOS

## Tools

### 1. GetDetailedSymbolInfo
Get detailed information about a symbol at a specific location in a file.

**Parameters:**
- `solutionPath`: Absolute path to the solution file
- `filePath`: Path to the file containing the symbol
- `lineNumber`: Line number where the symbol is located (1-based)
- `tokenName`: Name of the symbol to find

**Example:**
```json
{
  "solutionPath": "/path/to/MySolution.sln",
  "filePath": "MyProject/MyClass.cs",
  "lineNumber": 25,
  "tokenName": "MyMethod"
}
```

### 2. GetTypeDocumentation
Get comprehensive documentation and inheritance information for a fully qualified type name.

**Parameters:**
- `solutionPath`: Absolute path to the solution file
- `fullyQualifiedTypeName`: Fully qualified type name (e.g., `System.Collections.Generic.List<T>`)

**Example:**
```json
{
  "solutionPath": "/path/to/MySolution.sln",
  "fullyQualifiedTypeName": "MyNamespace.MyClass"
}
```

### 3. GetTypeDocumentationFromContext
Get type documentation for an unqualified type name by resolving it in the context of a specific file. This tool intelligently resolves types based on using directives, current namespace, and referenced assemblies.

**Parameters:**
- `solutionPath`: Absolute path to the solution file
- `filePath`: Path to the file containing the context for type resolution
- `unqualifiedTypeName`: Unqualified type name (e.g., `Person`, `List<string>`, `Dictionary<int, Person>`)

**Example:**
```json
{
  "solutionPath": "/path/to/MySolution.sln",
  "filePath": "MyProject/MyClass.cs",
  "unqualifiedTypeName": "List<string>"
}
```

### 4. FindSymbolDeclaration
Find where a symbol is declared in the codebase.

**Parameters:**
- `solutionPath`: Absolute path to the solution file
- `filePath`: Path to the file where the symbol is referenced
- `lineNumber`: Line number of the symbol reference (1-based)
- `tokenName`: Name of the symbol to find

**Example:**
```json
{
  "solutionPath": "/path/to/MySolution.sln",
  "filePath": "MyProject/MyClass.cs",
  "lineNumber": 30,
  "tokenName": "CalculateTotal"
}
```

### 5. FindSymbolReferences
Find all references to a symbol across the solution.

**Parameters:**
- `solutionPath`: Absolute path to the solution file
- `filePath`: Path to the file containing the symbol
- `lineNumber`: Line number where the symbol is located (1-based)
- `tokenName`: Name of the symbol to find references for

**Example:**
```json
{
  "solutionPath": "/path/to/MySolution.sln",
  "filePath": "MyProject/MyClass.cs",
  "lineNumber": 15,
  "tokenName": "UserId"
}
```

## Installation

### Prerequisites
- .NET 9.0 SDK or later
- A C# solution file (.sln) with associated projects

### Building from Source
```bash
# Clone the repository
git clone https://github.com/yourusername/RoslynMCP.git
cd RoslynMCP

# Build the solution
dotnet build

# Run the MCP server
dotnet run --project RoslynMCP
```

## Usage

### Running as an MCP Server

RoslynMCP supports both HTTP and stdio transports:

**HTTP Mode (default):**
```bash
dotnet run --project RoslynMCP
```
The server will start on http://localhost:5000

**Stdio Mode:**
```bash
dotnet run --project RoslynMCP -- --stdio
```

### Testing with the Client

A test client is included to verify the server functionality:

```bash
# In a separate terminal, with the server running
dotnet run --project TestApp
```

### Integration with AI Assistants

To use RoslynMCP with an AI assistant that supports MCP:

1. Configure your AI assistant to connect to the RoslynMCP server
2. Provide the path to your C# solution when making requests
3. Use the available tools to analyze your codebase

### Example Workflow

1. **Understanding a Type**: Use `GetTypeDocumentationFromContext` to get documentation for a type without knowing its full namespace:
   ```
   "What does the Person class do?" 
   → GetTypeDocumentationFromContext with unqualifiedTypeName="Person"
   ```

2. **Finding Usages**: Use `FindSymbolReferences` to find all places where a method or property is used:
   ```
   "Where is the CalculateTotal method used?"
   → FindSymbolReferences with tokenName="CalculateTotal"
   ```

3. **Navigation**: Use `FindSymbolDeclaration` to jump to where a symbol is defined:
   ```
   "Show me where UserId is declared"
   → FindSymbolDeclaration with tokenName="UserId"
   ```

## Architecture

### Technology Stack
- **Microsoft.CodeAnalysis (Roslyn)**: For C# code analysis
- **MSBuildWorkspace**: For loading and analyzing entire solutions
- **Model Context Protocol**: For standardized tool exposure
- **ASP.NET Core**: For HTTP transport
- **Serilog**: For structured logging
- **System.CommandLine**: For CLI argument parsing

### Key Components

- `RoslynTool`: Main tool class containing all MCP-exposed methods (organized as partial classes)
- `RoslynWorkspaceService`: Manages MSBuildWorkspace lifecycle and caching
- `ParseGenericTypeName`: Handles generic type parsing (e.g., `List<T>` → `List`1`)

### Project Structure
```
RoslynMCP/
├── RoslynMCP/              # Main MCP server project
│   ├── Program.cs          # Server entry point
│   ├── Services/           # Core services
│   │   └── RoslynWorkspaceService.cs
│   └── Tools/              # MCP tools
│       ├── RoslynTool.cs
│       ├── RoslynTool.GetDetailedSymbolInfo.cs
│       ├── RoslynTool.GetTypeDocumentation.cs
│       ├── RoslynTool.GetTypeDocumentationFromContext.cs
│       ├── RoslynTool.FindSymbolDeclaration.cs
│       └── RoslynTool.FindSymbolReferences.cs
├── TestApp/                # Test client
├── Tests/                  # Unit tests
│   └── RoslynMCP.Tests/
└── TestSln/                # Test solution for unit tests
```

## Development

### Running Tests
```bash
dotnet test
```

### Adding New Tools

1. Create a new partial class file: `RoslynTool.YourToolName.cs`
2. Add the tool method with `[McpServerTool]` attribute
3. Create corresponding test file: `RoslynToolTests.YourToolName.cs`
4. Follow existing patterns for error handling and logging
5. Reuse shared methods like `FindTokenPositionOnLine` when applicable

### Logging

The server uses Serilog for structured logging. Logs include:
- Tool invocations
- Workspace operations
- Error details
- Performance metrics

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## License

[Add your license here]

## Acknowledgments

- Built on [Microsoft Roslyn](https://github.com/dotnet/roslyn)
- Implements the [Model Context Protocol](https://modelcontextprotocol.io/) specification
- Uses [Serilog](https://serilog.net/) for logging