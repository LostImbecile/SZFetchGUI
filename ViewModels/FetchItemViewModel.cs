using System;
using System.Diagnostics;
using SZExtractorGUI.Mvvm;
using SZExtractorGUI.Services.FileInfo;

namespace SZExtractorGUI.Viewmodels
{
    public class FetchItemViewModel : BindableBase
    {
        private readonly IPackageInfo _packageInfo;
        private bool _isSelected;
        private string _characterName;
        private string _characterId;
        private string _type;
        private string _container;
        private bool _isMod;
        private string _contentPath;
        private bool _extractionFailed;
        private string _currentDisplayLanguage;
        private readonly object _lockObject = new object();

        public FetchItemViewModel(IPackageInfo packageInfo)
        {
            _packageInfo = packageInfo ?? throw new ArgumentNullException(nameof(packageInfo));
        }

        public FetchItemViewModel(IPackageInfo packageInfo, string filePath, string container, string contentType = null, string displayLanguage = "en")
            : this(packageInfo)
        {
            ContentPath = filePath;
            Container = container;
            Type = contentType;
            CharacterId = _packageInfo.GetCharacterIdFromPath(filePath);
            IsMod = _packageInfo.IsMod(filePath);
            
            // Initialize with proper character name
            UpdateCharacterName(displayLanguage);
        }

        public bool ExtractionFailed
        {
            get => _extractionFailed;
            set => SetProperty(ref _extractionFailed, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string CharacterName
        {
            get => _characterName;
            private set
            {
                if (SetProperty(ref _characterName, value))
                {
                    // Only notify DisplayName change if it actually changed
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        // DisplayName is now just a proxy to CharacterName
        public string DisplayName => CharacterName;

        public string CharacterId
        {
            get => _characterId;
            private set => SetProperty(ref _characterId, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string Container
        {
            get => _container;
            set => SetProperty(ref _container, value);
        }

        public bool IsMod
        {
            get => _isMod;
            private set => SetProperty(ref _isMod, value);
        }

        public string ContentPath
        {
            get => _contentPath;
            private set => SetProperty(ref _contentPath, value);
        }

        public void UpdateCharacterName(string displayLanguage)
        {
            if (string.IsNullOrEmpty(displayLanguage) || string.IsNullOrEmpty(CharacterId))
            {
                Debug.WriteLine($"[UpdateCharacterName] Invalid update request - Lang: {displayLanguage}, ID: {CharacterId}");
                return;
            }

            lock (_lockObject)
            {
                if (_currentDisplayLanguage == displayLanguage && !string.IsNullOrEmpty(CharacterName))
                {
                    Debug.WriteLine($"[UpdateCharacterName] Already using language {displayLanguage} for {CharacterId}");
                    return;
                }

                try
                {
                    var newName = _packageInfo.GetCharacterNameFromPath(CharacterId, displayLanguage);
                    Debug.WriteLine($"[UpdateCharacterName] Updating {CharacterId} from '{CharacterName}' to '{newName}' using {displayLanguage}");

                    _currentDisplayLanguage = displayLanguage;
                    CharacterName = newName; // This will trigger both CharacterName and DisplayName property changes
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UpdateCharacterName] Error updating name: {ex.Message}");
                    // Fallback to character ID if name update fails
                    CharacterName = CharacterId;
                }
            }
        }
    }
}
