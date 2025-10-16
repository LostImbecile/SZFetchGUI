using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Collections.Generic;
using System.Linq;

namespace SZExtractorGUI.Models
{
    public static class ConfigKeys
    {
        public const string GameDirectory = "GameDirectory";
        public const string ToolsDirectory = "Tools_Directory";
        public const string EngineVersion = "EngineVersion";
        public const string AesKey = "AesKey";
        public const string OutputPath = "OutputPath";
        public const string DisplayLanguage = "DisplayLanguage";
        public const string TextLanguage = "TextLanguage";
        public const string ServerPort = "ServerPort";

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

        private string _displayLanguage = "en";
        private string _textLanguage = "en";
        private int _serverPort = 5000;

        public string GameDirectory { get; set; } = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\DRAGON BALL Sparking! ZERO\\SparkingZERO\\Content\\Paks";
        public string ServerExecutableName { get; set; } = "SZ_Extractor_Server.exe";
        public string ServerExecutablePath { get; private set; }
        public string EngineVersion { get; set; } = "GAME_UE5_1";
        public string AesKey { get; set; } = "0xb2407c45ea7c528738a94c0a25ea8f419de4377628eb30c0ae6a80dd9a9f3ef0";
        
        public int ServerPort
        {
            get => _serverPort;
            set
            {
                if (value < 1024 || value > 65535)
                {
                    throw new ArgumentException("Server port must be between 1024 and 65535");
                }
                _serverPort = value;
            }
        }
        
        public string ServerBaseUrl => $"http://localhost:{ServerPort}/";

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

                string fullPath = Path.GetFullPath(
                    Path.IsPathRooted(value) 
                        ? value 
                        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, value)
                );

                _outputPath = fullPath;
                EnsureOutputDirectoryExists();
            }
        }

        public int ServerStartupTimeoutMs { get; set; } = 10000;

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
            ToolsDirectory = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools")
            );

            OutputPath = "Output";
        }

        private void UpdateServerPath()
        {
            if (string.IsNullOrEmpty(_toolsDirectory))
            {
                throw new InvalidOperationException("Tools directory cannot be null or empty");
            }

            if (!Directory.Exists(_toolsDirectory))
            {
                Directory.CreateDirectory(_toolsDirectory);
            }

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
