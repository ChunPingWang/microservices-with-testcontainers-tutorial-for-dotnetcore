using Microsoft.Extensions.Configuration;
using SharedKernel.Domain.Ports;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;

namespace PaymentService.Infrastructure.Secret;

public sealed class VaultSecretProvider : ISecretProvider
{
    private readonly IVaultClient _client;
    private readonly string _mount;

    public VaultSecretProvider(string address, string token, string mount = "kv")
    {
        IAuthMethodInfo auth = new TokenAuthMethodInfo(token);
        var settings = new VaultClientSettings(address, auth);
        _client = new VaultClient(settings);
        _mount = mount;
    }

    public async Task<string> GetSecretAsync(string path, string key, CancellationToken ct = default)
    {
        var resp = await _client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path, mountPoint: _mount);
        return resp.Data.Data.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string path,
        CancellationToken ct = default)
    {
        var resp = await _client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path, mountPoint: _mount);
        return resp.Data.Data.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
    }
}

public sealed class ConfigSecretProvider(IConfiguration config) : ISecretProvider
{
    public Task<string> GetSecretAsync(string path, string key, CancellationToken ct = default)
    {
        var v = config[$"Secrets:{path}:{key}"] ?? "";
        return Task.FromResult(v);
    }

    public Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string path,
        CancellationToken ct = default)
    {
        var section = config.GetSection($"Secrets:{path}");
        IReadOnlyDictionary<string, string> dict = section.GetChildren()
            .ToDictionary(c => c.Key, c => c.Value ?? "");
        return Task.FromResult(dict);
    }
}
