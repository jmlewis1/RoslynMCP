using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting TestApp MCP Client");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();
    var host = builder.Build();

    var logger = host.Services.GetRequiredService<ILogger<Program>>();

    // Create SSE client transport
    var clientTransport = new SseClientTransport(new SseClientTransportOptions
    {
        Endpoint = new Uri("http://localhost:5000")
    });

    logger.LogInformation("Connecting to MCP server at http://localhost:5000...");

    // Create MCP client using the SSE transport
    await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport);

    logger.LogInformation("Connected to MCP server successfully");

    // List available tools
    var tools = await mcpClient.ListToolsAsync();
    logger.LogInformation("Available tools:");
    foreach (var tool in tools)
    {
        logger.LogInformation("- {ToolName}: {Description}", tool.Name, tool.Description);
        Console.WriteLine($"Tool: {tool.Name} - {tool.Description}");
    }

    // Call the echo tool
    logger.LogInformation("Calling echo tool with message: 'Hello from TestApp!'");
    
    var echoArguments = new Dictionary<string, object?>
    {
        ["message"] = "Hello from TestApp!"
    };

    var echoResult = await mcpClient.CallToolAsync("echo", echoArguments);
    
    logger.LogInformation("Echo tool result: {Result}", echoResult);
    Console.WriteLine($"Echo tool returned: {echoResult}");

    // Call the LoadSolution tool
    logger.LogInformation("Calling LoadSolution tool with TestSln solution");
    
    var solutionPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "TestSln", "TestSln.sln");
    var loadSolutionArguments = new Dictionary<string, object?>
    {
        ["solutionPath"] = solutionPath
    };

    var solutionResult = await mcpClient.CallToolAsync("load_solution", loadSolutionArguments);
    
    logger.LogInformation("LoadSolution tool result: {Result}", solutionResult);
    Console.WriteLine($"LoadSolution tool returned: {solutionResult}");

    // Call the GetSymbolInfo tool
    logger.LogInformation("Calling GetSymbolInfo tool for a specific symbol");
    
    var getSymbolInfoArguments = new Dictionary<string, object?>
    {
        ["solutionPath"] = solutionPath,
        ["filePath"] = "Program.cs",
        ["line"] = 23,  // Line with "Name = "John Doe""
        ["character"] = 32  // Position of "John Doe"
    };

    var symbolInfoResult = await mcpClient.CallToolAsync("get_symbol_info", getSymbolInfoArguments);
    
    logger.LogInformation("GetSymbolInfo tool result: {Result}", symbolInfoResult);
    Console.WriteLine($"GetSymbolInfo tool returned: {symbolInfoResult}");

    // Call the GetDetailedSymbolInfo tool for deserializedPerson variable
    logger.LogInformation("Calling GetDetailedSymbolInfo tool for deserializedPerson variable");
    
    var getDetailedSymbolInfoArguments = new Dictionary<string, object?>
    {
        ["solutionPath"] = solutionPath,
        ["filePath"] = "Program.cs",
        ["line"] = 61,  // Line with "var deserializedPerson = JsonConvert.DeserializeObject<Person>(json);"
        ["tokenToFind"] = "deserializedPerson"  // Token to get information about
    };

    var detailedSymbolInfoResult = await mcpClient.CallToolAsync("get_detailed_symbol_info", getDetailedSymbolInfoArguments);
    
    logger.LogInformation("GetDetailedSymbolInfo tool result: {Result}", detailedSymbolInfoResult);
    Console.WriteLine($"GetDetailedSymbolInfo tool returned: {detailedSymbolInfoResult}");

    logger.LogInformation("TestApp completed successfully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly: {Error}", ex.Message);
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine("Note: Make sure the RoslynMCP server is running on http://localhost:5000");
    Console.WriteLine("You can start it by running: dotnet run --project RoslynMCP");
}
finally
{
    Log.CloseAndFlush();
}