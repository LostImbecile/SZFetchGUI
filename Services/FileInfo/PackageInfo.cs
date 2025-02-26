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

namespace SZExtractorGUI.Services.FileInfo
{
    public class PackageInfo : IPackageInfo
    {
        private readonly ICharacterNameManager _characterNameManager;

        public PackageInfo(ICharacterNameManager characterNameManager)
        {
            _characterNameManager = characterNameManager;
        }

        public string GetCharacterIdFromPath(string path)
        {

            return Path.GetFileNameWithoutExtension(path).Split('.').FirstOrDefault()
                ?? string.Empty;
        }

        // ...existing code...
        public string GetCharacterNameFromPath(string filePath, string displayLanguage = "en")
        {
            String id = GetCharacterIdFromPath(filePath);
            String name = "";
            if (string.IsNullOrEmpty(id))
                return string.Empty;

            if (!filePath.Contains("Localization", StringComparison.OrdinalIgnoreCase))
            {
                bool isCnk = false;
                bool isSoundEffect = false;
                if (id.Contains("_Cnk", StringComparison.OrdinalIgnoreCase))
                    isCnk = true;
                if (id.StartsWith("se_", StringComparison.OrdinalIgnoreCase) || id.StartsWith("BTLSE", StringComparison.OrdinalIgnoreCase))
                    isSoundEffect = true;

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
                    name = _characterNameManager.GetCharacterName(strippedId, displayLanguage);
                    if (string.IsNullOrEmpty(name))
                    {
                        Debug.WriteLine($"[Localization] Character name not found for ID: {strippedId}, using ID as name");
                        name = strippedId;
                    }
                    else
                        name = name.Replace("Super Saiyan God", "SSG").Replace("Super Saiyan", "SSJ").Replace("SSG SSJ", "SSB");
                    
                }
                else
                    name = id;

                if (isCnk)
                    name += " (Secondary)";

                if (isSoundEffect)
                    name += " (Sound Effects)";

                if (LanguageUtil.IsJapaneseContent(id))
                    name = "(JP) " + name;
                else if (LanguageUtil.IsEnglishContent(id))
                    name = "(EN) " + name;
            }
            else if (!String.IsNullOrEmpty(filePath))
            {
                String[] temp = filePath.Split("/");
                String language = temp.ElementAtOrDefault(temp.Length - 2);
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

        public bool IsMod(string path)
        {
            return path.Contains("_p", StringComparison.OrdinalIgnoreCase);
        }
    }
}
