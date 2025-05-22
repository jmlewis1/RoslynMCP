using Microsoft.Build.Locator;
using RoslynMCP.Services;
using Serilog;
using System.CommandLine;

namespace RoslynMCP;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            MSBuildLocator.RegisterDefaults();
            
            var rootCommand = CreateRootCommand();
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static RootCommand CreateRootCommand()
    {
        // Create options with proper descriptions for auto-generated help
        var stdioOption = new Option<bool>(
            aliases: new[] { "--stdio", "-s" },
            description: "Use stdio transport for MCP communication");

        var httpOption = new Option<bool>(
            aliases: new[] { "--http" },
            description: "Use HTTP transport for MCP communication (default)");

        var portOption = new Option<int>(
            aliases: new[] { "--port", "-p" },
            getDefaultValue: () => 5000,
            description: "Port number for HTTP transport");

        // Create root command with description for help
        var rootCommand = new RootCommand("RoslynMCP - Roslyn-based Model Context Protocol server")
        {
            stdioOption,
            httpOption,
            portOption
        };

        // Set handler using the modern pattern
        rootCommand.SetHandler(async (bool useStdio, bool useHttp, int port) =>
        {
            await RunServerAsync(useStdio, useHttp, port);
        }, stdioOption, httpOption, portOption);

        return rootCommand;
    }

    private static async Task RunServerAsync(bool useStdio, bool useHttp, int port)
    {
        // Determine transport mode - default to HTTP if neither specified
        bool shouldUseStdio = useStdio && !useHttp;
        bool shouldUseHttp = useHttp || (!useStdio && !useHttp); // Default to HTTP

        var builder = CreateWebApplicationBuilder(shouldUseHttp, port);
        ConfigureServices(builder.Services);
        ConfigureMcpServices(builder.Services, shouldUseStdio);

        var app = builder.Build();
        ConfigureApp(app);

        LogServerConfiguration(shouldUseStdio, port, app);
        
        await app.RunAsync();
    }

    private static WebApplicationBuilder CreateWebApplicationBuilder(bool shouldUseHttp, int port)
    {
        var builder = WebApplication.CreateBuilder();

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

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // Set port for HTTP transport
        if (shouldUseHttp)
        {
            builder.WebHost.UseUrls($"http://localhost:{port}");
        }

        return builder;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Add RoslynWorkspaceService as a singleton
        services.AddSingleton<IRoslynWorkspaceService, RoslynWorkspaceService>();
    }

    private static void ConfigureMcpServices(IServiceCollection services, bool shouldUseStdio)
    {
        var mcpBuilder = services.AddMcpServer();
        
        if (shouldUseStdio)
        {
            Log.Information("Configuring MCP server with stdio transport");
            mcpBuilder.WithStdioServerTransport();
        }
        else
        {
            Log.Information("Configuring MCP server with HTTP transport");
            mcpBuilder.WithHttpTransport();
        }
        
        mcpBuilder.WithToolsFromAssembly();
    }

    private static void ConfigureApp(WebApplication app)
    {
        // Configure MCP
        app.MapMcp();
    }

    private static void LogServerConfiguration(bool shouldUseStdio, int port, WebApplication app)
    {
        Log.Information("RoslynMCP server configured with {Transport} transport", 
            shouldUseStdio ? "stdio" : "HTTP");
        
        if (!shouldUseStdio)
        {
            Log.Information("HTTP server starting on port {Port}", port);
            
            var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints;
            Log.Information("Mapped {Count} endpoints", endpoints.Count);
            foreach (var endpoint in endpoints)
            {
                Log.Information("Endpoint: {Name}", endpoint.DisplayName);
            }
        }
    }
}