using Users.Domain.Common;

namespace Users.Domain.Users;

public sealed record UserId
{
    private UserId(Guid value) => Value = value;

    public Guid Value { get; }

    public static UserId New() => new(Guid.NewGuid());

    public static UserId Create(Guid value) =>
        value == Guid.Empty ? throw new DomainException("User id cannot be empty.") : new UserId(value);

    public override string ToString() => Value.ToString();
}
