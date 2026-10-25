using System.Net;
using System.Net.Http.Json;
using Library.Adapters.Cli;
using Library.Adapters.Outbound;
using Library.Core;
using Library.Core.Ports.Inbound;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Tests;

public class RestAdapterTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Add_Lend_Return_RoundTrip()
    {
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/books", new { title = "Hexagonal Architecture" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var book = (await created.Content.ReadFromJsonAsync<BookDto>())!;

        var lent = await client.PostAsJsonAsync($"/api/books/{book.Id}/lend", new { borrower = "Ana" });
        Assert.Equal(HttpStatusCode.OK, lent.StatusCode);
        Assert.False((await lent.Content.ReadFromJsonAsync<BookDto>())!.IsAvailable);

        var returned = await client.PostAsync($"/api/books/{book.Id}/return", null);
        Assert.True((await returned.Content.ReadFromJsonAsync<BookDto>())!.IsAvailable);
    }

    [Fact]
    public async Task Lend_AlreadyLentBook_Returns422()
    {
        var client = factory.CreateClient();
        var book = (await (await client.PostAsJsonAsync("/api/books", new { title = "X" })).Content.ReadFromJsonAsync<BookDto>())!;
        await client.PostAsJsonAsync($"/api/books/{book.Id}/lend", new { borrower = "Ana" });

        var again = await client.PostAsJsonAsync($"/api/books/{book.Id}/lend", new { borrower = "Luis" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
    }

    [Fact]
    public async Task Return_UnknownBook_Returns404()
    {
        var response = await factory.CreateClient().PostAsync($"/api/books/{Guid.NewGuid()}/return", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>The CLI drives the very same inbound ports as REST: that is the point of the hexagon.</summary>
public class CliAdapterTests
{
    private static (CliRunner Runner, StringWriter Out) Build()
    {
        var output = new StringWriter();
        var provider = new ServiceCollection()
            .AddLibraryCore()
            .AddOutboundAdapters()
            .BuildServiceProvider();
        var runner = new CliRunner(
            provider.GetRequiredService<IAddBook>(), provider.GetRequiredService<ILendBook>(),
            provider.GetRequiredService<IReturnBook>(), provider.GetRequiredService<IListBooks>(), output);
        return (runner, output);
    }

    [Fact]
    public async Task Add_Then_List_ShowsBook()
    {
        var (runner, output) = Build();

        Assert.Equal(0, await runner.RunAsync(["add", "Clean", "Code"]));
        Assert.Equal(0, await runner.RunAsync(["list"]));

        Assert.Contains("Clean Code  available", output.ToString());
    }

    [Fact]
    public async Task Lend_UnknownBook_PrintsErrorAndExitCode1()
    {
        var (runner, output) = Build();

        var code = await runner.RunAsync(["lend", Guid.NewGuid().ToString(), "Ana"]);

        Assert.Equal(1, code);
        Assert.Contains("was not found", output.ToString());
    }

    [Fact]
    public async Task UnknownCommand_PrintsUsageAndExitCode2()
    {
        var (runner, output) = Build();

        Assert.Equal(2, await runner.RunAsync(["bogus"]));
        Assert.Contains("Usage:", output.ToString());
    }
}
