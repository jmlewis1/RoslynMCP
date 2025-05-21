using Microsoft.Extensions.Logging;
using Moq;
using RoslynMCP.Tools;

namespace RoslynMCP.Tests;

[TestFixture]
public class RoslynToolTests
{
    private Mock<ILogger<RoslynTool>> _mockLogger;
    private RoslynTool _roslynTool;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<RoslynTool>>();
        _roslynTool = new RoslynTool(_mockLogger.Object);
    }

    [Test]
    public async Task LoadSolution_WithValidSolutionPath_ReturnsSuccessMessage()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        
        solutionPath = Path.GetFullPath(solutionPath);
        // Act
        var result = await _roslynTool.LoadSolution(solutionPath);
        
        // Assert
        Assert.That(result, Does.Contain("Solution loaded successfully!"));
        Assert.That(result, Does.Contain("TestSln.sln"));
        Assert.That(result, Does.Contain("Project Count: 1"));
        Assert.That(result, Does.Contain("TestProject"));
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Loading solution")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task LoadSolution_WithInvalidSolutionPath_ReturnsErrorMessage()
    {
        // Arrange
        var invalidPath = "nonexistent.sln";
        
        // Act
        var result = await _roslynTool.LoadSolution(invalidPath);
        
        // Assert
        Assert.That(result, Does.StartWith("Error loading solution:"));
        
        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to load solution")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task LoadSolution_WithNullPath_ReturnsErrorMessage()
    {
        // Arrange
        string nullPath = null!;
        
        // Act
        var result = await _roslynTool.LoadSolution(nullPath);
        
        // Assert
        Assert.That(result, Does.StartWith("Error loading solution:"));
    }

    [Test]
    public async Task LoadSolution_WithEmptyPath_ReturnsErrorMessage()
    {
        // Arrange
        var emptyPath = string.Empty;
        
        // Act
        var result = await _roslynTool.LoadSolution(emptyPath);
        
        // Assert
        Assert.That(result, Does.StartWith("Error loading solution:"));
    }
}