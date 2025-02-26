using System.Collections.ObjectModel;

using SZExtractorGUI.Models;

namespace SZExtractorGUI.Services.FileInfo
{
    public interface IContentTypeService
    {
        ObservableCollection<ContentType> GetContentTypes();
        ContentType GetDefaultContentType();
    }
}
