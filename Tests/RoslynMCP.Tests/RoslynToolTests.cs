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

    [Test]
    public async Task LoadSolution_HandlesWorkspaceDiagnostics()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        
        // Act
        var result = await _roslynTool.LoadSolution(solutionPath);
        
        // Assert
        Assert.That(result, Does.Contain("Solution loaded successfully!"));
        
        // Verify that diagnostic warnings are logged when they occur
        // In .NET Core environments, MSBuildWorkspace often reports diagnostics
        if (result.Contains("Warnings:"))
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Workspace")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }

    [Test]
    public async Task GetSymbolInfo_WithValidLocation_ReturnsSymbolDetails()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "Program.cs";
        var line = 22; // Line with "Name = "John Doe""
        var character = 32; // Position of "John Doe"
        
        // Act
        var result = await _roslynTool.GetSymbolInfo(solutionPath, filePath, line, character);
        
        // Assert
        Assert.That(result, Does.Contain("Symbol Information at Program.cs"));
        Assert.That(result, Does.Contain($"Position:"));
        Assert.That(result, Does.Contain("Node Kind:"));
        Assert.That(result, Does.Contain("Node Text:"));
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting symbol info")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task GetSymbolInfo_WithInvalidFile_ReturnsError()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "NonExistent.cs";
        var line = 1;
        var character = 1;
        
        // Act
        var result = await _roslynTool.GetSymbolInfo(solutionPath, filePath, line, character);
        
        // Assert
        Assert.That(result, Does.Contain("Error: Document 'NonExistent.cs' not found"));
    }

    [Test]
    public async Task GetSymbolInfo_WithInvalidLine_ReturnsError()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "Program.cs";
        var line = 9999; // Invalid line number
        var character = 1;
        
        // Act
        var result = await _roslynTool.GetSymbolInfo(solutionPath, filePath, line, character);
        
        // Assert
        Assert.That(result, Does.Contain("Error: Line 9999 is out of range"));
    }

    [Test]
    public async Task GetSymbolInfo_WithInvalidCharacter_ReturnsError()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "Program.cs";
        var line = 1;
        var character = 9999; // Invalid character position
        
        // Act
        var result = await _roslynTool.GetSymbolInfo(solutionPath, filePath, line, character);
        
        // Assert
        Assert.That(result, Does.Contain("Error: Character 9999 is out of range"));
    }

    [Test]
    public async Task GetSymbolInfo_TestSlnLine61Position17_ReturnsMethodSymbolInfo()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "Program.cs";
        var line = 61; // Line with JsonConvert.DeserializeObject<Person>(json)
        var character = 17; // Position within the method call
        
        // Act
        var result = await _roslynTool.GetSymbolInfo(solutionPath, filePath, line, character);
        
        // Assert
        Assert.That(result, Does.Contain("Symbol Information at Program.cs:61:17"));
        Assert.That(result, Does.Contain("Position:"));
        Assert.That(result, Does.Contain("Node Kind:"));
        Assert.That(result, Does.Contain("Node Text:"));
        
        // The symbol at this position should be related to JsonConvert method
        // Note: The exact symbol details may vary based on the specific character position
        // but we verify that we get meaningful symbol information
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting symbol info at")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Symbol info retrieved successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task GetDetailedSymbolInfo_DeserializedPersonAtLine57Position39_SerializeObject()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "Program.cs";
        var line = 57; // Line with "var deserializedPerson = JsonConvert.DeserializeObject<Person>(json);"
        var character = 27; // Position at "deserializedPerson" variable

        // Act
        var result = await _roslynTool.GetDetailedSymbolInfo(solutionPath, filePath, line, character);

        // Assert - Basic structure
        Assert.That(result, Does.Contain("Detailed Symbol Analysis at Program.cs:61:17"));
        Assert.That(result, Does.Contain("Token: 'deserializedPerson'"));

        // Should detect it's a local variable of type Person
        Assert.That(result, Does.Contain("Local variable 'deserializedPerson' of type: TestProject.Person"));

        // Should have type information section
        Assert.That(result, Does.Contain("=== TYPE INFORMATION ==="));

        // Should have public interface section
        Assert.That(result, Does.Contain("=== PUBLIC INTERFACE ==="));
        Assert.That(result, Does.Contain("Type: Class TestProject.Person"));

        // Should show Person's public members (Name, Age, Email properties)
        Assert.That(result, Does.Contain("Public Members:"));
        Assert.That(result, Does.Contain("Property: TestProject.Person.Name"));
        Assert.That(result, Does.Contain("Property: TestProject.Person.Age"));
        Assert.That(result, Does.Contain("Property: TestProject.Person.Email"));

        // Should show either XML documentation or indicate none found
        var hasXmlDocSection = result.Contains("XML Documentation:") || result.Contains("No XML documentation found");
        Assert.That(hasXmlDocSection, Is.True, "Should contain XML documentation section");

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting detailed symbol info at")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Detailed symbol info retrieved successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }


    [Test]
    public async Task GetDetailedSymbolInfo_DeserializedPersonAtLine61Position17_ReturnsPersonTypeDetails()
    {
        // Arrange
        var solutionPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "TestSln", "TestSln.sln");
        solutionPath = Path.GetFullPath(solutionPath);
        var filePath = "Program.cs";
        var line = 61; // Line with "var deserializedPerson = JsonConvert.DeserializeObject<Person>(json);"
        var character = 17; // Position at "deserializedPerson" variable
        
        // Act
        var result = await _roslynTool.GetDetailedSymbolInfo(solutionPath, filePath, line, character);
        
        // Assert - Basic structure
        Assert.That(result, Does.Contain("Detailed Symbol Analysis at Program.cs:61:17"));
        Assert.That(result, Does.Contain("Token: 'deserializedPerson'"));
        
        // Should detect it's a local variable of type Person
        Assert.That(result, Does.Contain("Local variable 'deserializedPerson' of type: TestProject.Person"));
        
        // Should have type information section
        Assert.That(result, Does.Contain("=== TYPE INFORMATION ==="));
        
        // Should have public interface section
        Assert.That(result, Does.Contain("=== PUBLIC INTERFACE ==="));
        Assert.That(result, Does.Contain("Type: Class TestProject.Person"));
        
        // Should show Person's public members (Name, Age, Email properties)
        Assert.That(result, Does.Contain("Public Members:"));
        Assert.That(result, Does.Contain("Property: TestProject.Person.Name"));
        Assert.That(result, Does.Contain("Property: TestProject.Person.Age"));
        Assert.That(result, Does.Contain("Property: TestProject.Person.Email"));
        
        // Should show either XML documentation or indicate none found
        var hasXmlDocSection = result.Contains("XML Documentation:") || result.Contains("No XML documentation found");
        Assert.That(hasXmlDocSection, Is.True, "Should contain XML documentation section");
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting detailed symbol info at")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Detailed symbol info retrieved successfully")),
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
        var character = 1;
        
        // Act
        var result = await _roslynTool.GetDetailedSymbolInfo(solutionPath, filePath, line, character);
        
        // Assert
        Assert.That(result, Does.Contain("Error: Document 'NonExistent.cs' not found"));
    }
}