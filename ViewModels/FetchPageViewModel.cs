using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Diagnostics;
using SZExtractorGUI.Mvvm;
using System.ComponentModel;
using System.Windows.Data;
using SZExtractorGUI.Services;
using System;
using System.Threading.Tasks;
using SZExtractorGUI.Models;
using System.IO;
using System.Runtime;
using System.Net.Http;
using System.Windows.Threading;
using System.Windows;
using SZExtractorGUI.Services.Initialisation;
using SZExtractorGUI.Services.Fetch;
using SZExtractorGUI.Services.FileInfo;
using SZExtractorGUI.Services.Localization;
using SZExtractorGUI.Services.Configuration;
using SZExtractorGUI.Viewmodels;
using System.Collections.Concurrent;
using SZExtractorGUI.Services.FileFetch;
using SZExtractorGUI.Utilities;
using SZExtractorGUI.Services.State;
using Microsoft.Win32;
using System.Text;

namespace SZExtractorGUI.ViewModels
{
    public class FetchPageViewModel : BindableBase, IDisposable
    {
        private bool _isOperationInProgress = true;
        private ObservableCollection<ContentType> _contentTypes;
        private ContentType _selectedContentType;
        private ObservableCollection<FetchItemViewModel> _remoteItems = [];
        private ObservableCollection<FetchItemViewModel> _selectedItems;
        private FetchItemViewModel _selectedRemoteItem;
        private string _searchText;
        private bool _showModsOnly;
        private bool _showGameFilesOnly;
        private ICollectionView _remoteItemsView;
        private bool _disposed;
        private bool _initialized;
        private bool _isRefreshing;

        private readonly IContentTypeService _contentTypeService;
        private readonly IFetchOperationService _fetchOperationService;
        private readonly IItemFilterService _itemFilterService;
        private readonly IBackgroundOperationsService _backgroundOps;
        private readonly IInitializationService _initializationService;
        private readonly ICharacterNameManager _characterNameManager;
        private readonly Settings _settings;
        private readonly IPackageInfo _packageInfo;
        private readonly Configuration _configuration;
        private readonly IServerConfigurationService _serverConfigurationService;

        private RelayCommand _fetchFilesCommand;
        private RelayCommand _refreshCommand;
        private RelayCommand _exportToCsvCommand;

        private readonly Dictionary<string, FetchItemViewModel> _itemCache = [];

        private readonly Dictionary<string, string> _languageNameMapping = new(StringComparer.Ordinal)
        {
            { "all", "All Languages" },
            { "en", "English" },
            { "ja", "Japanese" },
            { "zh-Hans", "Chinese (Simplified)" },
            { "zh-Hant", "Chinese (Traditional)" },
            { "ko", "Korean" },
            { "th", "Thai" },
            { "id", "Indonesian" },
            { "ar", "Arabic" },
            { "pl", "Polish" },
            { "es", "Spanish" },
            { "es-419", "Spanish (Latin America)" },
            { "ru", "Russian" },
            { "de", "German" },
            { "it", "Italian" },
            { "fr", "French" },
            { "pt-BR", "Portuguese (Brazil)" }
        };
        public ObservableCollection<LanguageItem> AvailableLanguages { get; } = [];

