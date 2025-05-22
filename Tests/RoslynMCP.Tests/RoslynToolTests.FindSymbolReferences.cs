using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Moq;
using RoslynMCP.Services;
using RoslynMCP.Tools;

namespace RoslynMCP.Tests;

public partial class RoslynToolTests
{
    [Test]
    public async Task FindSymbolReferences_WithValidParameters_CallsWorkspaceService()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "Program.cs";
        var line = 52;
        var tokenToFind = "person";
        
        // Setup mock solution and document
        _mockWorkspaceService
            .Setup(ws => ws.GetSolutionAsync(solutionPath))
            .ReturnsAsync(null as Solution);
        
        // Act
        var result = await _roslynTool.FindSymbolReferences(solutionPath, filePath, line, tokenToFind);
        
        // Assert
        _mockWorkspaceService.Verify(ws => ws.GetSolutionAsync(solutionPath), Times.Once);
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Finding symbol references")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task FindSymbolReferences_WithInvalidSolution_ReturnsError()
    {
        // Arrange
        var solutionPath = "NonExistent.sln";
        var filePath = "Program.cs";
        var line = 1;
        var tokenToFind = "someToken";
        
        // Setup mock to return null solution
        _mockWorkspaceService
            .Setup(ws => ws.GetSolutionAsync(solutionPath))
            .ReturnsAsync(null as Solution);
        
        // Act
        var result = await _roslynTool.FindSymbolReferences(solutionPath, filePath, line, tokenToFind);
        
        // Assert
        Assert.That(result, Does.Contain("Error: Could not load solution"));
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Finding symbol references")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task FindSymbolReferences_WithPersonVariable_FindsAllUsages()
    {
        // Test finding all references to the 'person' variable in TestSln
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding references to the 'person' variable
        var result = await realRoslynTool.FindSymbolReferences(
            testSolutionPath,
            "Program.cs",
            52, // Line with 'var person = new Person'
            "person"); // The variable name

        // Verify the result contains reference information
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find person variable references");
        
        // Should indicate what symbol we're finding references for
        Assert.That(result, Does.Contain("References to 'person'"), 
            "Should indicate which symbol references are being found");
        
        // Should contain reference count information
        Assert.That(result, Does.Contain("Found") & Does.Contain("references"), 
            "Should contain reference count information");
        
        // Should contain file and line number information
        Assert.That(result, Does.Contain("Program.cs:"), 
            "Should contain filename in the output");
        
        // Should contain line numbers and usage text
        Assert.That(result, Does.Match(@"\d+:.*person"), 
            "Should contain line numbers and usage text");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task FindSymbolReferences_WithPersonType_FindsAllTypeUsages()
    {
        // Test finding all references to the Person type in TestSln
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding references to the Person type (the class declaration)
        var result = await realRoslynTool.FindSymbolReferences(
            testSolutionPath,
            "Program.cs",
            11, // Line with Person class declaration
            "Person"); // The class name

        // Verify the result contains reference information
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find Person type references");
        
        // Should indicate what symbol we're finding references for
        Assert.That(result, Does.Contain("References to 'Person'"), 
            "Should indicate which symbol references are being found");
        
        // Should contain multiple references since Person is used in several places
        Assert.That(result, Does.Contain("Found") & Does.Contain("references"), 
            "Should contain reference count information");
        
        // Should find references in Program.cs
        Assert.That(result, Does.Contain("Program.cs:"), 
            "Should find references in Program.cs");
        
        // Should find the class declaration and usage in new expression
        Assert.That(result, Does.Match(@"\d+:.*Person"), 
            "Should contain line numbers and Person usage");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task FindSymbolReferences_WithPropertyName_FindsPropertyUsages()
    {
        // Test finding all references to the Name property in TestSln
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding references to the Name property
        var result = await realRoslynTool.FindSymbolReferences(
            testSolutionPath,
            "Program.cs",
            17, // Line with Name property declaration
            "Name"); // The property name

        // Verify the result contains reference information
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find Name property references");
        
        // Should indicate what symbol we're finding references for
        Assert.That(result, Does.Contain("References to 'Name'"), 
            "Should indicate which symbol references are being found");
        
        // Should contain reference information
        Assert.That(result, Does.Contain("Found") & Does.Contain("references"), 
            "Should contain reference count information");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task FindSymbolReferences_WithNonExistentToken_ReturnsError()
    {
        // Test with a token that doesn't exist
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding references to a non-existent token
        var result = await realRoslynTool.FindSymbolReferences(
            testSolutionPath,
            "Program.cs",
            11, // Valid line
            "NonExistentToken"); // Token that doesn't exist

        // Verify the result indicates an error
        Assert.That(result, Does.StartWith("Error:"), 
            "Should return error when token is not found");
        Assert.That(result, Does.Contain("Couldn't find token 'NonExistentToken'"), 
            "Error message should indicate which token was not found");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task FindSymbolReferences_WithInvalidLine_ReturnsError()
    {
        // Test with invalid line number
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test with invalid line number
        var result = await realRoslynTool.FindSymbolReferences(
            testSolutionPath,
            "Program.cs",
            9999, // Invalid line number
            "someToken");

        // Verify the result indicates an error
        Assert.That(result, Does.StartWith("Error:"), 
            "Should return error when line number is invalid");
        Assert.That(result, Does.Contain("out of range"), 
            "Error message should indicate line is out of range");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task FindSymbolReferences_OutputFormat_IsCorrect()
    {
        // Test that the output format matches the specification
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding references to person variable
        var result = await realRoslynTool.FindSymbolReferences(
            testSolutionPath,
            "Program.cs",
            52, // Line with 'var person = new Person'
            "person"); // The variable name

        // Verify the output format matches specification:
        // filename:
        //     linenumber: text of usage
        
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find references");
        
        // Should have filename followed by colon
        Assert.That(result, Does.Match(@"Program\.cs:"), 
            "Should have filename followed by colon");
        
        // Should have line numbers indented with usage text
        Assert.That(result, Does.Match(@"\s+\d+:.*person"), 
            "Should have indented line numbers with usage text");
        
        // Should contain summary information at the top
        var lines = result.Split('\n');
        Assert.That(lines[0], Does.Contain("References to 'person'"), 
            "First line should contain reference summary");
        
        realWorkspaceService.Dispose();
    }
}