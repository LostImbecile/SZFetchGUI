using System;
using System.IO;
using System.Diagnostics;
using SZExtractorGUI.Models;

namespace SZExtractorGUI.Services
{
    public class Configuration
    {
        private const string ConfigFile = "config.ini";
        
        public Settings Settings { get; }

        public Configuration()
        {
            Settings = new Settings();
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            if (!File.Exists(ConfigFile))
            {
                throw new FileNotFoundException($"Configuration file {ConfigFile} not found");
            }

            Debug.WriteLine("[Config] Loading configuration from file");
            foreach (var line in File.ReadAllLines(ConfigFile))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("["))
                {
                    Debug.WriteLine($"[Config] Skipping line: {line}");
                    continue;
                }

                var parts = line.Split('=', 2);
                if (parts.Length != 2)
                {
                    Debug.WriteLine($"[Config] Invalid line format: {line}");
                    continue;
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim().Trim('"'); // Remove quotes if present

                Debug.WriteLine($"[Config] Processing: {key}={value}");

                switch (key.ToLower())
                {
                    case "gamedirectory":
                        Settings.GameDirectory = value;
                        Debug.WriteLine($"[Config] Set GameDirectory = '{value}'");
                        break;
                    case "tools_directory":
                        Settings.ToolsDirectory = value;
                        Debug.WriteLine($"[Config] Set ToolsDirectory = '{value}'");
                        break;
                    case "engineversion":
                        Settings.EngineVersion = value;
                        Debug.WriteLine($"[Config] Set EngineVersion = '{value}'");
                        break;
                    case "aeskey":
                        Settings.AesKey = value;
                        Debug.WriteLine($"[Config] Set AesKey = '{value}'");
                        break;
                    case "outputpath":
                        Settings.OutputPath = value;
                        Debug.WriteLine($"[Config] Set OutputPath = '{value}'");
                        break;
                    default:
                        Debug.WriteLine($"[Config] Unknown key: {key}");
                        break;
                }
            }

            Debug.WriteLine($"[Config] Final Settings:\n  GameDirectory: '{Settings.GameDirectory}'\n  EngineVersion: '{Settings.EngineVersion}'\n  OutputPath: '{Settings.OutputPath}'\n  AesKey: '{Settings.AesKey}'");

            ValidateSettings();
        }

        private void ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(Settings.GameDirectory))
                throw new InvalidOperationException("Game_Directory not specified in config.ini");

            if (!Directory.Exists(Settings.GameDirectory))
                throw new DirectoryNotFoundException($"Game directory not found: {Settings.GameDirectory}");
        }
    }
}