        private LanguageOption _selectedLanguage = LanguageOption.All;
        public LanguageOption SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    _remoteItemsView?.Refresh();
                }
            }
        }

        public event EventHandler<(FetchItemViewModel Item, bool Success)> ItemExtractionCompleted;

        public FetchPageViewModel(
            IContentTypeService contentTypeService,
            IFetchOperationService fetchOperationService,
            IItemFilterService itemFilterService,
            IBackgroundOperationsService backgroundOps,
            IInitializationService initializationService,
            ICharacterNameManager characterNameManager,
            IPackageInfo packageInfo,
            Settings settings,
            Configuration configuration,
            IServerConfigurationService serverConfigurationService)
        {
            _contentTypeService = contentTypeService;
            _fetchOperationService = fetchOperationService;
            _itemFilterService = itemFilterService;
            _backgroundOps = backgroundOps;
            _initializationService = initializationService;
            _characterNameManager = characterNameManager;
            _packageInfo = packageInfo;
            _settings = settings;
            _configuration = configuration;
            _serverConfigurationService = serverConfigurationService;

            InitializeLanguages();
            InitializeCollections();

            _isRefreshing = false;
            _backgroundOps.OperationStatusChanged += (s, isActive) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsOperationInProgress = isActive;
                    UpdateCommandStates();
                });
            };

            _initializationService.ConfigurationCompleted += (s, success) =>
            {
                if (!success)
                {
                    IsOperationInProgress = false;
                }
            };

            InitializeCommands();
            _ = InitializeAsync();
        }

        private void InitializeCommands()
        {
            _fetchFilesCommand = new RelayCommand(
                async () => await ExtractSelectedItemsAsync(),
                () => SelectedItems?.Any() == true && !IsOperationInProgress
            );

            _refreshCommand = new RelayCommand(
                async () => await ExecuteRefreshAsync(),
                () => !_isRefreshing && !IsOperationInProgress && SelectedContentType != null
            );

            _exportToCsvCommand = new RelayCommand(
                () => ExportToCsv(),
                () => RemoteItems?.Any() == true && !IsOperationInProgress
            );
        }

        public ICommand FetchFilesCommand => _fetchFilesCommand;
        public ICommand RefreshCommand => _refreshCommand;
        public ICommand ExportToCsvCommand => _exportToCsvCommand;

        private void InitializeLanguages()
        {
            Debug.WriteLine("[Languages] Initializing language collections");

            var languageItems = _languageNameMapping
                .Select(kvp => new LanguageItem
                {
                    Code = kvp.Key,
                    DisplayName = kvp.Value
                })
                .OrderBy(item => item.Code == "all" ? 0 : 1)
                .ThenBy(item => item.DisplayName);

            AvailableLanguages.Clear();
            foreach (var item in languageItems)
            {
                AvailableLanguages.Add(item);
            }

            if (string.IsNullOrEmpty(_settings.DisplayLanguage))
                _settings.DisplayLanguage = "en";

            if (string.IsNullOrEmpty(_settings.TextLanguage))
                _settings.TextLanguage = "en";

            _selectedDisplayLanguageItem = AvailableLanguages.FirstOrDefault(x =>
                x.Code.Equals(_settings.DisplayLanguage, StringComparison.OrdinalIgnoreCase))
                ?? AvailableLanguages.First(x => x.Code == "en");

            _selectedTextLanguageItem = AvailableLanguages.FirstOrDefault(x =>
                x.Code.Equals(_settings.TextLanguage, StringComparison.OrdinalIgnoreCase))
                ?? AvailableLanguages.First(x => x.Code == "en");

            Debug.WriteLine($"[Languages] Initial Display Language: {_selectedDisplayLanguageItem.DisplayName}");
            Debug.WriteLine($"[Languages] Initial Text Language: {_selectedTextLanguageItem.DisplayName}");

            OnPropertyChanged(nameof(AvailableLanguages));
            OnPropertyChanged(nameof(SelectedDisplayLanguageItem));
            OnPropertyChanged(nameof(SelectedTextLanguageItem));
            OnPropertyChanged(nameof(DisplayLanguage));
            OnPropertyChanged(nameof(TextLanguage));
        }

        private async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("[Initialize] Starting initialization");
                await _backgroundOps.ExecuteOperationAsync(async () =>
                {
                    Debug.WriteLine("[Initialize] Waiting for initialization service");

                    await _initializationService.InitializeAsync();

                    if (_initializationService.IsInitialized)
                    {
                        Debug.WriteLine("[Initialize] Initialization complete");

                        Debug.WriteLine("[Initialize] Starting initial fetch");
                        await FetchItemsAsync();

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _remoteItemsView?.Refresh();
                        });
                    }
                    else
                    {
                        Debug.WriteLine("[Initialize] Initialization service not initialized");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Initialize] Error: {ex.Message}");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsOperationInProgress = false;
                    (_fetchFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                });
            }
        }


        private async Task ExecuteRefreshAsync()
        {
            if (SelectedContentType == null || _isRefreshing) return;

            try
            {
                _isRefreshing = true;
                (_refreshCommand as RelayCommand)?.RaiseCanExecuteChanged();

                await _backgroundOps.ExecuteOperationAsync(async () =>
                {
                    await ExecuteFullRefreshWithConfigureAsync();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Refresh] Error: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
                (_refreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private async Task ExecuteFullRefreshWithConfigureAsync()
        {
            try
            {
                Debug.WriteLine("[Refresh] Starting first refresh before configuration");
                await FetchItemsAsync();
                
                Debug.WriteLine("[Refresh] Executing server configuration");
                bool configSuccess = await _serverConfigurationService.ConfigureServerAsync(_settings);
                
                if (configSuccess)
                {
                    Debug.WriteLine("[Refresh] Configuration successful, starting second refresh");
                }
                else
                {
                    Debug.WriteLine("[Refresh] Configuration failed or completed with warnings");
                }
                
                Debug.WriteLine("[Refresh] Starting second refresh after configuration");
                await FetchItemsAsync();
                
                Debug.WriteLine("[Refresh] Full refresh cycle completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Refresh] Error during full refresh cycle: {ex.Message}");
                throw;
            }
        }

        private async Task ExecuteSimpleRefreshAsync()
        {
            if (SelectedContentType == null || _isRefreshing) return;

            try
            {
                _isRefreshing = true;
                (_refreshCommand as RelayCommand)?.RaiseCanExecuteChanged();

                await _backgroundOps.ExecuteOperationAsync(async () =>
                {
                    Debug.WriteLine("[SimpleRefresh] Refreshing items without configuration");
                    await FetchItemsAsync();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimpleRefresh] Error: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
                (_refreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private async Task FetchItemsAsync()
        {
            if (SelectedContentType == null) return;
            if (_settings.DisplayLanguage != "all")
                await LoadLocresForLanguageAsync(_settings.DisplayLanguage);

            var items = await _fetchOperationService.FetchItemsAsync(
                SelectedContentType,
                _packageInfo,
                _settings.DisplayLanguage);

            if (items != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateItemsCollectionAsync(items).Wait();
                    OnPropertyChanged(nameof(RemoteItemsView));
                });
            }
        }

        public async Task UpdateItemsCollectionAsync(IEnumerable<FetchItemViewModel> items)
        {
            if (items == null) return;

            Debug.WriteLine($"[UpdateItems] Updating {items.Count()} items");

            var selectedItemIds = new HashSet<string>(SelectedItems.Select(item => GetItemUniqueKey(item)));

            RemoteItems.Clear();

            foreach (var item in items)
            {
                var key = GetItemUniqueKey(item);

                if (_itemCache.TryGetValue(key, out var existingItem))
                {
                    existingItem.UpdateCharacterName(_settings.DisplayLanguage);
                    existingItem.IsSelected = selectedItemIds.Contains(key);
                    RemoteItems.Add(existingItem);
                }
                else
                {
                    item.UpdateCharacterName(_settings.DisplayLanguage);
                    _itemCache[key] = item;
                    item.IsSelected = selectedItemIds.Contains(key);
                    RemoteItems.Add(item);
                }
            }

            _remoteItemsView?.Refresh();
            (_fetchFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(RemoteItems));
        }

        private static string GetItemUniqueKey(FetchItemViewModel item)
        {
            return $"{item.ContentPath}|{item.Container}";
        }

        private bool FilterItems(object item)
        {
            if (item is not FetchItemViewModel fetchItem)
                return false;

            var parameters = new FilterParameters
            {
                SearchText = SearchText,
                ShowModsOnly = ShowModsOnly,
                ShowGameFilesOnly = ShowGameFilesOnly,
                LanguageOption = SelectedLanguage,
                ContentType = SelectedContentType,
                CurrentTextLanguage = _settings.TextLanguage
            };

            return _itemFilterService.FilterItem(fetchItem, parameters);
        }

        private async Task ExtractSelectedItemsAsync()
        {
            var selectedItems = SelectedItems.ToList();
            if (selectedItems.Count == 0)
            {
                Debug.WriteLine("[Extract] No items selected");
                return;
            }

            Debug.WriteLine($"[Extract] Starting extraction of {selectedItems.Count} items");

            try
            {
                foreach (var item in selectedItems)
                {
                    await _backgroundOps.ExecuteOperationAsync(async () =>
                    {
                        Debug.WriteLine($"[Extract] Extracting item: {GetItemUniqueKey(item)}");
                        var success = await _fetchOperationService.ExtractItemAsync(item);

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            Debug.WriteLine($"[Extract] Item {GetItemUniqueKey(item)} extraction {(success ? "succeeded" : "failed")}");
                            item.ExtractionFailed = !success;

                            if (success)
                            {
                                item.IsSelected = false;
                                SelectedItems.Remove(item);
                            }

                            ItemExtractionCompleted?.Invoke(this, (item, success));
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Extract] Error during extraction: {ex.Message}");
                throw;
            }
            finally
            {
                (_fetchFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void ExportToCsv()
        {
            try
            {
                // Get the program's directory
                var programDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                // Create filename with content type (replace spaces with underscores)
                var contentTypeName = SelectedContentType?.Name ?? "Items";
                var sanitizedContentType = string.Join("_", contentTypeName.Split(Path.GetInvalidFileNameChars()))
                    .Replace(" ", "_");
                var defaultFileName = $"SZ_{sanitizedContentType}_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = defaultFileName,
                    InitialDirectory = programDirectory
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var itemsToExport = _remoteItemsView.Cast<FetchItemViewModel>().ToList();
                    
                    if (itemsToExport.Count == 0)
                    {
                        MessageBox.Show("No items to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var csv = new StringBuilder();
                    
                    // Header with underscores instead of spaces
                    csv.AppendLine("Name,ID,Type,Container,Is_Mod,Content_Path");

                    // Data rows
                    foreach (var item in itemsToExport)
                    {
                        csv.AppendLine($"\"{EscapeCsvField(item.CharacterName)}\"," +
                                     $"\"{EscapeCsvField(item.CharacterId)}\"," +
                                     $"\"{EscapeCsvField(item.Type)}\"," +
                                     $"\"{EscapeCsvField(item.Container)}\"," +
                                     $"\"{(item.IsMod ? "Yes" : "No")}\"," +
                                     $"\"{EscapeCsvField(item.ContentPath)}\"");
                    }

                    File.WriteAllText(saveFileDialog.FileName, csv.ToString(), Encoding.UTF8);
                    
                    MessageBox.Show($"Successfully exported {itemsToExport.Count} items to:\n{saveFileDialog.FileName}", 
                                  "Export Complete", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                    
                    Debug.WriteLine($"[Export] Exported {itemsToExport.Count} items to {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Export] Error: {ex.Message}");
                MessageBox.Show($"Failed to export items: {ex.Message}", 
                              "Export Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;
            
            // Escape double quotes by doubling them
            return field.Replace("\"", "\"\"");
        }

        public void Dispose()
        {

            if (_disposed) return;

            if (_remoteItems != null)
            {
                RemoteItems.CollectionChanged -= Items_CollectionChanged;
                foreach (var item in RemoteItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }

            _itemCache.Clear();
            _languageLoadSemaphore?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (FetchItemViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (FetchItemViewModel item in e.NewItems)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FetchItemViewModel.IsSelected))
            {
                var item = (FetchItemViewModel)sender;
                HandleItemSelection(item, item.IsSelected);
            }
        }

        public ObservableCollection<FetchItemViewModel> SelectedItems
        {
            get => _selectedItems;
            set => SetProperty(ref _selectedItems, value);
        }

        public ObservableCollection<ContentType> ContentTypes
        {
            get => _contentTypes;
            set => SetProperty(ref _contentTypes, value);
        }

        public ContentType SelectedContentType
        {
            get => _selectedContentType;
            set
            {
                if (SetProperty(ref _selectedContentType, value))
                {
                    if (_initialized)
                    {
                        _ = ExecuteSimpleRefreshAsync();
                    }
                    (_fetchFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<FetchItemViewModel> RemoteItems
        {
            get => _remoteItems;
            private set => SetProperty(ref _remoteItems, value);
        }

        public FetchItemViewModel SelectedRemoteItem
        {
            get => _selectedRemoteItem;
            set
            {
                if (SetProperty(ref _selectedRemoteItem, value))
                {
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _remoteItemsView?.Refresh();
                }
            }
        }

        public bool ShowModsOnly
        {
            get => _showModsOnly;
            set
            {
                if (SetProperty(ref _showModsOnly, value))
                {
                    _remoteItemsView?.Refresh();
                }
            }
        }

        public bool ShowGameFilesOnly
        {
            get => _showGameFilesOnly;
            set
            {
                if (SetProperty(ref _showGameFilesOnly, value))
                {
                    _remoteItemsView?.Refresh();
                }
            }
        }

        public ICollectionView RemoteItemsView => _remoteItemsView;
        public bool IsOperationInProgress
        {
            get => _isOperationInProgress;
            set
            {
                if (SetProperty(ref _isOperationInProgress, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        private void HandleItemSelection(FetchItemViewModel item, bool isSelected)
        {
            if (item == null) return;

            if (isSelected && !SelectedItems.Contains(item))
            {
                SelectedItems.Add(item);
            }
            else if (!isSelected && SelectedItems.Contains(item))
            {
                SelectedItems.Remove(item);
                if (SelectedRemoteItem == item)
                {
                    SelectedRemoteItem = null;
                }
            }

            _fetchFilesCommand?.RaiseCanExecuteChanged();
        }

        private void InitializeCollections()
        {
            RemoteItems = [];
            SelectedItems = [];

            _remoteItemsView = CollectionViewSource.GetDefaultView(RemoteItems);
            _remoteItemsView.Filter = FilterItems;
            _remoteItemsView.SortDescriptions.Add(
                new SortDescription("CharacterId", ListSortDirection.Ascending));

            RemoteItems.CollectionChanged += Items_CollectionChanged;

            ContentTypes = _contentTypeService.GetContentTypes();

            _selectedContentType = _contentTypeService.GetDefaultContentType();
            OnPropertyChanged(nameof(SelectedContentType));

            _initialized = true;
        }

        public string DisplayLanguage
        {
            get => GetLanguageDisplayName(_settings.DisplayLanguage);
            set
            {
                var languageCode = GetLanguageCode(value);
                if (_settings.DisplayLanguage != languageCode)
                {
                    Debug.WriteLine($"[Language] Changing display language from {_settings.DisplayLanguage} to {languageCode}");
                    _settings.DisplayLanguage = languageCode;
                    _configuration.SaveConfiguration();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (languageCode != "all")
                                await LoadLocresForLanguageAsync(languageCode);

                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    Debug.WriteLine($"[Language] Updating character names for {_itemCache.Count} items");

                                    foreach (var item in _itemCache.Values)
                                    {
                                        item.UpdateCharacterName(languageCode);
                                    }

                                    _remoteItemsView?.Refresh();

                                    var newItem = AvailableLanguages.FirstOrDefault(x =>
                                        x.Code.Equals(languageCode, StringComparison.Ordinal));
                                    if (newItem != null && _selectedDisplayLanguageItem != newItem)
                                    {
                                        _selectedDisplayLanguageItem = newItem;
                                        OnPropertyChanged(nameof(SelectedDisplayLanguageItem));
                                    }

                                    OnPropertyChanged(nameof(DisplayLanguage));
                                    UpdateCommandStates();
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[Language] Error updating UI: {ex.Message}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Language] Error during language change: {ex.Message}");
                        }
                    });
                }
            }
        }

            private async Task LoadLocresForLanguageAsync(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                Debug.WriteLine("[LoadLocres] Empty language code provided");
                return;
            }

            if (_characterNameManager.IsLocresLoaded(language))
            {
                Debug.WriteLine($"[LoadLocres] Language {language} already loaded, skipping");
                return;
            }

            if (!_languageLoadingState.TryAdd(language, true))
            {
                Debug.WriteLine($"[LoadLocres] Language {language} is already being loaded by another thread");

                var waitStart = DateTime.UtcNow;
                while (_languageLoadingState.ContainsKey(language))
                {
                    if ((DateTime.UtcNow - waitStart).TotalMilliseconds > LANGUAGE_LOAD_TIMEOUT_MS)
                    {
                        Debug.WriteLine($"[LoadLocres] Timeout waiting for language {language} to load");
                        throw new TimeoutException($"Timeout waiting for language {language} to load");
                    }
                    await Task.Delay(100);
                }

                if (_characterNameManager.IsLocresLoaded(language))
                {
                    Debug.WriteLine($"[LoadLocres] Language {language} was loaded by another thread");
                    return;
                }
            }

            try
            {
                Debug.WriteLine($"[LoadLocres] Attempting to acquire semaphore for {language}");
                if (!await _languageLoadSemaphore.WaitAsync(LANGUAGE_LOAD_TIMEOUT_MS))
                {
                    throw new TimeoutException($"Timeout waiting for semaphore to load language {language}");
                }

                Debug.WriteLine($"[LoadLocres] Acquired semaphore for {language}");

                if (_characterNameManager.IsLocresLoaded(language))
                {
                    Debug.WriteLine($"[LoadLocres] Language {language} was loaded while waiting for semaphore");
                    return;
                }

                await _backgroundOps.ExecuteOperationAsync(async () =>
                {
                    try
                    {
                        var locresFiles = await _fetchOperationService.GetLocresFiles();
                        var languageFile = locresFiles?.FirstOrDefault(f =>
                            LanguageCodeValidator.ExtractLanguageFromFileName(f) == language);

                        if (languageFile != null)
                        {
                            Debug.WriteLine($"[LoadLocres] Found locres file: {languageFile}");

                            const int maxRetries = 3;
                            for (int attempt = 1; attempt <= maxRetries; attempt++)
                            {
                                try
                                {
                                    await _characterNameManager.LoadLocresFile(language, languageFile);

                                    if (_characterNameManager.IsLocresLoaded(language))
                                    {
                                        Debug.WriteLine($"[LoadLocres] Successfully loaded {language} locres");
                                        return;
                                    }

                                    if (attempt < maxRetries)
                                    {
                                        Debug.WriteLine($"[LoadLocres] Retrying load for {language}, attempt {attempt + 1}");
                                        await Task.Delay(100 * attempt);
                                    }
                                }
                                catch (Exception ex) when (attempt < maxRetries)
                                {
                                    Debug.WriteLine($"[LoadLocres] Attempt {attempt} failed: {ex.Message}");
                                }
                            }

                            throw new InvalidOperationException($"Failed to load locres for {language} after {maxRetries} attempts");
                        }
                        else
                        {
                            Debug.WriteLine($"[LoadLocres] No locres file found for {language}");
                            throw new FileNotFoundException($"No locres file found for {language}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LoadLocres] Error in background operation: {ex.Message}");
                        throw;
                    }
                });
            }
            finally
            {
                _languageLoadSemaphore.Release();
                Debug.WriteLine($"[LoadLocres] Released semaphore for {language}");

                _languageLoadingState.TryRemove(language, out _);
                Debug.WriteLine($"[LoadLocres] Removed loading state for {language}");
            }
        }

        private string GetLanguageDisplayName(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return _languageNameMapping["en"];

            return _languageNameMapping.TryGetValue(languageCode, out var name)
                ? name
                : languageCode;
        }

        private string GetLanguageCode(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return "en";

            if (_languageNameMapping.ContainsKey(displayName))
                return displayName.ToLowerInvariant();

            return _languageNameMapping
                .FirstOrDefault(x => x.Value.Equals(displayName, StringComparison.OrdinalIgnoreCase))
                .Key ?? "en";
        }

        public string TextLanguage
        {
            get => GetLanguageDisplayName(_settings.TextLanguage);
            set
            {
                var languageCode = GetLanguageCode(value);
                if (_settings.TextLanguage != languageCode)
                {
                    _settings.TextLanguage = languageCode;
                    _configuration.SaveConfiguration();

                    var newItem = AvailableLanguages.FirstOrDefault(x => x.Code == languageCode);
                    if (newItem != null && _selectedTextLanguageItem != newItem)
                    {
                        SelectedTextLanguageItem = newItem;
                    }
                    OnPropertyChanged();
                    _remoteItemsView?.Refresh();
                }
            }
        }

        private LanguageItem _selectedDisplayLanguageItem;
        private LanguageItem _selectedTextLanguageItem;

        public LanguageItem SelectedDisplayLanguageItem
        {
            get => _selectedDisplayLanguageItem;
            set
            {
                if (SetProperty(ref _selectedDisplayLanguageItem, value) && value != null)
                {
                    Debug.WriteLine($"[Languages] Display Language changed to: {value.DisplayName} ({value.Code})");
                    DisplayLanguage = value.DisplayName;
                }
            }
        }

        public LanguageItem SelectedTextLanguageItem
        {
            get => _selectedTextLanguageItem;
            set
            {
                if (SetProperty(ref _selectedTextLanguageItem, value) && value != null)
                {
                    Debug.WriteLine($"[Languages] Text Language changed to: {value.DisplayName} ({value.Code})");
                    TextLanguage = value.DisplayName;
                }
            }
        }

        private static readonly SemaphoreSlim _languageLoadSemaphore = new(1, 1);
        private static readonly ConcurrentDictionary<string, bool> _languageLoadingState = new();
        private const int LANGUAGE_LOAD_TIMEOUT_MS = 30000;
        private readonly object _commandLock = new();

        private void UpdateCommandStates()
        {
            lock (_commandLock)
            {
                (_fetchFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (_refreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (_exportToCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public class LanguageItem
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
    }
}
