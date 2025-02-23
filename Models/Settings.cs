using System;
using System.IO;
using System.Diagnostics;

namespace SZExtractorGUI.Models
{
    public class Settings
    {
        private string _toolsDirectory;
        
        public string GameDirectory { get; set; }
        public string ServerExecutableName { get; set; } = "SZ_Extractor_Server.exe";
        public string ServerExecutablePath { get; private set; }
        public string EngineVersion { get; set; } = "GAME_UE5_1";
        public string AesKey { get; set; }
        public string OutputPath { get; set; }
        public string ServerBaseUrl { get; set; } = "http://localhost:5000/";

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
