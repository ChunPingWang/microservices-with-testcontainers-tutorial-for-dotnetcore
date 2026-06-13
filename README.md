# dot Net Core 微服務應用 — Testcontainers 學習專案

> 用一個真正會跑的電商範例，學會 **DDD + CQRS + Hexagonal Architecture**，
> 並用 **Testcontainers for .NET** 把所有外部依賴搬進測試裡驗證。

本專案是一份**寫給初學者**的完整教學程式碼倉庫。讀完並親手跑過後，你會具備
下列能力：

- 用 C# 14 + .NET 10 寫出層次分明、可單獨測試的領域邏輯
- 用 MediatR 把寫入（Command）與查詢（Query）分開
- 用 Hexagonal Port/Adapter 把資料庫、訊息佇列、快取等基礎設施抽象起來
- 在測試裡用 Testcontainers 啟動真正的 PostgreSQL / Kafka / Redis / Elasticsearch /
  Keycloak / MinIO / Vault / GCP Pub/Sub Emulator，而不是只 mock 介面
- 用 xUnit Collection Fixture、WebApplicationFactory、NetArchTest、Reqnroll BDD
  把測試金字塔疊起來
- 在不改動任何業務程式碼的前提下，把 Real Adapter 換成 Fake Adapter 或反之

---

## 目錄

