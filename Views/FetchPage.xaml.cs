using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using SZExtractorGUI.Viewmodels;
using SZExtractorGUI.ViewModels;

namespace SZExtractorGUI.Views
{
    /// <summary>
    /// Interaction logic for FetchPage.xaml
    /// </summary>
    public partial class FetchPage : Page
    {
        public FetchPage(FetchPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void SelectedItemsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                var listView = (ListView)sender;
                var selectedItems = listView.SelectedItems.Cast<FetchItemViewModel>().ToList();
                foreach (var item in selectedItems)
                {
                    item.IsSelected = false;
                }
            }
        }
    }
}
