# Testcontainers 電商微服務整合測試教程（.NET Core 版）

## 教程目標

以 .NET 9 + C# 13 為基礎，採用 DDD + CQRS + Hexagonal Architecture，
建構三個電商微服務（商品、支付、庫存），透過 Testcontainers for .NET 驗證完整訂購流程。
重點展示「可抽換外部介接」的架構設計，讓基礎設施元件可在測試與正式環境間無縫切換。

---

## 一、技術棧

### 1.1 Java 版 → .NET 版技術對照

| 職責                | Java / Spring Boot                     | .NET Core                                     |
|---------------------|----------------------------------------|------------------------------------------------|
| Framework           | Spring Boot 4                          | ASP.NET Core 9 (Minimal API / Controller)     |
| Language            | Java 21 (Record, Sealed, Pattern)      | C# 13 (.NET 9) — record, required, pattern    |
| DI Container        | Spring IoC                             | Microsoft.Extensions.DependencyInjection       |
| ORM                 | Spring Data JPA / Hibernate            | EF Core 9                                      |
| DB Migration        | Flyway                                 | EF Core Migrations 或 FluentMigrator           |
| Messaging           | Spring Kafka                           | MassTransit + Confluent.Kafka                  |
| Cache               | Spring Data Redis                      | StackExchange.Redis / Microsoft.Extensions.Caching |
| Search              | Spring Data Elasticsearch              | Elastic.Clients.Elasticsearch (NEST 後繼)      |
| Auth                | Spring Security OAuth2                 | ASP.NET Core Authentication + JWT Bearer       |
| Object Storage      | AWS SDK v2 (S3 → MinIO)               | AWSSDK.S3 或 Minio.NET SDK                     |
| Secrets             | Spring Cloud Vault                     | VaultSharp                                      |
| GCP Pub/Sub         | Spring Cloud GCP                       | Google.Cloud.PubSub.V1                          |
| Test Framework      | JUnit 5                                | xUnit + FluentAssertions                        |
| Test Container      | Testcontainers (Java)                  | Testcontainers for .NET (官方)                  |
| BDD                 | Cucumber                               | Reqnroll (SpecFlow 後繼，.NET 9 支援)           |
| Architecture Test   | ArchUnit                               | NetArchTest.Rules                               |
| Async Await (Test)  | Awaitility                             | Polly + custom AsyncWaiter                      |
| Mediator (CQRS)     | 手動 CommandHandler                     | MediatR                                         |

### 1.2 NuGet 套件清單

```xml
<!-- Testcontainers 核心 -->
Testcontainers                          <!-- 基礎 API -->
Testcontainers.PostgreSql               <!-- PostgreSQL 模組 -->
Testcontainers.Kafka                    <!-- Kafka 模組 -->
Testcontainers.Redis                    <!-- Redis 模組 -->
Testcontainers.Elasticsearch            <!-- Elasticsearch 模組 -->
Testcontainers.Keycloak                 <!-- Keycloak 模組 (社群) -->
Testcontainers.Minio                    <!-- MinIO (或 GenericContainer) -->

<!-- 無預建模組 → 使用 GenericContainer 自建 -->
<!-- Vault: ContainerBuilder + hashicorp/vault -->
<!-- GCP Pub/Sub Emulator: ContainerBuilder + google-cloud-cli -->

<!-- CQRS -->
MediatR                                 <!-- Command / Query 分離 -->

<!-- Messaging -->
MassTransit                             <!-- 訊息匯流排抽象層 -->
MassTransit.Kafka                       <!-- Kafka Transport -->

<!-- Test -->
xUnit
FluentAssertions
NSubstitute                             <!-- Mock / Stub -->
NetArchTest.Rules                       <!-- 架構守門員 -->
Reqnroll                                <!-- BDD -->
Microsoft.AspNetCore.Mvc.Testing        <!-- WebApplicationFactory -->
```

---

## 二、C# 語言特性應用

### 2.1 Java 21 → C# 13 語法對照

