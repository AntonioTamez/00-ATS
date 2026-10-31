using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Helpdesk.Tests;

public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Created(Guid Id, string Title, string Status);
    private sealed record Page(List<Row> Items, int Total);
    private sealed record Row(Guid Id, string Title, string Status);

    // A distinct database per test keeps the end-to-end tests independent.
    private HttpClient Client() =>
        factory.WithWebHostBuilder(b => b.UseSetting("Database:Name", $"api-{Guid.NewGuid():N}")).CreateClient();

    [Fact]
    public async Task Open_Assign_Close_Get_RoundTrip()
    {
        var client = Client();

        var open = await client.PostAsJsonAsync("/api/tickets", new { title = "VPN down", description = "since 9am" });
        Assert.Equal(HttpStatusCode.Created, open.StatusCode);
        var ticket = (await open.Content.ReadFromJsonAsync<Created>())!;
        Assert.Equal($"/api/tickets/{ticket.Id}", open.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign", new { assignee = "Ana" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/close", new { resolution = "Restarted gateway" })).StatusCode);

        var fetched = await client.GetFromJsonAsync<Created>($"/api/tickets/{ticket.Id}");
        Assert.Equal("Closed", fetched!.Status);
    }

    [Fact]
    public async Task Open_EmptyTitle_Returns422()
    {
        var response = await Client().PostAsJsonAsync("/api/tickets", new { title = "" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Get_Unknown_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync($"/api/tickets/{Guid.NewGuid()}")).StatusCode);

    [Fact]
    public async Task Close_Twice_Returns422()
    {
        var client = Client();
        var ticket = (await (await client.PostAsJsonAsync("/api/tickets", new { title = "X" })).Content.ReadFromJsonAsync<Created>())!;
        await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/close", new { resolution = "done" });

        var again = await client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/close", new { resolution = "done" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
    }

    [Fact]
    public async Task List_FiltersAndPages()
    {
        var client = Client();
        foreach (var title in new[] { "A", "B", "C" })
            await client.PostAsJsonAsync("/api/tickets", new { title });

        var page = await client.GetFromJsonAsync<Page>("/api/tickets?page=1&pageSize=2");

        Assert.Equal(3, page!.Total);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task List_InvalidPageSize_Returns400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Client().GetAsync("/api/tickets?pageSize=500")).StatusCode);
}
