using Microsoft.Extensions.Logging;
using Moq;
using RoslynMCP.Services;
using RoslynMCP.Tools;

namespace RoslynMCP.Tests;

[TestFixture]
public class TokenFindingTests
{
    private Mock<ILogger<RoslynTool>> _mockLogger;
    private Mock<IRoslynWorkspaceService> _mockWorkspaceService;
    private RoslynTool _roslynTool;
    private string _testSolutionPath;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<RoslynTool>>();
        _mockWorkspaceService = new Mock<IRoslynWorkspaceService>();
        _roslynTool = new RoslynTool(_mockLogger.Object, _mockWorkspaceService.Object);
        
        // Get the path to TestSln
        _testSolutionPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory, 
            "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        _testSolutionPath = Path.GetFullPath(_testSolutionPath);
    }

    [Test]
    public async Task TokenFinding_WithRealWorkspace_FindsVariableToken()
    {
        // This test uses the real workspace service to test token finding
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        // Test finding a variable token that should exist in TestSln Program.cs
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
            "Program.cs",
            46, // Line with Main method parameters
            "args"); // The args parameter
        
        // Verify the result contains expected information about the args parameter
        Assert.That(result, Does.Not.StartWith("Error:"), 
            "Token finding should succeed for 'args' parameter");
        Assert.That(result, Does.Contain("args"), 
            "Result should contain information about the 'args' token");
        Assert.That(result, Does.Contain("Parameter") | Does.Contain("string[]"), 
            "Result should indicate this is a parameter of type string[]");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task TokenFinding_WithRealWorkspace_FindsClassNameToken()
    {
        // This test uses the real workspace service to test token finding
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        // Test finding a class name token
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
            "Program.cs",
            11, // Line with Person class declaration
            "Person"); // The class name
        
        // Verify the result contains expected information about the Person class
        Assert.That(result, Does.Not.StartWith("Error:"), 
            "Token finding should succeed for 'Person' class");
        Assert.That(result, Does.Contain("Person"), 
            "Result should contain information about the 'Person' token");
        Assert.That(result, Does.Contain("Class") | Does.Contain("Type"), 
            "Result should indicate this is a class/type");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task TokenFinding_WithRealWorkspace_FindsPropertyToken()
    {
        // This test uses the real workspace service to test token finding
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        // Test finding a property token
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
            "Program.cs",
            17, // Line with Name property
            "Name"); // The property name
        
        // Verify the result contains expected information about the Name property
        Assert.That(result, Does.Not.StartWith("Error:"), 
            "Token finding should succeed for 'Name' property");
        Assert.That(result, Does.Contain("Name"), 
            "Result should contain information about the 'Name' token");
        Assert.That(result, Does.Contain("Property") | Does.Contain("string"), 
            "Result should indicate this is a property of type string");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task TokenFinding_WithNonExistentToken_ReturnsError()
    {
        // This test uses the real workspace service to test error handling
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        // Test finding a token that doesn't exist
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
            "Program.cs",
            11, // Valid line
            "NonExistentToken"); // Token that doesn't exist on this line
        
        // Verify the result indicates an error
        Assert.That(result, Does.StartWith("Error:"), 
            "Should return error when token is not found");
        Assert.That(result, Does.Contain("Couldn't find token 'NonExistentToken'"), 
            "Error message should indicate which token was not found");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task TokenFinding_WithInvalidLine_ReturnsError()
    {
        // This test uses the real workspace service to test error handling
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        // Test with invalid line number
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
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
    public async Task TokenFinding_WithRealWorkspace_FindsVariableDeclaration_person()
    {
        // Test finding 'person' variable declaration on line 52
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
            "Program.cs",
            52, // Line with 'var person = new Person'
            "person"); // The variable name
        
        Assert.That(result, Does.Not.StartWith("Error:"), 
            "Token finding should succeed for 'person' variable");
        Assert.That(result, Does.Contain("person"), 
            "Result should contain information about the 'person' token");
        Assert.That(result, Does.Contain("Variable") | Does.Contain("Local"), 
            "Result should indicate this is a variable/local");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task TokenFinding_WithRealWorkspace_FindsClassInNewExpression_Person()
    {
        // Test finding 'Person' class in new expression on line 52
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
            "Program.cs",
            52, // Line with 'new Person'
            "Person"); // The class name in new expression
        
        Assert.That(result, Does.Not.StartWith("Error:"), 
            "Token finding should succeed for 'Person' in new expression");
        Assert.That(result, Does.Contain("Person"), 
            "Result should contain information about the 'Person' token");
        Assert.That(result, Does.Contain("Class") | Does.Contain("Type"), 
            "Result should indicate this is a class/type");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task TokenFinding_WithRealWorkspace_FindsSystemClass_Console()
    {
        // Test finding 'Console' system class on line 64
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
            "Program.cs",
            64, // Line with Console.WriteLine
            "Console"); // The Console class
        
        Assert.That(result, Does.Not.StartWith("Error:"), 
            "Token finding should succeed for 'Console' system class");
        Assert.That(result, Does.Contain("Console"), 
            "Result should contain information about the 'Console' token");
        Assert.That(result, Does.Contain("Class") | Does.Contain("Type"), 
            "Result should indicate this is a class/type");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task TokenFinding_WithRealWorkspace_FindsFieldDeclaration_aField()
    {
        // Test finding 'aField' field declaration on line 31
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
            "Program.cs",
            31, // Line with 'public int aField = 0;'
            "aField"); // The field name
        
        Assert.That(result, Does.Not.StartWith("Error:"), 
            "Token finding should succeed for 'aField' field");
        Assert.That(result, Does.Contain("aField"), 
            "Result should contain information about the 'aField' token");
        Assert.That(result, Does.Contain("Field") | Does.Contain("int"), 
            "Result should indicate this is a field of type int");
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task TokenFinding_WithRealWorkspace_FindsVariableDeclaration_services()
    {
        // Test finding 'services' variable declaration on line 67
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var result = await realRoslynTool.GetDetailedSymbolInfo(
            _testSolutionPath,
            "Program.cs",
            67, // Line with 'var services = new ServiceCollection();'
            "services"); // The variable name
        
        Assert.That(result, Does.Not.StartWith("Error:"), 
            "Token finding should succeed for 'services' variable");
        Assert.That(result, Does.Contain("services"), 
            "Result should contain information about the 'services' token");
        Assert.That(result, Does.Contain("Variable") | Does.Contain("Local"), 
            "Result should indicate this is a variable/local");
        
        realWorkspaceService.Dispose();
    }
}