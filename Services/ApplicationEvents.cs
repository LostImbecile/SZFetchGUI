using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace SZExtractorGUI.Services
{
    public class ServerStateChangedEventArgs : EventArgs
    {
        public ServerStateChangedEventArgs(string message)
        {
            Message = message ?? "Operation in progress...";
        }

        public string Message { get; }
    }

    public interface IApplicationEvents
    {
        void UpdateStatus(string message);
        event EventHandler<string> StatusChanged;
    }

    public class ApplicationEvents : IApplicationEvents
    {
        private readonly Dispatcher _dispatcher;

        public event EventHandler<string> StatusChanged;

        public ApplicationEvents()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public void UpdateStatus(string message)
        {
            if (_dispatcher.CheckAccess())
            {
                StatusChanged?.Invoke(this, message);
            }
            else
            {
                _dispatcher.InvokeAsync(() => StatusChanged?.Invoke(this, message));
            }
        }
    }
}
