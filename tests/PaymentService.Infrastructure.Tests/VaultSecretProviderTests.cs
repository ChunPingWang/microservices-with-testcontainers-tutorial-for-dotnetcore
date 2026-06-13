using FluentAssertions;
using PaymentService.Infrastructure.Secret;
using TestInfrastructure.Containers;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using Xunit;

namespace PaymentService.Infrastructure.Tests;

[Collection("SharedContainers")]
public class VaultSecretProviderTests(SharedContainerFixture containers)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSecret_ReturnsValueWrittenToKV()
    {
        // Pre-seed the secret via Vault SDK admin client
        var settings = new VaultClientSettings(containers.VaultAddress,
            new TokenAuthMethodInfo(SharedContainerFixture.VaultRootToken));
        IVaultClient admin = new VaultClient(settings);
        // Vault dev mode mounts kv-v2 at "secret/" by default
        await admin.V1.Secrets.KeyValue.V2.WriteSecretAsync(
            "payment", new Dictionary<string, object> { ["psp_api_key"] = "sk_test_xyz" },
            mountPoint: "secret");

        var provider = new VaultSecretProvider(
            containers.VaultAddress, SharedContainerFixture.VaultRootToken, mount: "secret");

        var value = await provider.GetSecretAsync("payment", "psp_api_key");
        value.Should().Be("sk_test_xyz");
    }
}
