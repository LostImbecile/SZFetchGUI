using System.Collections.ObjectModel;

using SZExtractorGUI.Models;

namespace SZExtractorGUI.Services
{
    public class ContentTypeService : IContentTypeService
    {
        private readonly ObservableCollection<ContentType> _contentTypes;

        public ContentTypeService()
        {
            _contentTypes = new ObservableCollection<ContentType>
            {
                new ContentType(
                    name: "BGM",
                    filter: "\\\\bgm_main.*\\.awb",
                    description: "Background Music Files"
                ),
                new ContentType(
                    name: "Character Sound Effects",
                    filter: "\\BTLSE.*\\.awb",
                    description: "Character Voice and Sound Effects"
                ),
                new ContentType(
                    name: "Game Sound Effects",
                    filter: "\\\\se_.*\\.awb",
                    description: "Game Sound Effects"
                ),
                new ContentType(
                    name: "Characters",
                    filter: "\\BTLCV_.*\\.awb",
                    description: "Character Assets"
                )
            };
        }

        public ObservableCollection<ContentType> GetContentTypes() => _contentTypes;

        public ContentType GetDefaultContentType() => _contentTypes.FirstOrDefault();
    }
}
