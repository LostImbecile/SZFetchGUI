// CustomDataGrid.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;
using System.Collections;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Diagnostics;

namespace SZExtractorGUI.Views  // Consider a more generic namespace if you intend to share this control
{
    /// <summary>
    /// A custom DataGrid control with additional features such as single and double click events,
    /// initial sorting, and selection management.
    /// </summary>
    public class CustomDataGrid : DataGrid
    {
        private readonly DispatcherTimer _doubleClickTimer;
        private bool _isSingleClick = true;
        private object _clickedItem;
        private bool _isMouseOverButtonOrCheckbox = false;
        private object _lastSelectedItem;
        private int _lastSelectedIndex = -1;

        /// <summary>
        /// Occurs when an item is single-clicked.
        /// </summary>
        public event EventHandler<object> ItemSingleClicked;

        /// <summary>
        /// Occurs when an item is double-clicked.
        /// </summary>
        public event EventHandler<object> ItemDoubleClicked;

        /// <summary>
        /// Dependency property for the name of the property that indicates selection.
        /// </summary>
        public static readonly DependencyProperty IsSelectedPropertyNameProperty =
            DependencyProperty.Register("IsSelectedPropertyName", typeof(string), typeof(CustomDataGrid), new PropertyMetadata("IsSelected"));

        /// <summary>
        /// Gets or sets the name of the property that indicates selection.
        /// </summary>
        public string IsSelectedPropertyName
        {
            get { return (string)GetValue(IsSelectedPropertyNameProperty); }
            set { SetValue(IsSelectedPropertyNameProperty, value); }
        }