```
┌─────────────────────────┬─────────────────────────────────────────┐
│  Java 21                │  C# 13                                  │
├─────────────────────────┼─────────────────────────────────────────┤
│  record OrderId(UUID v) │  public record OrderId(Guid Value);     │
│                         │                                         │
│  sealed interface       │  // 以下皆為 record，自帶 Equals/Hash   │
│    OrderStatus          │  public abstract record OrderStatus     │
│  permits Created, Paid  │  {                                      │
│  { ... }                │      public sealed record Created(...)  │
│                         │          : OrderStatus;                 │
│  record Created(...)    │      public sealed record Paid(...)     │
│    implements           │          : OrderStatus;                 │
│    OrderStatus {}       │      public sealed record Completed(...)│
│                         │          : OrderStatus;                 │
│                         │  }                                      │
│                         │                                         │
│  switch (status) {      │  var result = status switch             │
│    case Created c ->    │  {                                      │
│    case Paid p ->       │      OrderStatus.Created c  => ...,     │
│  }                      │      OrderStatus.Paid p     => ...,     │
│                         │      _ => throw new ...                 │
│                         │  };                                     │
│                         │                                         │
│  Virtual Threads        │  async/await (原生非同步，無需特殊啟用)  │
└─────────────────────────┴─────────────────────────────────────────┘
```

### 2.2 Domain Model 範例（C# 風格）

```csharp
// Value Object — record 自帶不可變 + 值相等
public record OrderId(Guid Value);
public record Money(decimal Amount, string Currency);
public record OrderLine(ProductId ProductId, int Quantity, Money UnitPrice);

// Domain Event — sealed hierarchy
public abstract record OrderEvent
{
    public sealed record OrderCreated(OrderId Id, IReadOnlyList<OrderLine> Lines, Money Total)
        : OrderEvent;
    public sealed record OrderPaid(OrderId Id, PaymentId PaymentId)
        : OrderEvent;
    public sealed record OrderCancelled(OrderId Id, string Reason)
        : OrderEvent;
}

// Aggregate Root
public sealed class Order   // class，有狀態變化
{
    public OrderId Id { get; }
    public OrderStatus Status { get; private set; }
    private readonly List<OrderEvent> _domainEvents = [];

    public IReadOnlyList<OrderEvent> DomainEvents => _domainEvents;

    public void MarkPaid(PaymentId paymentId)
    {
        if (Status is not OrderStatus.Created)
            throw new DomainException("Only CREATED orders can be paid.");

        Status = new OrderStatus.Paid(DateTime.UtcNow, paymentId);
        _domainEvents.Add(new OrderEvent.OrderPaid(Id, paymentId));
    }
}
```

---

## 三、架構設計

### 3.1 Hexagonal Architecture（與 Java 版一致）

```
              Driving Adapters                    Driven Adapters
        ┌──────────────────────┐           ┌───────────────────────────┐
        │ ASP.NET Controller   │           │ EF Core Repository        │
        │ MassTransit Consumer │           │ MassTransit Producer      │
        │ Background Service   │           │ StackExchange.Redis       │
        └─────────┬────────────┘           │ Elastic Client            │
                  │ calls                  │ Minio.NET / AWSSDK.S3     │
        ┌─────────▼────────────┐           │ VaultSharp                │
        │   Inbound Ports      │           │ Google.Cloud.PubSub       │
        │   (interface)        │           └───────────┬───────────────┘
        ├──────────────────────┤                       │ implements
        │   Domain Model       │           ┌───────────▼───────────────┐
        │   (Core)             │           │   Outbound Ports           │
        ├──────────────────────┤           │   (interface in Domain)    │
        │   Outbound Ports     ├───────────┘                           │
        │   (interface)        │                                       │
        └──────────────────────┘                                       │
```

### 3.2 CQRS with MediatR

```
Controller / Consumer
    │
    ├── Send(new PlaceOrderCommand(...))
    │       └── IRequestHandler<PlaceOrderCommand, OrderId>
    │               └── 寫入 PostgreSQL (Write Model)
    │               └── 發送 Domain Event → MassTransit → Kafka
    │
    └── Send(new SearchProductQuery(...))
            └── IRequestHandler<SearchProductQuery, PagedResult<ProductDto>>
                    └── 查詢 Elasticsearch / Redis (Read Model)
```

**MediatR 分離 Command / Query：**

```csharp
// Command（改變狀態）
public record PlaceOrderCommand(List<OrderLineDto> Lines)
    : IRequest<OrderId>;

// Query（不改變狀態）
public record SearchProductQuery(string Keyword, int Page)
    : IRequest<PagedResult<ProductDto>>;

// Pipeline Behavior（橫切關注）
public class LoggingBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes> { }
public class ValidationBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes> { }
```

### 3.3 MassTransit 作為 Messaging 抽象層

