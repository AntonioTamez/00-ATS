namespace Shop.Domain.Model;

/// <summary>A business rule was violated. Raised by the model; never depends on HTTP or persistence.</summary>
public sealed class DomainException(string message) : Exception(message);

/// <summary>Value object: immutable, compared by value, never negative.</summary>
public readonly record struct Money
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0)
            throw new DomainException("Money cannot be negative.");
        Amount = decimal.Round(amount, 2);
    }

    public static Money Zero => new(0m);

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);

    public Money Times(int quantity) => new(Amount * quantity);
    public Money Percent(decimal percent) => new(Amount * percent / 100m);
}
