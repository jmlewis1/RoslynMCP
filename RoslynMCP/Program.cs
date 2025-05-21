using Microsoft.Build.Locator;
using RoslynMCP.Services;
using Serilog;

MSBuildLocator.RegisterDefaults();

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

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

app.Run();