MassTransit 本身就是 .NET 生態的 Port/Adapter 模式實踐：

```
IPublishEndpoint (Port)
    │
    ├── MassTransit.Kafka        (Adapter — 正式環境)
    ├── MassTransit.RabbitMQ     (Adapter — 替代方案)
    ├── MassTransit.InMemory     (Adapter — 單元測試)
    └── MassTransit.TestHarness  (Adapter — 整合測試觀察)
```

切換 Transport 只需在 `Program.cs` 更換一行 DI 註冊，Consumer 程式碼完全不動。

---

## 四、Solution 結構

```
ECommerceTestcontainers.sln
│
├── src/
│   ├── SharedKernel/                             # 跨 Context 共用
│   │   ├── SharedKernel.Domain/
│   │   │   ├── ValueObjects/                     # Money.cs, Quantity.cs
│   │   │   ├── Events/                           # IIntegrationEvent.cs
│   │   │   └── Ports/                            # IEventPublisher.cs, IObjectStorage.cs
│   │   └── SharedKernel.Infrastructure/
│   │       └── Messaging/                        # MassTransitEventPublisher.cs
│   │
│   ├── ProductService/
│   │   ├── ProductService.Domain/                # 零外部依賴
│   │   │   ├── Model/
│   │   │   │   ├── Order.cs                      # Aggregate Root
│   │   │   │   ├── Product.cs                    # Entity
│   │   │   │   ├── OrderStatus.cs                # sealed record hierarchy
│   │   │   │   └── ValueObjects/                 # OrderId, OrderLine, Price
│   │   │   ├── Events/                           # OrderEvent (Domain Event)
│   │   │   ├── Ports/
│   │   │   │   ├── Inbound/                      # IPlaceOrderUseCase, ISearchProductUseCase
│   │   │   │   └── Outbound/                     # IOrderWriteRepo, ISearchPort, ICachePort
│   │   │   └── Services/                         # PricingService
│   │   │
│   │   ├── ProductService.Application/           # MediatR Handlers
│   │   │   ├── Commands/                         # PlaceOrderCommandHandler
│   │   │   ├── Queries/                          # SearchProductQueryHandler
│   │   │   └── Behaviors/                        # ValidationBehavior, LoggingBehavior
│   │   │
│   │   ├── ProductService.Infrastructure/        # Driven Adapters
│   │   │   ├── Persistence/
│   │   │   │   ├── ProductDbContext.cs            # EF Core DbContext
│   │   │   │   ├── EfOrderRepository.cs          # implements IOrderWriteRepo
│   │   │   │   ├── Configurations/               # IEntityTypeConfiguration<>
│   │   │   │   └── Migrations/
│   │   │   ├── Search/                           # ElasticSearchAdapter : ISearchPort
│   │   │   ├── Cache/                            # RedisCacheAdapter : ICachePort
│   │   │   ├── Storage/                          # MinioStorageAdapter : IObjectStorage
│   │   │   ├── Messaging/                        # KafkaPublisher, Consumers
│   │   │   └── Auth/                             # KeycloakConfig
│   │   │
│   │   └── ProductService.Api/                   # Driving Adapter (Host)
│   │       ├── Controllers/                      # or Minimal API endpoints
│   │       ├── Program.cs                        # DI 組裝 + Profile 切換
│   │       └── appsettings.{env}.json
│   │
│   ├── PaymentService/                           # 同樣四層結構
│   │   ├── PaymentService.Domain/
│   │   │   ├── Model/                            # Payment, IdempotencyKey
│   │   │   ├── Ports/Outbound/                   # IPaymentWriteRepo, ISecretProvider,
│   │   │   │                                     # IReceiptStorage, INotificationPort
│   │   │   └── Services/                         # PaymentValidationService
│   │   ├── PaymentService.Application/
│   │   ├── PaymentService.Infrastructure/
│   │   │   ├── Persistence/                      # EF Core
│   │   │   ├── Messaging/                        # MassTransit Kafka Consumer + Producer
│   │   │   ├── Notification/                     # PubSubNotificationAdapter : INotificationPort
│   │   │   ├── Storage/                          # MinioReceiptAdapter : IReceiptStorage
│   │   │   └── Secret/                           # VaultSecretProvider : ISecretProvider
│   │   └── PaymentService.Api/
│   │
│   └── InventoryService/                         # 同樣四層結構
│       ├── InventoryService.Domain/
│       │   ├── Model/                            # Stock (Aggregate), StockLevel, Reservation
│       │   ├── Ports/Outbound/                   # IStockWriteRepo, IDistributedLock
│       │   └── Services/                         # StockAllocationService
│       ├── InventoryService.Application/
│       ├── InventoryService.Infrastructure/
│       │   ├── Persistence/                      # EF Core
│       │   ├── Messaging/                        # MassTransit Kafka
│       │   ├── Lock/                             # RedisDistributedLock : IDistributedLock
│       │   └── Secret/                           # VaultSecretProvider
│       └── InventoryService.Api/
│
└── tests/
    ├── SharedKernel.Tests/
    │
    ├── ProductService.Domain.Tests/              # 純 Unit Test，無容器
    ├── ProductService.Application.Tests/         # MediatR Handler + InMemory Adapter
    ├── ProductService.Infrastructure.Tests/       # 每個 Adapter 配對一個容器
    ├── ProductService.Api.Tests/                 # WebApplicationFactory 整合測試
    │
    ├── PaymentService.Domain.Tests/
    ├── PaymentService.Application.Tests/
    ├── PaymentService.Infrastructure.Tests/
    ├── PaymentService.Api.Tests/
    │
    ├── InventoryService.Domain.Tests/
    ├── InventoryService.Application.Tests/
    ├── InventoryService.Infrastructure.Tests/
    ├── InventoryService.Api.Tests/
    │
    ├── EndToEnd.Tests/                           # 全鏈路，8 個容器
    │
    ├── Architecture.Tests/                       # NetArchTest 分層規則
    │
    └── TestInfrastructure/                       # 測試共用
        ├── Containers/                           # SharedContainerFixture (Singleton)
        ├── Fakes/                                # InMemory Adapter 實作
        └── Fixtures/                             # 測試資料工廠
```

