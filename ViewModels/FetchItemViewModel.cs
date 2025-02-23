using SZExtractorGUI.Mvvm;
using SZExtractorGUI.Services;
using System.IO;

namespace SZExtractorGUI.ViewModels
{
    public class FetchItemViewModel : BindableBase
    {
        private bool _isSelected;
        private string _characterName;
        private string _characterId;
        private string _type;
        private string _container;
        private bool _isMod;
        private string _contentPath;

        public FetchItemViewModel(string filePath, string container, string contentType = null)
        {
            ContentPath = filePath;
            
            Container = container;
            IsMod = PackageInfo.IsMod(container);
            
            CharacterId = PackageInfo.GetCharacterIdFromPath(filePath);
            CharacterName = PackageInfo.GetCharacterName(CharacterId);

            Type = contentType ?? Path.GetExtension(filePath).TrimStart('.');
        }

        // For ListView only
        public string DisplayName => IsMod ? 
            $"[MOD] {CharacterName} ({CharacterId})" : 
            $"{CharacterName} ({CharacterId})";

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // For DataGrid
        public string CharacterName
        {
            get => _characterName;
            set => SetProperty(ref _characterName, value);
        }

        public string CharacterId
        {
            get => _characterId;
            set => SetProperty(ref _characterId, value);
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
            set => SetProperty(ref _isMod, value);
        }

        public string ContentPath
        {
            get => _contentPath;
            set => SetProperty(ref _contentPath, value);
        }
    }
}
