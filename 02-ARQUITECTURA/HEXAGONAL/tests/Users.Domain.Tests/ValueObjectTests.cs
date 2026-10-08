using Users.Domain.Common;
using Users.Domain.Users;

namespace Users.Domain.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void UserId_Create_RejectsEmptyGuid() =>
        Assert.Throws<DomainException>(() => UserId.Create(Guid.Empty));

    [Fact]
    public void UserId_New_IsNeverEmpty() =>
        Assert.NotEqual(Guid.Empty, UserId.New().Value);

    [Fact]
    public void UserId_EqualityIsByValue()
    {
        var guid = Guid.NewGuid();
        Assert.Equal(UserId.Create(guid), UserId.Create(guid));
    }

    [Fact]
    public void Email_IsTrimmedAndLowerCased() =>
        Assert.Equal("ana@example.com", Email.Create("  Ana@Example.COM ").Value);

    [Fact]
    public void Email_EqualityIsByNormalizedValue() =>
        Assert.Equal(Email.Create("A@b.com"), Email.Create("a@B.com"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("two@@example.com")]
    [InlineData("@example.com")]
    [InlineData("ana@")]
    [InlineData("ana@nodot")]
    [InlineData("an a@example.com")]
    public void Email_RejectsInvalidValues(string raw) =>
        Assert.Throws<DomainException>(() => Email.Create(raw));

    [Fact]
    public void Email_RejectsNull() =>
        Assert.Throws<DomainException>(() => Email.Create(null));

    [Fact]
    public void FullName_IsTrimmed() =>
        Assert.Equal("Ana Lopez", FullName.Create("  Ana Lopez ").Value);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FullName_RejectsBlank(string raw) =>
        Assert.Throws<DomainException>(() => FullName.Create(raw));

    [Fact]
    public void FullName_RejectsTooLong() =>
        Assert.Throws<DomainException>(() => FullName.Create(new string('a', FullName.MaxLength + 1)));

    [Fact]
    public void FullName_AcceptsMaxLength() =>
        Assert.Equal(FullName.MaxLength, FullName.Create(new string('a', FullName.MaxLength)).Value.Length);
}
