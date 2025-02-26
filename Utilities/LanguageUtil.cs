using SZExtractorGUI.Models;

namespace SZExtractorGUI.Utilities
{
    public static class LanguageUtil
    {
        public static bool IsEnglishContent(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            
            id = id.ToUpperInvariant();
            return id.EndsWith("_EN") || id.EndsWith("_US") 
                || id.Contains("_EN_") || id.Contains("_US_");
        }

        public static bool IsJapaneseContent(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            
            id = id.ToUpperInvariant();
            return id.EndsWith("_JP") || id.Contains("_JP_");
        }

        public static bool MatchesLanguage(string id, LanguageOption language) => language switch
        {
            LanguageOption.All => true,
            LanguageOption.English => !IsJapaneseContent(id),
            LanguageOption.Japanese => !IsEnglishContent(id),
            _ => true
        };
    }
}
