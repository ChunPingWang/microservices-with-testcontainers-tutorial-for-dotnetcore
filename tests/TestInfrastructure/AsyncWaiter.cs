using Polly;
using Polly.Retry;

namespace TestInfrastructure;

/// <summary>
/// Java's Awaitility, in spirit. Wait until a predicate returns true (or async equivalent),
/// otherwise throw.
/// </summary>
public static class AsyncWaiter
{
    public static async Task WaitUntilAsync(
        Func<CancellationToken, Task<bool>> condition,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        string? message = null,
        CancellationToken ct = default)
    {
        timeout ??= TimeSpan.FromSeconds(15);
        interval ??= TimeSpan.FromMilliseconds(200);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout.Value);

        var policy = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = int.MaxValue,
                Delay = interval.Value,
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = new PredicateBuilder().Handle<Exception>()
            })
            .Build();

        try
        {
            await policy.ExecuteAsync(async token =>
            {
                if (!await condition(token).ConfigureAwait(false))
                    throw new InvalidOperationException("Condition not yet met.");
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(message ?? "Condition was not met within timeout.");
        }
    }
}
