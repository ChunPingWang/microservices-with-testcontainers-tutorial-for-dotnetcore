namespace SharedKernel.Domain.ValueObjects;

public sealed record Quantity(int Value)
{
    public static readonly Quantity Zero = new(0);

    public static Quantity Of(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Quantity cannot be negative.");
        return new Quantity(value);
    }

    public Quantity Add(Quantity other) => Of(Value + other.Value);

    public Quantity Subtract(Quantity other)
    {
        if (Value < other.Value)
            throw new InvalidOperationException("Quantity would be negative.");
        return new Quantity(Value - other.Value);
    }

    public static Quantity operator +(Quantity a, Quantity b) => a.Add(b);
    public static Quantity operator -(Quantity a, Quantity b) => a.Subtract(b);

    public override string ToString() => Value.ToString();
}
