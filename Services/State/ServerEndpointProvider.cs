using System;
using System;
using SZExtractorGUI.Models;

namespace SZExtractorGUI.Services.State
{
    public interface IServerEndpointProvider
    {
        string BaseUrl { get; }
        int Port { get; }
        void SetPort(int port);
        Uri BuildUri(string relativePath = "");
        event Action<string>? EndpointChanged;
    }

    public class ServerEndpointProvider : IServerEndpointProvider
    {
        private readonly Settings _settings;
        private string _baseUrl;

        public event Action<string>? EndpointChanged;

        public ServerEndpointProvider(Settings settings)
        {
            _settings = settings;
            _baseUrl = settings.ServerBaseUrl;
        }

        public string BaseUrl => _baseUrl;

        public int Port => _settings.ServerPort;

        public void SetPort(int port)
        {
            if (port == _settings.ServerPort)
                return;

            _settings.ServerPort = port;
            var old = _baseUrl;
            _baseUrl = _settings.ServerBaseUrl;
            if (!string.Equals(old, _baseUrl, StringComparison.OrdinalIgnoreCase))
            {
                EndpointChanged?.Invoke(_baseUrl);
            }
        }

        public Uri BuildUri(string relativePath = "")
        {
            var baseUri = new Uri(_baseUrl, UriKind.Absolute);
            if (string.IsNullOrWhiteSpace(relativePath))
                return baseUri;
            return new Uri(baseUri, relativePath);
        }
    }
}
