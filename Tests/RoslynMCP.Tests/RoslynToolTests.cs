using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Moq;
using RoslynMCP.Services;
using RoslynMCP.Tools;

namespace RoslynMCP.Tests;

[TestFixture]
public class RoslynToolTests
{
    private Mock<ILogger<RoslynTool>> _mockLogger;
    private Mock<IRoslynWorkspaceService> _mockWorkspaceService;
    private RoslynTool _roslynTool;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<RoslynTool>>();
        _mockWorkspaceService = new Mock<IRoslynWorkspaceService>();
        _roslynTool = new RoslynTool(_mockLogger.Object, _mockWorkspaceService.Object);
    }

    [Test]
    public async Task GetDetailedSymbolInfo_WithValidParameters_CallsWorkspaceService()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "Program.cs";
        var line = 61;
        var tokenToFind = "deserializedPerson";
        
        // Setup mock document
        _mockWorkspaceService
            .Setup(ws => ws.GetDocumentAsync(solutionPath, filePath))
            .ReturnsAsync((Document?)null);
        
        // Act
        var result = await _roslynTool.GetDetailedSymbolInfo(solutionPath, filePath, line, tokenToFind);
        
        // Assert
        _mockWorkspaceService.Verify(ws => ws.GetDocumentAsync(solutionPath, filePath), Times.Once);
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting detailed symbol info")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task GetDetailedSymbolInfo_WithInvalidLocation_ReturnsError()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "NonExistent.cs";
        var line = 1;
        var tokenToFind = "nonExistentToken";
        
        // Setup mock to return null document
        _mockWorkspaceService
            .Setup(ws => ws.GetDocumentAsync(solutionPath, filePath))
            .ReturnsAsync((Document?)null);
        
        // Act
        var result = await _roslynTool.GetDetailedSymbolInfo(solutionPath, filePath, line, tokenToFind);
        
        // Assert
        Assert.That(result, Does.Contain("Error: Document 'NonExistent.cs' not found"));
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting detailed symbol info")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}