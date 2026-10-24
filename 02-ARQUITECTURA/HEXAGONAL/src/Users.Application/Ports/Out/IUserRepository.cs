using Users.Domain.Users;

namespace Users.Application.Ports.Out;

/// <summary>Driven (secondary) port: what the core needs from persistence.</summary>
public interface IUserRepository
{
    Task<(IReadOnlyList<User> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
