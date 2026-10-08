using Microsoft.Extensions.Time.Testing;
using Users.Domain.Common;
using Users.Domain.Users;

namespace Users.Domain.Tests;

public sealed class UserTests
{
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));

    private static User NewUser() =>
        User.Register(FullName.Create("Ana Lopez"), Email.Create("ana@example.com"), Clock);

    [Fact]
    public void Register_CreatesActiveUserWithGeneratedIdAndTimestamp()
    {
        var user = NewUser();

        Assert.NotEqual(Guid.Empty, user.Id.Value);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(Clock.GetUtcNow(), user.CreatedAt);
        Assert.Equal("Ana Lopez", user.Name.Value);
    }

    [Fact]
    public void Deactivate_ChangesStatus()
    {
        var user = NewUser();
        user.Deactivate();
        Assert.Equal(UserStatus.Inactive, user.Status);
    }

    [Fact]
    public void Deactivate_Twice_Throws()
    {
        var user = NewUser();
        user.Deactivate();
        Assert.Throws<DomainException>(user.Deactivate);
    }

    [Fact]
    public void Activate_WhenActive_Throws() =>
        Assert.Throws<DomainException>(NewUser().Activate);

    [Fact]
    public void Activate_AfterDeactivate_RestoresActive()
    {
        var user = NewUser();
        user.Deactivate();
        user.Activate();
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void ChangeEmail_UpdatesEmail()
    {
        var user = NewUser();
        user.ChangeEmail(Email.Create("new@example.com"));
        Assert.Equal("new@example.com", user.Email.Value);
    }

    [Fact]
    public void ChangeEmail_WhenInactive_Throws()
    {
        var user = NewUser();
        user.Deactivate();
        Assert.Throws<DomainException>(() => user.ChangeEmail(Email.Create("new@example.com")));
    }

    [Fact]
    public void Rename_UpdatesName()
    {
        var user = NewUser();
        user.Rename(FullName.Create("Ana Maria"));
        Assert.Equal("Ana Maria", user.Name.Value);
    }

    [Fact]
    public void Rename_WhenInactive_Throws()
    {
        var user = NewUser();
        user.Deactivate();
        Assert.Throws<DomainException>(() => user.Rename(FullName.Create("Other")));
    }

    [Fact]
    public void Rehydrate_RestoresStateWithoutChangingIt()
    {
        var id = UserId.New();
        var at = new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero);

        var user = User.Rehydrate(id, FullName.Create("Bob"), Email.Create("bob@example.com"), UserStatus.Inactive, at);

        Assert.Equal(id, user.Id);
        Assert.Equal(UserStatus.Inactive, user.Status);
        Assert.Equal(at, user.CreatedAt);
    }
}
