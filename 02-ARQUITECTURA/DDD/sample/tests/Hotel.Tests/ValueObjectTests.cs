using Hotel.Domain.Common;

namespace Hotel.Tests;

/// <summary>Value objects: compared by value, immutable, valid by construction.</summary>
public class ValueObjectTests
{
    private static DateOnly D(int day) => new(2026, 3, day);

    // --- Money

    [Fact]
    public void Money_WithSameAmountAndCurrency_AreEqual() =>
        Assert.Equal(new Money(10m, "eur"), new Money(10m, "EUR"));

    [Fact]
    public void Money_RoundsToCents() => Assert.Equal(3.33m, new Money(3.333m, "EUR").Amount);

    [Fact]
    public void Money_Negative_Throws() => Assert.Throws<DomainException>(() => new Money(-1m, "EUR"));

    [Theory]
    [InlineData("")]
    [InlineData("EURO")]
    public void Money_InvalidCurrency_Throws(string currency) =>
        Assert.Throws<DomainException>(() => new Money(1m, currency));

    [Fact]
    public void Money_AddingDifferentCurrencies_Throws() =>
        Assert.Throws<DomainException>(() => new Money(1m, "EUR") + new Money(1m, "USD"));

    [Fact]
    public void Money_OperationsReturnNewValues_AndLeaveTheOriginalUntouched()
    {
        var price = new Money(100m, "EUR");

        var discounted = price - price.Percent(10m);

        Assert.Equal(90m, discounted.Amount);
        Assert.Equal(100m, price.Amount);
    }

    // --- DateRange

    [Fact]
    public void DateRange_CountsNights() => Assert.Equal(3, new DateRange(D(1), D(4)).Nights);

    [Theory]
    [InlineData(5, 5)]
    [InlineData(5, 4)]
    public void DateRange_CheckOutNotAfterCheckIn_Throws(int checkIn, int checkOut) =>
        Assert.Throws<DomainException>(() => new DateRange(D(checkIn), D(checkOut)));

    [Fact]
    public void DateRange_BackToBackStays_DoNotOverlap() =>
        Assert.False(new DateRange(D(1), D(4)).Overlaps(new DateRange(D(4), D(6))));

    [Fact]
    public void DateRange_PartialOverlap_Overlaps() =>
        Assert.True(new DateRange(D(1), D(5)).Overlaps(new DateRange(D(4), D(8))));

    [Fact]
    public void DateRange_Includes_CheckInButNotCheckOut()
    {
        var stay = new DateRange(D(1), D(4));

        Assert.True(stay.Includes(D(1)));
        Assert.False(stay.Includes(D(4)));
    }

    // --- GuestInfo

    [Fact]
    public void GuestInfo_NormalizesNameAndEmail()
    {
        var guest = new GuestInfo("  Ana ", " ANA@Mail.com ");

        Assert.Equal("Ana", guest.Name);
        Assert.Equal("ana@mail.com", guest.Email);
    }

    [Theory]
    [InlineData("", "a@b.com")]
    [InlineData("Ana", "")]
    [InlineData("Ana", "not-an-email")]
    [InlineData("Ana", "@b.com")]
    [InlineData("Ana", "a@")]
    [InlineData("Ana", "a@@b.com")]
    public void GuestInfo_Invalid_Throws(string name, string email) =>
        Assert.Throws<DomainException>(() => new GuestInfo(name, email));
}
