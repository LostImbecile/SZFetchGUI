using System;
using System.Threading;
using System.Threading.Tasks;

namespace SZExtractorGUI.Services
{
    public interface IBackgroundOperationsService
    {
        Task ExecuteOperationAsync(Func<Task> operation);
        Task<T> ExecuteOperationAsync<T>(Func<Task<T>> operation);
        bool HasActiveOperations { get; }
        event EventHandler<bool> OperationStatusChanged;
    }

    public class BackgroundOperationsService : IBackgroundOperationsService
    {
        private const int MaxRetries = 3;
        private const int BaseDelayMs = 500;
        private int _activeOperations;
        public event EventHandler<bool> OperationStatusChanged;

        public async Task ExecuteOperationAsync(Func<Task> operation)
        {
            if (operation == null) return;

            try
            {
                Interlocked.Increment(ref _activeOperations);
                OperationStatusChanged?.Invoke(this, true);

                // Add small delay to ensure UI updates
                await Task.Delay(50);

                await operation();
            }
            finally
            {
                Interlocked.Decrement(ref _activeOperations);
                if (_activeOperations == 0)
                {
                    OperationStatusChanged?.Invoke(this, false);
                }
            }
        }

        public async Task<T> ExecuteOperationAsync<T>(Func<Task<T>> operation)
        {
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    Interlocked.Increment(ref _activeOperations);
                    OperationStatusChanged?.Invoke(this, true);

                    return await Task.Run(operation);
                }
                catch (Exception) when (attempt < MaxRetries)
                {
                    await Task.Delay(BaseDelayMs * (attempt + 1));
                }
                finally
                {
                    Interlocked.Decrement(ref _activeOperations);
                    if (_activeOperations == 0)
                    {
                        OperationStatusChanged?.Invoke(this, false);
                    }
                }
            }

            return default;
        }

        public bool HasActiveOperations => _activeOperations > 0;
    }
}
