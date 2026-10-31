using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lending.Tests;

/// <summary>All three layers, through HTTP.</summary>
public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Created(int Id);

    private static async Task<int> PostForIdAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private HttpClient Fresh() => factory.WithWebHostBuilder(_ => { }).CreateClient();

    [Fact]
    public async Task ABookCanBeAdded_AndFound()
    {
        var client = Fresh();

        var id = await PostForIdAsync(client, "/books", new { title = "DDD", author = "Evans" });
        var book = await client.GetFromJsonAsync<JsonElement>($"/books/{id}");

        Assert.Equal(("DDD", true), (book.GetProperty("title").GetString(), book.GetProperty("available").GetBoolean()));
    }

    [Fact]
    public async Task ABookWithoutTitle_Returns422() =>
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Fresh().PostAsJsonAsync("/books", new { title = "", author = "x" })).StatusCode);

    [Fact]
    public async Task AnUnknownBook_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Fresh().GetAsync("/books/99")).StatusCode);

    [Fact]
    public async Task ABorrowedBook_ShowsAsUnavailable_AndAppearsInTheMembersLoans()
    {
        var client = Fresh();
        var book = await PostForIdAsync(client, "/books", new { title = "DDD", author = "Evans" });
        var member = await PostForIdAsync(client, "/members", new { name = "Ana", email = "ana@x.com" });

        var loanId = await PostForIdAsync(client, "/loans", new { memberId = member, bookId = book });

        var after = await client.GetFromJsonAsync<JsonElement>($"/books/{book}");
        Assert.False(after.GetProperty("available").GetBoolean());
        var loans = await client.GetFromJsonAsync<JsonElement>($"/members/{member}/loans");
        Assert.Equal(loanId, loans[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task BorrowingABookTwice_Returns422()
    {
        var client = Fresh();
        var book = await PostForIdAsync(client, "/books", new { title = "DDD", author = "Evans" });
        var ana = await PostForIdAsync(client, "/members", new { name = "Ana", email = "ana@x.com" });
        var bob = await PostForIdAsync(client, "/members", new { name = "Bob", email = "bob@x.com" });
        await PostForIdAsync(client, "/loans", new { memberId = ana, bookId = book });

        var second = await client.PostAsJsonAsync("/loans", new { memberId = bob, bookId = book });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Contains("already on loan", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReturningALoan_MakesTheBookAvailableAgain()
    {
        var client = Fresh();
        var book = await PostForIdAsync(client, "/books", new { title = "DDD", author = "Evans" });
        var member = await PostForIdAsync(client, "/members", new { name = "Ana", email = "ana@x.com" });
        var loan = await PostForIdAsync(client, "/loans", new { memberId = member, bookId = book });

        var response = await client.PostAsync($"/loans/{loan}/return", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await client.GetFromJsonAsync<JsonElement>($"/books/{book}")).GetProperty("available").GetBoolean());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/loans/{loan}/return", null)).StatusCode);
    }

    [Fact]
    public async Task LoanForUnknownThings_Returns404()
    {
        var client = Fresh();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/loans", new { memberId = 9, bookId = 9 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/loans/9/return", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/members/9/loans")).StatusCode);
    }

    [Fact]
    public async Task RegisteringTheSameEmailTwice_Returns422()
    {
        var client = Fresh();
        await PostForIdAsync(client, "/members", new { name = "Ana", email = "ana@x.com" });

        var again = await client.PostAsJsonAsync("/members", new { name = "Other", email = "ana@x.com" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
    }
}
