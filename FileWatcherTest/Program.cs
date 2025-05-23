using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoslynMCP.Services;
using System.IO;

namespace FileWatcherTest;

class Program
{
    static async Task Main(string[] args)
    {
        // Set up dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "HH:mm:ss ";
                options.SingleLine = true;
            });
        });
        services.AddSingleton<RoslynWorkspaceService>();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var workspaceService = serviceProvider.GetRequiredService<RoslynWorkspaceService>();

        try
        {
            // Load the test solution
            var testSolutionPath = Path.GetFullPath(Path.Combine("..", "TestSln", "TestSln.sln"));
            logger.LogInformation("Loading solution: {SolutionPath}", testSolutionPath);
            
            var workspace = await workspaceService.GetWorkspaceAsync(testSolutionPath);
            logger.LogInformation("Solution loaded successfully");
            
            // Create test directory for file operations
            var testDir = Path.GetFullPath(Path.Combine("..", "TestSln", "TestProject", "TestFiles"));
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
            Directory.CreateDirectory(testDir);
            logger.LogInformation("Created test directory: {TestDir}", testDir);

            // Give file watcher time to start
            await Task.Delay(1000);

            Console.WriteLine("\n=== Starting File Operation Tests ===\n");
            Console.WriteLine("Starting tests in 2 seconds...");
            await Task.Delay(2000);

            // Test 1: Create a new file
            logger.LogInformation("\n--- Test 1: Creating new file ---");
            var testFile1 = Path.Combine(testDir, "TestClass1.cs");
            await File.WriteAllTextAsync(testFile1, @"
namespace TestProject.TestFiles
{
    public class TestClass1
    {
        public string Name { get; set; } = ""Test"";
    }
}");
            logger.LogInformation("Created file: {FilePath}", testFile1);
            await Task.Delay(2000); // Wait for file watcher to process

            // Test 2: Update the file
            logger.LogInformation("\n--- Test 2: Updating file ---");
            await File.WriteAllTextAsync(testFile1, @"
namespace TestProject.TestFiles
{
    public class TestClass1
    {
        public string Name { get; set; } = ""Updated"";
        public int Age { get; set; } = 25;
    }
}");
            logger.LogInformation("Updated file: {FilePath}", testFile1);
            await Task.Delay(2000);

            // Test 3: Editor-style save (rename to temp, write new, delete temp)
            logger.LogInformation("\n--- Test 3: Editor-style save ---");
            var tempFile = testFile1 + ".tmp";
            File.Move(testFile1, tempFile);
            logger.LogInformation("Renamed to temp: {TempFile}", tempFile);
            await Task.Delay(500);
            
            await File.WriteAllTextAsync(testFile1, @"
namespace TestProject.TestFiles
{
    public class TestClass1
    {
        public string Name { get; set; } = ""Editor Save"";
        public int Age { get; set; } = 30;
        public string Email { get; set; } = ""test@example.com"";
    }
}");
            logger.LogInformation("Wrote new file: {FilePath}", testFile1);
            await Task.Delay(500);
            
            File.Delete(tempFile);
            logger.LogInformation("Deleted temp file: {TempFile}", tempFile);
            await Task.Delay(2000);

            // Test 4: Create multiple files
            logger.LogInformation("\n--- Test 4: Creating multiple files ---");
            for (int i = 2; i <= 5; i++)
            {
                var testFile = Path.Combine(testDir, $"TestClass{i}.cs");
                await File.WriteAllTextAsync(testFile, $@"
namespace TestProject.TestFiles
{{
    public class TestClass{i}
    {{
        public int Id {{ get; set; }} = {i};
    }}
}}");
                logger.LogInformation("Created file: {FilePath}", testFile);
                await Task.Delay(500);
            }
            await Task.Delay(2000);

            // Test 5: Delete a file
            logger.LogInformation("\n--- Test 5: Deleting file ---");
            var fileToDelete = Path.Combine(testDir, "TestClass3.cs");
            File.Delete(fileToDelete);
            logger.LogInformation("Deleted file: {FilePath}", fileToDelete);
            await Task.Delay(2000);

            // Test 6: Rename a file
            logger.LogInformation("\n--- Test 6: Renaming file ---");
            var oldName = Path.Combine(testDir, "TestClass4.cs");
            var newName = Path.Combine(testDir, "RenamedClass.cs");
            File.Move(oldName, newName);
            logger.LogInformation("Renamed file from {OldName} to {NewName}", oldName, newName);
            await Task.Delay(2000);

            // Test 7: Create subdirectory with files
            logger.LogInformation("\n--- Test 7: Creating subdirectory with files ---");
            var subDir = Path.Combine(testDir, "SubFolder");
            Directory.CreateDirectory(subDir);
            logger.LogInformation("Created subdirectory: {SubDir}", subDir);
            await Task.Delay(1000);

            for (int i = 1; i <= 3; i++)
            {
                var subFile = Path.Combine(subDir, $"SubClass{i}.cs");
                await File.WriteAllTextAsync(subFile, $@"
namespace TestProject.TestFiles.SubFolder
{{
    public class SubClass{i}
    {{
        public string SubName {{ get; set; }} = ""Sub{i}"";
    }}
}}");
                logger.LogInformation("Created file in subdirectory: {FilePath}", subFile);
                await Task.Delay(500);
            }
            await Task.Delay(2000);

            // Test 8: Delete entire subdirectory
            logger.LogInformation("\n--- Test 8: Deleting subdirectory ---");
            Directory.Delete(subDir, true);
            logger.LogInformation("Deleted subdirectory: {SubDir}", subDir);
            await Task.Delay(2000);

            // Test 9: Create non-C# files (should be ignored)
            logger.LogInformation("\n--- Test 9: Creating non-C# files ---");
            var txtFile = Path.Combine(testDir, "readme.txt");
            await File.WriteAllTextAsync(txtFile, "This is a text file");
            logger.LogInformation("Created text file: {FilePath}", txtFile);
            
            var jsonFile = Path.Combine(testDir, "config.json");
            await File.WriteAllTextAsync(jsonFile, "{ \"test\": true }");
            logger.LogInformation("Created JSON file: {FilePath}", jsonFile);
            await Task.Delay(2000);

            // Check final workspace state
            logger.LogInformation("\n--- Final Workspace State ---");
            var solution = workspace.CurrentSolution;
            foreach (var project in solution.Projects)
            {
                logger.LogInformation("Project: {ProjectName}", project.Name);
                foreach (var document in project.Documents.OrderBy(d => d.Name))
                {
                    logger.LogInformation("  Document: {DocumentName} ({FilePath})", 
                        document.Name, document.FilePath);
                }
            }

            Console.WriteLine("\n=== Tests Complete ===");
            Console.WriteLine("Waiting 5 seconds before exit...");
            await Task.Delay(5000);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during testing");
        }
        finally
        {
            workspaceService.Dispose();
        }
    }
}