using Microsoft.Build.Locator;
using RoslynMCP.Services;
using RoslynMCP.Tools;
using Serilog;

try
{
    MSBuildLocator.RegisterDefaults();

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .CreateBootstrapLogger();

    // Replace default logging with Serilog
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    });

    builder.Logging.ClearProviders(); // optional: reset defaults
    builder.Logging.AddConsole();

    // Add RoslynWorkspaceService as a singleton
    builder.Services.AddSingleton<IRoslynWorkspaceService, RoslynWorkspaceService>();

    // Add MCP services
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly();

    var app = builder.Build();

    // Configure MCP
    app.MapMcp();

    Log.Information("Starting RoslynMCP server");
    
    Task appTask = app.RunAsync();
    var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints;
    Log.Information("Mapped {Count} endpoints", endpoints.Count);
    foreach (var endpoint in endpoints)
    {
        Log.Information("Endpoint: {Name}", endpoint.DisplayName);
    }
    await appTask;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed!");
}
finally
{
    Log.CloseAndFlush();
}