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

        public async Task<bool> ExtractItemsAsync(IEnumerable<FetchItemViewModel> items)
        {
            if (items == null || !items.Any())
            {
                return true; // No items to extract
            }

            var successCount = 0;
            var totalCount = items.Count();

            try
            {
                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.ContentPath))
                    {
                        _errorHandlingService.HandleError(
                            $"Failed to extract {item.CharacterName}", 
                            "Content path is missing");
                        continue;
                    }

                    var request = new ExtractRequest { ContentPath = item.ContentPath };
                    var response = await _extractorService.ExtractAsync(request);

                    if (response.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        _errorHandlingService.HandleError(
                            $"Failed to extract {item.CharacterName}", 
                            response.Message ?? "Unknown error");
                    }
                }

                // Return true only if all items were extracted successfully
                return successCount == totalCount;
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleError("Extract operation failed", ex);
                return false;
            }
        }
    }
}
