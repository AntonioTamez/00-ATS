using Users.Domain.Common;

namespace Users.Domain.Users;

/// <summary>Aggregate root. State changes only through behavior; no public setters.</summary>
public sealed class User
{
    private User(UserId id, FullName name, Email email, UserStatus status, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Email = email;
        Status = status;
        CreatedAt = createdAt;
    }

    public UserId Id { get; }

    public FullName Name { get; private set; }

    public Email Email { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Creates a brand-new active user, enforcing creation invariants.</summary>
    public static User Register(FullName name, Email email, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new User(UserId.New(), name, email, UserStatus.Active, timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Reconstitutes a persisted user. Intended for repositories only: the data is trusted
    /// (already validated when it was first registered), so no transition rules are applied.
    /// </summary>
    public static User Rehydrate(UserId id, FullName name, Email email, UserStatus status, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(email);

        return new User(id, name, email, status, createdAt);
    }

    public void Deactivate()
    {
        if (Status == UserStatus.Inactive)
        {
            throw new DomainException("User is already inactive.");
        }

        Status = UserStatus.Inactive;
    }

    public void Activate()
    {
        if (Status == UserStatus.Active)
        {
            throw new DomainException("User is already active.");
        }

        Status = UserStatus.Active;
    }

    public void ChangeEmail(Email email)
    {
        ArgumentNullException.ThrowIfNull(email);
        EnsureActive();
        Email = email;
    }

    public void Rename(FullName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        EnsureActive();
        Name = name;
    }

    private void EnsureActive()
    {
        if (Status != UserStatus.Active)
        {
            throw new DomainException("Inactive users cannot be modified.");
        }
    }
}
