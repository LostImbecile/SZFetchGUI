using MahApps.Metro.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SZExtractorGUI.Views;

namespace SZExtractorGUI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    private readonly FetchPage _fetchPage;

    public MainWindow(FetchPage fetchPage)
    {
        InitializeComponent();
        _fetchPage = fetchPage;
        MainFrame.Navigate(_fetchPage);
    }
}
