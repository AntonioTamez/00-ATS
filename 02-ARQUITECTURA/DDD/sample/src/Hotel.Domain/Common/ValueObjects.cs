namespace Hotel.Domain.Common;

/// <summary>An amount in a currency. Immutable; operations return new values and have no side effects.</summary>
public readonly record struct Money : IValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException("Money cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new DomainException("A currency must be a 3-letter code.");

        Amount = decimal.Round(amount, 2);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money a, Money b)
    {
        a.EnsureSameCurrency(b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        a.EnsureSameCurrency(b);
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public Money Times(int quantity) => new(Amount * quantity, Currency);

    public Money Percent(decimal percent) => new(Amount * percent / 100m, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"Cannot combine {Currency} with {other.Currency}.");
    }
}

/// <summary>A stay: check-in is included, check-out is the departure day (not an occupied night).</summary>
public readonly record struct DateRange : IValueObject
{
    public DateOnly CheckIn { get; }
    public DateOnly CheckOut { get; }

    public DateRange(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn)
            throw new DomainException("Check-out must be after check-in.");

        CheckIn = checkIn;
        CheckOut = checkOut;
    }

    public int Nights => CheckOut.DayNumber - CheckIn.DayNumber;

    public bool Overlaps(DateRange other) => CheckIn < other.CheckOut && other.CheckIn < CheckOut;

    public bool Includes(DateOnly day) => day >= CheckIn && day < CheckOut;
}

/// <summary>Who is staying. Validated on creation, so an invalid guest cannot exist.</summary>
public readonly record struct GuestInfo : IValueObject
{
    public string Name { get; }
    public string Email { get; }

    public GuestInfo(string name, string email)
    {
        var n = name?.Trim() ?? string.Empty;
        var e = email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (n.Length == 0)
            throw new DomainException("A guest name is required.");
        var at = e.IndexOf('@');
        if (at < 1 || at != e.LastIndexOf('@') || at == e.Length - 1)
            throw new DomainException("A valid guest e-mail is required.");

        Name = n;
        Email = e;
    }
}
