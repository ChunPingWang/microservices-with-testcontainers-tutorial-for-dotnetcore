namespace SharedKernel.Domain.Ports;

public interface IObjectStorage
{
    Task PutAsync(string bucket, string key, Stream content, string contentType,
        CancellationToken ct = default);

    Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default);

    Task<Uri> PresignedGetUrlAsync(string bucket, string key, TimeSpan expiry,
        CancellationToken ct = default);

    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);
}
