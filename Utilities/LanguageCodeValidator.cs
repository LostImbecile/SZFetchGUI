using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        public static string ExtractLanguageFromFileName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            // First try to extract from directory structure
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                // Check immediate parent directory
                var dirName = Path.GetFileName(directory);
                if (ValidLanguages.Contains(dirName))
                {
                    return dirName;
                }

                // Check grandparent directory
                var parentDirectory = Path.GetDirectoryName(directory);
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    var parentDirName = Path.GetFileName(parentDirectory);
                    if (ValidLanguages.Contains(parentDirName))
                    {
                        return parentDirName;
                    }
                }
            }
            Debug.WriteLine($"[LanguageCodeValidator] Unable to extract language code from directory hierarchy: {directory}");

            // Fallback: Check legacy format "languagecode - Data.locres"
            var fileName = Path.GetFileName(filePath);
            var parts = fileName.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length == 2 && parts[1] == "Data.locres")
            {
                var languageCode = parts[0];
                return ValidLanguages.Contains(languageCode) ? languageCode : "all";
            }

            return null;
        }
    }
}
