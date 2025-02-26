using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using SZExtractorGUI.Models;

namespace SZExtractorGUI.Services.Configuration
{
    public class Configuration
    {
        private const string ConfigFile = "config.ini";
        
        public Settings Settings { get; }

        public Configuration()
        {
            Settings = new Settings();
            try
            {
                LoadConfiguration();
            }
            catch (Exception ex)
            {
                ShowFatalError($"Failed to load configuration:\n{ex.Message}");
            }
        }

        private static void ShowFatalError(string message)
        {
            MessageBox.Show(
                message,
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Environment.Exit(1);
        }

        private void LoadConfiguration()
        {
            if (!File.Exists(ConfigFile))
            {
                Debug.WriteLine("[Config] Configuration file not found, creating with default values");
                CreateDefaultConfiguration();
            }

            try
            {
                Debug.WriteLine("[Config] Loading configuration from file");
                foreach (var line in File.ReadAllLines(ConfigFile))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';') || line.StartsWith('#') || line.StartsWith('['))
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

                    try 
                    {
                        // Pass the raw value to Settings - it will handle path conversion internally
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
                            case "displaylanguage":
                                Settings.DisplayLanguage = value;
                                Debug.WriteLine($"[Config] Set DisplayLanguage = '{value}'");
                                break;
                            case "textlanguage":
                                Settings.TextLanguage = value;
                                Debug.WriteLine($"[Config] Set TextLanguage = '{value}'");
                                break;
                            default:
                                Debug.WriteLine($"[Config] Unknown key: {key}");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowFatalError($"Failed to set configuration value '{key}':\n{ex.Message}");
                    }
                }

                Debug.WriteLine($"[Config] Final Settings:\n  GameDirectory: '{Settings.GameDirectory}'\n  EngineVersion: '{Settings.EngineVersion}'\n  OutputPath: '{Settings.OutputPath}'\n  AesKey: '{Settings.AesKey}'");

                ValidateSettings();
            }
            catch (Exception ex)
            {
                ShowFatalError($"Failed to read configuration file:\n{ex.Message}");
            }
        }

        private void CreateDefaultConfiguration()
        {
            try
            {
                // Get app base directory once
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // Convert default absolute paths to relative where appropriate
                var gameDir = Settings.GameDirectory; // Keep as-is since it's a system path
                var toolsDir = Path.GetRelativePath(baseDir, Settings.ToolsDirectory);
                var outputDir = "Output"; // Use simple relative path

                var defaultConfig = new[]
                {
                    "; SZ Extractor Configuration File",
                    $"GameDirectory=\"{gameDir}\"",
                    $"Tools_Directory=\"{toolsDir}\"", 
                    $"EngineVersion=\"{Settings.EngineVersion}\"",
                    $"AesKey=\"{Settings.AesKey}\"",
                    $"OutputPath=\"{outputDir}\"",
                    $"DisplayLanguage=\"{Settings.DisplayLanguage}\"",
                    $"TextLanguage=\"{Settings.TextLanguage}\""
                };

                Debug.WriteLine("[Config] Writing default configuration file");
                File.WriteAllLines(ConfigFile, defaultConfig);
                Debug.WriteLine("[Config] Default configuration file created successfully");
            }
            catch (Exception ex)
            {
                ShowFatalError($"Failed to create default configuration file:\n{ex.Message}");
            }
        }

        private void ValidateSettings()
        {
            // Validate game directory
            if (string.IsNullOrWhiteSpace(Settings.GameDirectory))
            {
                ShowFatalError("Game_Directory not specified in config.ini");
            }

            if (!Directory.Exists(Settings.GameDirectory))
            {
                ShowFatalError($"Game directory not found, edit it in config.ini:\n{Settings.GameDirectory}");
            }

            // Validate server executable
            if (!Settings.ValidateServerPath())
            {
                ShowFatalError($"Server executable not found or not accessible at:\n{Settings.ServerExecutablePath}");
            }

            // Ensure output path is valid
            try
            {
                Directory.CreateDirectory(Settings.OutputPath);
            }
            catch (Exception ex)
            {
                ShowFatalError($"Failed to create or access output directory:\n{Settings.OutputPath}\nError: {ex.Message}");
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                var configLines = new[]
                {
                    $"GameDirectory=\"{Settings.GameDirectory}\"",
                    $"Tools_Directory=\"{Settings.ToolsDirectory}\"",
                    $"EngineVersion=\"{Settings.EngineVersion}\"",
                    $"AesKey=\"{Settings.AesKey}\"",
                    $"OutputPath=\"{Settings.OutputPath}\"",
                    $"DisplayLanguage=\"{Settings.DisplayLanguage}\"",
                    $"TextLanguage=\"{Settings.TextLanguage}\""
                };

                File.WriteAllLines(ConfigFile, configLines);
                Debug.WriteLine("[Config] Configuration file updated successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Config] Failed to update configuration file: {ex.Message}");
            }
        }
    }
}
