using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Model;
using PaymentService.Infrastructure.Persistence;
using SharedKernel.Domain.ValueObjects;
using TestInfrastructure.Containers;
using Xunit;

namespace PaymentService.Infrastructure.Tests;

[Collection("SharedContainers")]
public class EfPaymentRepositoryTests(SharedContainerFixture containers)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task IdempotencyLookup_FindsExistingPayment()
    {
        await using var db = NewContext(containers);
        await db.Database.EnsureCreatedAsync();
        var repo = new EfPaymentRepository(db);

        var payment = Payment.Create(new OrderId(Guid.NewGuid()),
            new Money(199m, "TWD"), IdempotencyKey.Of("order-abc"));
        await repo.AddAsync(payment);

        await using var db2 = NewContext(containers);
        var repo2 = new EfPaymentRepository(db2);
        var found = await repo2.FindByIdempotencyAsync(IdempotencyKey.Of("order-abc"));

        found.Should().NotBeNull();
        found!.Id.Should().Be(payment.Id);
        found.Status.Should().BeOfType<PaymentStatus.Pending>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Completion_Roundtrip_PersistsReceiptKey()
    {
        await using var db = NewContext(containers);
        await db.Database.EnsureCreatedAsync();
        var repo = new EfPaymentRepository(db);

        var payment = Payment.Create(new OrderId(Guid.NewGuid()),
            new Money(50m, "TWD"), IdempotencyKey.Of(Guid.NewGuid().ToString()));
        await repo.AddAsync(payment);
        payment.MarkCompleted("receipts/x.txt");
        await repo.UpdateAsync(payment);

        await using var db2 = NewContext(containers);
        var loaded = await new EfPaymentRepository(db2).FindAsync(payment.Id);
        loaded!.Status.Should().BeOfType<PaymentStatus.Completed>()
            .Which.ReceiptKey.Should().Be("receipts/x.txt");
    }

    private static PaymentDbContext NewContext(SharedContainerFixture fx)
    {
        var opts = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(fx.PaymentsDb.GetConnectionString())
            .Options;
        return new PaymentDbContext(opts);
    }
}
