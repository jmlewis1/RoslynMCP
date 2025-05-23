using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Moq;
using RoslynMCP.Services;
using RoslynMCP.Tools;

namespace RoslynMCP.Tests;

public partial class RoslynToolTests
{
    [Test]
    public async Task GetTypeDocumentationFromContext_WithValidParameters_CallsWorkspaceService()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "Program.cs";
        var typeName = "Person";
        
        // Setup mock solution and document
        _mockWorkspaceService
            .Setup(ws => ws.GetSolutionAsync(solutionPath))
            .ReturnsAsync(null as Solution);
        
        // Act
        var result = await _roslynTool.GetTypeDocumentationFromContext(solutionPath, filePath, typeName);
        
        // Assert
        _mockWorkspaceService.Verify(ws => ws.GetSolutionAsync(solutionPath), Times.Once);
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting type documentation for unqualified type")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task GetTypeDocumentationFromContext_WithInvalidSolution_ReturnsError()
    {
        // Arrange
        var solutionPath = "NonExistent.sln";
        var filePath = "Program.cs";
        var typeName = "Person";
        
        // Setup mock to return null solution
        _mockWorkspaceService
            .Setup(ws => ws.GetSolutionAsync(solutionPath))
            .ReturnsAsync(null as Solution);
        
        // Act
        var result = await _roslynTool.GetTypeDocumentationFromContext(solutionPath, filePath, typeName);
        
        // Assert
        Assert.That(result, Does.Contain("Error: Could not load solution"));
    }

    [Test]
    public async Task GetTypeDocumentationFromContext_WithUnqualifiedTypeName_ResolvesCorrectly()
    {
        // Test resolving "Person" in the context of TestSln
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test resolving "Person" type in Program.cs context
        var result = await realRoslynTool.GetTypeDocumentationFromContext(
            testSolutionPath,
            Path.Combine("TestProject", "Program.cs"),
            "Person");

        // Verify the result contains documentation
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully resolve Person type");
        Assert.That(result, Does.Contain("Type Documentation for: TestProject.Person"), 
            "Should resolve to fully qualified TestProject.Person");
        Assert.That(result, Does.Contain("=== PUBLIC INTERFACE ==="), 
            "Should contain public interface section");
        Assert.That(result, Does.Contain("Properties:"), 
            "Should contain properties section");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetTypeDocumentationFromContext_WithBuiltInType_ResolvesCorrectly()
    {
        // Test resolving built-in type alias "string"
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test resolving "string" type
        var result = await realRoslynTool.GetTypeDocumentationFromContext(
            testSolutionPath,
            Path.Combine("TestProject", "Program.cs"),
            "string");

        // Verify the result contains documentation
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully resolve string type");
        Assert.That(result, Does.Contain("Type Documentation for: System.String"), 
            "Should resolve to System.String");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetTypeDocumentationFromContext_WithSystemType_ResolvesCorrectly()
    {
        // Test resolving System namespace type without qualification
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test resolving "Console" type
        var result = await realRoslynTool.GetTypeDocumentationFromContext(
            testSolutionPath,
            Path.Combine("TestProject", "Program.cs"),
            "Console");

        // Verify the result contains documentation
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully resolve Console type");
        Assert.That(result, Does.Contain("Type Documentation for: System.Console"), 
            "Should resolve to System.Console");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetTypeDocumentationFromContext_WithGenericType_ResolvesCorrectly()
    {
        // Test resolving generic type "List" (should resolve to List<T>)
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test resolving "List" type
        var result = await realRoslynTool.GetTypeDocumentationFromContext(
            testSolutionPath,
            Path.Combine("TestProject", "Program.cs"),
            "List");

        // Verify the result contains documentation
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully resolve List type");
        Assert.That(result, Does.Contain("System.Collections.Generic.List"), 
            "Should resolve to System.Collections.Generic.List");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetTypeDocumentationFromContext_WithUsingDirectiveType_ResolvesCorrectly()
    {
        // Test resolving type from using directive (JsonConvert from Newtonsoft.Json)
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test resolving "JsonConvert" type
        var result = await realRoslynTool.GetTypeDocumentationFromContext(
            testSolutionPath,
            Path.Combine("TestProject", "Program.cs"),
            "JsonConvert");

        // Verify the result contains documentation
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully resolve JsonConvert type");
        Assert.That(result, Does.Contain("Newtonsoft.Json.JsonConvert"), 
            "Should resolve to Newtonsoft.Json.JsonConvert");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetTypeDocumentationFromContext_WithNonExistentType_ReturnsError()
    {
        // Test with a type that doesn't exist
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test resolving non-existent type
        var result = await realRoslynTool.GetTypeDocumentationFromContext(
            testSolutionPath,
            Path.Combine("TestProject", "Program.cs"),
            "NonExistentType");

        // Verify the result indicates an error
        Assert.That(result, Does.StartWith("Error:"), 
            "Should return error when type cannot be resolved");
        Assert.That(result, Does.Contain("Could not resolve type 'NonExistentType'"), 
            "Error message should indicate which type could not be resolved");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetTypeDocumentationFromContext_WithInvalidDocument_ReturnsError()
    {
        // Test with invalid document path
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test with non-existent document
        var result = await realRoslynTool.GetTypeDocumentationFromContext(
            testSolutionPath,
            Path.Combine("TestProject", "NonExistent.cs"),
            "Person");

        // Verify the result indicates an error
        Assert.That(result, Does.StartWith("Error:"), 
            "Should return error when document is not found");
        Assert.That(result, Does.Contain("Document 'NonExistent.cs' not found"), 
            "Error message should indicate which document was not found");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetTypeDocumentationFromContext_WithCollectionType_ResolvesCorrectly()
    {
        // Test resolving IHttpClientFactory from using Microsoft.Extensions.Http
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test resolving "IHttpClientFactory" type
        var result = await realRoslynTool.GetTypeDocumentationFromContext(
            testSolutionPath,
            Path.Combine("TestProject", "Program.cs"),
            "IHttpClientFactory");

        // Verify the result contains documentation
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully resolve IHttpClientFactory type");
        Assert.That(result, Does.Contain("Microsoft.Extensions.Http.IHttpClientFactory"), 
            "Should resolve to Microsoft.Extensions.Http.IHttpClientFactory");
        
        realWorkspaceService.Dispose();
    }
}