using SZExtractorGUI.Models;
using SZExtractorGUI.ViewModels;
using SZExtractorGUI.Utilities;
using SZExtractorGUI.Services.Localization;
using System.Diagnostics;
using SZExtractorGUI.Viewmodels;

namespace SZExtractorGUI.Services.FileInfo
{
    public class ItemFilterService : IItemFilterService
    {
        private readonly ICharacterNameManager _characterNameManager;
        private readonly Settings _settings;

        public ItemFilterService(ICharacterNameManager characterNameManager, Settings settings)
        {
            _characterNameManager = characterNameManager;
            _settings = settings;
        }

        public bool FilterItem(FetchItemViewModel item, FilterParameters parameters)
        {
            if (item == null || parameters == null)
                return false;

            // Apply base filters first
            if (!FilterByMods(item, parameters.ShowModsOnly, parameters.ShowGameFilesOnly))
                return false;

            if (!FilterBySearchText(item, parameters.SearchText))
                return false;

            // Always apply language content filtering using LanguageUtil
            if (!FilterByLanguage(item, parameters.LanguageOption))
                return false;

            // Apply text language filtering if current language is specified
            if (parameters.ContentType?.Name?.Contains("Text", StringComparison.OrdinalIgnoreCase) == true && !string.IsNullOrEmpty(parameters.CurrentTextLanguage) && parameters.CurrentTextLanguage != "all")
            {
                if (!FilterByTextLanguage(item, parameters.CurrentTextLanguage))
                    return false;
            }

            return true;
        }

        private bool FilterByMods(FetchItemViewModel item, bool showModsOnly, bool showGameFilesOnly)
        {
            if (showModsOnly && !item.IsMod)
                return false;
            if (showGameFilesOnly && item.IsMod)
                return false;
            return true;
        }

        private bool FilterBySearchText(FetchItemViewModel item, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            return item.CharacterName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   item.CharacterId.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   item.Container.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        private bool FilterByLanguage(FetchItemViewModel item, LanguageOption languageOption)
        {
            // Use LanguageUtil to check if the content matches the selected language option
            return LanguageUtil.MatchesLanguage(item.CharacterId, languageOption);
        }

        private bool FilterByTextLanguage(FetchItemViewModel item, string currentLanguage)
        {
            if (string.IsNullOrEmpty(item.CharacterName) || string.IsNullOrEmpty(currentLanguage))
                return true;

            Debug.WriteLine($"{currentLanguage}");
            // Check if the path contains the language code pattern "langcode -"
            return item.CharacterName.Contains($"{currentLanguage} -", StringComparison.OrdinalIgnoreCase);
        }
    }
}
