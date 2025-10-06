using System.Collections.ObjectModel;

using SZExtractorGUI.Models;

namespace SZExtractorGUI.Services.FileInfo
{
    public class ContentTypeService : IContentTypeService
    {
        private readonly ObservableCollection<ContentType> _contentTypes;

        public ContentTypeService()
        {
            _contentTypes = new ObservableCollection<ContentType>
            {
                 new ContentType(
                    name: "Characters",
                    filter: "\\BTLCV_.*\\.awb",
                    description: "Character Voice Files"
                ),
                new ContentType(
                    name: "BGM",
                    filter: "\\\\(bgm_main|bgm_DLC).*\\.awb",
                    description: "Background Music Files"
                ),
                new ContentType(
                    name: "Character Sound Effects",
                    filter: "\\BTLSE.*\\.awb",
                    description: "Character Specific Sound Effects"
                ), new ContentType(
                    name: "Misc",
                    filter: "\\\\(ADVIF|SHOP|VOICE|se)_.*\\.awb",
                    description: "Miscellaenous Sounds & Lines"
                ), new ContentType(
                    name: "Text",
                    filter: "\\.locres",
                    description: "Text Files"
                )

            };
        }

        public ObservableCollection<ContentType> GetContentTypes() => _contentTypes;

        public ContentType GetDefaultContentType() => _contentTypes.FirstOrDefault();
    }
}
