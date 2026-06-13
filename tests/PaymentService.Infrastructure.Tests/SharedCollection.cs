using TestInfrastructure.Containers;
using Xunit;

namespace PaymentService.Infrastructure.Tests;

[CollectionDefinition("SharedContainers")]
public sealed class SharedCollection : ICollectionFixture<SharedContainerFixture> { }
