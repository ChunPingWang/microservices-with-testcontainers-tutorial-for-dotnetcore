using Minio;
using Minio.DataModel.Args;
using PaymentService.Domain.Ports;

namespace PaymentService.Infrastructure.Storage;

public sealed class MinioReceiptStorage(IMinioClient minio, string bucket = "receipts") : IReceiptStorage
{
    private bool _ensured;

    public async Task<string> StoreAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucket(ct);
        var key = $"receipt-{Guid.NewGuid():N}.dat";
        var len = content.CanSeek ? content.Length : -1;
        if (content.CanSeek) content.Position = 0;
        await minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(len)
            .WithContentType(contentType), ct);
        return $"{bucket}/{key}";
    }

    private async Task EnsureBucket(CancellationToken ct)
    {
        if (_ensured) return;
        var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists) await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
        _ensured = true;
    }
}

public sealed class InMemoryReceiptStorage : IReceiptStorage
{
    public readonly List<(string Key, byte[] Data)> Stored = [];

    public async Task<string> StoreAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var key = $"in-memory/receipt-{Guid.NewGuid():N}";
        Stored.Add((key, ms.ToArray()));
        return key;
    }
}
