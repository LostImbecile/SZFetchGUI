using System.Diagnostics;
using System.IO;
using System.Net.Http;

using SZExtractorGUI.Models;
using SZExtractorGUI.ViewModels;

namespace SZExtractorGUI.Services
{
    public interface IServerConfigurationService
    {
        Task<bool> ConfigureServerAsync(Settings settings);
    }

    public class ServerConfigurationService : IServerConfigurationService, IDisposable
    {
        private readonly ISzExtractorService _extractorService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly SemaphoreSlim _configLock = new(1, 1);

        public ServerConfigurationService(
            ISzExtractorService extractorService,
            IErrorHandlingService errorHandlingService)
        {
            _extractorService = extractorService;
            _errorHandlingService = errorHandlingService;
            Debug.WriteLine("[Config] Server configuration service created");
        }

        public async Task<bool> ConfigureServerAsync(Settings settings)
        {
            try
            {
                await _configLock.WaitAsync();

                Debug.WriteLine($"[Config] Starting server configuration with:\n  GameDir: '{settings.GameDirectory}'\n  Engine: '{settings.EngineVersion}'");

                if (string.IsNullOrWhiteSpace(settings.GameDirectory))
                {
                    Debug.WriteLine("[Config] GameDirectory is null or empty!");
                    _errorHandlingService.HandleError("Configuration failed", "Game directory is not configured");
                    return false;
                }

                var configRequest = new ConfigureRequest
                {
                    GameDir = settings.GameDirectory,
                    EngineVersion = settings.EngineVersion,
                    AesKey = settings.AesKey,
                    OutputPath = settings.OutputPath
                };

                // Try configuration up to 3 times with backoff
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        if (attempt > 0)
                        {
                            await Task.Delay(attempt * 1000);
                            Debug.WriteLine($"[Config] Retry attempt {attempt + 1}/3");
                        }

                        var response = await _extractorService.ConfigureAsync(configRequest)
                            .ConfigureAwait(false);

                        // Consider configuration successful if either:
                        // 1. Response.Success is true
                        // 2. Message contains "mounted" (case-insensitive)
                        // 3. Message indicates configuration updated
                        if (response.Success || 
                            response.Message?.Contains("mounted", StringComparison.OrdinalIgnoreCase) == true ||
                            response.Message?.Contains("Configuration updated", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            Debug.WriteLine($"[Config] Configuration successful: {response.Message}");
                            _errorHandlingService.ClearError();  // Clear any error state
                            return true;
                        }

                        Debug.WriteLine($"[Config] Attempt {attempt + 1} failed: {response.Message}");
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                    {
                        Debug.WriteLine($"[Config] Network error on attempt {attempt + 1}: {ex.Message}");
                        if (attempt == 2) throw;
                    }
                }

                // Only report error if all attempts genuinely failed
                _errorHandlingService.HandleError("Configuration incomplete", "Server configuration did not complete successfully");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Config] Exception during configuration: {ex.Message}");
                _errorHandlingService.HandleError("Configuration failed", ex);
                return false;
            }
            finally
            {
                _configLock.Release();
            }
        }

        public void Dispose()
        {
            _configLock?.Dispose();
        }
    }
}
