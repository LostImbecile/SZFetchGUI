using System;
using System.Windows.Threading;

using SZExtractorGUI.Mvvm;

namespace SZExtractorGUI.Services.State
{
    public class ApplicationState : BindableBase
    {
        private readonly Dispatcher _dispatcher;
        private readonly IRetryService _retryService;
        private bool _isInitialized;
        private string _statusMessage;
        private bool _isLoading;

        public bool IsInitialized 
        {
            get => _isInitialized;
            private set => SetProperty(ref _isInitialized, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public ApplicationState(IApplicationEvents applicationEvents, IRetryService retryService)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _retryService = retryService;
            
            // Subscribe to status change event
            applicationEvents.StatusChanged += (s, msg) => UpdateState(state => state.StatusMessage = msg);
        }

        public void UpdateState(Action<ApplicationState> updateAction)
        {
            if (_dispatcher.CheckAccess())
            {
                updateAction(this);
            }
            else
            {
                _dispatcher.Invoke(() => updateAction(this));
            }
        }

        public void SetInitialized(bool initialized)
        {
            UpdateState(state =>
            {
                state.IsInitialized = initialized;
                if (initialized)
                {
                    state.StatusMessage = "Initialized";
                }
            });
        }

        public void SetLoading(bool loading)
        {
            UpdateState(state => state.IsLoading = loading);
        }

        public void SetStatus(string message)
        {
            UpdateState(state => state.StatusMessage = message);
        }
    }
}
