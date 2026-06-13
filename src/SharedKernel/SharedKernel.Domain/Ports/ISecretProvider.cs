namespace SharedKernel.Domain.Ports;

public interface ISecretProvider
{
    Task<string> GetSecretAsync(string path, string key, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string path,
        CancellationToken ct = default);
}
