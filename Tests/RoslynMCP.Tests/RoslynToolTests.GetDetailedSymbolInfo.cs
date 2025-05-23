using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Moq;
using RoslynMCP.Services;
using RoslynMCP.Tools;

namespace RoslynMCP.Tests;

public partial class RoslynToolTests
{
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

    [Test]
    public async Task GetDetailedSymbolInfo_WithPersonType_IncludesInheritanceInformation()
    {
        // Test that GetDetailedSymbolInfo now includes inheritance information
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding Person class reference in new expression
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            testSolutionPath,
            "Program.cs",
            53, // Line with 'new Person'
            "Person");

        // Verify the result contains the enhanced type information
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find Person type");
        
        // Should contain type information section
        Assert.That(result, Does.Contain("=== TYPE INFORMATION ==="), 
            "Should include type information section");
        
        // Should contain inheritance tree
        Assert.That(result, Does.Contain("=== INHERITANCE TREE ==="), 
            "Should include inheritance tree information");
        
        // Should contain XML documentation
        Assert.That(result, Does.Contain("=== XML DOCUMENTATION ==="), 
            "Should include XML documentation section");
        
        // Should contain fully qualified name
        Assert.That(result, Does.Contain("Full Name: TestProject.Person"), 
            "Should include fully qualified type name");
        
        // Should contain public interface information
        Assert.That(result, Does.Contain("=== PUBLIC INTERFACE ==="), 
            "Should include public interface section");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetDetailedSymbolInfo_WithPersonVariable_IncludesVariableAndTypeInformation()
    {
        // Test that GetDetailedSymbolInfo gets the variable 'person' from line 52, not the Type
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding person variable (the 'var person' part, not the 'new Person' part)
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            testSolutionPath,
            "Program.cs",
            53, // Line with 'var person = new Person'
            "person"); // The variable name

        // Verify the result contains information about the variable
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find person variable");
        
        // Should indicate this is a local variable
        Assert.That(result, Does.Contain("Local variable") | Does.Contain("Variable"), 
            "Should indicate this is a local variable");
        
        // Should still contain detailed type information about the Person type
        Assert.That(result, Does.Contain("=== DETAILED TYPE INFORMATION ==="), 
            "Should include detailed type information section");
        
        // Should contain inheritance information for the Person type
        Assert.That(result, Does.Contain("=== INHERITANCE TREE ==="), 
            "Should include inheritance tree for the Person type");
        
        // Should contain public interface information for the Person type
        Assert.That(result, Does.Contain("=== PUBLIC INTERFACE ==="), 
            "Should include public interface for the Person type");
        
        // Should contain fully qualified name of the type
        Assert.That(result, Does.Contain("Full Name: TestProject.Person"), 
            "Should include fully qualified type name of the Person type");
        
        realWorkspaceService.Dispose();
    }
}