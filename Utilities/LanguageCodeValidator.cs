using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SZExtractorGUI.Utilities
{
    public static class LanguageCodeValidator
    {
        // Use exact mappings from FetchPageViewModel
        private static readonly HashSet<string> ValidLanguages = new(StringComparer.Ordinal)
        {
            "en", "ja", "zh-Hans", "zh-Hant", "ko", "th", "id", "ar",
            "pl", "es", "es-419", "ru", "de", "it", "fr", "pt-BR"
        };

        public static bool IsValidLanguageCode(string code)
        {
            return !string.IsNullOrEmpty(code) && ValidLanguages.Contains(code);
        }

        public static string ExtractLanguageFromFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            
            // Simple exact match: "languagecode - Data.locres"
            var parts = fileName.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length != 2 || parts[1] != "Data.locres") return null;
            
            var languageCode = parts[0];
            return ValidLanguages.Contains(languageCode) ? languageCode : null;
        }
    }
}
