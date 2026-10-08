using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Users.Api.Tests;

public sealed class UsersEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_ReturnsPagedUsers()
    {
        var response = await _client.GetAsync(new Uri("/api/users?page=2&pageSize=10", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(10, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(25, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, body.GetProperty("totalPages").GetInt32());
        var items = body.GetProperty("items");
        Assert.Equal(10, items.GetArrayLength());
        var first = items[0];
        Assert.Equal("User 11", first.GetProperty("fullName").GetString());
        Assert.Equal("user11@example.com", first.GetProperty("email").GetString());
        Assert.Equal("Active", first.GetProperty("status").GetString());
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("createdAt", out _));
    }

    [Fact]
    public async Task Get_WithoutQuery_UsesDefaultPaging()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>(new Uri("/api/users", UriKind.Relative));

        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("/api/users?page=0")]
    [InlineData("/api/users?pageSize=0")]
    [InlineData("/api/users?pageSize=101")]
    [InlineData("/api/users?page=abc")]
    public async Task Get_WithInvalidPaging_ReturnsBadRequestProblemDetails(string url)
    {
        var response = await _client.GetAsync(new Uri(url, UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Get_BeyondLastPage_ReturnsEmptyItems()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>(new Uri("/api/users?page=99", UriKind.Relative));

        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
        Assert.Equal(25, body.GetProperty("totalCount").GetInt32());
    }
}
