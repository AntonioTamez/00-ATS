using Microsoft.Extensions.Time.Testing;
using Users.Application.ListUsers;
using Users.Application.Ports.Out;
using Users.Domain.Users;

namespace Users.Application.Tests;

public sealed class ListUsersHandlerTests
{
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));

    private static User UserNamed(string name) =>
        User.Register(FullName.Create(name), Email.Create($"{name.ToUpperInvariant()}@example.com"), Clock);

    [Fact]
    public async Task ExecuteAsync_MapsUsersToSummaries()
    {
        var user = UserNamed("ana");
        var handler = new ListUsersHandler(new FakeRepository([user]));

        var result = await handler.ExecuteAsync(ListUsersRequest.Create(1, 10), CancellationToken.None);

        var summary = Assert.Single(result.Items);
        Assert.Equal(user.Id.Value, summary.Id);
        Assert.Equal("ana", summary.FullName);
        Assert.Equal("ana@example.com", summary.Email);
        Assert.Equal("Active", summary.Status);
        Assert.Equal(Clock.GetUtcNow(), summary.CreatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEmptyPageWhenNoUsers()
    {
        var handler = new ListUsersHandler(new FakeRepository([]));

        var result = await handler.ExecuteAsync(ListUsersRequest.Default, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task ExecuteAsync_PassesPagingToRepositoryAndReportsMetadata()
    {
        var users = Enumerable.Range(1, 5).Select(i => UserNamed($"user{i}")).ToList();
        var repository = new FakeRepository(users);
        var handler = new ListUsersHandler(repository);

        var result = await handler.ExecuteAsync(ListUsersRequest.Create(2, 2), CancellationToken.None);

        Assert.Equal((2, 2), repository.LastPaging);
        Assert.Equal(["user3", "user4"], result.Items.Select(i => i.FullName));
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellationToken()
    {
        var repository = new FakeRepository([]);
        using var cts = new CancellationTokenSource();

        await new ListUsersHandler(repository).ExecuteAsync(ListUsersRequest.Default, cts.Token);

        Assert.Equal(cts.Token, repository.LastToken);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsNullRequest() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new ListUsersHandler(new FakeRepository([])).ExecuteAsync(null!, CancellationToken.None));

    private sealed class FakeRepository(IReadOnlyList<User> users) : IUserRepository
    {
        public (int Page, int PageSize)? LastPaging { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<(IReadOnlyList<User> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            LastPaging = (page, pageSize);
            LastToken = cancellationToken;
            IReadOnlyList<User> items = [.. users.Skip((page - 1) * pageSize).Take(pageSize)];
            return Task.FromResult((items, users.Count));
        }
    }
}
