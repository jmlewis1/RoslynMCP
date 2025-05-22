using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Moq;
using RoslynMCP.Services;
using RoslynMCP.Tools;

namespace RoslynMCP.Tests;

public partial class RoslynToolTests
{
    [Test]
    public async Task GetTypeDocumentation_WithValidTypeName_CallsWorkspaceService()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var typeName = "TestProject.Person";
        
        // Setup mock solution
        _mockWorkspaceService
            .Setup(ws => ws.GetSolutionAsync(solutionPath))
            .ReturnsAsync(null as Solution);
        
        // Act
        var result = await _roslynTool.GetTypeDocumentation(solutionPath, typeName);
        
        // Assert
        _mockWorkspaceService.Verify(ws => ws.GetSolutionAsync(solutionPath), Times.Once);
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting type documentation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task GetTypeDocumentation_WithInvalidSolution_ReturnsError()
    {
        // Arrange
        var solutionPath = "NonExistent.sln";
        var typeName = "SomeType";
        
        // Setup mock to return null solution
        _mockWorkspaceService
            .Setup(ws => ws.GetSolutionAsync(solutionPath))
            .ReturnsAsync(null as Solution);
        
        // Act
        var result = await _roslynTool.GetTypeDocumentation(solutionPath, typeName);
        
        // Assert
        Assert.That(result, Does.Contain("Error: Could not load solution"));
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Getting type documentation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task GetTypeDocumentation_WithReflectionTypes_FindsCommonTypes()
    {
        // This test uses Reflection to get fully qualified type names and tests Roslyn's ability to find them
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        // Test common .NET types using reflection
        var stringType = typeof(string);
        var intType = typeof(int);
        var listType = typeof(List<>);
        
        var testCases = new[]
        {
            stringType.FullName!, // System.String
            intType.FullName!,    // System.Int32  
            listType.FullName!    // System.Collections.Generic.List`1
        };

        // Create a minimal test solution that references these types
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        foreach (var typeName in testCases)
        {
            // Act
            var result = await realRoslynTool.GetTypeDocumentation(testSolutionPath, typeName);
            
            // Assert - For system types, we might not find them in our solution, but the method should handle it gracefully
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
            
            // The result should either show type info or indicate the type wasn't found in solution
            Assert.That(result.Contains("Type Documentation") || result.Contains("not found"), Is.True,
                $"Expected meaningful response for type {typeName}");
        }
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetTypeDocumentation_WithPersonFromTestSln_ReturnsInheritanceInfo()
    {
        // Bootstrap test for Person type in TestSln - we'll figure out the fully qualified name
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Try different possible fully qualified names for Person in TestSln
        var possibleNames = new[]
        {
            "TestProject.Person",
            "TestSln.TestProject.Person", 
            "Person",
            "TestSln.Person"
        };

        string? workingName = null;
        string? result = null;

        foreach (var name in possibleNames)
        {
            result = await realRoslynTool.GetTypeDocumentation(testSolutionPath, name);
            
            if (!result.Contains("not found"))
            {
                workingName = name;
                break;
            }
        }

        // Assert - we should find at least one working name
        Assert.That(workingName, Is.Not.Null, 
            $"Could not find Person type with any of these names: {string.Join(", ", possibleNames)}");
        
        Assert.That(result, Does.Contain("Type Documentation"));
        Assert.That(result, Does.Contain("INHERITANCE TREE"));
        Assert.That(result, Does.Contain("PUBLIC INTERFACE"));
        
        // Verify that the Full Name field shows the fully qualified name
        Assert.That(result, Does.Contain("Full Name: TestProject.Person"), 
            "Full Name should show the fully qualified type name");
        
        // The Person class should have some basic properties
        Assert.That(result, Does.Contain("Name") | Does.Contain("Age") | Does.Contain("Email"));
        
        realWorkspaceService.Dispose();
    }

    [Test]
    public async Task GetTypeDocumentation_VerifyFullyQualifiedNameFormat()
    {
        // Test to verify the Full Name field returns the correct fully qualified name format
        var realWorkspaceService = new RoslynWorkspaceService(
            new Mock<ILogger<RoslynWorkspaceService>>().Object);
        
        var realRoslynTool = new RoslynTool(_mockLogger.Object, realWorkspaceService);
        
        var testSolutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        testSolutionPath = Path.GetFullPath(testSolutionPath);

        // Test with TestProject.Person which we know exists
        var result = await realRoslynTool.GetTypeDocumentation(testSolutionPath, "TestProject.Person");
        
        // Verify the output format is correct
        Assert.That(result, Does.Not.StartWith("Error:"), "Should successfully find TestProject.Person");
        
        // Extract the Full Name line to verify exact format
        var lines = result.Split('\n');
        var fullNameLine = lines.FirstOrDefault(line => line.StartsWith("Full Name:"))?.Trim();
        
        Assert.That(fullNameLine, Is.Not.Null, "Should contain a 'Full Name:' line");
        Assert.That(fullNameLine, Is.EqualTo("Full Name: TestProject.Person"), 
            "Full Name should show the exact fully qualified type name");
        
        // Also verify Name field shows just the type name
        var nameLine = lines.FirstOrDefault(line => line.StartsWith("Name:"))?.Trim();
        Assert.That(nameLine, Is.EqualTo("Name: Person"), 
            "Name should show just the type name without namespace");
        
        realWorkspaceService.Dispose();
    }
}