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
        private bool _isRefreshing;

        private readonly IContentTypeService _contentTypeService;
        private readonly IFetchOperationService _fetchOperationService;
        private readonly IItemFilterService _itemFilterService;
        private readonly IBackgroundOperationsService _backgroundOps;
        private readonly ISzExtractorService _szExtractorService;
        private readonly IInitializationService _initializationService;

        // Add private field to store the command
        private RelayCommand _fetchFilesCommand;
        private RelayCommand _refreshCommand;

        // Update the property to use the field
        public ICommand FetchFilesCommand 
        { 
            get => _fetchFilesCommand;
            private set => SetProperty(ref _fetchFilesCommand, (RelayCommand)value);
        }

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

        // Add event for item extraction completion
        public event EventHandler<(FetchItemViewModel Item, bool Success)> ItemExtractionCompleted;

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

            // Listen for server configuration completion but don't trigger refresh
            _initializationService.ConfigurationCompleted += (s, success) =>
            {
                if (!success)
                {
                    // Only update progress ring if configuration fails
                    IsOperationInProgress = false;
                }
            };

            InitializeCommands();

            // Start initialization process
            _ = InitializeAsync();
        }

        private void InitializeCommands()
        {
            _fetchFilesCommand = new RelayCommand(
                async () => await ExtractSelectedItemsAsync(),
                () => SelectedItems?.Any() == true
            );

            RefreshCommand = new RelayCommand(
                async () => await ExecuteRefreshAsync(),
                () => !_isRefreshing
            );
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _backgroundOps.ExecuteOperationAsync(async () =>
                {
                    await _initializationService.InitializeAsync();
                    // Only fetch initial data after successful initialization
                    if (_initializationService.IsInitialized)
                    {
                        await FetchItemsAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Initialize] Error: {ex.Message}");
                IsOperationInProgress = false;
            }
        }

        private async Task ExecuteRefreshAsync()
        {
            if (SelectedContentType == null || _isRefreshing) return;

            try
            {
                _isRefreshing = true;
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
            }
        }

        private async Task FetchItemsAsync()
        {
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
        }

        public async Task UpdateItemsCollectionAsync(IEnumerable<FetchItemViewModel> items)
        {
            if (items == null) return;

            // Cache selected items state - do NOT clear SelectedItems
            var selectedItemIds = new HashSet<string>(SelectedItems.Select(x => x.CharacterId));

            // Clear only remote items
            RemoteItems.Clear();

            // Add new items and restore selection state for matching items
            foreach (var item in items)
            {
                RemoteItems.Add(item);

                // Preserve selection state if it was previously selected
                if (selectedItemIds.Contains(item.CharacterId))
                {
                    item.IsSelected = true;
                }
            }

            // Force UI updates
            _remoteItemsView?.Refresh();
            _fetchFilesCommand?.RaiseCanExecuteChanged();
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
            set => SetProperty(ref _isOperationInProgress, value);
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
            var selectedItems = SelectedItems.ToList();
            if (!selectedItems.Any()) return;

            try
            {
                foreach (var item in selectedItems)
                {
                    await _backgroundOps.ExecuteOperationAsync(async () =>
                    {
                        var success = await _fetchOperationService.ExtractItemAsync(item);
                        
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // Update extraction status
                            item.ExtractionFailed = !success;
                            
                            if (success)
                            {
                                // Only unselect if extraction was successful
                                item.IsSelected = false;
                            }
                            
                            ItemExtractionCompleted?.Invoke(this, (item, success));
                        });
                    });
                }
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
