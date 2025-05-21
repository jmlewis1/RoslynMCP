# RoslynMCP

A Model Context Protocol (MCP) server built with ASP.NET Core and Roslyn, featuring Serilog logging.

## Projects

### RoslynMCP
The main MCP server project that hosts tools via HTTP transport. Currently includes:
- **EchoTool**: A simple tool that echoes back the provided message

### TestApp  
A console application that acts as an MCP client using SseClientTransport to test the server functionality. It demonstrates proper MCP client usage by connecting to the server, listing available tools, and calling the echo tool.

## Setup and Running

### Prerequisites
- .NET 9.0 SDK
- All dependencies will be restored automatically via NuGet

### Running the Server
1. Open a terminal in the project root
2. Start the MCP server:
   ```bash
   dotnet run --project RoslynMCP
   ```
3. The server will start on http://localhost:5000
4. You should see Serilog output indicating the server has started

### Testing with the Client
1. With the server running, open another terminal
2. Run the test client:
   ```bash
   dotnet run --project TestApp
   ```
3. The client will attempt to call the echo tool and display the response

## Features

### Completed ✅
- [x] Basic MCP server with AspNetCore SSE transport
- [x] EchoTool implementation (currently does basic echo functionality)
- [x] Serilog logging setup in both projects
- [x] Serilog configured as the AspNet logger
- [x] Serilog injected into MCP tools for logging
- [x] TestApp console project with proper MCP client using SseClientTransport
- [x] Client follows official MCP SDK patterns for tool discovery and invocation
- [x] Basic solution structure with both projects

### Architecture
- Uses the official ModelContextProtocol packages (v0.2.0-preview.1)
- AspNetCore server with HTTP transport
- Dependency injection for tools and logging
- Structured logging with Serilog
- Both projects use consistent logging patterns

## Next Steps
- Replace the simple echo functionality with actual Roslyn-based code analysis tools
- Add more sophisticated MCP tools for C# code manipulation
- Enhance error handling and validation
- Add configuration management
