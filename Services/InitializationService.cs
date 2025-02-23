using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using SZExtractorGUI.Models;
using SZExtractorGUI.ViewModels;

namespace SZExtractorGUI.Services
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
        private readonly IContentTypeService _contentTypeService;
        private readonly IBackgroundOperationsService _backgroundOps;
        private readonly Settings _settings;
        private bool _isInitialized;
        
        public event EventHandler<bool> InitializationStateChanged;
        public event EventHandler<bool> ConfigurationCompleted;
        public bool IsInitialized => _isInitialized;

        public InitializationService(
            IServerLifecycleService serverLifecycleService,
            IServerConfigurationService serverConfigurationService,
            IErrorHandlingService errorHandlingService,
            IFetchOperationService fetchOperationService,
            IContentTypeService contentTypeService,
            IBackgroundOperationsService backgroundOps,
            Settings settings)
        {
            _serverLifecycleService = serverLifecycleService;
            _serverConfigurationService = serverConfigurationService;
            _errorHandlingService = errorHandlingService;
            _fetchOperationService = fetchOperationService;
            _contentTypeService = contentTypeService;
            _backgroundOps = backgroundOps;
            _settings = settings;
            Debug.WriteLine("[Init] Initialization service created");
        }

        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("[Init] Starting initialization");
                
                // Clear any existing error state before starting
                _errorHandlingService.ClearError();
                UpdateInitializationState(false);

                var serverStarted = await _serverLifecycleService.StartServerAsync();
                if (!serverStarted)
                {
                    throw new Exception("Failed to start server");
                }

                // Handle initial configuration here
                Debug.WriteLine("[Init] Performing initial server configuration");
                await Task.Delay(500); // Give server a moment to start
                
                var configured = await _serverConfigurationService.ConfigureServerAsync(_settings);
                
                // Signal to ServerLifecycleService that initial config is done
                _serverLifecycleService.SetInitialConfigurationComplete(configured);
                
                // Notify UI about configuration result
                ConfigurationCompleted?.Invoke(this, configured);
                
                if (!configured)
                {
                    Debug.WriteLine("[Init] Initial configuration reported issues but continuing");
                }

                // Continue with initialization
                UpdateInitializationState(true);
                _errorHandlingService.ClearError();
                Debug.WriteLine("[Init] Initialization completed successfully");
                
                // Trigger initial fetch with default content type
                await TriggerInitialFetchAsync();
            }
            catch (Exception ex)
            {
                UpdateInitializationState(false);
                _errorHandlingService.HandleError("Initialization failed", ex);
                Debug.WriteLine($"[Init] Initialization failed: {ex.Message}");
                throw;
            }
        }

        private async Task TriggerInitialFetchAsync()
        {
            try
            {
                // Add delay after configuration to ensure server is ready
                await Task.Delay(1000);
                
                var defaultType = _contentTypeService.GetDefaultContentType();
                if (defaultType != null)
                {
                    // Wrap in background operation like manual operations do
                    await _backgroundOps.ExecuteOperationAsync(async () =>
                    {
                        var items = await _fetchOperationService.FetchItemsAsync(defaultType);
                
                        // Ensure we're on UI thread for updating collection
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            Debug.WriteLine("[Init] Updating UI with initial fetch results");
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Init] Initial fetch failed: {ex.Message}");
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
    }
}
