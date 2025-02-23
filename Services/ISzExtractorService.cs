using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SZExtractorGUI.Models;

namespace SZExtractorGUI.Services
{
    public interface ISzExtractorService
    {
        Task<ConfigureResponse> ConfigureAsync(ConfigureRequest request);
        Task<ExtractResponse> ExtractAsync(ExtractRequest request);
        Task<DumpResponse> DumpAsync(DumpRequest request);
        Task<Dictionary<string, List<string>>> GetDuplicatesAsync();
    }
}
