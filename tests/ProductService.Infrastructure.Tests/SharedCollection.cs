using TestInfrastructure.Containers;
using Xunit;

namespace ProductService.Infrastructure.Tests;

// xUnit requires CollectionDefinition in the test assembly. Re-export the shared fixture.
[CollectionDefinition("SharedContainers")]
public sealed class SharedCollection : ICollectionFixture<SharedContainerFixture> { }
