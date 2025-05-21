using RoslynMCP.Tools;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add MCP services
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<EchoTool>();

var app = builder.Build();

// Configure MCP
app.MapMcp();

Log.Information("Starting RoslynMCP server");

app.Run();
