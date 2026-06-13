using SharedKernel.Domain;

namespace PaymentService.Domain.Model;

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct OrderId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public sealed record IdempotencyKey(string Value)
{
    public static IdempotencyKey Of(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 128)
            throw new DomainException("Idempotency key must be 1-128 chars.");
        return new IdempotencyKey(raw);
    }

    public override string ToString() => Value;
}
