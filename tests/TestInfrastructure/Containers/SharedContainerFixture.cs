using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Elasticsearch;
using Testcontainers.Kafka;
using Testcontainers.Keycloak;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace TestInfrastructure.Containers;

/// <summary>
/// Boots the full 8-container stack once per test collection. Backbone of the E2E suite.
///
/// Layering (per the plan):
///   Infra  → Postgres × 3, Redis, Kafka, Elasticsearch
///   Auth   → Keycloak
///   Secret → Vault
///   Misc   → MinIO, GCP Pub/Sub emulator
/// </summary>
public sealed class SharedContainerFixture : IAsyncLifetime
{
    public INetwork Network { get; private set; } = null!;

    public PostgreSqlContainer ProductsDb { get; private set; } = null!;
    public PostgreSqlContainer PaymentsDb { get; private set; } = null!;
    public PostgreSqlContainer InventoryDb { get; private set; } = null!;
    public RedisContainer Redis { get; private set; } = null!;
    public KafkaContainer Kafka { get; private set; } = null!;
    public ElasticsearchContainer Elasticsearch { get; private set; } = null!;
    public KeycloakContainer Keycloak { get; private set; } = null!;
    public MinioContainer Minio { get; private set; } = null!;
    public IContainer Vault { get; private set; } = null!;
    public IContainer PubSubEmulator { get; private set; } = null!;

    public const string VaultRootToken = "test-root-token";
    public const string PubSubProjectId = "demo-project";

    public string VaultAddress => $"http://localhost:{Vault.GetMappedPublicPort(8200)}";
    public string PubSubEmulatorHost => $"localhost:{PubSubEmulator.GetMappedPublicPort(8085)}";

    public async ValueTask InitializeAsync()
    {
        Network = new NetworkBuilder().WithName($"ecomm-{Guid.NewGuid():N}").Build();
        await Network.CreateAsync();

        ProductsDb = NewPg("products");
        PaymentsDb = NewPg("payments");
        InventoryDb = NewPg("inventory");
        Redis = new RedisBuilder().WithImage("redis:7-alpine").Build();
        Kafka = new KafkaBuilder().WithImage("confluentinc/cp-kafka:7.6.1").Build();
        Elasticsearch = new ElasticsearchBuilder()
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.15.0")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("xpack.security.http.ssl.enabled", "false")
            .WithEnvironment("xpack.security.transport.ssl.enabled", "false")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithEnvironment("bootstrap.memory_lock", "false")
            .Build();
        Keycloak = new KeycloakBuilder().WithImage("quay.io/keycloak/keycloak:25.0").Build();
        Minio = new MinioBuilder().WithImage("minio/minio:latest").Build();

        Vault = new ContainerBuilder()
            .WithImage("hashicorp/vault:1.17")
            .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", VaultRootToken)
            .WithEnvironment("VAULT_DEV_LISTEN_ADDRESS", "0.0.0.0:8200")
            .WithCommand("server", "-dev")
            .WithPortBinding(8200, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPath("/v1/sys/health").ForPort(8200)))
            .Build();

        PubSubEmulator = new ContainerBuilder()
            .WithImage("gcr.io/google.com/cloudsdktool/cloud-sdk:emulators")
            .WithEntrypoint("/bin/sh", "-c")
            .WithCommand(
                "gcloud beta emulators pubsub start --host-port=0.0.0.0:8085 --project=" + PubSubProjectId)
            .WithPortBinding(8085, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8085))
            .Build();

        // Parallelise startup
        await Task.WhenAll(
            ProductsDb.StartAsync(), PaymentsDb.StartAsync(), InventoryDb.StartAsync(),
            Redis.StartAsync(), Kafka.StartAsync(), Elasticsearch.StartAsync(),
            Keycloak.StartAsync(), Minio.StartAsync(),
            Vault.StartAsync(), PubSubEmulator.StartAsync());
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(
            ProductsDb.DisposeAsync().AsTask(),
            PaymentsDb.DisposeAsync().AsTask(),
            InventoryDb.DisposeAsync().AsTask(),
            Redis.DisposeAsync().AsTask(),
            Kafka.DisposeAsync().AsTask(),
            Elasticsearch.DisposeAsync().AsTask(),
            Keycloak.DisposeAsync().AsTask(),
            Minio.DisposeAsync().AsTask(),
            Vault.DisposeAsync().AsTask(),
            PubSubEmulator.DisposeAsync().AsTask());
        await Network.DeleteAsync();
    }

    private static PostgreSqlContainer NewPg(string db) => new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase(db)
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
}

[CollectionDefinition("SharedContainers")]
public sealed class SharedContainerCollection : ICollectionFixture<SharedContainerFixture> { }
