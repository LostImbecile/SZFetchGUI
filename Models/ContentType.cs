// Models/ContentType.cs
namespace SZExtractorGUI.Models
{
    public class ContentType
    {
        public string Name { get; set; }
        public string Filter { get; set; }
        public string Description { get; set; }

        public ContentType(string name, string filter = "", string description = null)
        {
            Name = name;
            Filter = filter;
            Description = description ?? name;
        }

        public override string ToString() => Name;
    }
}
