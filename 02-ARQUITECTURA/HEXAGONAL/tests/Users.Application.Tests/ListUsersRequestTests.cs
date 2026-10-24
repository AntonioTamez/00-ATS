using Users.Application.Common;
using Users.Application.ListUsers;

namespace Users.Application.Tests;

public sealed class ListUsersRequestTests
{
    [Fact]
    public void Create_AcceptsValidPaging()
    {
        var request = ListUsersRequest.Create(2, 50);

        Assert.Equal(2, request.Page);
        Assert.Equal(50, request.PageSize);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    public void Create_AcceptsBoundaries(int page, int pageSize) =>
        Assert.Equal(pageSize, ListUsersRequest.Create(page, pageSize).PageSize);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(1, -5)]
    public void Create_RejectsInvalidPaging(int page, int pageSize) =>
        Assert.Throws<RequestValidationException>(() => ListUsersRequest.Create(page, pageSize));

    [Fact]
    public void Default_UsesFirstPageAndDefaultSize()
    {
        Assert.Equal(1, ListUsersRequest.Default.Page);
        Assert.Equal(ListUsersRequest.DefaultPageSize, ListUsersRequest.Default.PageSize);
    }
}
