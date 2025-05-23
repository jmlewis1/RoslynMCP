using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RoslynMCP.Utils
{
    public static class PathConverter
    {
        private static readonly bool IsRunningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly bool IsRunningOnWSL = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && 
            System.IO.File.Exists("/proc/version") && 
            System.IO.File.ReadAllText("/proc/version").Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
        
        /// <summary>
        /// Converts any path to the native format for the current runtime environment and normalizes it.
        /// If running on Windows, converts to Windows path format.
        /// If running on WSL, converts to WSL path format.
        /// </summary>
        public static string ConvertToNativePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string convertedPath;
            
            if (IsRunningOnWindows)
            {
                // Convert to Windows path format
                convertedPath = ToWindowsPath(path);
            }
            else if (IsRunningOnWSL)
            {
                // Convert to WSL path format
                convertedPath = ToWslPath(path);
            }
            else
            {
                // On other Linux/Unix systems, assume paths are already in correct format
                convertedPath = path;
            }

            // Normalize the path using Path.GetFullPath
            try
            {
                return Path.GetFullPath(convertedPath);
            }
            catch
            {
                // If normalization fails, return the converted path as-is
                return convertedPath;
            }
        }

        /// <summary>
        /// Gets whether the application is running on Windows (native or WSL).
        /// Returns true if running on Windows native or WSL, false otherwise.
        /// </summary>
        public static bool IsWindowsEnvironment => IsRunningOnWindows || IsRunningOnWSL;

        /// <summary>
        /// Gets whether the application is running on native Windows (not WSL).
        /// </summary>
        public static bool IsNativeWindows => IsRunningOnWindows;

        /// <summary>
        /// Gets whether the application is running on WSL.
        /// </summary>
        public static bool IsWSL => IsRunningOnWSL;

        public static string ToOtherPath(string path)
        {
            if(IsWindowsPath(path))
                return ToWslPath(path);
            if(IsWslPath(path))
                return ToWindowsPath(path);

            throw new ArgumentException($"{path} is neither a Windows or WSL path");
        }


        public static string ConvertWindowsToWslPath(string windowsPath)
        {
            if (string.IsNullOrEmpty(windowsPath))
                return windowsPath;

            // Handle UNC paths (\\server\share)
            if (windowsPath.StartsWith("\\\\"))
            {
                return windowsPath.Replace('\\', '/');
            }

            // Handle drive letter paths (C:\folder)
            if (windowsPath.Length >= 2 && windowsPath[1] == ':')
            {
                string driveLetter = windowsPath[0].ToString().ToLower();
                string remainingPath = windowsPath.Substring(2).Replace('\\', '/');
                return $"/mnt/{driveLetter}{remainingPath}";
            }

            return windowsPath;
        }

        public static string ConvertWslToWindowsPath(string wslPath)
        {
            if (string.IsNullOrEmpty(wslPath))
                return wslPath;

            // Handle /mnt/c/ style paths
            if (wslPath.StartsWith("/mnt/"))
            {
                var parts = wslPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string driveLetter = parts[1].ToUpper();
                    string remainingPath = string.Join("\\", parts.Skip(2));
                    return $"{driveLetter}:\\{remainingPath}";
                }
            }

            // Handle UNC-style paths (//<server>/share)
            if (wslPath.StartsWith("//"))
            {
                return wslPath.Replace('/', '\\');
            }

            return wslPath;
        }

        public static string ToWindowsPath(string path)
        {
            if (IsWindowsPath(path)) return path;
            return ConvertWslToWindowsPath(path);
        }

        public static string ToWslPath(string path)
        {
            if (IsWslPath(path)) return path;
            return ConvertWindowsToWslPath(path);
        }

        private static bool IsWindowsPath(string path) =>
            !string.IsNullOrEmpty(path) &&
            (path.Length >= 2 && path[1] == ':' || path.StartsWith("\\\\"));

        private static bool IsWslPath(string path) =>
            !string.IsNullOrEmpty(path) && path.StartsWith("/");
    }
}
