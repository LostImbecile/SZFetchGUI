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
using SZExtractorGUI.Utilities;

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
        private const string ExpectedServiceName = "SZ_Extractor_Server";
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
        private readonly IServerEndpointProvider _endpointProvider;

        public ServerState State => _state;
        public bool IsServerAvailable => _isConnected;

        public ServerLifecycleService(
            IApplicationEvents applicationEvents,
            ISzExtractorService extractorService,
            IErrorHandlingService errorHandlingService,
            IHttpClientFactory httpClientFactory,
            Settings settings,
            IRetryService retryService,
            IServerConfigurationService serverConfigurationService,
            IServerEndpointProvider endpointProvider)
        {
            _serverPath = settings.ServerExecutablePath;
            _settings = settings;
            _httpClient = httpClientFactory.CreateClient();
            _state = settings.ValidateServerPath() ? ServerState.NotStarted : ServerState.NotFound;
            _serverConfigurationService = serverConfigurationService;
            _endpointProvider = endpointProvider;

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

                // Ensure we have a valid, unused port or connect to an already-running server on the configured port
                var alreadyRunning = await EnsureValidPortOrConnectAsync();
                if (alreadyRunning)
                {
                    Debug.WriteLine("[Server] Reusing existing running server instance");
                    // Verify connection and return
                    var ok = await CheckServerStatusAsync();
                    return ok;
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
                        // Pass parent PID (existing behavior) and the chosen port so server binds correctly
                        Arguments = $"{Environment.ProcessId} --port {_settings.ServerPort}"
                    };

                    Debug.WriteLine($"[Server] Starting process with parent PID: {Environment.ProcessId}");
                    Debug.WriteLine($"[Server] Working directory: {startInfo.WorkingDirectory}");
                    Debug.WriteLine($"[Server] Using port: {_settings.ServerPort}");

                    if (_serverProcess != null)
                    {
                        Debug.WriteLine("[Server] Cleaning up previous server process instance");
                        _serverProcess.Disposed -= ServerProcess_Disposed;
                        _serverProcess.Exited -= ServerProcess_Exited;
                        _serverProcess.Dispose();
                    }

                    _serverProcess = new Process { StartInfo = startInfo };
                    _serverProcess.EnableRaisingEvents = true;

                    _serverProcess.Disposed += ServerProcess_Disposed;
                    _serverProcess.Exited += ServerProcess_Exited;

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

                    // Wait briefly and then poll for server identity until timeout
                    await Task.Delay(SERVER_STARTUP_DELAY_MS);

                    if (_serverProcess.HasExited)
                    {
                        Debug.WriteLine($"[Server] Process exited during startup with code: {_serverProcess.ExitCode}");
                        var exitCode = _serverProcess.ExitCode;
                        UpdateState(ServerState.Failed);
                        LogState($"Server process failed to start (Exit Code: {exitCode})");
                        return false;
                    }

                    var startupDeadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(_settings.ServerStartupTimeoutMs);
                    while (DateTime.UtcNow < startupDeadline)
                    {
                        if (await CheckServerStatusAsync())
                        {
                            Debug.WriteLine("[Server] Server identity confirmed and running");
                            return true;
                        }
                        await Task.Delay(300);
                    }

                    Debug.WriteLine("[Server] Startup timed out waiting for server to become available");
                    UpdateState(ServerState.Failed);
                    return false;
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
                    var shutdownUri = _endpointProvider.BuildUri("shutdown");
                    await _httpClient.DeleteAsync(shutdownUri, cts.Token);

                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Server] Graceful shutdown failed: {ex.Message}");
                }

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
                var isOurServer = await IsExpectedServerAsync(_endpointProvider.BaseUrl, cts.Token);

                if (isOurServer)
                {
                    var wasConnected = _isConnected;
                    _isConnected = true;
                    if (!wasConnected)
                    {
                        UpdateState(ServerState.Running);
                    }
                    return true;
                }
                else
                {
                    // Something else is listening or the response is unexpected
                    if (_isConnected)
                    {
                        Debug.WriteLine("[Server] Unexpected service on port; marking as disconnected");
                        UpdateState(ServerState.Failed);
                    }
                    _isConnected = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (_isConnected)
                {
                    Debug.WriteLine($"[Server] Connection lost: {ex.GetType().Name} - {ex.Message}");
                    UpdateState(ServerState.Failed);
                }
                _isConnected = false;
                return false;
            }
        }

        private async Task<bool> EnsureValidPortOrConnectAsync()
        {
            // If something is listening on the configured port, check identity
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800)))
            {
                if (await IsExpectedServerAsync(_endpointProvider.BaseUrl, cts.Token))
                {
                    Debug.WriteLine("[Server] Found existing SZ Extractor server on configured port; will connect");
                    // No need to change port; caller will attempt to connect
                    return true;
                }
            }

            // If no server is there or identity didn't match, ensure we can actually bind to this port using HttpListener
            if (NetworkUtil.IsHttpPortBindable(_settings.ServerPort))
            {
                Debug.WriteLine($"[Server] Desired port {_settings.ServerPort} is bindable by HttpListener");
                return false;
            }

            Debug.WriteLine($"[Server] Desired port {_settings.ServerPort} is not bindable; selecting alternative bindable port");
            var newPort = NetworkUtil.FindBindableHttpPort();
            Debug.WriteLine($"[Server] Selected alternate bindable port: {newPort}");
            _endpointProvider.SetPort(newPort);
            return false;
        }

        private async Task<bool> IsExpectedServerAsync(string baseUrl, CancellationToken ct)
        {
            try
            {
                // Try /identify first, then fall back to root
                var identifyUri = new Uri(new Uri(baseUrl), "identify");
                var resp = await _httpClient.GetAsync(identifyUri, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    // Try root
                    resp = await _httpClient.GetAsync(baseUrl, ct);
                }

                if (!resp.IsSuccessStatusCode)
                {
                    return false;
                }

                var json = await resp.Content.ReadAsStringAsync(ct);
                try
                {
                    var status = System.Text.Json.JsonSerializer.Deserialize<SZExtractorGUI.Models.ServerStatusResponse>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return status != null && string.Equals(status.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> StartAndConfigureServerAsync()
        {
            if (await StartServerAsync())
            {
                await Task.Delay(500, _cts.Token);

                if (!_initialConfigurationDone)
                {
                    Debug.WriteLine("[Server] Skipping initial configuration (will be handled by InitializationService)");
                    return true;
                }

                try
                {
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

        public void SetInitialConfigurationComplete(bool success)
        {
            _initialConfigurationDone = true;
            Debug.WriteLine($"[Server] Initial configuration marked as complete: {success}");
        }
    }
}