---

## 五、Testcontainers for .NET 使用模式

### 5.1 容器生命週期管理（xUnit）

```
┌───────────────────────────────────────────────────────────────────┐
│  xUnit Fixture 對照                                               │
│                                                                   │
│  Java @Container (per-class)   →  IAsyncLifetime (per-class)     │
│  Java Singleton Container      →  IAsyncLifetime + Collection     │
│                                    Fixture (跨類共享)              │
│  Java @DynamicPropertySource   →  WebApplicationFactory           │
│                                    .WithWebHostBuilder()          │
│                                    .ConfigureTestServices()       │
└───────────────────────────────────────────────────────────────────┘
```

### 5.2 容器共享策略

```
                    ┌───────────────────────────┐
                    │  SharedContainerFixture    │
                    │  (IAsyncLifetime)          │
                    │                            │
                    │  PostgreSQL × 3            │
                    │  Kafka (KRaft)             │
                    │  Redis                     │
                    │  Elasticsearch             │
                    │  GCP Pub/Sub Emulator      │
                    │  Keycloak                  │
                    │  MinIO                     │
                    │  Vault                     │
                    └─────────┬─────────────────┘
                              │
              [CollectionDefinition("Containers")]
                              │
                ┌─────────────┼─────────────────┐
                │             │                 │
         Product Tests  Payment Tests   Inventory Tests
         (IClassFixture)                 (IClassFixture)
```

### 5.3 WebApplicationFactory 整合

```
WebApplicationFactory<Program>
    │
    └── .WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // 覆寫連線字串 → 指向 Testcontainers
                services.RemoveAll<DbContextOptions<ProductDbContext>>();
                services.AddDbContext<ProductDbContext>(opts =>
                    opts.UseNpgsql(container.GetConnectionString()));

                // 覆寫 Adapter → 指向容器或換成 Fake
                services.RemoveAll<ICachePort>();
                services.AddSingleton<ICachePort>(new RedisCacheAdapter(
                    container.GetConnectionString()));
            });
        });
```

---

## 六、Port / Adapter 快速切換一覽表

