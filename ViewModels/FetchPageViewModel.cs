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

namespace SZExtractorGUI.ViewModels
{
    public class FetchPageViewModel : BindableBase, IDisposable
    {
        // Initialize as true to show loading on startup
        private bool _isOperationInProgress = true;
        private ObservableCollection<ContentType> _contentTypes;
        private ContentType _selectedContentType;
        private ObservableCollection<FetchItemViewModel> _remoteItems = new();
        private ObservableCollection<FetchItemViewModel> _selectedItems;
        private FetchItemViewModel _selectedRemoteItem;
        private string _searchText;
        private bool _showModsOnly;
        private bool _showGameFilesOnly;
        private ICollectionView _remoteItemsView;
        private bool _disposed;
        private bool _initialized;

        private readonly IContentTypeService _contentTypeService;
        private readonly IFetchOperationService _fetchOperationService;
        private readonly IItemFilterService _itemFilterService;
        private readonly IBackgroundOperationsService _backgroundOps;
        private readonly ISzExtractorService _szExtractorService;
        private readonly IInitializationService _initializationService;

        public ICommand FetchFilesCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

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

        public FetchPageViewModel(
            IContentTypeService contentTypeService,
            IFetchOperationService fetchOperationService,
            IItemFilterService itemFilterService,
            IBackgroundOperationsService backgroundOps,
            ISzExtractorService szExtractorService,
            IInitializationService initializationService)
        {
            _contentTypeService = contentTypeService;
            _fetchOperationService = fetchOperationService;
            _itemFilterService = itemFilterService;
            _backgroundOps = backgroundOps;
            _szExtractorService = szExtractorService;
            _initializationService = initializationService;

            // Initialize collections first
            InitializeCollections();

            _backgroundOps.OperationStatusChanged += (s, isActive) =>
            {
                IsOperationInProgress = isActive;
            };

            // Listen for server configuration completion
            _initializationService.ConfigurationCompleted += (s, success) =>
            {
                if (success)
                {
                    _ = ExecuteRefreshAsync();
                }
                else
                {
                    // Ensure progress ring is hidden if configuration fails
                    IsOperationInProgress = false;
                }
            };

            InitializeCommands();
        }

        private void InitializeCommands()
        {
            FetchFilesCommand = new RelayCommand(
                async () => await ExecuteFetchFilesAsync(),
                () => true  // Always enabled
            );

            RefreshCommand = new RelayCommand(
                async () => await ExecuteRefreshAsync(),
                () => true  // Always enabled
            );
        }

        private async Task ExecuteFetchFilesAsync()
        {
            if (SelectedContentType == null) return;

            try
            {
                await _backgroundOps.ExecuteOperationAsync(async () =>
                {
                    var items = await _fetchOperationService.FetchItemsAsync(SelectedContentType);
                    if (items != null)
                    {
                        await UpdateItemsCollectionAsync(items);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Fetch] Error fetching files: {ex.Message}");
            }
        }

        private async Task ExecuteRefreshAsync()
        {
            if (SelectedContentType == null) return;

            try
            {
                await _backgroundOps.ExecuteOperationAsync(async () =>
                {
                    // Then fetch
                    var items = await _fetchOperationService.FetchItemsAsync(SelectedContentType);
                    if (items != null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            UpdateItemsCollectionAsync(items).Wait();
                            // Force UI refresh
                            OnPropertyChanged(nameof(RemoteItemsView));
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Refresh] Error: {ex.Message}");
            }
        }

        public async Task UpdateItemsCollectionAsync(IEnumerable<FetchItemViewModel> items)
        {
            if (items == null) return;

            // Already on UI thread due to Dispatcher.InvokeAsync
            var selectedItemsCache = new HashSet<string>(SelectedItems.Select(x => x.CharacterId));

            RemoteItems.Clear();
            foreach (var item in items)
            {
                RemoteItems.Add(item);
                if (selectedItemsCache.Contains(item.CharacterId))
                {
                    item.IsSelected = true;
                }
            }

            // Force collection view refresh
            _remoteItemsView?.Refresh();
            OnPropertyChanged(nameof(RemoteItems));
        }

        private bool FilterItems(object item)
        {
            if (!(item is FetchItemViewModel fetchItem))
                return false;

            var parameters = new FilterParameters
            {
                SearchText = SearchText,
                ShowModsOnly = ShowModsOnly,
                ShowGameFilesOnly = ShowGameFilesOnly,
                LanguageOption = SelectedLanguage
            };

            return _itemFilterService.FilterItem(fetchItem, parameters);
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

            _disposed = true;
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
                    // Only trigger fetch when explicitly changed after initialization
                    if (_initialized)
                    {
                        _ = ExecuteRefreshAsync();
                    }
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
                    if (value != null && !SelectedItems.Contains(value))
                    {
                        SelectedItems.Add(value);
                    }
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
            set => SetProperty(ref _isOperationInProgress, value);
        }

        private void HandleItemSelection(FetchItemViewModel item, bool isSelected)
        {
            if (item == null) return;

            if (isSelected)
            {
                if (!SelectedItems.Contains(item))
                {
                    SelectedItems.Add(item);
                    _remoteItemsView?.Refresh();
                }
            }
            else
            {
                if (SelectedItems.Contains(item))
                {
                    SelectedItems.Remove(item);
                    _remoteItemsView?.Refresh();
                    if (SelectedRemoteItem == item)
                    {
                        SelectedRemoteItem = null;
                    }
                }
            }
        }

        private void InitializeCollections()
        {
            RemoteItems = new ObservableCollection<FetchItemViewModel>();
            SelectedItems = new ObservableCollection<FetchItemViewModel>();

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

        private async Task ExtractSelectedItemsAsync()
        {
            var selectedItems = RemoteItems.Where(x => x.IsSelected).ToList();
            if (!selectedItems.Any()) return;

            try
            {
                await _backgroundOps.ExecuteOperationAsync(async () =>
                {
                    await _fetchOperationService.ExtractItemsAsync(selectedItems);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Extract] Error extracting files: {ex.Message}");
            }
        }
    }

    public static class TaskExtensions
    {
        public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
            if (completedTask == task)
            {
                cts.Cancel();
                return await task;
            }
            throw new TimeoutException("The operation has timed out.");
        }
    }
}
