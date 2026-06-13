using FluentValidation;
using InventoryService.Domain.Model;
using InventoryService.Domain.Ports;
using InventoryService.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using SharedKernel.Domain.Events;
using SharedKernel.Domain.Ports;

namespace InventoryService.Application.Commands;

public sealed record DeductStockLineDto(Guid ProductId, int Quantity);

public sealed record DeductStockCommand(
    Guid OrderId,
    Guid PaymentId,
    IReadOnlyList<DeductStockLineDto> Lines) : IRequest<bool>;

public sealed class DeductStockCommandValidator : AbstractValidator<DeductStockCommand>
{
    public DeductStockCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEqual(Guid.Empty);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

public sealed class DeductStockCommandHandler(
    IStockWriteRepository stocks,
    IDistributedLock distributedLock,
    StockAllocationService allocator,
    IEventPublisher publisher,
    TimeProvider clock,
    ILogger<DeductStockCommandHandler> logger)
    : IRequestHandler<DeductStockCommand, bool>
{
    public async Task<bool> Handle(DeductStockCommand cmd, CancellationToken ct)
    {
        var productIds = cmd.Lines.Select(l => new ProductId(l.ProductId)).Distinct().ToList();

        // Order lock keys to avoid deadlocks
        var lockKeys = productIds
            .Select(p => $"stock:{p.Value}")
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var heldLocks = new List<IAsyncDisposable>();
        try
        {
            foreach (var key in lockKeys)
            {
                var l = await distributedLock.AcquireAsync(
                    key, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), ct);
                heldLocks.Add(l);
            }

            var loaded = await stocks.GetManyAsync(productIds, ct);
            var map = loaded.ToDictionary(s => s.ProductId);
            foreach (var pid in productIds)
            {
                if (!map.ContainsKey(pid))
                {
                    await PublishFailureAsync(cmd, $"Unknown product {pid.Value}", ct);
                    return false;
                }
            }

            var allocLines = cmd.Lines
                .Select(l => new AllocationLine(new ProductId(l.ProductId), l.Quantity))
                .ToList();

            try
            {
                allocator.AllocateAll(allocLines, map);
                allocator.CommitAll(allocLines, map);
            }
            catch (DomainException ex)
            {
                logger.LogWarning(ex, "Stock allocation failed for order {OrderId}", cmd.OrderId);
                await PublishFailureAsync(cmd, ex.Message, ct);
                return false;
            }

            foreach (var s in map.Values) await stocks.UpdateAsync(s, ct);
            await stocks.SaveChangesAsync(ct);

            var integration = new InventoryDeductedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredAtUtc: clock.GetUtcNow().UtcDateTime,
                OrderId: cmd.OrderId,
                Lines: [.. cmd.Lines.Select(l =>
                    new OrderLinePayload(l.ProductId, l.Quantity, 0m))]);
            await publisher.PublishAsync(integration, ct);
            return true;
        }
        finally
        {
            // Release in reverse order
            heldLocks.Reverse();
            foreach (var l in heldLocks)
                try { await l.DisposeAsync(); } catch (Exception ex)
                { logger.LogWarning(ex, "Failed to release distributed lock"); }
        }
    }

    private Task PublishFailureAsync(DeductStockCommand cmd, string reason, CancellationToken ct)
        => publisher.PublishAsync(new InventoryDeductionFailedIntegrationEvent(
            Guid.NewGuid(), clock.GetUtcNow().UtcDateTime, cmd.OrderId, cmd.PaymentId, reason), ct);
}
