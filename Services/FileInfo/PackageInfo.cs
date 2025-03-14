using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using SZExtractorGUI.Utilities;
using SZExtractorGUI.Services.Localization;
using System.Diagnostics;
using System.Windows.Media.TextFormatting;
using SZExtractorGUI.ViewModels;

namespace SZExtractorGUI.Services.FileInfo
{
    public partial class PackageInfo(ICharacterNameManager characterNameManager) : IPackageInfo
    {
        private readonly ICharacterNameManager _characterNameManager = characterNameManager;

        public string GetCharacterIdFromPath(string path)
        {

            return Path.GetFileNameWithoutExtension(path).Split('.').FirstOrDefault()
                ?? string.Empty;
        }

        public string GetCharacterNameFromPath(string filePath, string displayLanguage = "en")
        {
            String id = GetCharacterIdFromPath(filePath);
            String name = "";
            if (string.IsNullOrEmpty(id))
                return string.Empty;

            if (!filePath.Contains("Localization", StringComparison.OrdinalIgnoreCase))
            {
                bool isCnk = false;

                if (id.Contains("_Cnk", StringComparison.OrdinalIgnoreCase))
                    isCnk = true;

                if (id.Contains("ADVIF"))
                    name = "Story & Scenes";
                else if (id.Contains("Gallery", StringComparison.OrdinalIgnoreCase))
                    name = "Gallery";
                else if (id.Contains("Shop", StringComparison.OrdinalIgnoreCase))
                    name = "Shop";
                else if (id.Contains("BGM", StringComparison.OrdinalIgnoreCase))
                    name = "Background Music";
                else if (id.Equals("se_Battle", StringComparison.OrdinalIgnoreCase))
                    name = "Battle";
                else if (id.Contains("se_UI", StringComparison.OrdinalIgnoreCase))
                    name = "UI";
                else if (_characterNameManager.IsLocresLoaded(displayLanguage))
                {
                    String strippedId = id.Replace("_JP", "").Replace("_US", "").Replace("_EN", "")
                        .Replace("BTLCV_", "").Replace("BTLSE_", "");
                    bool isAlt = false;
                    if (strippedId.EndsWith("_A"))
                    {
                        strippedId = strippedId.Substring(0, strippedId.Length - 2);
                        isAlt = true;
                    }
                    name = _characterNameManager.GetCharacterName(strippedId, displayLanguage);
                    if (string.IsNullOrEmpty(name))
                    {
                        Debug.WriteLine($"[Localization] Character name not found for ID: {strippedId}, using ID as name");
                        name = strippedId;
                    }
                    else
                        name = name.Replace("Super Saiyan God", "SSG").Replace("Super Saiyan", "SSJ").Replace("SSG SSJ", "SSB").Replace("Ultra Instinct", "UI");

                    // Add regex replacement for "SSJ [0-9]" to "SSJN"
                    name = SSJRegex().Replace(name, "SSJ$1");

                    if (isAlt)
                        name += " (Alt)";

                }
                else
                    name = id.Replace("_JP", "").Replace("_US", "").Replace("_EN", "")
                        .Replace("BTLCV_", "").Replace("BTLSE_", "");

                if (isCnk)
                    name += " (Secondary)";
            }
            else if (!String.IsNullOrEmpty(filePath))
            {
                String[] temp = filePath.Split("/");
                String language = "??";
                if (temp.Length > 3)
                    language = temp.ElementAtOrDefault(temp.Length - 2);
                if (id.Equals("Data", StringComparison.OrdinalIgnoreCase))
                {
                    name = "General Text";
                }
                else if (id.Equals("Voice", StringComparison.OrdinalIgnoreCase))
                {
                    name = "Subtitle Text";
                }
                else
                    name = id;

                name = language + " - " + name;
            }
            return name;
        }

        public string getFileType(string id, string contentType)
        {
            if (id.StartsWith("se_", StringComparison.OrdinalIgnoreCase) || id.StartsWith("BTLSE", StringComparison.OrdinalIgnoreCase))
                return "Sound Effect";
            if (LanguageUtil.IsJapaneseContent(id))
                return "(JP) Voice";
            if (LanguageUtil.IsEnglishContent(id))
                return "(EN) Voice";
            return contentType;
        }

        public bool IsMod(string container)
        {
            // Check for _P which is a common mod indicator
            if (container.Contains("_p", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check if it's a valid game container using regex
            // Valid formats: pakchunkN-Windows, pakchunkNoptional-Windows, or DLC_*
            var baseGameRegex = GameContainerRegex();
            var dlcRegex = DlcContainerRegex();

            // If it matches either pattern, it's a valid game container (not a mod)
            bool isValidGameContainer = baseGameRegex.IsMatch(container) || dlcRegex.IsMatch(container);

            // Return true (is mod) when it's not a valid game container
            return !isValidGameContainer;
        }

        [GeneratedRegex(@"pakchunk\d+(?:optional)?-Windows")]
        private static partial Regex GameContainerRegex();

        [GeneratedRegex(@"DLC_[A-Za-z0-9]+")]
        private static partial Regex DlcContainerRegex();

        [GeneratedRegex(@"SSJ (\d)")]
        private static partial Regex SSJRegex();
    }
}
