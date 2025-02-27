using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Collections.Generic;
using System.Linq;

namespace SZExtractorGUI.Models
{
    // Add this new class for centralized config keys
    public static class ConfigKeys
    {
        public const string GameDirectory = "GameDirectory";
        public const string ToolsDirectory = "Tools_Directory";
        public const string EngineVersion = "EngineVersion";
        public const string AesKey = "AesKey";
        public const string OutputPath = "OutputPath";
        public const string DisplayLanguage = "DisplayLanguage";
        public const string TextLanguage = "TextLanguage";

        // Helper method to get all config keys
        public static IEnumerable<string> GetAllKeys()
        {
            return typeof(ConfigKeys)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                .Select(fi => (string)fi.GetValue(null)!);
        }
    }

    public class Settings
    {
        private string _toolsDirectory;
        private string _outputPath;

        // Update language properties to preserve case
        private string _displayLanguage = "en";
        private string _textLanguage = "en";

        public string GameDirectory { get; set; } = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\DRAGON BALL Sparking! ZERO\\SparkingZERO\\Content\\Paks";
        public string ServerExecutableName { get; set; } = "SZ_Extractor_Server.exe";
        public string ServerExecutablePath { get; private set; }
        public string EngineVersion { get; set; } = "GAME_UE5_1";
        public string AesKey { get; set; } = "0xb2407c45ea7c528738a94c0a25ea8f419de4377628eb30c0ae6a80dd9a9f3ef0";
        public string ServerBaseUrl { get; set; } = "http://localhost:5000/";

        public string DisplayLanguage
        {
            get => _displayLanguage;
            set => _displayLanguage = string.IsNullOrEmpty(value) ? "en" : value;
        }

        public string TextLanguage
        {
            get => _textLanguage;
            set => _textLanguage = string.IsNullOrEmpty(value) ? "en" : value;
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Output path cannot be null or empty");
                }

                // Get full path, handling both absolute and relative paths
                string fullPath = Path.GetFullPath(
                    Path.IsPathRooted(value) 
                        ? value 
                        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, value)
                );

                _outputPath = fullPath;
                EnsureOutputDirectoryExists();
            }
        }

        // Keep only essential server settings
        public int ServerStartupTimeoutMs { get; set; } = 10000;

        // Process ID of the parent application
        public int ParentProcessId { get; } = Process.GetCurrentProcess().Id;

        public string ToolsDirectory
        {
            get => _toolsDirectory;
            set
            {
                _toolsDirectory = value;
                UpdateServerPath();
            }
        }

        public Settings()
        {
            // Initialize with default tools directory relative to application
            ToolsDirectory = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools")
            );

            // Set default output path using the new property setter
            OutputPath = "Output";
        }

        private void UpdateServerPath()
        {
            if (string.IsNullOrEmpty(_toolsDirectory))
            {
                throw new InvalidOperationException("Tools directory cannot be null or empty");
            }

            // Ensure tools directory exists
            if (!Directory.Exists(_toolsDirectory))
            {
                Directory.CreateDirectory(_toolsDirectory);
            }

            // Get full path and verify it's within the application directory for security
            string fullPath = Path.GetFullPath(Path.Combine(_toolsDirectory, ServerExecutableName));
            if (!fullPath.StartsWith(AppDomain.CurrentDomain.BaseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Server executable must be within the application directory");
            }

            ServerExecutablePath = fullPath;
        }

        private void EnsureOutputDirectoryExists()
        {
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
        }

        public bool ValidateServerPath()
        {
            if (string.IsNullOrEmpty(ServerExecutablePath))
                return false;

            if (!File.Exists(ServerExecutablePath))
                return false;

            try
            {
                // Verify file permissions
                using var fs = File.OpenRead(ServerExecutablePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
