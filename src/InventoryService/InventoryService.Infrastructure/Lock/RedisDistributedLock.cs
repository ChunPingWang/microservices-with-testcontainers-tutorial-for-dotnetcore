using InventoryService.Domain.Ports;
using StackExchange.Redis;

namespace InventoryService.Infrastructure.Lock;

public sealed class RedisDistributedLock(IConnectionMultiplexer redis) : IDistributedLock
{
    private IDatabase Db => redis.GetDatabase();

    public async Task<IAsyncDisposable> AcquireAsync(string resource, TimeSpan expiry,
        TimeSpan acquireTimeout, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var key = $"lock:{resource}";
        var deadline = DateTime.UtcNow + acquireTimeout;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var taken = await Db.StringSetAsync(key, token, expiry, When.NotExists);
            if (taken) return new RedisLockHandle(Db, key, token);
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Lock acquire timed out for {resource}.");
            await Task.Delay(50, ct);
        }
    }

    private sealed class RedisLockHandle(IDatabase db, string key, string token) : IAsyncDisposable
    {
        private const string ReleaseScript = """
            if redis.call("get", KEYS[1]) == ARGV[1] then
                return redis.call("del", KEYS[1])
            else
                return 0
            end
        """;

        public async ValueTask DisposeAsync()
        {
            await db.ScriptEvaluateAsync(ReleaseScript,
                [key], [token]);
        }
    }
}

public sealed class SemaphoreDistributedLock : IDistributedLock
{
    private readonly Dictionary<string, SemaphoreSlim> _gates = new();
    private readonly System.Threading.Lock _gate = new();

    public async Task<IAsyncDisposable> AcquireAsync(string resource, TimeSpan expiry,
        TimeSpan acquireTimeout, CancellationToken ct = default)
    {
        SemaphoreSlim sem;
        lock (_gate)
        {
            if (!_gates.TryGetValue(resource, out sem!))
                _gates[resource] = sem = new SemaphoreSlim(1, 1);
        }

        if (!await sem.WaitAsync(acquireTimeout, ct))
            throw new TimeoutException($"Lock acquire timed out for {resource}.");
        return new Handle(sem);
    }

    private sealed class Handle(SemaphoreSlim sem) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { sem.Release(); return ValueTask.CompletedTask; }
    }
}
