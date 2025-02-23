using System.Collections.ObjectModel;
using SZExtractorGUI.Models;

namespace SZExtractorGUI.Services
{
    public interface IContentTypeService
    {
        ObservableCollection<ContentType> GetContentTypes();
        ContentType GetDefaultContentType();
    }
}
