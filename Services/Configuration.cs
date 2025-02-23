using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
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
            try
            {
                LoadConfiguration();
            }
            catch (Exception ex)
            {
                ShowFatalError($"Failed to load configuration:\n{ex.Message}");
            }
        }

        private void ShowFatalError(string message)
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
                ShowFatalError($"Configuration file {ConfigFile} not found");
            }

            try
            {
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

                    try 
                    {
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

        private void ValidateSettings()
        {
            // Validate game directory
            if (string.IsNullOrWhiteSpace(Settings.GameDirectory))
            {
                ShowFatalError("Game_Directory not specified in config.ini");
            }

            if (!Directory.Exists(Settings.GameDirectory))
            {
                ShowFatalError($"Game directory not found:\n{Settings.GameDirectory}");
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
    }
}
