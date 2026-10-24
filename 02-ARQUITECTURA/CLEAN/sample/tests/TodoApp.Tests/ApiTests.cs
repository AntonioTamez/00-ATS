using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TodoApp.Application.Todos;

namespace TodoApp.Tests;

public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Post_Then_Complete_Then_List()
    {
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/todos", new { title = "Learn Clean Architecture" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = (await created.Content.ReadFromJsonAsync<TodoDto>())!;

        var completed = await client.PostAsync($"/api/todos/{dto.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        var list = await client.GetFromJsonAsync<List<TodoDto>>("/api/todos");
        Assert.Contains(list!, t => t.Id == dto.Id && t.IsCompleted);
    }

    [Fact]
    public async Task Post_EmptyTitle_Returns422()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/todos", new { title = "" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Complete_UnknownId_Returns404()
    {
        var response = await factory.CreateClient().PostAsync($"/api/todos/{Guid.NewGuid()}/complete", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
