using NUnit.Framework;
using RoslynMCP.Utils;
using System.Runtime.InteropServices;

namespace RoslynMCP.Tests
{
    [TestFixture]
    public class PathConverterTests
    {
        private bool IsRunningOnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private bool IsRunningOnWSL => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && 
            System.IO.File.Exists("/proc/version") && 
            System.IO.File.ReadAllText("/proc/version").Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

        [Test]
        public void ConvertToNativePath_WithEmptyString_ReturnsEmptyString()
        {
            // Arrange
            string path = "";

            // Act
            string result = PathConverter.ConvertToNativePath(path);

            // Assert
            Assert.That(result, Is.EqualTo(""));
        }

        [Test]
        public void ConvertToNativePath_WithNull_ReturnsNull()
        {
            // Arrange
            string? path = null;

            // Act
            string? result = PathConverter.ConvertToNativePath(path!);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConvertToNativePath_WindowsPath_OnWindows_NormalizesPath()
        {
            // This test only makes sense when running on Windows
            if (!IsRunningOnWindows) 
            {
                Assert.Ignore("This test requires Windows environment");
                return;
            }

            // Arrange
            string path = "C:\\Users\\test\\..\\test\\file.txt";

            // Act
            string result = PathConverter.ConvertToNativePath(path);

            // Assert
            Assert.That(result, Does.EndWith(@"test\file.txt"));
            Assert.That(result, Does.StartWith("C:\\"));
        }

        [Test]
        public void ConvertToNativePath_WSLPath_OnWindows_ConvertsAndNormalizes()
        {
            // This test only makes sense when running on Windows
            if (!IsRunningOnWindows)
            {
                Assert.Ignore("This test requires Windows environment");
                return;
            }

            // Arrange
            string path = "/mnt/c/Users/test/file.txt";

            // Act
            string result = PathConverter.ConvertToNativePath(path);

            // Assert
            Assert.That(result, Is.EqualTo(@"C:\Users\test\file.txt"));
        }

        [Test]
        public void ConvertToNativePath_WindowsPath_OnWSL_ConvertsToWSLPath()
        {
            // This test only makes sense when running on WSL
            if (!IsRunningOnWSL)
            {
                Assert.Ignore("This test requires WSL environment");
                return;
            }

            // Arrange
            string path = "C:\\Users\\test\\file.txt";

            // Act
            string result = PathConverter.ConvertToNativePath(path);

            // Assert
            Assert.That(result, Does.StartWith("/mnt/c/"));
            Assert.That(result, Does.Contain("Users/test/file.txt"));
        }

        [Test]
        public void ConvertToNativePath_WSLPath_OnWSL_NormalizesPath()
        {
            // This test only makes sense when running on WSL
            if (!IsRunningOnWSL)
            {
                Assert.Ignore("This test requires WSL environment");
                return;
            }

            // Arrange
            string path = "/mnt/c/Users/test/../test/file.txt";

            // Act
            string result = PathConverter.ConvertToNativePath(path);

            // Assert
            Assert.That(result, Does.EndWith("test/file.txt"));
            Assert.That(result, Does.StartWith("/mnt/c/"));
        }

        [Test]
        public void ConvertToNativePath_UNCPath_OnWindows_PreservesUNCFormat()
        {
            if (!IsRunningOnWindows)
            {
                Assert.Ignore("This test requires Windows environment");
                return;
            }

            // Arrange
            string path = "\\\\server\\share\\file.txt";

            // Act
            string result = PathConverter.ConvertToNativePath(path);

            // Assert
            Assert.That(result, Is.EqualTo(@"\\server\share\file.txt"));
        }

        [Test]
        public void ToWindowsPath_WithWindowsPath_ReturnsSamePath()
        {
            // Arrange
            string path = "C:\\Users\\test\\file.txt";

            // Act
            string result = PathConverter.ToWindowsPath(path);

            // Assert
            Assert.That(result, Is.EqualTo(path));
        }

        [Test]
        public void ToWindowsPath_WithWSLPath_ConvertsCorrectly()
        {
            // Arrange
            string path = "/mnt/c/Users/test/file.txt";

            // Act
            string result = PathConverter.ToWindowsPath(path);

            // Assert
            Assert.That(result, Is.EqualTo(@"C:\Users\test\file.txt"));
        }

        [Test]
        public void ToWindowsPath_WithWSLPathDifferentDrive_ConvertsCorrectly()
        {
            // Arrange
            string path = "/mnt/d/Data/file.txt";

            // Act
            string result = PathConverter.ToWindowsPath(path);

            // Assert
            Assert.That(result, Is.EqualTo(@"D:\Data\file.txt"));
        }

        [Test]
        public void ToWslPath_WithWSLPath_ReturnsSamePath()
        {
            // Arrange
            string path = "/mnt/c/Users/test/file.txt";

            // Act
            string result = PathConverter.ToWslPath(path);

            // Assert
            Assert.That(result, Is.EqualTo(path));
        }

        [Test]
        public void ToWslPath_WithWindowsPath_ConvertsCorrectly()
        {
            // Arrange
            string path = "C:\\Users\\test\\file.txt";

            // Act
            string result = PathConverter.ToWslPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("/mnt/c/Users/test/file.txt"));
        }