| Outbound Port | Real Adapter (Testcontainers) | Fake Adapter (Unit Test) | 替代方案 Adapter |
|----------------|-------------------------------|--------------------------|-------------------|
| `IOrderWriteRepository` | `EfOrderRepository` + PostgreSQL | `InMemoryOrderRepository` | — |
| `IOrderReadRepository` | `EfOrderReadRepository` + PG | `InMemoryOrderReadRepository` | Dapper Raw SQL |
| `ISearchPort` | `ElasticSearchAdapter` + ES | `InMemorySearchAdapter` | `EfSearchAdapter` (降級) |
| `ICachePort` | `RedisCacheAdapter` + Redis | `InMemoryCacheAdapter` | `MemoryCacheAdapter` (IMemoryCache) |
| `IEventPublisher` | `MassTransitKafkaPublisher` | `MassTransit.TestHarness` | `MassTransitRabbitPublisher` |
| `INotificationPort` | `PubSubNotificationAdapter` + GCP | `LogNotificationAdapter` | `SesNotificationAdapter` (AWS) |
| `IObjectStorage` | `MinioStorageAdapter` + MinIO | `InMemoryStorageAdapter` | `S3StorageAdapter` (AWS) |
| `ISecretProvider` | `VaultSecretProvider` + Vault | `ConfigSecretProvider` (appsettings) | `AzureKeyVaultProvider` |
| `IDistributedLock` | `RedisLockAdapter` + Redis | `SemaphoreSlimLockAdapter` | `EfPessimisticLockAdapter` |
| `IAuthService` | `KeycloakAuthAdapter` + KC | `StubAuthHandler` (全放行) | `Auth0AuthAdapter` |

**切換機制：**

```csharp
// Program.cs — 根據環境切換
if (builder.Environment.IsProduction())
{
    services.AddSingleton<ICachePort, RedisCacheAdapter>();
    services.AddSingleton<ISecretProvider, VaultSecretProvider>();
}
else if (builder.Environment.IsEnvironment("Testing"))
{
    // Testcontainers 已啟動容器，注入真實 Adapter + 容器連線
    services.AddSingleton<ICachePort>(sp =>
        new RedisCacheAdapter(testContainerConnectionString));
}
else // Development
{
    services.AddSingleton<ICachePort, MemoryCacheAdapter>();
    services.AddSingleton<ISecretProvider, ConfigSecretProvider>();
}
```

---

## 七、章節規劃

### 第一章：專案骨架與架構基礎 (Day 1-2)

**1.1 Solution 搭建**
- .NET 9 SDK + C# 13
- Solution 多專案結構（每個微服務 4 層）
- Directory.Build.props 統一 NuGet 版本管理（等同 Java BOM）

**1.2 Shared Kernel**
- 共用 Value Object：`Money`、`Quantity`（C# record）
- Integration Event 介面：`IIntegrationEvent`
- 共用 Outbound Port：`IEventPublisher`、`IObjectStorage`

**1.3 NetArchTest 守門員**
- Domain 層不得引用 Infrastructure / EF Core / MassTransit
- Application 層只能依賴 Domain
- Infrastructure 必須實作 Domain.Ports.Outbound 介面
- CI Pipeline 中自動執行

---

### 第二章：商品服務 — Domain & CQRS (Day 3-4)

**2.1 Domain Model**
- `Order` Aggregate Root（class，內部狀態管理）
- `Product` Entity
- Value Object（record）：`OrderId`、`OrderLine`、`Money`
- Sealed record hierarchy：`OrderStatus`、`OrderEvent`
- Domain Service：`PricingService`
- 純 xUnit 測試：狀態流轉、業務規則

**2.2 CQRS — Command Side (MediatR)**
- `PlaceOrderCommand` → `PlaceOrderCommandHandler`
- Handler 透過 `IOrderWriteRepository` 寫入
- `ValidationBehavior` 管線驗證（FluentValidation）
- Unit Test：NSubstitute mock Port + InMemory Fake

**2.3 CQRS — Query Side (MediatR)**
- `SearchProductQuery` → `SearchProductQueryHandler`
- Handler 透過 `ISearchPort` + `ICachePort` 讀取
- Unit Test with `InMemorySearchAdapter`

**2.4 Adapter — PostgreSQL (EF Core)**
- `ProductDbContext`、`EfOrderRepository`
- EF Core Entity ↔ Domain Model 映射（`IEntityTypeConfiguration<>`）
- EF Core Migrations（等同 Flyway）
- Testcontainers：`PostgreSqlContainer`
- Contract Test：同一份測試跑 InMemory + EF Core

**2.5 Adapter — Elasticsearch**
- `ElasticSearchAdapter` : `ISearchPort`
- Testcontainers：`ElasticsearchContainer`
- 測試：索引建立、全文搜尋、分面篩選

**2.6 Adapter — Redis**
- `RedisCacheAdapter` : `ICachePort`
- Testcontainers：`RedisContainer`
- 測試：Cache Aside、TTL、快取失效

