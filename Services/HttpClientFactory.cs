using System;
using System.Net.Http;

namespace SZExtractorGUI.Services
{
    public interface IHttpClientFactory
    {
        HttpClient CreateClient();
    }

    public class HttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient()
        {
            return new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        }
    }
}
