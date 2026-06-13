using TestInfrastructure.Containers;
using Xunit;

namespace InventoryService.Infrastructure.Tests;

[CollectionDefinition("SharedContainers")]
public sealed class SharedCollection : ICollectionFixture<SharedContainerFixture> { }
