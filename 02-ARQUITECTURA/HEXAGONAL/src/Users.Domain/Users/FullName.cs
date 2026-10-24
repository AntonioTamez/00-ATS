using Users.Domain.Common;

namespace Users.Domain.Users;

public sealed record FullName
{
    public const int MaxLength = 100;

    private FullName(string value) => Value = value;

    public string Value { get; }

    public static FullName Create(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new DomainException("Full name cannot be blank.");
        }

        return value.Length > MaxLength
            ? throw new DomainException($"Full name cannot exceed {MaxLength} characters.")
            : new FullName(value);
    }

    public override string ToString() => Value;
}
