using Users.Application.Ports.In;
using Users.Application.Ports.Out;
using Users.Domain.Users;

namespace Users.Application.ListUsers;

/// <summary>Orchestrates the use case only: fetch a page of aggregates and project them to summaries.</summary>
public sealed class ListUsersHandler(IUserRepository repository) : IListUsersQuery
{
    public async Task<PagedResult<UserSummary>> ExecuteAsync(ListUsersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (users, total) = await repository.ListAsync(request.Page, request.PageSize, cancellationToken).ConfigureAwait(false);

        return new PagedResult<UserSummary>([.. users.Select(ToSummary)], request.Page, request.PageSize, total);
    }

    private static UserSummary ToSummary(User user) =>
        new(user.Id.Value, user.Name.Value, user.Email.Value, user.Status.ToString(), user.CreatedAt);
}
