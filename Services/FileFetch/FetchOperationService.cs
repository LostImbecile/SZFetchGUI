using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using SZExtractorGUI.Models;
using SZExtractorGUI.Services.FileInfo;
using SZExtractorGUI.Services.State;
using SZExtractorGUI.ViewModels;
using SZExtractorGUI.Utilities;
using SZExtractorGUI.Viewmodels;

namespace SZExtractorGUI.Services.Fetch
{
    public interface IFetchOperationService
    {
        Task<IEnumerable<FetchItemViewModel>> FetchItemsAsync(ContentType contentType, IPackageInfo packageInfo, String displayLanguage = "en");
        Task<bool> ExtractItemAsync(FetchItemViewModel item); 
        Task<bool> ExtractItemsAsync(IEnumerable<FetchItemViewModel> items);
        Task<IEnumerable<string>> GetLocresFiles(); 
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

        public async Task<IEnumerable<FetchItemViewModel>> FetchItemsAsync(ContentType contentType, IPackageInfo packageInfo,String displayLanguage = "en")
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
                        items.Add(new FetchItemViewModel(packageInfo, file, container, contentType.Name, displayLanguage));
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

        public async Task<IEnumerable<string>> GetLocresFiles()
        {
            try
            {
                Debug.WriteLine("[Fetch] Starting fetch for locres files");
                var dumpRequest = new DumpRequest { Filter = "Data.locres" };
                var response = await _extractorService.DumpAsync(dumpRequest);

                if (!response.Success || response.Files == null)
                {
                    Debug.WriteLine("[Fetch] Locres dump request failed or returned no files");
                    throw new InvalidOperationException(response.Message ?? "Failed to retrieve locres files");
                }

                var extractedFiles = new List<string>();
                foreach (var (container, files) in response.Files)
                {
                    if (container.Contains("_P")) continue; // Skip patch containers
                    
                    foreach (var file in files)
                    {
                        // Extract and rename maintaining original language code case
                        var languageCode = ExtractLanguageCode(file);
                        if (languageCode != null)
                        {
                            var extractRequest = new ExtractRequest
                            {
                                ContentPath = file,
                                ArchiveName = container
                            };

                            var extractResponse = await _extractorService.ExtractAsync(extractRequest);
                            if (extractResponse.Success && extractResponse.FilePaths?.Any() == true)
                            {
                                foreach (var extractedPath in extractResponse.FilePaths)
                                {
                                    Debug.WriteLine($"[Fetch] Extracted locres file: {extractedPath}");
                                    var directory = Path.GetDirectoryName(extractedPath);
                                    var newPath = Path.Combine(directory!, $"{languageCode} - Data.locres");
                                    
                                    try
                                    {
                                        if (File.Exists(newPath))
                                        {
                                            File.Delete(newPath);
                                        }
                                        File.Move(extractedPath, newPath);
                                        extractedFiles.Add(newPath);
                                        Debug.WriteLine($"[Fetch] Renamed to: {newPath}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[Fetch] Failed to rename file: {ex.Message}");
                                        extractedFiles.Add(extractedPath); // Use original path as fallback
                                    }
                                }
                            }
                        }
                    }
                }

                return extractedFiles;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Fetch] Error: {ex.Message}");
                _errorHandlingService.HandleError("Failed to fetch locres files", ex);
                return Enumerable.Empty<string>();
            }
        }

        private string ExtractLanguageCode(string path)
        {
            // Extract language code based on path structure
            var parts = path.Split('/');
            foreach (var part in parts)
            {
                if (LanguageCodeValidator.IsValidLanguageCode(part))
                {
                    return part; // Return exact case-sensitive match
                }
            }
            return null;
        }
    }
}