        /// <summary>
        /// Dependency property to exclude the select column from selection logic.
        /// </summary>
        public static readonly DependencyProperty ExcludeSelectColumnProperty =
            DependencyProperty.Register("ExcludeSelectColumn", typeof(bool), typeof(CustomDataGrid), new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets a value indicating whether to exclude the select column from selection logic.
        /// </summary>
        public bool ExcludeSelectColumn
        {
            get { return (bool)GetValue(ExcludeSelectColumnProperty); }
            set { SetValue(ExcludeSelectColumnProperty, value); }
        }

        /// <summary>
        /// Dependency property for the list of selected items.
        /// </summary>
        public static readonly DependencyProperty SelectedItemsListProperty =
            DependencyProperty.Register("SelectedItemsList", typeof(IList), typeof(CustomDataGrid), new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the list of selected items.
        /// </summary>
        public IList SelectedItemsList
        {
            get { return (IList)GetValue(SelectedItemsListProperty); }
            set { SetValue(SelectedItemsListProperty, value); }
        }

        /// <summary>
        /// Dependency property for the selected item (supports two-way binding).
        /// </summary>
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(object), typeof(CustomDataGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// Gets or sets the selected item.
        /// </summary>
        public new object SelectedItem
        {
            get { return GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        /// <summary>
        /// Dependency property for the initial sort column.
        /// </summary>
        public static readonly DependencyProperty InitialSortColumnProperty =
            DependencyProperty.Register("InitialSortColumn", typeof(string), typeof(CustomDataGrid), new PropertyMetadata(null));

        /// <summary>
        /// Gets or sets the initial sort column.
        /// </summary>
        public string InitialSortColumn
        {
            get { return (string)GetValue(InitialSortColumnProperty); }
            set { SetValue(InitialSortColumnProperty, value); }
        }

        /// <summary>
        /// Dependency property for the initial sort direction.
        /// </summary>
        public static readonly DependencyProperty InitialSortDirectionProperty =
            DependencyProperty.Register("InitialSortDirection", typeof(ListSortDirection), typeof(CustomDataGrid), new PropertyMetadata(ListSortDirection.Ascending));

        /// <summary>
        /// Gets or sets the initial sort direction.
        /// </summary>
        public ListSortDirection InitialSortDirection
        {
            get { return (ListSortDirection)GetValue(InitialSortDirectionProperty); }
            set { SetValue(InitialSortDirectionProperty, value); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomDataGrid"/> class.
        /// </summary>
        public CustomDataGrid()
        {
            _doubleClickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _doubleClickTimer.Tick += OnDoubleClickTimerTick;
            this.Loaded += CustomDataGrid_Loaded;
            this.Focusable = true;
            this.IsTabStop = true;
        }

        /// <summary>
        /// Handles the Loaded event of the CustomDataGrid control.
        /// </summary>
        private void CustomDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply initial sort
            if (this.InitialSortColumn != null)
            {
                this.Items.SortDescriptions.Add(new SortDescription(InitialSortColumn, InitialSortDirection));
            }
        }

        /// <summary>
        /// Handles the PreviewKeyDown event of the CustomDataGrid control.
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            _doubleClickTimer.Stop();
            
            // Handle Ctrl+A for select all
            if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SelectAllItems(true);
                e.Handled = true;
                return;
            }
            
            // Handle Del and Backspace for deselection
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if (SelectedItemsList != null && SelectedItemsList.Count > 0)
                {
                    // Create a new array to avoid collection modification issues
                    var itemsToDeselect = SelectedItemsList.Cast<object>().ToArray();
                    foreach (var item in itemsToDeselect)
                    {
                        var isSelectedProperty = TypeDescriptor.GetProperties(item).Find(IsSelectedPropertyName, false);
                        if (isSelectedProperty != null)
                        {
                            isSelectedProperty.SetValue(item, false);
                            UpdateSelectedItemsList(item, false);
                        }
                    }
                    e.Handled = true;
                    return;
                }
            }
            
            if (e.Key == Key.Enter && SelectedItem != null)
            {
                OnItemDoubleClick(SelectedItem);
                e.Handled = true;
            }
            else
            {
                base.OnPreviewKeyDown(e);
            }
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event of the CustomDataGrid control.
        /// </summary>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            var originalSource = (DependencyObject)e.OriginalSource;
            var cell = FindParent<DataGridCell>(originalSource);

            _isMouseOverButtonOrCheckbox = FindParent<ButtonBase>(originalSource) != null;

            if (cell == null)
            {
                base.OnPreviewMouseLeftButtonDown(e);
                return;
            }

            if (cell.DataContext is object clickedItem)
            {
                _clickedItem = clickedItem;

                // Handle "Select" column
                if (IsSelectColumn(cell))
                {
                    HandleSelectColumnClick(cell, e);
                    return;
                }

                int currentIndex = Items.IndexOf(clickedItem);

                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && _lastSelectedIndex != -1)
                {
                    // Shift-click selection
                    SelectRange(_lastSelectedIndex, currentIndex);
                    SelectedItem = clickedItem;
                    e.Handled = true;
                }
                else
                {
                    // Normal selection handling
                    if (DisableDoubleClick)
                    {
                        OnItemSingleClick(clickedItem);
                    }
                    else if (_isMouseOverButtonOrCheckbox)
                    {
                        _isSingleClick = false;
                        _doubleClickTimer.Stop();
                    }
                    else if (!_doubleClickTimer.IsEnabled)
                    {
                        _doubleClickTimer.Start();
                        _isSingleClick = true;
                    }
                    else if (_doubleClickTimer.IsEnabled)
                    {
                        _isSingleClick = false;
                        _doubleClickTimer.Stop();
                        OnMouseDoubleClick(e);
                        e.Handled = true;
                        return;
                    }

                    _lastSelectedIndex = currentIndex;
                    _lastSelectedItem = clickedItem;
                    SelectedItem = clickedItem;
                }
            }
            else
            {
                _clickedItem = null;
            }

            base.OnPreviewMouseLeftButtonDown(e);
        }

        /// <summary>
        /// Handles the click event for the select column.
        /// </summary>
        private void HandleSelectColumnClick(DataGridCell cell, MouseButtonEventArgs e)
        {
            if (ExcludeSelectColumn && cell.DataContext is object clickedItem)
            {
                _clickedItem = clickedItem;
                SelectedItem = clickedItem;

                // Toggle the selection property
                var isSelectedProperty = TypeDescriptor.GetProperties(clickedItem).Find(IsSelectedPropertyName, false);
                if (isSelectedProperty != null)
                {
                    bool isSelected = (bool)isSelectedProperty.GetValue(clickedItem);
                    isSelectedProperty.SetValue(clickedItem, !isSelected);

                    UpdateSelectedItemsList(clickedItem, !isSelected);
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// Determines whether the specified cell is in the select column.
        /// </summary>
        private bool IsSelectColumn(DataGridCell cell)
        {
            // Check if it's a template column and it's the first column when ExcludeSelectColumn is true
            return ExcludeSelectColumn && cell.Column is DataGridTemplateColumn &&
                   cell.Column == this.Columns[0];
        }

        /// <summary>
        /// Handles the MouseDoubleClick event of the CustomDataGrid control.
        /// </summary>
        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            _isSingleClick = false;
            _doubleClickTimer.Stop();

            var originalSource = (DependencyObject)e.OriginalSource;
            var cell = FindParent<DataGridCell>(originalSource);

            // Ignore double-clicks on Buttons or Checkboxes
            if (FindParent<ButtonBase>(originalSource) != null)
            {
                e.Handled = true;
                return;
            }

            if (cell != null && cell.DataContext is not null && _clickedItem != null && _clickedItem == cell.DataContext)
            {
                OnItemDoubleClick(_clickedItem);
                e.Handled = true;
            }

            base.OnMouseDoubleClick(e);
        }

        /// <summary>
        /// Handles the tick event of the double-click timer.
        /// </summary>
        private void OnDoubleClickTimerTick(object sender, EventArgs e)
        {
            _doubleClickTimer.Stop();
            if (!_isSingleClick) return;
            _isSingleClick = false;

            OnItemSingleClick(_clickedItem);
        }

        /// <summary>
        /// Raises the ItemSingleClicked event.
        /// </summary>
        protected virtual void OnItemSingleClick(object item)
        {
            if (item != null)
            {
                // Handle selection
                var isSelectedProperty = TypeDescriptor.GetProperties(item).Find(IsSelectedPropertyName, false);
                if (isSelectedProperty != null)
                {
                    bool isSelected = (bool)isSelectedProperty.GetValue(item);
                    isSelectedProperty.SetValue(item, !isSelected); // Toggle

                    UpdateSelectedItemsList(item, !isSelected);
                }

                SelectedItem = item;
            }
        }

        /// <summary>
        /// Updates the list of selected items.
        /// </summary>
        private void UpdateSelectedItemsList(object item, bool isSelected)
        {
            if (SelectedItemsList != null)
            {
                if (isSelected && !SelectedItemsList.Contains(item))
                {
                    SelectedItemsList.Add(item);
                }
                else if (!isSelected && SelectedItemsList.Contains(item))
                {
                    SelectedItemsList.Remove(item);
                }
            }
        }

        /// <summary>
        /// Raises the ItemDoubleClicked event.
        /// </summary>
        protected virtual void OnItemDoubleClick(object item)
        {
            if (item != null)
            {
                ItemDoubleClicked?.Invoke(this, item);
            }
        }

        /// <summary>
        /// Handles the MouseEnter event of the CustomDataGrid control.
        /// </summary>
        protected override void OnMouseEnter(MouseEventArgs e)
        {
            var originalSource = (DependencyObject)e.OriginalSource;
            _isMouseOverButtonOrCheckbox = (originalSource is Button || originalSource is CheckBox || FindParent<ButtonBase>(originalSource) != null);
            base.OnMouseEnter(e);
        }

        /// <summary>
        /// Handles the MouseLeave event of the CustomDataGrid control.
        /// </summary>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            var originalSource = (DependencyObject)e.OriginalSource;
            if (originalSource is Button || originalSource is CheckBox || FindParent<ButtonBase>(originalSource) != null)
            {
                _isMouseOverButtonOrCheckbox = false;
            }
            base.OnMouseLeave(e);
        }

        /// <summary>
        /// Finds the parent of a specified type for a given child element.
        /// </summary>
        /// <typeparam name="T">The type of the parent to find.</typeparam>
        /// <param name="child">The child element.</param>
        /// <returns>The parent element of the specified type, or null if not found.</returns>
        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            if (parentObject is T parent)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        /// <summary>
        /// Dependency property to disable double-click handling.
        /// </summary>
        public static readonly DependencyProperty DisableDoubleClickProperty =
            DependencyProperty.Register("DisableDoubleClick", typeof(bool), typeof(CustomDataGrid), new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets a value indicating whether double-click handling is disabled.
        /// </summary>
        public bool DisableDoubleClick
        {
            get { return (bool)GetValue(DisableDoubleClickProperty); }
            set { SetValue(DisableDoubleClickProperty, value); }
        }

        public static readonly DependencyProperty HideSelectionHighlightProperty =
            DependencyProperty.Register("HideSelectionHighlight", typeof(bool), typeof(CustomDataGrid),
            new PropertyMetadata(false));

        public bool HideSelectionHighlight
        {
            get { return (bool)GetValue(HideSelectionHighlightProperty); }
            set { SetValue(HideSelectionHighlightProperty, value); }
        }

        private void CustomDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (HideSelectionHighlight)
            {
                e.Row.Style = new Style(typeof(DataGridRow))
                {
                    Setters = {
                        new Setter(DataGridRow.BackgroundProperty, SystemColors.WindowBrushKey),
                        new Setter(DataGridRow.BorderBrushProperty, SystemColors.WindowBrushKey)
                    }
                };
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_isMouseOverButtonOrCheckbox)
            {
                e.Handled = true;
                return;
            }

            var cell = GetCellFromMouseEvent(e);
            if (cell?.DataContext != null)
            {
                _clickedItem = cell.DataContext;
                _isSingleClick = true;
                _doubleClickTimer.Start();
            }

            base.OnMouseLeftButtonDown(e);
        }

        private DataGridCell GetCellFromMouseEvent(MouseEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this));
            var cell = hit?.VisualHit as DataGridCell;
            return cell;
        }

        private void SelectAllItems(bool select)
        {
            foreach (var item in Items)
            {
                var isSelectedProperty = TypeDescriptor.GetProperties(item).Find(IsSelectedPropertyName, false);
                if (isSelectedProperty != null)
                {
                    isSelectedProperty.SetValue(item, select);
                    UpdateSelectedItemsList(item, select);
                }
            }
        }

        private void SelectRange(int startIndex, int endIndex)
        {
            int min = Math.Min(startIndex, endIndex);
            int max = Math.Max(startIndex, endIndex);

            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                var isSelectedProperty = TypeDescriptor.GetProperties(item).Find(IsSelectedPropertyName, false);
                if (isSelectedProperty != null)
                {
                    bool shouldBeSelected = i >= min && i <= max;
                    isSelectedProperty.SetValue(item, shouldBeSelected);
                    UpdateSelectedItemsList(item, shouldBeSelected);
                }
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            this.Focus(); // Ensure the grid itself can receive key events
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            
            // Always try to focus the grid when clicked anywhere
            if (!IsFocused)
            {
                Focus();
                e.Handled = true;
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            // Focus the grid when clicking on empty areas
            if (e.OriginalSource is ScrollViewer || e.OriginalSource is Grid)
            {
                Focus();
            }
            
            base.OnMouseDown(e);
        }
    }
}