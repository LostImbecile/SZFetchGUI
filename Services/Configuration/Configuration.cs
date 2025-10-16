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
                            case var _ when key.Equals(ConfigKeys.GameDirectory, StringComparison.OrdinalIgnoreCase):
                                // DLCs are in /Content/DLC, this is just to allow them to be picked up
                                if (value.EndsWith("\\Content\\Paks", StringComparison.OrdinalIgnoreCase)) { 
                                    value = value[..^"\\Paks".Length];
                                }

                                Settings.GameDirectory = value;
                                Debug.WriteLine($"[Config] Set GameDirectory = '{value}'");
                                break;
                            case var _ when key.Equals(ConfigKeys.ToolsDirectory, StringComparison.OrdinalIgnoreCase):
                                Settings.ToolsDirectory = value;
                                Debug.WriteLine($"[Config] Set ToolsDirectory = '{value}'");
                                break;
                            case var _ when key.Equals(ConfigKeys.EngineVersion, StringComparison.OrdinalIgnoreCase):
                                Settings.EngineVersion = value;
                                Debug.WriteLine($"[Config] Set EngineVersion = '{value}'");
                                break;
                            case var _ when key.Equals(ConfigKeys.AesKey, StringComparison.OrdinalIgnoreCase):
                                Settings.AesKey = value;
                                Debug.WriteLine($"[Config] Set AesKey = '{value}'");
                                break;
                            case var _ when key.Equals(ConfigKeys.OutputPath, StringComparison.OrdinalIgnoreCase):
                                Settings.OutputPath = value;
                                Debug.WriteLine($"[Config] Set OutputPath = '{value}'");
                                break;
                            case var _ when key.Equals(ConfigKeys.DisplayLanguage, StringComparison.OrdinalIgnoreCase):
                                Settings.DisplayLanguage = value;
                                Debug.WriteLine($"[Config] Set DisplayLanguage = '{value}'");
                                break;
                            case var _ when key.Equals(ConfigKeys.TextLanguage, StringComparison.OrdinalIgnoreCase):
                                Settings.TextLanguage = value;
                                Debug.WriteLine($"[Config] Set TextLanguage = '{value}'");
                                break;
                            case var _ when key.Equals(ConfigKeys.ServerPort, StringComparison.OrdinalIgnoreCase):
                                if (int.TryParse(value, out int port))
                                {
                                    Settings.ServerPort = port;
                                    Debug.WriteLine($"[Config] Set ServerPort = {port}");
                                }
                                else
                                {
                                    Debug.WriteLine($"[Config] Invalid ServerPort value: {value}, using default");
                                }
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
                    $"{ConfigKeys.GameDirectory}=\"{gameDir}\"",
                    $"{ConfigKeys.ToolsDirectory}=\"{toolsDir}\"", 
                    $"{ConfigKeys.EngineVersion}=\"{Settings.EngineVersion}\"",
                    $"{ConfigKeys.AesKey}=\"{Settings.AesKey}\"",
                    $"{ConfigKeys.OutputPath}=\"{outputDir}\"",
                    $"{ConfigKeys.DisplayLanguage}=\"{Settings.DisplayLanguage}\"",
                    $"{ConfigKeys.TextLanguage}=\"{Settings.TextLanguage}\"",
                    $"{ConfigKeys.ServerPort}={Settings.ServerPort}"
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
            // First check if required entries exist in config and append if missing
            var existingLines = File.ReadAllLines(ConfigFile).ToList();
            bool needsSave = false;

            // Check and append required entries before validation
            if (!existingLines.Any(l => l.TrimStart().StartsWith($"{ConfigKeys.GameDirectory}=", StringComparison.OrdinalIgnoreCase)))
            {
                Debug.WriteLine("[Config] GameDirectory entry missing in config.ini, appending");
                existingLines.Add($"{ConfigKeys.GameDirectory}=\"{Settings.GameDirectory}\"");
                needsSave = true;
            }

            if (!existingLines.Any(l => l.TrimStart().StartsWith($"{ConfigKeys.OutputPath}=", StringComparison.OrdinalIgnoreCase)))
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var relativeOutputPath = Path.GetRelativePath(baseDir, Settings.OutputPath);
                Debug.WriteLine("[Config] OutputPath entry missing in config.ini, appending");
                existingLines.Add($"{ConfigKeys.OutputPath}=\"{relativeOutputPath}\"");
                needsSave = true;
            }

            // Save missing entries before validation
            if (needsSave)
            {
                Debug.WriteLine("[Config] Writing appended entries to config.ini");
                File.WriteAllLines(ConfigFile, existingLines);
            }

            // Now do the actual validation that might trigger fatal errors
            if (!Directory.Exists(Settings.GameDirectory))
            {
                ShowFatalError($"Game directory not found, edit it in config.ini:\n{Settings.GameDirectory}");
            }

            if (!Settings.ValidateServerPath())
            {
                ShowFatalError($"Server executable not found or not accessible at:\n{Settings.ServerExecutablePath}");
            }

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
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var relativeToolsDirectory = Path.GetRelativePath(baseDir, Settings.ToolsDirectory);
                var relativeOutputPath = Path.GetRelativePath(baseDir, Settings.OutputPath);

                var configLines = File.Exists(ConfigFile) ? File.ReadAllLines(ConfigFile).ToList() : new List<string>();

                var updatedConfig = new Dictionary<string, string>
                {
                    { ConfigKeys.GameDirectory, Settings.GameDirectory },
                    { ConfigKeys.ToolsDirectory, relativeToolsDirectory },
                    { ConfigKeys.EngineVersion, Settings.EngineVersion },
                    { ConfigKeys.AesKey, Settings.AesKey },
                    { ConfigKeys.OutputPath, relativeOutputPath },
                    { ConfigKeys.DisplayLanguage, Settings.DisplayLanguage },
                    { ConfigKeys.TextLanguage, Settings.TextLanguage },
                    { ConfigKeys.ServerPort, Settings.ServerPort.ToString() }
                };

                foreach (var key in updatedConfig.Keys)
                {
                    var lineIndex = configLines.FindIndex(line => line.TrimStart().StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase));
                    var newLine = $"{key}=\"{updatedConfig[key]}\"";

                    if (lineIndex >= 0)
                    {
                        configLines[lineIndex] = newLine;
                    }
                    else
                    {
                        configLines.Add(newLine);
                    }
                }

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
