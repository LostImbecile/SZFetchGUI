using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;

namespace SZExtractorGUI.Services
{
    public static class PackageInfo
    {
        private static readonly Dictionary<string, int> _packageFileCounts = new();

        public static string GetCharacterIdFromPath(string path)
        {
            return 
                   Path.GetFileNameWithoutExtension(path).Split('.').FirstOrDefault() ?? string.Empty;
        }

        public static string GetCharacterName(string id)
        {
            if (string.IsNullOrEmpty(id))
                return string.Empty;

            return id;
        }

        public static bool IsMod(string path)
        {
            return path.Contains("_p", StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasUpdates(Dictionary<string, List<string>> newDump)
        {
            if (_packageFileCounts.Count != newDump.Count) return true;

            foreach (var (package, files) in newDump)
            {
                if (!_packageFileCounts.TryGetValue(package, out int currentCount) 
                    || currentCount != files.Count)
                {
                    return true;
                }
            }
            return false;
        }

        public static void UpdateFileCounts(Dictionary<string, List<string>> dump)
        {
            _packageFileCounts.Clear();
            foreach (var (package, files) in dump)
            {
                _packageFileCounts[package] = files.Count;
            }
        }
    }
}
