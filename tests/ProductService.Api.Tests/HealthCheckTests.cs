using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TestInfrastructure.Containers;
using Xunit;

namespace ProductService.Api.Tests;

[Collection("SharedContainers")]
public class HealthCheckTests(SharedContainerFixture containers)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_ReturnsHealthy_OverWebApplicationFactory()
    {
        await using var factory = new TestProductFactory(containers);
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    private sealed class TestProductFactory(SharedContainerFixture fx)
        : WebApplicationFactory<ProductService.Api.Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Env-var wins over appsettings.json no matter which order config providers run
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__Postgres", fx.ProductsDb.GetConnectionString());
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}

[CollectionDefinition("SharedContainers")]
public sealed class SharedCollection : Xunit.ICollectionFixture<SharedContainerFixture> { }
