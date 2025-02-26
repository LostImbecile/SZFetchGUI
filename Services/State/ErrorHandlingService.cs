using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace SZExtractorGUI.Services.State
{
    public interface IErrorHandlingService
    {
        void HandleError(string message, Exception ex);
        void HandleError(string message, string details);
        string LastError { get; }
        event EventHandler<string> ErrorOccurred;
        void ClearError();
    }

    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly Dispatcher _dispatcher;
        private string _lastError;
        public event EventHandler<string> ErrorOccurred;

        public ErrorHandlingService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public string LastError => _lastError;

        private void InvokeOnUiThread(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.Invoke(action);
            }
        }

        public void HandleError(string message, Exception ex)
        {
            var errorMessage = $"{message}: {ex.Message}";
            Debug.WriteLine($"[Error] {errorMessage}");
            Debug.WriteLine($"[Error] Stack Trace: {ex.StackTrace}");

            InvokeOnUiThread(() =>
            {
                _lastError = errorMessage;
                ErrorOccurred?.Invoke(this, errorMessage);
            });
        }

        public void HandleError(string message, string details)
        {
            var errorMessage = $"{message}: {details}";
            Debug.WriteLine($"[Error] {errorMessage}");

            InvokeOnUiThread(() =>
            {
                _lastError = errorMessage;
                ErrorOccurred?.Invoke(this, errorMessage);
            });
        }
        
        public void ClearError()
        {
            if (_lastError != null)
            {
                Debug.WriteLine("[Error] Clearing error state");
                InvokeOnUiThread(() =>
                {
                    _lastError = null;
                    ErrorOccurred?.Invoke(this, null);
                });
            }
        }
    }
}
