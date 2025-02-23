using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SZExtractorGUI.Models;
using SZExtractorGUI.ViewModels;

namespace SZExtractorGUI.Services
{
    public interface IFetchOperationService
    {
        Task<IEnumerable<FetchItemViewModel>> FetchItemsAsync(ContentType contentType);
        Task<bool> ExtractItemAsync(FetchItemViewModel item); // New method for single item
        Task<bool> ExtractItemsAsync(IEnumerable<FetchItemViewModel> items);
    }

    public class FetchOperationService : IFetchOperationService
    {
        private readonly ISzExtractorService _extractorService;
        private readonly IErrorHandlingService _errorHandlingService;

        public FetchOperationService(
            ISzExtractorService extractorService,
            IErrorHandlingService errorHandlingService)
        {
            _extractorService = extractorService;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<IEnumerable<FetchItemViewModel>> FetchItemsAsync(ContentType contentType)
        {
            try
            {
                var items = new List<FetchItemViewModel>();
                Debug.WriteLine($"[Fetch] Starting fetch for {contentType.Name} & {contentType.Filter}");
                var dumpRequest = new DumpRequest { Filter = contentType.Filter };

                var response = await _extractorService.DumpAsync(dumpRequest);

                if (!response.Success || response.Files == null)
                {
                    Debug.WriteLine("[Fetch] Dump request failed or returned no files");
                    throw new InvalidOperationException(response.Message ?? "Failed to retrieve file list");
                }

                Debug.WriteLine($"[Fetch] Received {response.Files.Count} containers");
                foreach (var (container, files) in response.Files)
                {
                    Debug.WriteLine($"[Fetch] Processing container {container} with {files.Count} files");
                    foreach (var file in files)
                    {
                        items.Add(new FetchItemViewModel(file, container, contentType.Name));
                    }
                }

                Debug.WriteLine($"[Fetch] Completed with {items.Count} total items");
                return items;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Fetch] Error: {ex.Message}");
                _errorHandlingService.HandleError("Failed to fetch items", ex);
                return Enumerable.Empty<FetchItemViewModel>();
            }
        }

        // New method to handle single item extraction
        public async Task<bool> ExtractItemAsync(FetchItemViewModel item)
        {
            if (item == null || string.IsNullOrEmpty(item.CharacterId))
            {
                _errorHandlingService.HandleError(
                    $"Failed to extract {item?.CharacterName ?? "unknown item"}", 
                    "Invalid item or missing Character ID");
                return false;
            }

            try
            {
                var successCount = 0;
                var expectedCount = 0;

                // Extract .awb file with container
                var awbRequest = new ExtractRequest 
                { 
                    ContentPath = $"{item.CharacterId}.awb",
                    ArchiveName = item.Container 
                };
                expectedCount++;

                var awbResponse = await _extractorService.ExtractAsync(awbRequest);
                // Check Success property instead of message
                if (awbResponse.Success && awbResponse.FilePaths?.Any() == true)
                {
                    successCount++;
                }
                else
                {
                    _errorHandlingService.HandleError(
                        $"Failed to extract {item.CharacterName} (.awb)", 
                        awbResponse.Message ?? "Unknown error");
                }

                // For non-mods, also extract .uasset file without container
                if (!item.IsMod)
                {
                    expectedCount++;
                    var uassetRequest = new ExtractRequest 
                    { 
                        ContentPath = $"{item.CharacterId}.uasset".Replace("_Cnk_00",""),
                        ArchiveName = null
                    };

                    var uassetResponse = await _extractorService.ExtractAsync(uassetRequest);
                    // Check Success property and file paths
                    if (uassetResponse.Success && uassetResponse.FilePaths?.Any() == true)
                    {
                        successCount++;
                    }
                    else
                    {
                        _errorHandlingService.HandleError(
                            $"Failed to extract {item.CharacterName} (.uasset)", 
                            uassetResponse.Message ?? "Unknown error");
                    }
                }

                // Only return true if we got all expected files
                return successCount == expectedCount;
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleError($"Extract operation failed for {item.CharacterName}", ex);
                return false;
            }
        }

        // Update existing method to use new single item extraction
        public async Task<bool> ExtractItemsAsync(IEnumerable<FetchItemViewModel> items)
        {
            if (items == null || !items.Any())
            {
                return true;
            }

            var allSucceeded = true;
            foreach (var item in items)
            {
                if (!await ExtractItemAsync(item))
                {
                    allSucceeded = false;
                }
            }
            return allSucceeded;
        }
    }
}
