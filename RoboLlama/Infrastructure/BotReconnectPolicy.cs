using Polly;
using Polly.Retry;

namespace RoboLlama.Infrastructure;

public class IrcConnectionPolicy
{
    private const int RetryCount = 20;
    private const int DelayBetweenRetriesInSeconds = 10;

    private readonly AsyncRetryPolicy retryPolicy;

    public IrcConnectionPolicy(int retries = -1, int delay = -1)
    {
        if (retries < 0) retries = RetryCount;
        if (delay < 0) delay = DelayBetweenRetriesInSeconds;
        retryPolicy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(retries, _ =>
                TimeSpan.FromSeconds(delay),
                (exception, timeSpan, retryCount, _) =>
                {
                    // This is an optional part where you can log the retries.
                    BotConsole.WriteSystemLine($"Retry {retryCount} encountered an error: {exception.Message}. Waiting {timeSpan} before next retry.");
                });
    }

    public async Task ConnectWithRetriesAsync(Func<Task> action)
    {
        await retryPolicy.ExecuteAsync(action);
    }
}