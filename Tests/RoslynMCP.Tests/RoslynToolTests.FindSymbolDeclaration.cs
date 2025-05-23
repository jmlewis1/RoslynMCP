using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Moq;
using RoslynMCP.Services;
using RoslynMCP.Tools;

namespace RoslynMCP.Tests;

public partial class RoslynToolTests
{
    [Test]
    public async Task FindSymbolDeclaration_WithValidParameters_CallsWorkspaceService()
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
        var result = await _roslynTool.FindSymbolDeclaration(solutionPath, filePath, line, tokenToFind);
        
        // Assert
        _mockWorkspaceService.Verify(ws => ws.GetSolutionAsync(solutionPath), Times.Once);
        // GetDocumentAsync should not be called when solution is null
        _mockWorkspaceService.Verify(ws => ws.GetDocumentAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Finding symbol declaration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task FindSymbolDeclaration_WithInvalidSolution_ReturnsError()
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
        var result = await _roslynTool.FindSymbolDeclaration(solutionPath, filePath, line, tokenToFind);
        
        // Assert
        Assert.That(result, Does.Contain("Error: Could not load solution"));
    }

    [Test]
    public async Task FindSymbolDeclaration_WithPersonClass_Line11_FindsClassDeclaration()
    {
        // Test finding the declaration of Person class from line 11
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding declaration of Person class on line 11
        var result = await realRoslynTool.FindSymbolDeclaration(
            testSolutionPath,
            "Program.cs",
            11, // Line with 'public class Person'
            "Person"); // The class name

        // Verify the result contains declaration information
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find Person class declaration");
        
        // Should contain symbol information
        Assert.That(result, Does.Contain("Symbol: Person"), 
            "Should show the symbol name");
        Assert.That(result, Does.Contain("Kind: NamedType"), 
            "Should identify it as a NamedType");
        
        // Should contain declaration location
        Assert.That(result, Does.Contain("Declaration:"), 
            "Should have a declaration section");
        Assert.That(result, Does.Contain("File:") & Does.Contain("Program.cs"), 
            "Should show the file containing the declaration");
        Assert.That(result, Does.Contain("Line: 11"), 
            "Should show line 11 as the declaration location");
        
        // Should contain type documentation
        Assert.That(result, Does.Contain("Type Documentation:"), 
            "Should include type documentation section");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task FindSymbolDeclaration_WithNameProperty_Line17_FindsPropertyDeclaration()
    {
        // Test finding the declaration of Name property from line 17
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding declaration of Name property on line 17
        var result = await realRoslynTool.FindSymbolDeclaration(
            testSolutionPath,
            "Program.cs",
            17, // Line with 'public string Name { get; set; }'
            "Name"); // The property name

        // Verify the result contains declaration information
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find Name property declaration");
        
        // Should contain symbol information
        Assert.That(result, Does.Contain("Symbol: Name"), 
            "Should show the symbol name");
        Assert.That(result, Does.Contain("Kind: Property"), 
            "Should identify it as a Property");
        
        // Should contain declaration location
        Assert.That(result, Does.Contain("Line: 17"), 
            "Should show line 17 as the declaration location");
        
        // Should contain member information
        Assert.That(result, Does.Contain("Member of: TestProject.Person"), 
            "Should show it's a member of Person class");
        Assert.That(result, Does.Contain("Type: string"), 
            "Should show the property type");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task FindSymbolDeclaration_WithAFieldField_Line31_FindsFieldDeclaration()
    {
        // Test finding the declaration of aField field from line 31
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding declaration of aField field on line 31
        var result = await realRoslynTool.FindSymbolDeclaration(
            testSolutionPath,
            "Program.cs",
            31, // Line with 'public int aField = 0;'
            "aField"); // The field name

        // Verify the result contains declaration information
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find aField field declaration");
        
        // Should contain symbol information
        Assert.That(result, Does.Contain("Symbol: aField"), 
            "Should show the symbol name");
        Assert.That(result, Does.Contain("Kind: Field"), 
            "Should identify it as a Field");
        
        // Should contain declaration location
        Assert.That(result, Does.Contain("Line: 31"), 
            "Should show line 31 as the declaration location");
        
        // Should contain member information
        Assert.That(result, Does.Contain("Member of: TestProject.Person"), 
            "Should show it's a member of Person class");
        Assert.That(result, Does.Contain("Type: int"), 
            "Should show the field type");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task FindSymbolDeclaration_WithPersonVariable_Line52_FindsVariableDeclaration()
    {
        // Test finding the declaration of person variable used on line 59, declared on line 52
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding declaration of person variable from its usage on line 59
        var result = await realRoslynTool.FindSymbolDeclaration(
            testSolutionPath,
            "Program.cs",
            59, // Line with usage: 'string json = JsonConvert.SerializeObject(person, Formatting.Indented);'
            "person"); // The variable name

        // Verify the result contains declaration information
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find person variable declaration");
        
        // Should contain symbol information
        Assert.That(result, Does.Contain("Symbol: person"), 
            "Should show the symbol name");
        Assert.That(result, Does.Contain("Kind: Local"), 
            "Should identify it as a Local variable");
        
        // Should contain declaration location
        Assert.That(result, Does.Contain("Line: 52"), 
            "Should show line 52 as the declaration location");
        
        // Should contain variable information
        Assert.That(result, Does.Contain("Local Variable Information:"), 
            "Should have local variable section");
        Assert.That(result, Does.Contain("Type: TestProject.Person"), 
            "Should show the variable type");
        
        // Should contain type documentation for the variable's type
        Assert.That(result, Does.Contain("Variable Type Documentation:"), 
            "Should include documentation for the variable's type");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task FindSymbolDeclaration_WithInvalidLine_ReturnsError()
    {
        // Test with invalid line number
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test with invalid line number
        var result = await realRoslynTool.FindSymbolDeclaration(
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
    public async Task FindSymbolDeclaration_WithNonExistentToken_ReturnsError()
    {
        // Test with a token that doesn't exist
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding a non-existent token
        var result = await realRoslynTool.FindSymbolDeclaration(
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
    public async Task FindSymbolDeclaration_WithMetadataSymbol_ReturnsMetadataInfo()
    {
        // Test finding declaration of a System type (like Console)
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test finding declaration of Console (metadata symbol)
        var result = await realRoslynTool.FindSymbolDeclaration(
            testSolutionPath,
            "Program.cs",
            48, // Line with 'Console.WriteLine(...)'
            "Console"); // System.Console

        // Verify the result contains metadata information
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find Console metadata");
        
        // Should contain symbol information
        Assert.That(result, Does.Contain("Symbol: Console"), 
            "Should show the symbol name");
        
        // Should indicate it's from metadata
        Assert.That(result, Does.Contain("Location: Metadata"), 
            "Should indicate it's from metadata/external assembly");
        Assert.That(result, Does.Contain("Namespace: System"), 
            "Should show the System namespace");
        
        realWorkspaceService.Dispose();
    }
}