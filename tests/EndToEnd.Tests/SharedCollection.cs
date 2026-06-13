using TestInfrastructure.Containers;
using Xunit;

namespace EndToEnd.Tests;

[CollectionDefinition("SharedContainers")]
public sealed class SharedCollection : ICollectionFixture<SharedContainerFixture> { }
