using System.Threading.Tasks;

namespace SZExtractorGUI.Services.Localization
{
    public interface ICharacterNameManager
    {
        Task LoadLocresFile(string language, string filePath);
        string GetCharacterName(string characterId, string language);
        bool IsLocresLoaded(string language);
        void ClearCache();
    }
}
