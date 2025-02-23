using SZExtractorGUI.Models;
using SZExtractorGUI.ViewModels;

namespace SZExtractorGUI.Services
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
    }
}
