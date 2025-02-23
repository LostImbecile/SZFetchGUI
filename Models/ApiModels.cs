using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace SZExtractorGUI.Models
{
    public class ConfigureRequest
    {
        [JsonPropertyName("gameDir")]
        public string GameDir { get; set; }

        [JsonPropertyName("engineVersion")]
        public string EngineVersion { get; set; }

        [JsonPropertyName("aesKey")]
        public string AesKey { get; set; }

        [JsonPropertyName("outputPath")]
        public string OutputPath { get; set; }
    }

    public class ConfigureResponse
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonPropertyName("Message")]
        public string Message { get; set; }

        [JsonPropertyName("MountedFiles")]
        public int MountedFiles { get; set; }
    }

    public class ExtractRequest
    {
        [JsonPropertyName("contentPath")]
        public string ContentPath { get; set; }

        [JsonPropertyName("outputPath")]
        public string OutputPath { get; set; }
        [JsonPropertyName("archiveName")]
        public string? ArchiveName { get; set; }

    }

    public class ExtractResponse
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonPropertyName("Message")]
        public string Message { get; set; }

        [JsonPropertyName("FilePaths")]
        public List<string> FilePaths { get; set; }
    }

    public class DumpRequest
    {
        [JsonPropertyName("filter")]
        public required string Filter { get; set; } 
    }

    public class DumpResponse
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonPropertyName("Message")]
        public string Message { get; set; }

        [JsonPropertyName("Files")]
        public Dictionary<string, List<string>> Files { get; set; } = new();
        [JsonPropertyName("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ServerStatusResponse
    {
        [JsonPropertyName("IsAvailable")]
        public bool IsAvailable { get; set; }

        [JsonPropertyName("Message")]
        public string Message { get; set; }

        [JsonPropertyName("MountedFiles")]
        public int? MountedFiles { get; set; }

        [JsonPropertyName("Version")]
        public string Version { get; set; }
    }
}
