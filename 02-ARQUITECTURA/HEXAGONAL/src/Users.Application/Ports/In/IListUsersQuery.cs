using Users.Application.ListUsers;

namespace Users.Application.Ports.In;

/// <summary>Driving (primary) port: how the outside world asks the core for a page of users.</summary>
public interface IListUsersQuery
{
    Task<PagedResult<UserSummary>> ExecuteAsync(ListUsersRequest request, CancellationToken cancellationToken);
}
