using Microsoft.AspNetCore.Mvc;
using Users.Api.Contracts;
using Users.Application.ListUsers;
using Users.Application.Ports.In;

namespace Users.Api.Controllers;

/// <summary>Inbound adapter. Depends on the driving port only.</summary>
[ApiController]
[Route("api/users")]
public sealed class UsersController(IListUsersQuery listUsers) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ListUsersRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var request = ListUsersRequest.Create(page, pageSize);
        var result = await listUsers.ExecuteAsync(request, cancellationToken);

        return Ok(new PagedResponse<UserResponse>(
            [.. result.Items.Select(u => new UserResponse(u.Id, u.FullName, u.Email, u.Status, u.CreatedAt))],
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }
}
