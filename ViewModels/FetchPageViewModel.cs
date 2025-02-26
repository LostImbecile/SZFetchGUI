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

namespace SZExtractorGUI.ViewModels
{
    public class FetchPageViewModel : BindableBase, IDisposable
    {
        // Initialize as true to show loading on startup
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

        // Add private field to store the command
        private RelayCommand _fetchFilesCommand;
        private RelayCommand _refreshCommand;

        // Add near the top with other private fields
        private readonly Dictionary<string, FetchItemViewModel> _itemCache = [];

        // Update the language mapping to preserve exact case
        private readonly Dictionary<string, string> _languageNameMapping = new(StringComparer.Ordinal)
        {
            { "all", "All Languages" },
            { "en", "English" },
            { "ja", "Japanese" },
            { "zh-Hans", "Chinese (Simplified)" },    // Preserve exact case
            { "zh-Hant", "Chinese (Traditional)" },   // Preserve exact case
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
            { "pt-BR", "Portuguese (Brazil)" }        // Preserve exact case
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

        // Add event for item extraction completion
        public event EventHandler<(FetchItemViewModel Item, bool Success)> ItemExtractionCompleted;

        // Update constructor to initialize languages immediately
        public FetchPageViewModel(
            IContentTypeService contentTypeService,
            IFetchOperationService fetchOperationService,
            IItemFilterService itemFilterService,
            IBackgroundOperationsService backgroundOps,
            IInitializationService initializationService,
            ICharacterNameManager characterNameManager,
            IPackageInfo packageInfo,
            Settings settings,
            Configuration configuration) // Add configuration parameter
        {
            _contentTypeService = contentTypeService;
            _fetchOperationService = fetchOperationService;
            _itemFilterService = itemFilterService;
            _backgroundOps = backgroundOps;
            _initializationService = initializationService;
            _characterNameManager = characterNameManager;
            _packageInfo = packageInfo;
            _settings = settings;
            _configuration = configuration; // Initialize configuration

            // Initialize languages first, before any async operations
            InitializeLanguages();

            // Initialize collections
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

            // Start initialization process
            _ = InitializeAsync();
        }

        // Replace the existing InitializeCommands method
        private void InitializeCommands()
        {
            _fetchFilesCommand = new RelayCommand(
                async () => await ExtractSelectedItemsAsync(),
                () => SelectedItems?.Any() == true && !IsOperationInProgress // Remove content type dependency
            );

            _refreshCommand = new RelayCommand(
                async () => await ExecuteRefreshAsync(),
                () => !_isRefreshing && !IsOperationInProgress && SelectedContentType != null
            );
        }

        // Add property for FetchFilesCommand (if not already present)
        public ICommand FetchFilesCommand => _fetchFilesCommand;
        public ICommand RefreshCommand => _refreshCommand;

        // New method to handle immediate language initialization
        private void InitializeLanguages()
        {
            Debug.WriteLine("[Languages] Initializing language collections");

            // Convert all mapped languages to LanguageItems
            var languageItems = _languageNameMapping
                .Select(kvp => new LanguageItem
                {
                    Code = kvp.Key,
                    DisplayName = kvp.Value
                })
                .OrderBy(item => item.Code == "all" ? 0 : 1)
                .ThenBy(item => item.DisplayName);

            // Clear and populate available languages
            AvailableLanguages.Clear();
            foreach (var item in languageItems)
            {
                AvailableLanguages.Add(item);
            }

            // Ensure settings have valid defaults
            if (string.IsNullOrEmpty(_settings.DisplayLanguage))
                _settings.DisplayLanguage = "en";

            if (string.IsNullOrEmpty(_settings.TextLanguage))
                _settings.TextLanguage = "en";

            // Set initial selected items
            _selectedDisplayLanguageItem = AvailableLanguages.FirstOrDefault(x =>
                x.Code.Equals(_settings.DisplayLanguage, StringComparison.OrdinalIgnoreCase))
                ?? AvailableLanguages.First(x => x.Code == "en");

            _selectedTextLanguageItem = AvailableLanguages.FirstOrDefault(x =>
                x.Code.Equals(_settings.TextLanguage, StringComparison.OrdinalIgnoreCase))
                ?? AvailableLanguages.First(x => x.Code == "en");

            Debug.WriteLine($"[Languages] Initial Display Language: {_selectedDisplayLanguageItem.DisplayName}");
            Debug.WriteLine($"[Languages] Initial Text Language: {_selectedTextLanguageItem.DisplayName}");

            // Notify UI of initial values
            OnPropertyChanged(nameof(AvailableLanguages));
            OnPropertyChanged(nameof(SelectedDisplayLanguageItem));
            OnPropertyChanged(nameof(SelectedTextLanguageItem));
            OnPropertyChanged(nameof(DisplayLanguage));
            OnPropertyChanged(nameof(TextLanguage));
        }

        // Update InitializeAsync to properly handle language initialization timing
        private async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("[Initialize] Starting initialization");
                await _backgroundOps.ExecuteOperationAsync(async () =>
                {
                    Debug.WriteLine("[Initialize] Waiting for initialization service");

                    // Wait for server initialization first
                    await _initializationService.InitializeAsync();

                    if (_initializationService.IsInitialized)
                    {
                        Debug.WriteLine("[Initialize] Initialization complete");

                        // Only fetch after locres is loaded
                        Debug.WriteLine("[Initialize] Starting initial fetch");
                        await FetchItemsAsync();

                        // Update UI after everything is loaded
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
                    await FetchItemsAsync();
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

        private async Task FetchItemsAsync()
        {
            if (SelectedContentType == null) return;
            if (_settings.DisplayLanguage != "all")
                await LoadLocresForLanguageAsync(_settings.DisplayLanguage);

            var items = await _fetchOperationService.FetchItemsAsync(
                SelectedContentType,
                _packageInfo,
                _settings.DisplayLanguage);  // Always use settings version

            if (items != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateItemsCollectionAsync(items).Wait();
                    OnPropertyChanged(nameof(RemoteItemsView));
                });
            }
        }

        // Replace the existing UpdateItemsCollectionAsync method
        public async Task UpdateItemsCollectionAsync(IEnumerable<FetchItemViewModel> items)
        {
            if (items == null) return;

            Debug.WriteLine($"[UpdateItems] Updating {items.Count()} items");

            // Cache selected items state
            var selectedItemIds = new HashSet<string>(SelectedItems.Select(item => GetItemUniqueKey(item)));

            // Clear only remote items
            RemoteItems.Clear();

            foreach (var item in items)
            {
                var key = GetItemUniqueKey(item);

                // Check if we already have this item in cache
                if (_itemCache.TryGetValue(key, out var existingItem))
                {
                    // Update existing item's character name with current display language
                    existingItem.UpdateCharacterName(_settings.DisplayLanguage);
                    existingItem.IsSelected = selectedItemIds.Contains(key);
                    RemoteItems.Add(existingItem);
                }
                else
                {
                    // Initialize new item with current display language
                    item.UpdateCharacterName(_settings.DisplayLanguage);
                    _itemCache[key] = item;
                    item.IsSelected = selectedItemIds.Contains(key);
                    RemoteItems.Add(item);
                }
            }

            // Force UI updates
            _remoteItemsView?.Refresh();
            (_fetchFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(RemoteItems));
        }

        // Replace the existing GetItemUniqueKey method
        private static string GetItemUniqueKey(FetchItemViewModel item)
        {
            return $"{item.CharacterId}|{item.Container}";
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
                ContentType = SelectedContentType,  // Add ContentType
                CurrentTextLanguage = _settings.TextLanguage  // Add current language
            };

            // Pass the current content type to the filter service
            return _itemFilterService.FilterItem(fetchItem, parameters);
        }

        // Update ExtractSelectedItemsAsync with better error handling and logging
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
                                // Remove from selected items collection
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
                throw; // Rethrow to let the UI handle the error
            }
            finally
            {
                (_fetchFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
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

        // Replace SelectedContentType property
        public ContentType SelectedContentType
        {
            get => _selectedContentType;
            set
            {
                if (SetProperty(ref _selectedContentType, value))
                {
                    // Don't clear selections when changing content type
                    if (_initialized)
                    {
                        _ = ExecuteRefreshAsync();
                    }
                    // Ensure fetch command state is updated
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
                    // Don't modify item selection state here, let the grid handle it
                    // This is just for tracking the currently focused item
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

            // Simple synchronization - this is triggered by checkbox/selection changes
            if (isSelected && !SelectedItems.Contains(item))
            {
                SelectedItems.Add(item);
            }
            else if (!isSelected && SelectedItems.Contains(item))
            {
                SelectedItems.Remove(item);
                // Also clear SelectedRemoteItem if this was the selected item
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

            // Initialize the collection view immediately
            _remoteItemsView = CollectionViewSource.GetDefaultView(RemoteItems);
            _remoteItemsView.Filter = FilterItems;
            _remoteItemsView.SortDescriptions.Add(
                new SortDescription("CharacterId", ListSortDirection.Ascending));

            RemoteItems.CollectionChanged += Items_CollectionChanged;

            // Load content types after view initialization
            ContentTypes = _contentTypeService.GetContentTypes();

            // Set selected type without triggering fetch
            _selectedContentType = _contentTypeService.GetDefaultContentType();
            OnPropertyChanged(nameof(SelectedContentType));

            _initialized = true;
        }

        // Update DisplayLanguage property to include better UI synchronization
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
                    _configuration.SaveConfiguration(); // Save configuration

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
                            // Consider showing error to user here
                        }
                    });
                }
            }
        }

