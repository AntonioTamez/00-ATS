using Users.Application.Common;

namespace Users.Application.ListUsers;

/// <summary>Paging input of the list-users use case. Validated at creation (application-level guard).</summary>
public sealed record ListUsersRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private ListUsersRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    public static ListUsersRequest Default { get; } = new(1, DefaultPageSize);

    public int Page { get; }

    public int PageSize { get; }

    public static ListUsersRequest Create(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new RequestValidationException("Page must be greater than or equal to 1.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new RequestValidationException($"Page size must be between 1 and {MaxPageSize}.");
        }

        return new ListUsersRequest(page, pageSize);
    }
}
