using Users.Application.Ports.Out;
using Users.Domain.Users;

namespace Users.Infrastructure.Persistence;

/// <summary>First driven adapter: an in-memory store seeded through genuine domain behavior.</summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private const int SeedCount = 25;

    private readonly List<UserRecord> _records;

    public InMemoryUserRepository(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _records = Seed(timeProvider);
    }

    public Task<(IReadOnlyList<User> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<User> items = [.. _records.Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.ToDomain())];
        return Task.FromResult((items, _records.Count));
    }

    private static List<UserRecord> Seed(TimeProvider timeProvider)
    {
        var records = new List<UserRecord>(SeedCount);
        for (var i = 1; i <= SeedCount; i++)
        {
            var user = User.Register(
                FullName.Create($"User {i:00}"),
                Email.Create($"user{i:00}@example.com"),
                timeProvider);

            if (i % 5 == 0)
            {
                user.Deactivate();
            }

            records.Add(UserRecord.From(user));
        }

        return records;
    }
}
