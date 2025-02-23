using SZExtractorGUI.Models;
using SZExtractorGUI.ViewModels;

namespace SZExtractorGUI.Services
{
    public class ItemFilterService : IItemFilterService
    {
        public bool FilterItem(FetchItemViewModel item, FilterParameters parameters)
        {
            if (item == null) return false;

            // Apply basic filters
            if (parameters.ShowModsOnly && !item.IsMod)
                return false;

            if (parameters.ShowGameFilesOnly && item.IsMod)
                return false;

            if (!string.IsNullOrWhiteSpace(parameters.SearchText))
            {
                var search = parameters.SearchText.Trim().ToUpperInvariant();
                if (!(item.CharacterName?.ToUpperInvariant().Contains(search) == true ||
                      item.CharacterId?.ToUpperInvariant().Contains(search) == true ||
                      item.Container?.ToUpperInvariant().Contains(search) == true))
                {
                    return false;
                }
            }

            // Apply language filter
            if (parameters.LanguageOption != LanguageOption.All && !string.IsNullOrEmpty(item.CharacterId))
            {
                var id = item.CharacterId.ToUpperInvariant();
                
                switch (parameters.LanguageOption)
                {
                    case LanguageOption.Japanese:
                        // For Japanese, exclude items that end with _EN or _US
                        if (id.EndsWith("_EN") || id.EndsWith("_US"))
                            return false;
                        break;
                        
                    case LanguageOption.English:
                        // For English, exclude items that end with _JP
                        if (id.EndsWith("_JP"))
                            return false;
                        break;
                }
            }

            return true;
        }
    }
}
