using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using SZExtractorGUI.Models;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SZExtractorGUI.Services.Fetch;

namespace SZExtractorGUI.Services.State
{
    public enum ServerState
    {
        NotStarted,
        NotFound,
        Starting,
        Running,
        Failed,
        Stopped
    }

    public interface IServerLifecycleService : IDisposable
    {
        bool IsServerAvailable { get; }
        Task<bool> StartServerAsync();
        Task StopServerAsync();
        Task<bool> CheckServerStatusAsync();
        ServerState State { get; }
        void SetInitialConfigurationComplete(bool success);
    }

    public class ServerLifecycleService : IServerLifecycleService, IDisposable
    {
        // Remove job object related fields
        private bool _initialConfigurationDone;
        private readonly string _serverPath;
        private readonly HttpClient _httpClient;
        private Process _serverProcess;
        private readonly SemaphoreSlim _processLock = new(1, 1);
        private bool _disposed;
        private readonly CancellationTokenSource _cts = new();
        private bool _isConnected;
        private readonly object _restartLock = new();
        private readonly Settings _settings;
        private ServerState _state;
        private readonly IServerConfigurationService _serverConfigurationService;
        private const int SERVER_STARTUP_DELAY_MS = 2000;
        private readonly SemaphoreSlim _startupLock = new(1, 1);

        public ServerState State => _state;
        public bool IsServerAvailable => _isConnected;

        public ServerLifecycleService(
            IApplicationEvents applicationEvents,
            ISzExtractorService extractorService,
            IErrorHandlingService errorHandlingService,
            IHttpClientFactory httpClientFactory,
            Settings settings,
            IRetryService retryService,
            IServerConfigurationService serverConfigurationService)
        {
            _serverPath = settings.ServerExecutablePath;
            _settings = settings;
            _httpClient = httpClientFactory.CreateClient();
            _state = settings.ValidateServerPath() ? ServerState.NotStarted : ServerState.NotFound;
            _serverConfigurationService = serverConfigurationService;

            Task.Run(MonitorServerAsync);
        }

