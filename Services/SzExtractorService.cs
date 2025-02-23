using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SZExtractorGUI.Models;

namespace SZExtractorGUI.Services
{
    public class SzExtractorService : ISzExtractorService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Settings _settings;
        private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);
        private bool _disposed;

        public SzExtractorService(
            IRetryService retryService,
            IHttpClientFactory httpClientFactory,
            Settings settings)
        {
            _settings = settings;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri(settings.ServerBaseUrl);
            _httpClient.Timeout = _requestTimeout;
        }
        public async Task<ConfigureResponse> ConfigureAsync(ConfigureRequest request)
        {
            try
            {
                using var cts = new CancellationTokenSource(_requestTimeout);
                var response = await _httpClient.PostAsJsonAsync("configure", request, cts.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    return new ConfigureResponse { Success = false, Message = await response.Content.ReadAsStringAsync() };
                }
                
                var configResponse = await response.Content.ReadFromJsonAsync<ConfigureResponse>();
                return configResponse;
            }
            catch (Exception ex)
            {
                return new ConfigureResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<ExtractResponse> ExtractAsync(ExtractRequest request)
        {
            try
            {
                Debug.WriteLine($"[Extract] Starting extraction for path: {request.ContentPath}");

                using var cts = new CancellationTokenSource(_requestTimeout);
                Debug.WriteLine($"[Extract] Sending extract request...");
                var response = await _httpClient.PostAsJsonAsync("extract", request, cts.Token);
                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[Extract] Server response: {response.StatusCode}\nContent: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[Extract] Extraction failed: {content}");
                    return new ExtractResponse { Success = false, Message = content };
                }

                var extractResponse = await response.Content.ReadFromJsonAsync<ExtractResponse>();
                Debug.WriteLine($"[Extract] Extraction {(extractResponse.Success ? "succeeded" : "failed")}: {extractResponse.Message}");
                return extractResponse;
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                Debug.WriteLine($"[Extract] Error during extraction: {ex.GetType().Name}\n{ex}");
                return new ExtractResponse { Success = false, Message = message };
            }
        }

        public async Task<Dictionary<string, List<string>>> GetDuplicatesAsync()
        {
            try
            {
                Debug.WriteLine("[Duplicates] Fetching duplicate files list");

                using var cts = new CancellationTokenSource(_requestTimeout);
                var response = await _httpClient.GetAsync("duplicates", cts.Token);
                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[Duplicates] Server response: {response.StatusCode}\nContent: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[Duplicates] Failed to get duplicates: {content}");
                    return new Dictionary<string, List<string>>();
                }

                var duplicates = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
                Debug.WriteLine($"[Duplicates] Retrieved {duplicates.Count} duplicate entries");
                return duplicates;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Duplicates] Error getting duplicates: {ex.GetType().Name}\n{ex}");
                return new Dictionary<string, List<string>>();
            }
        }

        public async Task<DumpResponse> DumpAsync(DumpRequest request)
        {
            try
            {
                Debug.WriteLine($"[Dump] Starting dump with filter: {request.Filter}");

                using var cts = new CancellationTokenSource(_requestTimeout);
                
                // Use null for PropertyNamingPolicy to preserve PascalCase
                var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = null, // Preserve original casing (PascalCase)
                    WriteIndented = true
                });
                Debug.WriteLine($"[Dump] Request JSON:\n{requestJson}");

                // Create request message with preserved casing
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "dump")
                {
                    Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json")
                };

                Debug.WriteLine("[Dump] Request headers:");
                foreach (var header in requestMessage.Content.Headers)
                {
                    Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }

                // Send request and get raw response
                var response = await _httpClient.SendAsync(requestMessage, cts.Token);
                var rawContent = await response.Content.ReadAsStringAsync();
                
                Debug.WriteLine($"[Dump] Response status: {response.StatusCode}");
                Debug.WriteLine("[Dump] Response headers:");
                foreach (var header in response.Headers)
                {
                    Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                Debug.WriteLine($"[Dump] Raw response content:\n{rawContent}");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[Dump] Dump failed: {rawContent}");
                    return new DumpResponse 
                    { 
                        Success = false, 
                        Message = $"Server error: {response.StatusCode} - {rawContent}" 
                    };
                }

                try
                {
                    var filesDictionary = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
                        rawContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    
                    if (filesDictionary == null)
                    {
                        Debug.WriteLine("[Dump] Deserialized response was null");
                        return new DumpResponse 
                        { 
                            Success = false, 
                            Message = "Failed to parse server response" 
                        };
                    }

                    if (PackageInfo.HasUpdates(filesDictionary))
                    {
                        PackageInfo.UpdateFileCounts(filesDictionary);
                    }

                    return new DumpResponse
                    {
                        Success = true,
                        Files = filesDictionary,
                        Message = $"Found {filesDictionary.Sum(kvp => kvp.Value.Count)} files in {filesDictionary.Count} packages"
                    };
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"[Dump] JSON deserialization error: {ex.Message}\nPath: {ex.Path}");
                    return new DumpResponse
                    {
                        Success = false,
                        Message = $"Failed to parse server response: {ex.Message}"
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dump] Error during dump: {ex.GetType().Name}\n{ex}");
                return new DumpResponse 
                { 
                    Success = false, 
                    Message = $"Error retrieving files: {ex.Message}" 
                };
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
