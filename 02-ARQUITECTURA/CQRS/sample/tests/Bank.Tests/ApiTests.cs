using System.Net;
using System.Net.Http.Json;
using Bank.Infrastructure;
using Bank.Queries;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bank.Tests;

/// <summary>HTTP end to end. The background projector is removed so each test decides when the read model catches up.</summary>
public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Created(Guid Id);

    private (HttpClient Client, IProjector Projector) Start()
    {
        var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.RemoveAll<IHostedService>()));
        return (app.CreateClient(), app.Services.GetRequiredService<IProjector>());
    }

    private static async Task<Guid> OpenAsync(HttpClient client, string owner, decimal overdraft = 0m)
    {
        var response = await client.PostAsJsonAsync("/accounts", new { owner, overdraftLimit = overdraft });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    [Fact]
    public async Task OpenAccount_Returns201_ButTheReadSideDoesNotKnowItYet()
    {
        var (client, projector) = Start();

        var id = await OpenAsync(client, "Ana");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/accounts/{id}")).StatusCode);
        await projector.RunPendingAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/accounts/{id}")).StatusCode);
    }

    [Fact]
    public async Task DepositAndWithdraw_ReturnNoContent_AndShowUpAfterProjection()
    {
        var (client, projector) = Start();
        var id = await OpenAsync(client, "Ana", 20m);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/accounts/{id}/deposit", new { amount = 100m })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/accounts/{id}/withdraw", new { amount = 30m })).StatusCode);
        await projector.RunPendingAsync();

        var summary = (await client.GetFromJsonAsync<AccountSummary>($"/accounts/{id}"))!;
        Assert.Equal((70m, 2), (summary.Balance, summary.TransactionCount));

        var transactions = (await client.GetFromJsonAsync<List<TransactionItem>>($"/accounts/{id}/transactions"))!;
        Assert.Equal(["Withdrawal", "Deposit"], transactions.Select(t => t.Type));
    }

    [Fact]
    public async Task WithdrawBeyondOverdraft_Returns422_AndTheReadModelKeepsTheOldBalance()
    {
        var (client, projector) = Start();
        var id = await OpenAsync(client, "Ana");
        await client.PostAsJsonAsync($"/accounts/{id}/deposit", new { amount = 10m });

        var response = await client.PostAsJsonAsync($"/accounts/{id}/withdraw", new { amount = 11m });
        await projector.RunPendingAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Insufficient funds", await response.Content.ReadAsStringAsync());
        Assert.Equal(10m, (await client.GetFromJsonAsync<AccountSummary>($"/accounts/{id}"))!.Balance);
    }

    [Fact]
    public async Task CommandOnUnknownAccount_Returns404()
    {
        var (client, _) = Start();

        var response = await client.PostAsJsonAsync($"/accounts/{Guid.NewGuid()}/deposit", new { amount = 1m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenAccount_WithoutOwner_Returns422()
    {
        var (client, _) = Start();

        var response = await client.PostAsJsonAsync("/accounts", new { owner = "", overdraftLimit = 0 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CloseAccount_WithBalance_Returns422_AndWithoutBalance_Returns204()
    {
        var (client, projector) = Start();
        var rich = await OpenAsync(client, "Rich");
        await client.PostAsJsonAsync($"/accounts/{rich}/deposit", new { amount = 5m });
        var empty = await OpenAsync(client, "Empty");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/accounts/{rich}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/accounts/{empty}/close", null)).StatusCode);
        await projector.RunPendingAsync();

        Assert.Equal("Closed", (await client.GetFromJsonAsync<AccountSummary>($"/accounts/{empty}"))!.Status);
    }

    [Fact]
    public async Task ListAndTop_ServeFromTheReadModel()
    {
        var (client, projector) = Start();
        var ana = await OpenAsync(client, "Ana");
        var bob = await OpenAsync(client, "Bob");
        await client.PostAsJsonAsync($"/accounts/{ana}/deposit", new { amount = 10m });
        await client.PostAsJsonAsync($"/accounts/{bob}/deposit", new { amount = 99m });
        await projector.RunPendingAsync();

        var onlyAna = await client.GetFromJsonAsync<List<AccountSummary>>("/accounts?owner=ana");
        var top = await client.GetFromJsonAsync<List<AccountSummary>>("/accounts/top?count=1");

        Assert.Equal([ana], onlyAna!.Select(a => a.Id));
        Assert.Equal([bob], top!.Select(a => a.Id));
    }

    [Fact]
    public async Task TheBackgroundWorker_EventuallyMakesTheReadModelCatchUp()
    {
        using var app = factory.WithWebHostBuilder(_ => { });      // the real worker stays registered
        var client = app.CreateClient();
        var id = await OpenAsync(client, "Worker");

        HttpStatusCode status = default;
        for (var i = 0; i < 50 && status != HttpStatusCode.OK; i++)
        {
            await Task.Delay(100);
            status = (await client.GetAsync($"/accounts/{id}")).StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task Transactions_OfUnknownAccount_Return404()
    {
        var (client, _) = Start();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/accounts/{Guid.NewGuid()}/transactions")).StatusCode);
    }
}