        public async Task<bool> StartServerAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ServerLifecycleService));

            try
            {
                await _startupLock.WaitAsync();
                await _processLock.WaitAsync();

                Debug.WriteLine($"[Server] Attempting to start server from: {_serverPath}");

                // Check if server is already running
                if (IsServerProcessRunning())
                {
                    Debug.WriteLine("[Server] Found existing server process, attempting to connect first");
                    if (await CheckServerStatusAsync())
                    {
                        Debug.WriteLine("[Server] Successfully connected to existing server");
                        return true;
                    }
                    Debug.WriteLine("[Server] Existing server not responding, will start new instance");
                }

                if (!File.Exists(_serverPath))
                {
                    LogState($"Server executable not found at: {_serverPath}");
                    return false;
                }

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _serverPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(_serverPath),
                        Arguments = Environment.ProcessId.ToString()
                    };

                    Debug.WriteLine($"[Server] Starting process with parent PID: {Environment.ProcessId}");
                    Debug.WriteLine($"[Server] Working directory: {startInfo.WorkingDirectory}");

                    if (_serverProcess != null)
                    {
                        Debug.WriteLine("[Server] Cleaning up previous server process instance");
                        _serverProcess.Disposed -= ServerProcess_Disposed;
                        _serverProcess.Exited -= ServerProcess_Exited;
                        _serverProcess.Dispose();
                    }

                    _serverProcess = new Process { StartInfo = startInfo };
                    _serverProcess.EnableRaisingEvents = true;

                    // Enhanced process lifetime monitoring
                    _serverProcess.Disposed += ServerProcess_Disposed;
                    _serverProcess.Exited += ServerProcess_Exited;

                    // Detailed output logging
                    _serverProcess.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            Debug.WriteLine($"[Server Output] {DateTime.Now:HH:mm:ss.fff} {e.Data}");
                        }
                    };

                    _serverProcess.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            Debug.WriteLine($"[Server Error] {DateTime.Now:HH:mm:ss.fff} {e.Data}");
                        }
                    };

                    if (!_serverProcess.Start())
                    {
                        LogState("Failed to start server process");
                        return false;
                    }

                    Debug.WriteLine($"[Server] Process started successfully with PID: {_serverProcess.Id}");

                    _serverProcess.BeginOutputReadLine();
                    _serverProcess.BeginErrorReadLine();

                    UpdateState(ServerState.Starting);

                    // Give the server time to initialize
                    await Task.Delay(SERVER_STARTUP_DELAY_MS);

                    // Verify the process hasn't exited during startup
                    if (_serverProcess.HasExited)
                    {
                        Debug.WriteLine($"[Server] Process exited during startup with code: {_serverProcess.ExitCode}");
                        var exitCode = _serverProcess.ExitCode;
                        UpdateState(ServerState.Failed);
                        LogState($"Server process failed to start (Exit Code: {exitCode})");
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Server] Start error: {ex.Message}\n{ex.StackTrace}");
                    UpdateState(ServerState.Failed);
                    return false;
                }
            }
            finally
            {
                _processLock.Release();
                _startupLock.Release();
            }
        }

        private void ServerProcess_Exited(object sender, EventArgs e)
        {
            var process = sender as Process;
            Debug.WriteLine($"[Server] Process exited with code: {process?.ExitCode}");
            Debug.WriteLine($"[Server] Process exit time: {DateTime.Now:HH:mm:ss.fff}");
            UpdateState(ServerState.Stopped);
            _isConnected = false;
        }

        private void ServerProcess_Disposed(object sender, EventArgs e)
        {
            Debug.WriteLine($"[Server] Process disposed at: {DateTime.Now:HH:mm:ss.fff}");
        }

        public void Dispose()
        {
            if (_disposed) return;

            Debug.WriteLine("[Server] Starting disposal sequence");

            _cts.Cancel();

            _processLock.Dispose();
            _startupLock.Dispose();
            _httpClient.Dispose();

            if (_serverProcess != null)
            {
                _serverProcess.Disposed -= ServerProcess_Disposed;
                _serverProcess.Exited -= ServerProcess_Exited;
                _serverProcess.Dispose();
                _serverProcess = null;
            }

            _disposed = true;
            Debug.WriteLine("[Server] Disposal completed");
            GC.SuppressFinalize(this);
        }

        // Helper method to check for existing server process
        private bool IsServerProcessRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_serverPath));
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Server] Error checking for existing process: {ex.Message}");
                return false;
            }
        }

        public async Task StopServerAsync()
        {
            if (_disposed) return;

            try
            {
                await _processLock.WaitAsync();

                if (_serverProcess == null || _serverProcess.HasExited)
                    return;

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _httpClient.DeleteAsync($"{_settings.ServerBaseUrl}/shutdown", cts.Token);

                    // Wait for process to exit naturally
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Server] Graceful shutdown failed: {ex.Message}");
                }

                // Only force kill if we're disposing the application
                if (_disposed && !_serverProcess.HasExited)
                {
                    try
                    {
                        _serverProcess.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Server] Error during force stop: {ex.Message}");
                    }
                }

                if (_serverProcess.HasExited)
                {
                    _serverProcess.Dispose();
                    _serverProcess = null;
                }
            }
            finally
            {
                _processLock.Release();
            }
        }

        public async Task<bool> CheckServerStatusAsync()
        {
            if (_disposed) return false;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                var response = await _httpClient.GetAsync(_settings.ServerBaseUrl, cts.Token);
                var content = await response.Content.ReadAsStringAsync();

                _isConnected = true;

                if (!_isConnected) // State transition from disconnected to connected
                {
                    UpdateState(ServerState.Running);
                }
                return true;
            }
            catch (Exception ex)
            {
                if (_isConnected) // Only log when transitioning from connected to disconnected
                {
                    Debug.WriteLine($"[Server] Connection lost: {ex.GetType().Name} - {ex.Message}");
                    UpdateState(ServerState.Failed);
                }
                _isConnected = false;
                return false;
            }
        }

        private async Task<bool> StartAndConfigureServerAsync()
        {
            if (await StartServerAsync())
            {
                // Give the server a moment to start up
                await Task.Delay(500, _cts.Token);

                // Skip configuration if this is the initial startup
                if (!_initialConfigurationDone)
                {
                    Debug.WriteLine("[Server] Skipping initial configuration (will be handled by InitializationService)");
                    return true;
                }

                try
                {
                    // Only configure on subsequent restarts
                    var configured = await _serverConfigurationService.ConfigureServerAsync(_settings);
                    Debug.WriteLine($"[Server] Subsequent configuration completed: {configured}");
                    return configured;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Server] Configuration failed: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        private async Task MonitorServerAsync()
        {
            Debug.WriteLine("[Server] Starting server monitor task");
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (!await CheckServerStatusAsync())
                    {
                        Debug.WriteLine("[Server] Server unavailable, attempting to start...");
                        var result = await StartAndConfigureServerAsync();
                        Debug.WriteLine($"[Server] Startup attempt result: {result}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Server] Monitor error: {ex.Message}");
                    UpdateState(ServerState.Failed);
                }

                await Task.Delay(1000, _cts.Token);
            }
        }

        private void UpdateState(ServerState newState)
        {
            var oldState = _state;
            _state = newState;

            if (oldState != newState)
            {
                Debug.WriteLine($"[Server] State changed: {oldState} -> {newState} at {DateTime.Now:HH:mm:ss.fff}");
            }
        }

        private void LogState(string message)
        {
            Debug.WriteLine($"[Server] {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        // Add method for InitializationService to signal initial config is done
        public void SetInitialConfigurationComplete(bool success)
        {
            _initialConfigurationDone = true;
            Debug.WriteLine($"[Server] Initial configuration marked as complete: {success}");
        }
    }
}
