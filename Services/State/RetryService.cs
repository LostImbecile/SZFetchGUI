using System;
using System.Threading.Tasks;

namespace SZExtractorGUI.Services.State
{
    public interface IRetryService
    {
        Task ExecuteWithRetryAsync(Func<Task> operation);
        Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation);
    }

    public class RetryService : IRetryService
    {
        private const int MaxRetries = 3;
        private const int BaseDelayMs = 500;

        public async Task ExecuteWithRetryAsync(Func<Task> operation)
        {
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await operation();
                    return;
                }
                catch (Exception) when (attempt < MaxRetries)
                {
                    await Task.Delay(BaseDelayMs * (attempt + 1));
                }
            }
        }

        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
        {
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception) when (attempt < MaxRetries)
                {
                    await Task.Delay(BaseDelayMs * (attempt + 1));
                }
            }
            throw new TimeoutException("Operation failed after maximum retries");
        }
    }
}
