using System.Windows.Media.TextFormatting;

using SZExtractorGUI.Models;
using SZExtractorGUI.Viewmodels;
using SZExtractorGUI.ViewModels;

namespace SZExtractorGUI.Services.FileInfo
{
    public interface IItemFilterService
    {
        bool FilterItem(FetchItemViewModel item, FilterParameters parameters);
    }

    public class FilterParameters
    {
        public string SearchText { get; set; }
        public bool ShowModsOnly { get; set; }
        public bool ShowGameFilesOnly { get; set; }
        public LanguageOption LanguageOption { get; set; }
        public ContentType ContentType { get; set; }  // Add ContentType to parameters
        public string CurrentTextLanguage { get; set; }  // Add current text language
    }
}
