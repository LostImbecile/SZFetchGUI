using System;
using System.Diagnostics;
using SZExtractorGUI.Mvvm;
using SZExtractorGUI.Services.FileInfo;

namespace SZExtractorGUI.Viewmodels
{
    public class FetchItemViewModel(IPackageInfo packageInfo) : BindableBase
    {
        private readonly IPackageInfo _packageInfo = packageInfo ?? throw new ArgumentNullException(nameof(packageInfo));
        private bool _isSelected;
        private string _characterName;
        private string _characterId;
        private string _type;
        private string _container;
        private bool _isMod;
        private string _contentPath;
        private bool _extractionFailed;
        private readonly object _lockObject = new();

        public FetchItemViewModel(IPackageInfo packageInfo, string filePath, string container, string contentType = null, string displayLanguage = "en")
            : this(packageInfo)
        {
            ContentPath = filePath;
            Container = container;
           
            CharacterId = _packageInfo.GetCharacterIdFromPath(filePath);
            Type = _packageInfo.getFileType(CharacterId, contentType);
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

        // Update UpdateCharacterName method to handle 'all' language case
        public void UpdateCharacterName(string displayLanguage)
        {
            if (string.IsNullOrEmpty(displayLanguage))
                return;

            lock (_lockObject)
            {
                // Get localized name from package info
                CharacterName = _packageInfo.GetCharacterNameFromPath(ContentPath, displayLanguage);
                
                // Fallback to ID if name is empty or null
                if (string.IsNullOrWhiteSpace(CharacterName))
                {
                    CharacterName = CharacterId;
                }
            }
        }
    }
}
