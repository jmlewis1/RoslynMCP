# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run specific test filter
dotnet test --filter "Name~FindSymbolReferences"

# Run the MCP server (HTTP mode by default)
dotnet run --project RoslynMCP

# Run the MCP server in stdio mode
dotnet run --project RoslynMCP -- --stdio

# Run the test client
dotnet run --project TestApp
```

## Architecture Overview

### Solution Structure
- **RoslynMCP**: Main MCP server implementing Roslyn-based code analysis tools
- **TestApp**: Console client for testing MCP server functionality
- **Tests/RoslynMCP.Tests**: NUnit test suite for the RoslynMCP tools
- **TestSln**: Test solution used by unit tests for code analysis scenarios

### Key Architectural Patterns

#### Partial Class Tool Architecture
The RoslynTool class uses partial classes to organize different tools:
- `RoslynTool.cs`: Base class with shared functionality
- `RoslynTool.GetDetailedSymbolInfo.cs`: Symbol information analysis
- `RoslynTool.GetTypeDocumentation.cs`: Type documentation extraction
- `RoslynTool.FindSymbolReferences.cs`: Symbol reference finding

#### Shared Token Finding
The `FindTokenPositionOnLine` method in `RoslynTool.GetDetailedSymbolInfo.cs` is protected and shared across tools for consistent token location.

#### Workspace Service Pattern
- `IRoslynWorkspaceService` interface for Roslyn workspace operations
- `RoslynWorkspaceService` implementation handles MSBuildWorkspace management
- Singleton pattern ensures workspace reuse

### MCP Server Configuration
- Supports both HTTP (default) and stdio transports
- Uses System.CommandLine for CLI argument parsing
- Tools are discovered automatically via `[McpServerTool]` attribute
- Dependency injection provides ILogger and IRoslynWorkspaceService to tools

### Testing Strategy
- Unit tests use real Roslyn workspaces with TestSln
- Tests verify both mock interactions and real workspace operations
- Test files reference specific line numbers in TestSln/TestProject/Program.cs

## Development Notes

When adding new tools to RoslynTool:
1. Create a new partial class file: `RoslynTool.YourToolName.cs`
2. Add the tool method with `[McpServerTool]` attribute
3. Create corresponding test file: `RoslynToolTests.YourToolName.cs`
4. Follow existing patterns for error handling and logging
5. Reuse shared methods like `FindTokenPositionOnLine` when applicable