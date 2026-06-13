namespace PaymentService.Domain.Model;

public abstract record PaymentStatus
{
    private PaymentStatus() { }

    public sealed record Pending(DateTime AtUtc) : PaymentStatus;
    public sealed record Completed(DateTime AtUtc, string ReceiptKey) : PaymentStatus;
    public sealed record Failed(DateTime AtUtc, string Reason) : PaymentStatus;
    public sealed record Refunded(DateTime AtUtc, string Reason) : PaymentStatus;

    public string Name => GetType().Name;
}