        [Test]
        public void ToWslPath_WithWindowsPathDifferentDrive_ConvertsCorrectly()
        {
            // Arrange
            string path = "D:\\Data\\file.txt";

            // Act
            string result = PathConverter.ToWslPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("/mnt/d/Data/file.txt"));
        }

        [Test]
        public void ToOtherPath_WithWindowsPath_ReturnsWSLPath()
        {
            // Arrange
            string path = "C:\\Users\\test\\file.txt";

            // Act
            string result = PathConverter.ToOtherPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("/mnt/c/Users/test/file.txt"));
        }

        [Test]
        public void ToOtherPath_WithWSLPath_ReturnsWindowsPath()
        {
            // Arrange
            string path = "/mnt/c/Users/test/file.txt";

            // Act
            string result = PathConverter.ToOtherPath(path);

            // Assert
            Assert.That(result, Is.EqualTo(@"C:\Users\test\file.txt"));
        }

        [Test]
        public void ToOtherPath_WithInvalidPath_ThrowsException()
        {
            // Arrange
            string path = "relative/path/file.txt";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => PathConverter.ToOtherPath(path));
        }

        [Test]
        public void IsWindowsEnvironment_ReturnsCorrectValue()
        {
            // Act
            bool result = PathConverter.IsWindowsEnvironment;

            // Assert
            Assert.That(result, Is.EqualTo(IsRunningOnWindows || IsRunningOnWSL));
        }

        [Test]
        public void IsNativeWindows_ReturnsCorrectValue()
        {
            // Act
            bool result = PathConverter.IsNativeWindows;

            // Assert
            Assert.That(result, Is.EqualTo(IsRunningOnWindows));
        }

        [Test]
        public void IsWSL_ReturnsCorrectValue()
        {
            // Act
            bool result = PathConverter.IsWSL;

            // Assert
            Assert.That(result, Is.EqualTo(IsRunningOnWSL));
        }

        [Test]
        public void ConvertWindowsToWslPath_HandlesPathWithSpaces()
        {
            // Arrange
            string path = @"C:\Program Files\Some App\file.txt";

            // Act
            string result = PathConverter.ConvertWindowsToWslPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("/mnt/c/Program Files/Some App/file.txt"));
        }

        [Test]
        public void ConvertWslToWindowsPath_HandlesPathWithSpaces()
        {
            // Arrange
            string path = "/mnt/c/Program Files/Some App/file.txt";

            // Act
            string result = PathConverter.ConvertWslToWindowsPath(path);

            // Assert
            Assert.That(result, Is.EqualTo(@"C:\Program Files\Some App\file.txt"));
        }

        [Test]
        public void ConvertWindowsToWslPath_HandlesUNCPath()
        {
            // Arrange
            string path = @"\\server\share\folder\file.txt";

            // Act
            string result = PathConverter.ConvertWindowsToWslPath(path);

            // Assert
            Assert.That(result, Is.EqualTo("//server/share/folder/file.txt"));
        }

        [Test]
        public void ConvertWslToWindowsPath_HandlesUNCStylePath()
        {
            // Arrange
            string path = "//server/share/folder/file.txt";

            // Act
            string result = PathConverter.ConvertWslToWindowsPath(path);

            // Assert
            Assert.That(result, Is.EqualTo(@"\\server\share\folder\file.txt"));
        }
    }
}