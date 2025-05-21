using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace RoslynMCP.Tools;

[McpServerToolType]
public sealed class EchoTool
{
    private readonly ILogger<EchoTool> _logger;

    public EchoTool(ILogger<EchoTool> logger)
    {
        _logger = logger;
    }

    [McpServerTool, Description("Echoes the provided message back to the caller.")]
    public string Echo(string message)
    {
        _logger.LogInformation("Echo tool called with message: {Message}", message);
        return $"Echo: {message}";
    }
}