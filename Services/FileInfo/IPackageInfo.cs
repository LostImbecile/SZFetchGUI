namespace SZExtractorGUI.Services.FileInfo
{
    public interface IPackageInfo
    {
        string GetCharacterIdFromPath(string path);
        string GetCharacterNameFromPath(string filePath, string displayLanguage = "en");
        string getFileType(string characterId, string contentType);
        bool IsMod(string path);
    }
}