        // Update LoadLocresForLanguageAsync to ensure proper loading sequence
        private async Task LoadLocresForLanguageAsync(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                Debug.WriteLine("[LoadLocres] Empty language code provided");
                return;
            }

            // Quick check if already loaded before taking semaphore
            if (_characterNameManager.IsLocresLoaded(language))
            {
                Debug.WriteLine($"[LoadLocres] Language {language} already loaded, skipping");
                return;
            }

            // Try to mark this language as loading. If false, another thread is already loading it
            if (!_languageLoadingState.TryAdd(language, true))
            {
                Debug.WriteLine($"[LoadLocres] Language {language} is already being loaded by another thread");

                // Wait for the other thread to finish loading (up to timeout)
                var waitStart = DateTime.UtcNow;
                while (_languageLoadingState.ContainsKey(language))
                {
                    if ((DateTime.UtcNow - waitStart).TotalMilliseconds > LANGUAGE_LOAD_TIMEOUT_MS)
                    {
                        Debug.WriteLine($"[LoadLocres] Timeout waiting for language {language} to load");
                        throw new TimeoutException($"Timeout waiting for language {language} to load");
                    }
                    await Task.Delay(100); // Small delay to prevent tight loop
                }

                // Verify it was actually loaded
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

                // Double-check if loaded after acquiring semaphore
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

                            // Add retry logic for robustness
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
                                        await Task.Delay(100 * attempt); // Progressive delay between retries
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

                // Remove the loading state regardless of success/failure
                _languageLoadingState.TryRemove(language, out _);
                Debug.WriteLine($"[LoadLocres] Removed loading state for {language}");
            }
        }

        // Update GetLanguageDisplayName to be case-sensitive
        private string GetLanguageDisplayName(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return _languageNameMapping["en"]; // Default to English display name

            return _languageNameMapping.TryGetValue(languageCode, out var name)
                ? name
                : languageCode; // Fallback to code if no mapping exists
        }

        // Update GetLanguageCode to be case-sensitive and preserve original case
        private string GetLanguageCode(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return "en"; // Default to English code

            // First try direct match (in case it's already a code)
            if (_languageNameMapping.ContainsKey(displayName))
                return displayName.ToLowerInvariant();

            // Then look for display name match
            return _languageNameMapping
                .FirstOrDefault(x => x.Value.Equals(displayName, StringComparison.OrdinalIgnoreCase))
                .Key ?? "en"; // Default to English if no match found
        }

        // Update TextLanguage property to ensure it properly reflects settings
        public string TextLanguage
        {
            get => GetLanguageDisplayName(_settings.TextLanguage);
            set
            {
                var languageCode = GetLanguageCode(value);
                if (_settings.TextLanguage != languageCode)
                {
                    _settings.TextLanguage = languageCode;
                    _configuration.SaveConfiguration(); // Save configuration

                    // Update selected item if changed externally
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

        // Add these fields near the top of the class with other private fields
        private LanguageItem _selectedDisplayLanguageItem;
        private LanguageItem _selectedTextLanguageItem;

        // Add these properties after the other public properties
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

        // Add these near the top of the class
        private static readonly SemaphoreSlim _languageLoadSemaphore = new(1, 1);
        private static readonly ConcurrentDictionary<string, bool> _languageLoadingState = new();
        private const int LANGUAGE_LOAD_TIMEOUT_MS = 30000; // 30 second timeout
        private readonly object _commandLock = new();

        // Add this helper method for updating command states
        private void UpdateCommandStates()
        {
            lock (_commandLock)
            {
                (_fetchFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (_refreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    // Add this class for language selection
    public class LanguageItem
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
    }
}
