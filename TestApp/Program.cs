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
    
    await Task.Delay(1000);
    
    // Create SSE client transport
    var clientTransport = new SseClientTransport(new SseClientTransportOptions
    {
        Endpoint = new Uri("http://localhost:5001/sse")
    });

    logger.LogInformation("Connecting to MCP server at http://localhost:5001...");

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
    
    var solutionPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "TestSln", "TestSln.sln");
    solutionPath = Path.GetFullPath(solutionPath);

    // Call the GetDetailedSymbolInfo tool for deserializedPerson variable
    logger.LogInformation("Calling GetDetailedSymbolInfo tool for deserializedPerson variable");
    
    var getDetailedSymbolInfoArguments = new Dictionary<string, object?>
    {
        ["solutionPath"] = solutionPath,
        ["filePath"] = "Program.cs",
        ["line"] = 61,  // Line with "var deserializedPerson = JsonConvert.DeserializeObject<Person>(json);"
        ["tokenToFind"] = "deserializedPerson"  // Token to get information about
    };

    var detailedSymbolInfoResult = await mcpClient.CallToolAsync("GetDetailedSymbolInfo", getDetailedSymbolInfoArguments);
    
    logger.LogInformation("GetDetailedSymbolInfo tool result: {Result}", detailedSymbolInfoResult.Content.First().Text);
    Console.WriteLine($"GetDetailedSymbolInfo tool returned: {detailedSymbolInfoResult.Content.First().Text}");

    await Task.Delay(20000);

    detailedSymbolInfoResult = await mcpClient.CallToolAsync("GetDetailedSymbolInfo", getDetailedSymbolInfoArguments);
    logger.LogInformation("GetDetailedSymbolInfo tool result: {Result}", detailedSymbolInfoResult.Content.First().Text);
    Console.WriteLine($"GetDetailedSymbolInfo tool returned: {detailedSymbolInfoResult.Content.First().Text}");

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