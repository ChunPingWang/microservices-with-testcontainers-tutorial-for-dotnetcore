using Minio;
using Minio.DataModel.Args;
using SharedKernel.Domain.Ports;

namespace ProductService.Infrastructure.Storage;

public sealed class MinioStorageAdapter(IMinioClient minio) : IObjectStorage
{
    public async Task EnsureBucketAsync(string bucket, CancellationToken ct = default)
    {
        var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
    }

    public async Task PutAsync(string bucket, string key, Stream content, string contentType,
        CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);
        await minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(content.CanSeek ? content.Length : -1)
            .WithContentType(contentType), ct);
    }

    public async Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithCallbackStream(s => s.CopyTo(ms)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task<Uri> PresignedGetUrlAsync(string bucket, string key, TimeSpan expiry,
        CancellationToken ct = default)
    {
        var url = await minio.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithExpiry((int)expiry.TotalSeconds));
        return new Uri(url);
    }
}

public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly Dictionary<(string bucket, string key), byte[]> _store = new();

    public Task EnsureBucketAsync(string bucket, CancellationToken ct = default) => Task.CompletedTask;

    public async Task PutAsync(string bucket, string key, Stream content, string contentType,
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        _store[(bucket, key)] = ms.ToArray();
    }

    public Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default)
    {
        if (!_store.TryGetValue((bucket, key), out var bytes))
            throw new FileNotFoundException($"{bucket}/{key}");
        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    public Task<Uri> PresignedGetUrlAsync(string bucket, string key, TimeSpan expiry,
        CancellationToken ct = default)
        => Task.FromResult(new Uri($"http://in-memory/{bucket}/{key}?exp={expiry.TotalSeconds}"));
}
