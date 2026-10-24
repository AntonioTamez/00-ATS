using Users.Domain.Users;

namespace Users.Infrastructure.Persistence;

/// <summary>Persistence model. Kept separate from the domain aggregate so storage concerns never leak into the core.</summary>
internal sealed record UserRecord(Guid Id, string FullName, string Email, int Status, DateTimeOffset CreatedAt)
{
    public static UserRecord From(User user) =>
        new(user.Id.Value, user.Name.Value, user.Email.Value, (int)user.Status, user.CreatedAt);

    public User ToDomain() =>
        User.Rehydrate(
            UserId.Create(Id),
            Domain.Users.FullName.Create(FullName),
            Domain.Users.Email.Create(Email),
            (UserStatus)Status,
            CreatedAt);
}