**2.7 Adapter — MinIO**
- `MinioStorageAdapter` : `IObjectStorage`
- Testcontainers：`GenericContainerBuilder` + `minio/minio`
- 測試：商品圖片上傳、Presigned URL

**2.8 Adapter — Keycloak**
- ASP.NET Core JWT Bearer Authentication
- Testcontainers：Keycloak Container + realm import
- 測試：無 Token → 401、錯誤角色 → 403、正確 JWT → 200
- `StubAuthHandler` 對照：單元測試繞過認證

---

### 第三章：支付服務 — Event-Driven & Secrets (Day 5-6)

**3.1 Domain Model**
- `Payment` Aggregate Root
- `IdempotencyKey` Value Object（防重複支付）
- Sealed record：`PaymentStatus`、`PaymentEvent`
- Unit Test：冪等性、金額驗證

**3.2 CQRS Command**
- `ProcessPaymentCommand` → Handler
- Outbound Port：`IPaymentWriteRepository`、`ISecretProvider`、`IReceiptStorage`

**3.3 Adapter — MassTransit + Kafka**
- `IConsumer<OrderCreatedEvent>`（MassTransit Consumer = Driving Adapter）
- `IEventPublisher` → `MassTransitKafkaPublisher`
- Testcontainers：`KafkaContainer`
- 測試：收到 OrderCreatedEvent → 處理支付 → 發送 PaymentCompletedEvent
- MassTransit Test Harness 對照：不啟動 Kafka 的輕量驗證

**3.4 Adapter — Vault**
- `VaultSecretProvider` : `ISecretProvider`
- Testcontainers：`GenericContainerBuilder` + `hashicorp/vault` (dev mode)
- 測試：啟動時注入第三方支付 API Key
- 切換展示：`ConfigSecretProvider`（讀 appsettings.json）

**3.5 Adapter — GCP Pub/Sub**
- `PubSubNotificationAdapter` : `INotificationPort`
- Testcontainers：`GenericContainerBuilder` + GCP Pub/Sub Emulator
- 設定 `PUBSUB_EMULATOR_HOST` 環境變數
- 測試：支付完成 → 發送通知 → 驗證 subscriber 收到正確 payload

**3.6 Adapter — MinIO (收據歸檔)**
- 複用 `IObjectStorage`，展示同一 Port 跨 Bounded Context 複用

---

### 第四章：庫存服務 — 併發控制 & 動態密鑰 (Day 7)

**4.1 Domain Model**
- `Stock` Aggregate Root（EF Core Concurrency Token 樂觀鎖）
- `StockLevel`、`Reservation` Value Object
- Domain Service：`StockAllocationService`
- Unit Test：庫存不足拋 `DomainException`

**4.2 Adapter — Redis 分散式鎖**
- `RedisLockAdapter` : `IDistributedLock`（基於 RedLock.net 或 StackExchange.Redis）
- Testcontainers：Redis Container
- 測試：併發扣庫存 → 鎖保護 → 最終庫存正確
- 切換展示：`EfPessimisticLockAdapter`（用 SELECT FOR UPDATE 替代）

**4.3 Adapter — Vault 動態密鑰**
- Vault Database Secret Engine → 臨時 DB 帳密
- 測試：lease renewal、credential rotation

**4.4 Adapter — MassTransit Kafka**
- Consumer：`PaymentCompletedEvent`
- Producer：`InventoryDeductedEvent`

---

### 第五章：E2E 全鏈路測試 (Day 8)

**5.1 SharedContainerFixture**
- `IAsyncLifetime` 管理 8 個容器
- `INetwork` 讓容器間互通
- 啟動順序：Infra (PG, Redis, ES, Kafka) → Auth (Keycloak) → Secrets (Vault)
- 健康檢查：`WaitStrategy` 確保容器就緒

**5.2 Full Flow — Happy Path**

```
Keycloak 取得 JWT
    → POST /api/orders (商品服務 WebApplicationFactory)
        → Kafka: OrderCreatedEvent
            → 支付服務 Consumer 處理
                → Vault 取密鑰
                → MinIO 存收據
                → Pub/Sub 發通知
                → Kafka: PaymentCompletedEvent
                    → 庫存服務 Consumer 處理
                        → Redis Lock → 扣庫存 (PG)
                        → Kafka: InventoryDeductedEvent
                            → 商品服務 Consumer 更新訂單
                                → ES 索引更新
                                → Redis 快取失效
```

