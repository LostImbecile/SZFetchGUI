using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using LocresLib;
using SZExtractorGUI.Services.State;
using SZExtractorGUI.Utilities;

namespace SZExtractorGUI.Services.Localization
{
    public class CharacterNameManager : ICharacterNameManager
    {
        private readonly Dictionary<string, Dictionary<string, string>> _characterNames = new(StringComparer.Ordinal); // Case-sensitive language lookup
        private readonly Dictionary<string, string> _loadedLocresFiles = new(StringComparer.Ordinal);
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly object _syncLock = new object();  // Add lock for thread safety

        public CharacterNameManager(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
            Debug.WriteLine("[Localization] Character name manager service created");
        }

        public async Task LoadLocresFile(string language, string filePath)
        {
            if (string.IsNullOrEmpty(language))
            {
                Debug.WriteLine("[Localization] Empty language code provided");
                throw new ArgumentException("Language code cannot be empty", nameof(language));
            }

            Debug.WriteLine($"[Localization] Attempting to load language: {language} from file: {filePath}");

            if (!LanguageCodeValidator.IsValidLanguageCode(language))
            {
                Debug.WriteLine($"[Localization] Invalid language code: {language}");
                throw new ArgumentException($"Invalid language code: {language}", nameof(language));
            }

            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"[Localization] File not found: {filePath}");
                _errorHandlingService.HandleError($"Locres file not found for {language}", $"File not found: {filePath}");
                return;
            }

            lock (_syncLock)
            {
                if (_loadedLocresFiles.ContainsKey(language))
                {
                    Debug.WriteLine($"[Localization] Language {language} already loaded");
                    return;
                }

                try
                {
                    _loadedLocresFiles[language] = filePath;
                    _characterNames[language] = new Dictionary<string, string>(StringComparer.Ordinal);

                    using var stream = File.OpenRead(filePath);
                    var locres = new LocresFile();
                    locres.Load(stream);

                    var entriesCount = 0;
                    foreach (var locresNamespace in locres)
                    {
                        foreach (var entry in locresNamespace)
                        {
                            if (entry.Key.StartsWith("ST_CHR_NAME_FULL_", StringComparison.Ordinal))
                            {
                                var characterId = entry.Key.Substring("ST_CHR_NAME_FULL_".Length);
                                _characterNames[language][characterId] = entry.Value;
                                entriesCount++;
                            }
                        }
                    }
                    Debug.WriteLine($"[Localization] Successfully loaded {entriesCount} character names for {language}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Localization] Failed to load locres file {filePath}: {ex.Message}");
                    _loadedLocresFiles.Remove(language);
                    _characterNames.Remove(language);
                    _errorHandlingService.HandleError($"Failed to load character names for {language}", ex);
                    throw;
                }
            }
        }

        public string GetCharacterName(string characterId, string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                Debug.WriteLine("[Localization] Empty language code in GetCharacterName");
                return characterId;
            }

            if (!LanguageCodeValidator.IsValidLanguageCode(language))
            {
                Debug.WriteLine($"[Localization] Invalid language code requested: {language}");
                return characterId;
            }

            lock (_syncLock)
            {
                if (_characterNames.TryGetValue(language, out var names) &&
                    names.TryGetValue(characterId, out var name))
                {
                    return name;
                }
            }
            
            Debug.WriteLine($"[Localization] Character name not found for {characterId} in {language}");
            return characterId;
        }

        public bool IsLocresLoaded(string language)
        {
            if (string.IsNullOrEmpty(language)) return false;
            
            lock (_syncLock)
            {
                return _loadedLocresFiles.ContainsKey(language);
            }
        }

        public void ClearCache()
        {
            lock (_syncLock)
            {
                _characterNames.Clear();
                _loadedLocresFiles.Clear();
                Debug.WriteLine("[Localization] Character name cache cleared");
            }
        }
    }
}
