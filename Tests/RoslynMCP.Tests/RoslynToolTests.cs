using Microsoft.Extensions.Logging;
using Moq;
using RoslynMCP.Services;
using RoslynMCP.Tools;

namespace RoslynMCP.Tests;

[TestFixture]
public partial class RoslynToolTests
{
    protected Mock<ILogger<RoslynTool>> _mockLogger;
    protected Mock<IRoslynWorkspaceService> _mockWorkspaceService;
    protected RoslynTool _roslynTool;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<RoslynTool>>();
        _mockWorkspaceService = new Mock<IRoslynWorkspaceService>();
        _roslynTool = new RoslynTool(_mockLogger.Object, _mockWorkspaceService.Object);
    }
}