- 使用 `Polly.Retry` 等待非同步事件收斂
- 最終驗證：訂單狀態 COMPLETED、庫存減少、MinIO 有收據、ES 索引已更新

**5.3 Saga 補償**
- 支付成功但庫存不足 → `InventoryDeductionFailedEvent` → 退款
- MassTransit Fault Consumer 處理 Dead Letter
- 最終一致性驗證

**5.4 Profile 切換展示**
- `Testing-Full`：8 個容器全啟動
- `Testing-Lite`：MassTransit InMemory Transport（不啟動 Kafka）
- 同一套 E2E 測試，兩個 Profile 都通過

---

### 第六章：進階主題 (Day 9-10)

**6.1 Contract Test — Port 行為契約**
- 為每個 Outbound Port 定義抽象測試基底類別
- 同一份測試：`InMemoryOrderRepository` 跑一次、`EfOrderRepository` + PG 跑一次
- 確保 Fake 與 Real 行為一致

**6.2 Chaos Testing — Toxiproxy**
- Testcontainers + `ToxiproxyContainer`
- 模擬：Kafka 延遲、Redis 斷線、PG timeout
- 驗證：Polly Retry / Circuit Breaker / Fallback

**6.3 BDD (Reqnroll)**

```gherkin
Feature: 商品訂購流程

  Scenario: 成功訂購並扣庫存
    Given 商品 "iPhone 16" 庫存為 100
    And 使用者 "buyer01" 已通過 Keycloak 認證
    When 使用者下單購買 1 件 "iPhone 16"
    And 支付成功
    Then 庫存應減少為 99
    And 訂單狀態應為 "COMPLETED"
    And 支付憑證應存在於 MinIO
    And 客戶通知應已發送至 GCP Pub/Sub

  Scenario: 庫存不足觸發退款
    Given 商品 "iPhone 16" 庫存為 0
    When 使用者下單購買 1 件 "iPhone 16"
    And 支付成功
    Then 應觸發退款流程
    And 訂單狀態應為 "REFUNDED"
```

**6.4 CI/CD (GitHub Actions)**
- Docker-in-Docker 設定
- 測試分群：`[Trait("Category", "Unit")]` / `Integration` / `E2E`
- `dotnet test --filter Category=Unit` 快速回饋
- E2E 僅在 PR merge 或 nightly build 執行

---

## 八、.NET 版 vs Java 版 關鍵差異摘要

| 面向 | Java / Spring Boot | .NET Core | 影響 |
|------|-------------------|-----------|------|
| CQRS 中介 | 手動 Handler 或 Axon | MediatR (生態主流) | .NET 版 CQRS 接線更簡潔 |
| Messaging 抽象 | 無標準抽象層 | MassTransit (天然 Port/Adapter) | .NET 版切換 Transport 更容易 |
| DI 切換 | `@Profile` + `@ConditionalOnProperty` | `IServiceCollection` 手動替換 | 兩者效果等價 |
| 測試容器共享 | Static Container + Singleton | xUnit Collection Fixture | xUnit 原生支援更乾淨 |
| Auth 測試 | `@WithMockUser` | `AddAuthentication` + Stub Handler | 需手動配置但彈性更大 |
| ORM 映射隔離 | JPA Entity ≠ Domain Model (需映射) | EF Core 同上，用 Shadow Property 減少污染 | 兩者都需 ACL |
| 非同步模型 | Virtual Threads (Java 21) | async/await (原生) | .NET 無需特殊啟用 |

---

## 九、可行性與風險評估

| 面向         | 評估 |
|--------------|------|
| 技術可行性   | ✅ Testcontainers for .NET 官方維護，全部元件可支援 |
| 架構合理性   | ✅ Hexagonal + CQRS + MediatR 是 .NET 社群主流架構 |
| MassTransit  | ✅ 天然 Port/Adapter，切換 Kafka ↔ RabbitMQ ↔ InMemory 零改動 |
| SOLID 體現   | ✅ C# interface + DI 天然契合 DIP / ISP |
| 學習曲線     | ⚠️ MediatR + MassTransit + DDD 三者組合需有基礎 |
| 硬體需求     | ⚠️ 全量 8 容器 + Rider/VS → 建議 16GB RAM |
| 開發時間     | 完整教程含程式碼約 10 個工作天 |