1. [為什麼需要這份教程](#1-為什麼需要這份教程)
2. [前置知識與環境](#2-前置知識與環境)
3. [五分鐘上手](#3-五分鐘上手)
4. [架構導覽](#4-架構導覽)
5. [程式碼地圖](#5-程式碼地圖)
6. [章節學習路徑](#6-章節學習路徑)
7. [核心程式模式說明](#7-核心程式模式說明)
8. [測試金字塔與分類執行](#8-測試金字塔與分類執行)
9. [Testcontainers 常見坑與解法](#9-testcontainers-常見坑與解法)
10. [Profile 切換：同一套程式碼跑三種環境](#10-profile-切換同一套程式碼跑三種環境)
11. [常見錯誤排查清單](#11-常見錯誤排查清單)
12. [延伸練習](#12-延伸練習)

---

## 1. 為什麼需要這份教程

如果你看過 [Testcontainers Java 版的範例](https://java.testcontainers.org/)，
然後想把同樣的思路用 .NET 寫，會發現幾個落差：

| 問題 | 你會碰到什麼 | 本教程怎麼處理 |
|---|---|---|
| .NET 沒有「JPA Entity」的概念，要怎麼讓 DDD Entity 跟 EF Core Entity 同時存在？ | 兩種思路：1) Domain Entity 跟 EF 直連 2) Domain Entity 與 DB 物件分開、寫對應。本教程示範**直連 + 用 OwnsOne/Field 隱藏 EF 細節** | `ProductService.Domain.Model.Order` + `Persistence/Configurations/OrderConfiguration` |
| Spring 的 `@DynamicPropertySource` 在 .NET 等於什麼？ | `WebApplicationFactory<Program>` + `ConfigureAppConfiguration` | `tests/EndToEnd.Tests/Factories.cs` |
| .NET 有 MediatR + MassTransit 的話還需要手寫 CQRS 嗎？ | 不用，但要懂它在做什麼，否則一改架構就壞 | 第 7 章詳細拆解 |
| Testcontainers for .NET 跟 Java 版差在哪？ | 大部分 API 一致，但**生命週期管理用 xUnit Fixture**而不是 JUnit `@Container` | 第 8、9 章 |

本教程不會逼你一次學完所有概念。每個檔案夾、每支測試都有對應的「為什麼這樣寫」
說明在第 6、7 章。**建議的學習順序**：

> Domain Unit Test → Application + InMemory Fake → 單一 Adapter 配 Testcontainers → WebApplicationFactory → E2E

這也是測試金字塔的方向，由內往外。

---

## 2. 前置知識與環境

### 2.1 你應該已經會的事

- C# 基本語法（class、interface、async/await、LINQ）
- 任何一個 web framework（Node/Express、Spring、Rails、Django 都行）
- Git 與 command line 基本操作
- 已經寫過至少一個有資料庫的小專案

如果你 C# 都還沒寫過，建議先讀 [Microsoft 官方 C# 入門](https://learn.microsoft.com/dotnet/csharp/tour-of-csharp/)
再回來。

### 2.2 開發工具

- **.NET 10 SDK**（RC1 或更新版本）
  ```bash
  dotnet --version       # 應顯示 10.0.x
  ```
- **任何 Docker 相容的 container runtime**：
  - [Docker Desktop](https://www.docker.com/products/docker-desktop/) — 最簡單
  - [Podman](https://podman.io/) + 把 socket symlink 到 `/var/run/docker.sock`
  - [Colima](https://github.com/abiosoft/colima)、Rancher Desktop、Lima 也可以
- **IDE**：Visual Studio 2022、JetBrains Rider、或 VS Code + C# Dev Kit
- **記憶體**：
  - 只跑單元測試：4 GB 就夠
  - 跑 Infrastructure tests：6 GB 起跳（PG + Redis + ES + Kafka 同時開）
  - 跑完整 E2E（10 個容器）：**至少 8 GB 給 container runtime 的 VM**
    - Docker Desktop：Preferences → Resources → Memory ≥ 8 GB
    - Podman：`podman machine set --memory 8192`

### 2.3 你不需要先學會的事

- DDD 不用先念完《Domain-Driven Design》整本書 — 看本教程就會了
- MediatR、MassTransit、EF Core 我們會在用到時解釋
- Kubernetes / Docker Compose 都不用 — 容器啟動完全由 Testcontainers 處理

---

## 3. 五分鐘上手

```bash
# 1. 還原套件、編譯整個 solution
dotnet build

# 2. 跑所有「不需要容器」的測試（單元 + 架構守則 + BDD）
dotnet test --filter "Category=Unit|Category=Contract"
# 預期：35 個測試通過，秒級完成

# 3. 跑一個容器整合測試做煙霧測試（只啟動 Redis）
dotnet test tests/ProductService.Infrastructure.Tests \
            --filter "Category=Smoke"
# 預期：1 個測試通過，~3 秒（含 Redis 容器啟動）

# 4. 跑完整 Infrastructure 測試（會啟動 10 個容器，需 8 GB VM 記憶體）
dotnet test tests/ProductService.Infrastructure.Tests --filter "Category=Integration"
```

如果第 3 步通過了，代表你的 Testcontainers + Docker 環境一切正常，可以放心進入後面章節。

---

## 4. 架構導覽

### 4.1 三層 + 三服務的整體圖

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SharedKernel (跨 Context 共用)                    │
│   ValueObjects: Money, Quantity                                      │
│   Events: IIntegrationEvent + 五種跨服務事件                          │
│   Ports: IEventPublisher, IObjectStorage, ISecretProvider            │
└─────────────────────────────────────────────────────────────────────┘
              ▲                    ▲                    ▲
              │                    │                    │
   ┌──────────┴──────────┐  ┌─────┴────────┐  ┌────────┴──────────┐
   │   ProductService    │  │  PaymentSvc  │  │  InventoryService │
   ├─────────────────────┤  ├──────────────┤  ├───────────────────┤
   │ Domain              │  │ Domain       │  │ Domain            │
   │ Application (CQRS)  │  │ Application  │  │ Application       │
   │ Infrastructure      │  │ Infra.       │  │ Infrastructure    │
   │ Api (ASP.NET Core)  │  │ Api          │  │ Api               │
   └─────────────────────┘  └──────────────┘  └───────────────────┘
              │                    │                    │
              └────── Kafka (MassTransit Transport) ────┘
```

每個微服務內部都是 **Hexagonal Architecture**（六角形架構，又稱 Ports & Adapters）。

### 4.2 Hexagonal Architecture 三十秒理解

```
        Driving Adapters                       Driven Adapters
  (誰呼叫我？)                                   (我呼叫誰？)
  ┌───────────────────┐                       ┌──────────────────────┐
  │ ASP.NET Endpoint  │                       │ EF Core + PostgreSQL │
  │ MassTransit Cons. │                       │ MassTransit + Kafka  │
  │ Background Job    │                       │ StackExchange.Redis  │
  └────────┬──────────┘                       │ Elasticsearch Client │
           │                                  │ Minio.NET SDK        │
           │ 呼叫                              │ VaultSharp           │
  ┌────────▼──────────┐    實作               └────────▲─────────────┘
  │   Inbound Port    │    這些 interface              │
  │   (介面)           │                              │
  ├───────────────────┤                              │
  │   Domain Model    │   ←  零外部依賴的純邏輯        │
  │   (Core)          │                              │
  ├───────────────────┤                              │
  │   Outbound Port   │  ─────  介面 ──────────────►│
  │   (介面)           │                              │
  └───────────────────┘                              │
```

**規則只有兩條**：

1. Domain 層**只能依賴自己 + SharedKernel.Domain**。不能 import EF Core、不能 import
   MassTransit、不能 import ASP.NET。
2. Adapter 層**負責實作 Domain 定義的 Port 介面**。要換 PostgreSQL 為 SQLite、要從
   Kafka 換成 RabbitMQ，都只在 Adapter 改。

這兩條規則由 **NetArchTest** 在 build 時強制檢查
（見 [`tests/Architecture.Tests/LayeringRules.cs`](tests/Architecture.Tests/LayeringRules.cs)）。

### 4.3 CQRS 三十秒理解

> Command（改變狀態） vs Query（讀取狀態）分開設計。

```csharp
// Command：改變狀態。寫入 PostgreSQL、發送事件
public record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines)
    : IRequest<Guid>;

// Query：唯讀，可以走快取、走 Elasticsearch、走任何讀模型
public record SearchProductQuery(string? Keyword, int Page, int PageSize)
    : IRequest<PagedResult<ProductDto>>;
```

兩邊都用 **MediatR** 派發到對應的 Handler。好處：

- Command 變更 → 只影響 CommandHandler、不會弄壞 Query
- Query 慢了 → 加快取、換 Elasticsearch、加 read replica，都跟 Command 無關
- 跨切關注（logging、validation）寫成 `IPipelineBehavior<,>`，全域生效

---

## 5. 程式碼地圖

```
ECommerceTestcontainers.sln
│
├── Directory.Build.props          ← 統一 net10.0 + nullable + analyzer
├── Directory.Packages.props       ← Central Package Management，集中 NuGet 版本
├── global.json                    ← 固定 .NET SDK 版本
│
├── src/
│   ├── SharedKernel/
│   │   ├── SharedKernel.Domain/          純粹 record / interface，零外部依賴
│   │   └── SharedKernel.Infrastructure/  MassTransitEventPublisher 的實作
│   │
│   ├── ProductService/
│   │   ├── ProductService.Domain/        ★ 純邏輯（Order/Product/PricingService）
│   │   ├── ProductService.Application/   ★ MediatR Handlers + Behaviors
│   │   ├── ProductService.Infrastructure/★ EF Core / Redis / ES / MinIO Adapter
│   │   └── ProductService.Api/           ASP.NET Minimal API 入口
│   │
│   ├── PaymentService/                   同樣四層
│   └── InventoryService/                 同樣四層
│
└── tests/
    ├── SharedKernel.Tests/
    ├── *.Domain.Tests/             單元測試，秒級
    ├── *.Application.Tests/        含 Contract Test 抽象基類
    ├── *.Infrastructure.Tests/     Testcontainers 整合測試
    ├── *.Api.Tests/                WebApplicationFactory + 真實 PG 容器
    ├── EndToEnd.Tests/             跨服務全鏈路
    ├── Architecture.Tests/         NetArchTest 分層守則
    ├── BDD.Tests/                  Reqnroll + .feature 中文情境
    └── TestInfrastructure/         共用 SharedContainerFixture + AsyncWaiter
```

關鍵檔案速覽：

- [`src/ProductService/ProductService.Domain/Model/Order.cs`](src/ProductService/ProductService.Domain/Model/Order.cs) — 怎麼寫一個有狀態流轉的 Aggregate Root
- [`src/ProductService/ProductService.Application/Commands/PlaceOrder.cs`](src/ProductService/ProductService.Application/Commands/PlaceOrder.cs) — Command + Validator + Handler 一條龍
- [`src/ProductService/ProductService.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`](src/ProductService/ProductService.Infrastructure/Persistence/Configurations/OrderConfiguration.cs) — 怎麼把 sealed record hierarchy 存進關聯式資料庫
- [`src/ProductService/ProductService.Infrastructure/DependencyInjection.cs`](src/ProductService/ProductService.Infrastructure/DependencyInjection.cs) — Profile 切換的 DI 註冊在哪
- [`tests/TestInfrastructure/Containers/SharedContainerFixture.cs`](tests/TestInfrastructure/Containers/SharedContainerFixture.cs) — 10 個容器並行啟動的標準模式

---

## 6. 章節學習路徑

本教程的詳細設計藍圖在 [`testcontainers-tutorial-plan-dotnet.md`](testcontainers-tutorial-plan-dotnet.md)。
這裡列出**程式碼跟章節的對應關係**，方便你按順序學：

### 第 1 章：專案骨架與架構守門員

讀這些檔案：

- `Directory.Build.props`、`Directory.Packages.props`、`global.json`
- `src/SharedKernel/SharedKernel.Domain/ValueObjects/Money.cs`
- `src/SharedKernel/SharedKernel.Domain/Events/IntegrationEvents.cs`
- `tests/Architecture.Tests/LayeringRules.cs`

跑這些測試：

```bash
dotnet test tests/SharedKernel.Tests
dotnet test tests/Architecture.Tests
```

**學到**：CPM (Central Package Management) 的用法、值物件設計、分層守則怎麼寫成可執行的測試。

### 第 2 章：ProductService（最大一章）

按這個順序讀：

1. `Order.cs`、`Product.cs`、`OrderStatus.cs` — Domain Model
2. `Ports/Outbound/IOrderRepository.cs` — Port 怎麼定義
3. `Application/Commands/PlaceOrder.cs` — Command + Handler
4. `Application/Queries/SearchProductQuery.cs` — Query + 快取的標準寫法
5. `Infrastructure/Persistence/EfOrderRepository.cs` — Adapter 怎麼把 Domain 寫進 DB
6. `Infrastructure/Cache/RedisCacheAdapter.cs` — InMemory + Redis 雙實作對照
7. `Infrastructure/Search/ElasticSearchAdapter.cs` — 同上
8. `Api/Program.cs` — 怎麼根據環境組裝完全不同的 Adapter

跑這些測試：

```bash
dotnet test tests/ProductService.Domain.Tests          # 純邏輯
dotnet test tests/ProductService.Application.Tests     # 用 InMemory Fake
dotnet test tests/ProductService.Infrastructure.Tests --filter "Category=Integration"  # 真容器
```

### 第 3 章：PaymentService（事件驅動 + 機密管理）

讀：
- `Payment.cs`、`IdempotencyKey` value object（防重複扣款）
- `Application/Commands/ProcessPayment.cs` — 冪等檢查、Vault 取密鑰、MinIO 存收據、發事件，一條龍
- `Infrastructure/Messaging/OrderCreatedConsumer.cs` — 收 Kafka 事件 → 觸發 Command
- `Infrastructure/Secret/VaultSecretProvider.cs` + 對照 `ConfigSecretProvider`
- `Infrastructure/Notification/PubSubNotificationAdapter.cs`

### 第 4 章：InventoryService（併發控制）

讀：
- `Stock.cs` — EF Core 樂觀鎖（`Version` 屬性 + `IsConcurrencyToken()`）
- `Services/StockAllocationService.cs` — 多商品 all-or-nothing 配貨
- `Infrastructure/Lock/RedisDistributedLock.cs` — 用 Lua 腳本實作的 Redis 分散式鎖
- 對照 `SemaphoreDistributedLock`（單機 fallback）

### 第 5 章：E2E 全鏈路

讀：
- `tests/TestInfrastructure/Containers/SharedContainerFixture.cs` — 怎麼用 `IAsyncLifetime` 並行啟動 10 個容器
- `tests/EndToEnd.Tests/Factories.cs` — 三個服務的 `WebApplicationFactory` 怎麼共用同一組容器
- `tests/EndToEnd.Tests/OrderFlowTests.cs` — 跨服務 happy path

### 第 6 章：進階主題

- Contract Test：`tests/ProductService.Application.Tests/Contract/OrderRepositoryContract.cs`
- BDD：`tests/BDD.Tests/Features/OrderPlacement.feature` + `OrderPlacementSteps.cs`
- CI：`.github/workflows/ci.yml`（每個 Infrastructure 專案開獨立 job，避免 ES 連跑不穩）

---

## 7. 核心程式模式說明

### 7.1 Aggregate Root：有狀態的 class，不是 record

```csharp
public sealed class Order
{
    private List<OrderLine> _lines;
    private readonly List<OrderEvent> _domainEvents = [];

    public OrderId Id { get; private set; }
    public OrderStatus Status { get; private set; }   // 不可變的 record hierarchy
    public IReadOnlyList<OrderEvent> DomainEvents => _domainEvents;

    // 給 EF Core 用的無參數建構子
    private Order() { _lines = []; /* 初始化 ... */ }

    private Order(OrderId id, /* ... */) { /* 正規建構 */ }

    public static Order Place(CustomerId customerId, IEnumerable<OrderLine> lines, Func<DateTime>? clock = null)
    {
        // 業務規則：至少一條 line、計算 total、初始狀態
        var order = new Order(/* ... */);
        order._domainEvents.Add(new OrderEvent.OrderCreated(/* ... */));
        return order;
    }

    public void MarkPaid(PaymentId paymentId, Func<DateTime>? clock = null)
    {
        if (Status is not OrderStatus.Created)
            throw new DomainException("Only CREATED orders can be paid.");
        Status = new OrderStatus.Paid(/* ... */);
        _domainEvents.Add(new OrderEvent.OrderPaid(/* ... */));
    }
}
```

**重點**：
- Aggregate 是 `class`（有狀態變化），ValueObject 是 `record`（不可變）
- 狀態轉移的合法性**寫在 Domain 裡**，不要靠 Application 層擋
- Domain Events 在聚合內收集，由 Application Handler 在交易結束時取出來發送
- `Func<DateTime>? clock` 注入時間以利測試（不用 `DateTime.UtcNow`）

### 7.2 OrderStatus：sealed record 取代 enum

```csharp
public abstract record OrderStatus
{
    private OrderStatus() { }   // 禁止外部繼承

    public sealed record Created(DateTime AtUtc) : OrderStatus;
    public sealed record Paid(DateTime AtUtc, PaymentId PaymentId) : OrderStatus;
    public sealed record Completed(DateTime AtUtc) : OrderStatus;
    public sealed record Cancelled(DateTime AtUtc, string Reason) : OrderStatus;
    public sealed record Refunded(DateTime AtUtc, string Reason) : OrderStatus;
}
```

**用法**：

```csharp
var summary = order.Status switch
{
    OrderStatus.Paid p     => $"已付款（{p.PaymentId}）",
    OrderStatus.Refunded r => $"已退款（{r.Reason}）",
    OrderStatus.Created    => "待付款",
    _                       => "其他"
};
```

比 enum 多了：**狀態自帶資料**（Paid 帶 PaymentId、Cancelled 帶 Reason），編譯器幫你檢查
pattern matching 涵蓋率。

### 7.3 MediatR Handler：Command 寫入流程

```csharp
public sealed class PlaceOrderCommandHandler(
    IProductRepository products,
    IOrderWriteRepository orders,
    IEventPublisher eventPublisher,
    PricingService pricing,
    TimeProvider clock)
    : IRequestHandler<PlaceOrderCommand, Guid>
{
    public async Task<Guid> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        // 1. 載入領域物件
        var catalog = (await products.GetManyAsync(/* ids */, ct)).ToDictionary(p => p.Id);

        // 2. 領域邏輯
        var lines = pricing.BuildLines(/* ... */, catalog);
        var order = Order.Place(new CustomerId(cmd.CustomerId), lines, () => clock.GetUtcNow().UtcDateTime);

        // 3. 持久化
        await orders.AddAsync(order, ct);

        // 4. 發送整合事件
        foreach (var ev in order.DomainEvents)
        {
            if (ev is Domain.Events.OrderEvent.OrderCreated created)
                await eventPublisher.PublishAsync(/* OrderCreatedIntegrationEvent */, ct);
        }
        order.ClearDomainEvents();

        return order.Id.Value;
    }
}
```

**重點**：Handler 不擁有業務規則 — 規則在 `Order.Place` 裡。Handler 做的事是
**協調**：載入 → 呼叫領域 → 存檔 → 發事件。

### 7.4 IPipelineBehavior：橫切關注

```csharp
// 一支 LoggingBehavior 自動裝飾所有 Command/Query
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest req, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try { return await next(ct); }
        finally { logger.LogInformation("{Req} took {Ms}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds); }
    }
}
```

註冊一次：

```csharp
services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssemblyContaining<PlaceOrderCommand>();
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});
```

從此每支 Handler 都自動 timed + validated，不用一支一支改。

### 7.5 Port + 雙 Adapter

定義一個 Port：

```csharp
// in Domain
public interface ICachePort
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
}
```

兩個 Adapter：

```csharp
// in Infrastructure — 正式環境用
public sealed class RedisCacheAdapter(IConnectionMultiplexer redis) : ICachePort { /* ... */ }

// in Infrastructure — 開發/單元測試用
public sealed class InMemoryCachePort : ICachePort { /* Dictionary-based */ }
```

DI 切換：

```csharp
if (builder.Environment.IsProduction())
    services.AddRedisCache(connectionString);
else
    services.AddInMemoryCache();
```

**所有 Handler、Domain Service 都不用改一行**。這就是 Hexagonal 真正的好處。

### 7.6 EF Core 與 DDD Aggregate 整合

EF Core 9 的限制：

1. **`OwnsOne<T>` 要求 T 是 class**，所以 `Money`、`Quantity` 用 `sealed record`
   而不是 `readonly record struct`。
2. **EF 需要無參數建構子**才能 materialise。Domain Aggregate 加一個 `private Order() {}`。
3. **EF 不能把 `OwnsOne` 的型別當建構子參數**，所以正規建構子加上欄位（不是
   readonly），讓 EF 透過反射填值。

```csharp
public sealed class Order
{
    private List<OrderLine> _lines;            // 注意：沒有 readonly
    public OrderId Id { get; private set; }    // 注意：private set，不是 init-only
    public Money Total { get; private set; }

    private Order()                            // EF Core 用
    {
        _lines = [];
        Total = Money.Zero("TWD");
        Status = new OrderStatus.Created(DateTime.UtcNow);
    }
}
```

EF 設定（用 navigation expression + HasField 把 `_lines` 串起來）：

```csharp
e.OwnsMany(o => o.Lines, l => {
    l.ToTable("order_lines");
    l.WithOwner().HasForeignKey("order_id");
    /* ... line columns ... */
});

e.Navigation(o => o.Lines)
    .UsePropertyAccessMode(PropertyAccessMode.Field)
    .HasField("_lines");
```

`OrderStatus` 是 sealed record hierarchy，EF 不直接支援多型欄位。我們的做法是
**shadow property 拆成四個欄位**（`status_name`、`status_at_utc`、`status_payment_id`、
`status_reason`），讀寫時在 Repository 裡轉換。範例：
[`EfOrderRepository.WriteStatusShadow`](src/ProductService/ProductService.Infrastructure/Persistence/EfOrderRepository.cs)。

### 7.7 MassTransit + Kafka：跨服務事件

```csharp
// 發送端（任何 Handler 注入 IEventPublisher）
await eventPublisher.PublishAsync(new OrderCreatedIntegrationEvent(
    EventId: Guid.NewGuid(),
    OccurredAtUtc: clock.GetUtcNow().UtcDateTime,
    OrderId: order.Id.Value,
    /* ... */), ct);

// 接收端（PaymentService.Infrastructure.Messaging.OrderCreatedConsumer）
public sealed class OrderCreatedConsumer(IMediator mediator)
    : IConsumer<OrderCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> ctx)
        => mediator.Send(new ProcessPaymentCommand(/* ... */), ctx.CancellationToken);
}
```

DI 註冊一個地方就切換 Transport：

```csharp
// Kafka (production)
services.AddMessaging(MessagingProfile.Kafka);
services.AddSingleton(new KafkaSettings("localhost:9092"));

// In-Memory (測試 / 開發)
services.AddMessaging(MessagingProfile.InMemory);
```

Consumer 程式碼**完全不動**。

---

## 8. 測試金字塔與分類執行

```
            ┌───────────────────────────────────┐
            │       E2E.Tests (10 containers)   │   ~3 個 / 分鐘級
            ├───────────────────────────────────┤
            │    *.Api.Tests + Infrastructure   │   ~15 個 / 1-5 秒
            ├───────────────────────────────────┤
            │  *.Application.Tests + Contract   │   ~10 個 / 毫秒級
            ├───────────────────────────────────┤
            │      *.Domain.Tests + Arch        │   ~30 個 / 毫秒級
            └───────────────────────────────────┘
```

每支測試都用 `[Trait("Category", "...")]` 分類，方便獨立執行：

| Category | 包含 | 跑法 |
|---|---|---|
| `Unit` | Domain、Architecture、Application | `dotnet test --filter "Category=Unit"` |
| `Contract` | Port 行為契約 | `dotnet test --filter "Category=Contract"` |
| `Smoke` | 單一容器煙霧測試 | `dotnet test --filter "Category=Smoke"` |
| `Integration` | Adapter 配 Testcontainers | `dotnet test --filter "Category=Integration"` |
| `E2E` | 跨服務全鏈路 | `dotnet test --filter "Category=E2E"` |

### 8.1 SharedContainerFixture：怎麼共用容器

xUnit 用 `[CollectionDefinition]` 標記哪些測試要共用 fixture：

```csharp
public sealed class SharedContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer ProductsDb { get; private set; } = null!;
    public RedisContainer Redis { get; private set; } = null!;
    // ... 共 10 個容器

    public async ValueTask InitializeAsync()
    {
        ProductsDb = new PostgreSqlBuilder().Build();
        Redis = new RedisBuilder().WithImage("redis:7-alpine").Build();
        // ...

        // 並行啟動所有容器
        await Task.WhenAll(
            ProductsDb.StartAsync(),
            Redis.StartAsync(),
            /* ... */);
    }
}

[CollectionDefinition("SharedContainers")]
public sealed class SharedCollection : ICollectionFixture<SharedContainerFixture> { }
```

每個測試類別標 `[Collection("SharedContainers")]` 就會共用同一組容器：

```csharp
[Collection("SharedContainers")]
public class RedisCacheAdapterTests(SharedContainerFixture containers)
{
    [Fact]
    public async Task SetGet_RoundtripsThroughRedis()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(containers.Redis.GetConnectionString());
        // ...
    }
}
```

⚠️ **重要**：xUnit v3 的 `xUnit1041` analyzer 要求 `[CollectionDefinition]` **必須跟測試
在同一個 assembly**。所以本專案每個測試 csproj 都有自己的 `SharedCollection.cs`
（一行宣告而已，fixture 本體還是放在 `TestInfrastructure`）。

### 8.2 AsyncWaiter：等待非同步條件成立

很多容器測試是「寫了東西，等系統收斂後再讀」。Java 用 Awaitility，.NET 我們寫了一個
Polly-based 版本：

```csharp
await AsyncWaiter.WaitUntilAsync(async _ =>
{
    var result = await adapter.SearchAsync("iPhone", 1, 10);
    return result.Items.Any(d => d.Id == expectedId);
}, TimeSpan.FromSeconds(30));
```

預設 200ms 輪詢一次，逾時自動拋 `TimeoutException`。

---

## 9. Testcontainers 常見坑與解法

### 9.1 Podman 也可以，不一定要 Docker Desktop

如果你用 Podman，只要把 socket symlink 到 `/var/run/docker.sock`，Testcontainers 會
自動識別：

```bash
podman machine start
sudo ln -sf /Users/<you>/.local/share/containers/podman/machine/podman.sock /var/run/docker.sock
```

執行測試前可以額外設定（避免 Ryuk cleanup container 在 Podman 上偶發失敗）：

```bash
TESTCONTAINERS_RYUK_DISABLED=true dotnet test ...
```

### 9.2 Elasticsearch 8.x 預設走 HTTPS

`docker.elastic.co/elasticsearch/elasticsearch:8.15.0` 即使設了
`xpack.security.enabled=false`，HTTP 監聽器還是會試圖走 TLS。需要明確關掉：

```csharp
.WithEnvironment("xpack.security.http.ssl.enabled", "false")
.WithEnvironment("xpack.security.transport.ssl.enabled", "false")
.WithEnvironment("discovery.type", "single-node")
.WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
```

並且 `ElasticsearchBuilder.GetConnectionString()` 仍會給你 `https://elastic:redacted@...`
這種 URL。**自己組 http URL**：

```csharp
var url = $"http://{containers.Elasticsearch.Hostname}:{containers.Elasticsearch.GetMappedPublicPort(9200)}";
var es = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(url)));
```

### 9.3 Vault dev mode 的 KV mount 點不是 `kv/`

Vault `server -dev` 啟動時，KV v2 secrets engine **掛在 `secret/`，不是 `kv/`**。

```csharp
await admin.V1.Secrets.KeyValue.V2.WriteSecretAsync("payment", data, mountPoint: "secret");
var provider = new VaultSecretProvider(vaultAddress, rootToken, mount: "secret");
```

### 9.4 連續跑多個 Infrastructure 測試專案會偶發失敗

每個測試專案都有自己的 `SharedContainerFixture`，連續跑時 Elasticsearch 在
back-to-back cold boot 偶發 wait timeout。

**解法**：
- 本地：一次只跑一個專案 `dotnet test tests/<single-project>/`
- CI：用 matrix strategy 平行跑（本專案的 `.github/workflows/ci.yml` 已示範）

### 9.5 EF Core 的「找不到合適的建構子」

如果你用 `[primary constructor record]` 或者建構子的參數型別是 `OwnsOne` 的對象
（如 `Money`），EF 會抱怨：

```
No suitable constructor was found for entity type 'Order'.
Cannot bind 'total', 'status' in 'Order(OrderId id, Money total, OrderStatus status)'
```

**解法**：給每個 EF managed entity 加一個 `private parameterless ctor`：

```csharp
private Order() {
    _lines = [];
    Total = Money.Zero("TWD");
    Status = new OrderStatus.Created(DateTime.UtcNow);
}
```

### 9.6 xUnit v3 的 CancellationToken 警告 (xUnit1051)

```
xUnit1051: Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken
```

是 warning-level 但加上 `TreatWarningsAsErrors` 就會 build 失敗。本專案的
`Directory.Build.props` 已把 `xUnit1051` 加進 `<NoWarn>`，要不要遵守看你的偏好。

### 9.7 Reqnroll.xUnit 還鎖在 xUnit v2

Reqnroll 目前的 xUnit adapter 還沒上 v3。本專案的 `tests/BDD.Tests/` 用 `VersionOverride`
把 `xunit.runner.visualstudio` 鎖在 v2，避免衝突。其他測試專案維持 v3。

---

## 10. Profile 切換：同一套程式碼跑三種環境

在 `Program.cs` 裡只有這一段切換：

```csharp
if (builder.Environment.IsProduction())
{
    builder.Services
        .AddRedisCache(cfg["Redis:ConnectionString"]!)
        .AddElasticSearch(cfg["Elasticsearch:Url"]!)
        .AddMinioStorage(/* ... */);
    builder.Services.AddMessaging(MessagingProfile.Kafka);
}
else
{
    builder.Services
        .AddInMemoryCache()
        .AddInMemorySearch()
        .AddInMemoryStorage();
    builder.Services.AddMessaging(MessagingProfile.InMemory);
}
```

整理成表格：

| 環境 | ICachePort | ISearchPort | IObjectStorage | IEventPublisher |
|---|---|---|---|---|
| **Development** | InMemoryCachePort | InMemorySearchAdapter | InMemoryObjectStorage | MassTransit InMemory |
| **Testing**（Testcontainers） | Redis Adapter（指向容器） | Elasticsearch Adapter（指向容器） | Minio Adapter（指向容器） | MassTransit Kafka 或 InMemory |
| **Production** | Redis Adapter（真實 cluster） | Elasticsearch Adapter | Minio / S3 | MassTransit Kafka |

整個 **Domain + Application 層完全沒被觸碰**。要新增一個 Profile（例如「灰度 — Redis +
S3 + Kafka」）也只是再寫一個 if 分支。

---

## 11. 常見錯誤排查清單

| 症狀 | 可能原因 | 怎麼修 |
|---|---|---|
| `NETSDK1057 : 您正在使用 .NET 預覽版本` | 你的 SDK 是 RC | 正常，目前 RC1 build 數字會這樣顯示，可忽略 |
| `error NU1008` | 在 PackageReference 加了 `Version=""` | 拿掉，改在 `Directory.Packages.props` 加 `<PackageVersion>` |
| `error CS0118: 'Lock' 是命名空間，但卻當成類型使用` | 命名空間叫 `Lock`，撞到 C# 13 的 `System.Threading.Lock` | 改用 `System.Threading.Lock` 完整名稱 |
| `Cannot consume scoped service 'X' from singleton 'Y'` | DI 生命週期錯配（如 IEventPublisher 註冊 Singleton 但依賴 IPublishEndpoint 是 Scoped） | 把 publisher 改 `AddScoped` |
| `28000: role "postgres" does not exist` | WebApplicationFactory 沒有正確覆蓋 connection string | 用 `Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", ...)` 覆蓋（環境變數優先序最高） |
| 測試卡住不動，沒有錯誤訊息 | 通常是 Testcontainers 在等容器健康檢查 | `docker ps` / `podman ps` 看容器狀態；ES exit code 70 = 記憶體不足 |
| `No suitable constructor was found for entity type` | EF Core 找不到能 materialise 的建構子 | 加一個 `private SealedClass() {}` |
| `OwnsOne(o => o.Money)` 編譯錯誤、推斷錯誤 | `Money` 是 struct，EF 的 `OwnsOne<T>` 需要 class | `Money` 改 `sealed record`（class 版本） |
| xUnit `xUnit1041: Fixture argument 'X' does not have a fixture source` | `[CollectionDefinition]` 在別的 assembly | 在這個測試 csproj 加一個 `SharedCollection.cs` |

---

## 12. 延伸練習

把這份教程當基底，下列練習可以加深理解：

### Level 1：在不破壞測試的前提下改動

- 把 `RedisCacheAdapter` 換成 `MemoryCacheAdapter`（用 `IMemoryCache`）
  - 寫一個新的 Adapter
  - 在 `Program.cs` 多加一個 Profile
  - **不能改任何 Domain / Application 層程式碼**
- 給 `Order` 加一個 `PartiallyShipped` 狀態
  - 在 `OrderStatus` 加 sealed record
  - 寫對應的狀態轉移方法
  - 加單元測試
  - EF Repository 的 shadow property 邏輯要更新

### Level 2：用 Contract Test 證明你的 Fake 跟 Real 行為一致

`tests/ProductService.Application.Tests/Contract/OrderRepositoryContract.cs` 已經示範了
in-memory 實作怎麼跑契約測試。請你**新增一個** `EfOrderRepositoryContractTests`，
讓同一份契約測試也用 EF Core + Postgres 容器跑一遍。

### Level 3：替換 Messaging Transport

把所有 Kafka 換成 RabbitMQ：

```csharp
x.AddRider(r => r.UsingRabbitMq(...));   // 取代 UsingKafka
```

預期：**Consumer 程式碼一行不動**，只改 DI 註冊。如果你需要改 Domain / Application 層，
代表你 Port 抽象有破口，需要修正。

### Level 4：加上 Saga 模式

目前 `OrderCreated → ProcessPayment → DeductStock` 是直線流程。把它改寫成 MassTransit
StateMachine Saga，處理：

- 支付成功但庫存不足 → 退款 + 訂單 Refunded
- 支付逾時 → 訂單 Cancelled
- 庫存配給逾時 → 釋放預留 + 退款

E2E 測試在第 5 章已經預留了 `Saga 補償` 的描述，你來把它寫成可執行的測試。

---

## 進一步閱讀

- [Testcontainers for .NET 官方文件](https://dotnet.testcontainers.org/)
- [Microsoft Learn — Domain-Driven Design](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/)
- [MassTransit 文件](https://masstransit.io/) — 特別是 Saga State Machine 章節
- [MediatR 維護者的文章](https://jimmybogard.com/) — 為什麼用 MediatR、用過頭的危險
- [Hexagonal Architecture 原文（Alistair Cockburn）](https://alistair.cockburn.us/hexagonal-architecture/)

---

## 授權與貢獻

這份教程的目的是**教學**。歡迎複製、改寫、用於你的講義或 workshop。如果你發現坑、想到
更好的範例、或者要加一章，PR 永遠歡迎。

> 設計藍圖：[`testcontainers-tutorial-plan-dotnet.md`](testcontainers-tutorial-plan-dotnet.md)
> 章節記憶體（開發過程踩到的雷）：`~/.claude/projects/.../memory/`
