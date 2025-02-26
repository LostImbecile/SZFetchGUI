using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using SZExtractorGUI.Models;
using SZExtractorGUI.Services.FileInfo;
using SZExtractorGUI.Services.Localization;
using SZExtractorGUI.Services.State;
using SZExtractorGUI.ViewModels;
using SZExtractorGUI.Utilities;
using SZExtractorGUI.Services.FileFetch;
using System.Runtime;

namespace SZExtractorGUI.Services.Initialisation
{
    public interface IInitializationService
    {
        Task InitializeAsync();
        Task ShutdownAsync();
        bool IsInitialized { get; }
        event EventHandler<bool> InitializationStateChanged;
        // Add new event for configuration completion
        event EventHandler<bool> ConfigurationCompleted;
    }

    public class InitializationService : IInitializationService
    {
        private readonly IServerLifecycleService _serverLifecycleService;
        private readonly IServerConfigurationService _serverConfigurationService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IFetchOperationService _fetchOperationService;
        private readonly Settings _settings;
        private bool _isInitialized;
        private readonly HashSet<string> _loadedLanguages = [];
        private readonly ICharacterNameManager _characterNameManager;
        
        public event EventHandler<bool> InitializationStateChanged;
        public event EventHandler<bool> ConfigurationCompleted;
        public bool IsInitialized => _isInitialized;

        public InitializationService(
            IServerLifecycleService serverLifecycleService,
            IServerConfigurationService serverConfigurationService,
            IErrorHandlingService errorHandlingService,
            IFetchOperationService fetchOperationService,
            ICharacterNameManager characterNameManager,
            Settings settings)
        {
            _serverLifecycleService = serverLifecycleService;
            _serverConfigurationService = serverConfigurationService;
            _errorHandlingService = errorHandlingService;
            _fetchOperationService = fetchOperationService;
            _characterNameManager = characterNameManager; // Initialize field
            _settings = settings;
            Debug.WriteLine("[Init] Initialization service created");
        }

        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("[Init] Starting initialization");
                
                _errorHandlingService.ClearError();
                UpdateInitializationState(false);

                var serverStarted = await _serverLifecycleService.StartServerAsync();
                if (!serverStarted)
                {
                    throw new Exception("Failed to start server");
                }

                // Server configuration
                await Task.Delay(500);
                var configured = await _serverConfigurationService.ConfigureServerAsync(_settings);
                _serverLifecycleService.SetInitialConfigurationComplete(configured);
                ConfigurationCompleted?.Invoke(this, configured);

                if (!configured)
                {
                    Debug.WriteLine("[Init] Initial configuration reported issues but continuing");
                }

                // Load locres files before completing initialization
                await InitializeLocresAsync();
                
                UpdateInitializationState(true);
                _errorHandlingService.ClearError();
                Debug.WriteLine("[Init] Initialization completed successfully");
            }
            catch (Exception ex)
            {
                UpdateInitializationState(false);
                _errorHandlingService.HandleError("Initialization failed", ex);
                Debug.WriteLine($"[Init] Initialization failed: {ex.Message}");
                OnConfigurationCompleted(false);
                throw;
            }
        }

        private async Task InitializeLocresAsync()
        {
            try
            {
                var locresFiles = await _fetchOperationService.GetLocresFiles();
                if (locresFiles == null || !locresFiles.Any())
                {
                    Debug.WriteLine("[Initialize] No locres files found");
                    return;
                }

                foreach (var locresFile in locresFiles)
                {
                    var fileName = Path.GetFileName(locresFile);
                    var language = LanguageCodeValidator.ExtractLanguageFromFileName(fileName);

                    if (!string.IsNullOrEmpty(language))
                    {
                        await LoadLanguageFileAsync(language, locresFile);
                    }
                    else
                    {
                        Debug.WriteLine($"[Initialize] Could not determine language for file: {locresFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Initialize] Error loading locres files: {ex.Message}");
                _errorHandlingService.HandleError("Failed to load language files", ex);
            }
        }

        private async Task LoadLanguageFileAsync(string language, string locresFile)
        {
            if (string.IsNullOrEmpty(language) || _loadedLanguages.Contains(language))
                return;

            if (!LanguageCodeValidator.IsValidLanguageCode(language))
            {
                Debug.WriteLine($"[Initialize] Invalid language code format: {language}");
                return;
            }

            try
            {
                Debug.WriteLine($"[Initialize] Loading locres file: {locresFile}");
                await _characterNameManager.LoadLocresFile(language, locresFile);
                _loadedLanguages.Add(language);
                Debug.WriteLine($"[Initialize] Successfully loaded locres for {language}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Initialize] Failed to load locres for {language}: {ex.Message}");
                _errorHandlingService.HandleError($"Failed to load language: {language}", ex);
            }
        }

        private void UpdateInitializationState(bool initialized)
        {
            if (_isInitialized != initialized)
            {
                _isInitialized = initialized;
                InitializationStateChanged?.Invoke(this, initialized);
            }
        }

        public async Task ShutdownAsync()
        {
            try
            {
                await _serverLifecycleService.StopServerAsync();
                UpdateInitializationState(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Init] Shutdown failed: {ex.Message}");
            }
        }

        protected virtual void OnConfigurationCompleted(bool success)
        {
            ConfigurationCompleted?.Invoke(this, success);
        }
    }
}
