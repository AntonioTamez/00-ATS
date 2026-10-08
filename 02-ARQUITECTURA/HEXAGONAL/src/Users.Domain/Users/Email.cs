using Users.Domain.Common;

namespace Users.Domain.Users;

public sealed record Email
{
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value))
        {
            throw new DomainException("Email cannot be empty.");
        }

        if (value.Length > MaxLength || !IsWellFormed(value))
        {
            throw new DomainException($"'{value}' is not a valid email address.");
        }

        return new Email(value);
    }

    public override string ToString() => Value;

    private static bool IsWellFormed(string value)
    {
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@'))
        {
            return false;
        }

        var domain = value[(at + 1)..];
        var dot = domain.IndexOf('.', StringComparison.Ordinal);
        return dot > 0
            && !domain.EndsWith('.')
            && !value.Any(char.IsWhiteSpace);
    }
}
