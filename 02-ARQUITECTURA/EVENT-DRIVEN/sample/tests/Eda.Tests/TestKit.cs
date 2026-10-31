using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Eda.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eda.Tests;

internal sealed class TestOutbox : Outbox;

internal sealed class TestInbox : Inbox;

internal static class Kit
{
    public static InMemoryBroker NewBroker() => new(NullLogger<InMemoryBroker>.Instance);

    public static Message NewMessage(string type, object? payload = null, string correlation = "c-1") =>
        Message.Create(type, payload ?? new { }, correlation, DateTimeOffset.UtcNow);
}

/// <summary>The whole system in memory. The background pump is removed so each test decides when "later" happens.</summary>
internal sealed class App : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public App()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureServices(s => s.RemoveAll<IHostedService>()));
        Http = _factory.CreateClient();
    }

    public HttpClient Http { get; }
    public InMemoryBroker Broker => _factory.Services.GetRequiredService<InMemoryBroker>();

    /// <summary>Lets everything that is pending happen: relay outboxes, deliver messages, until quiet.</summary>
    public Task Settle() => _factory.Services.GetRequiredService<IPump>().RunUntilIdleAsync();

    public async Task<Guid> PlaceAsync(string email, string sku, int quantity)
    {
        var response = await Http.PostAsJsonAsync("/orders", new { customerEmail = email, sku, quantity });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public async Task<JsonElement> GetOrderAsync(Guid id) => await Http.GetFromJsonAsync<JsonElement>($"/orders/{id}");

    public async Task<string> StatusAsync(Guid id) => (await GetOrderAsync(id)).GetProperty("status").GetString()!;

    public async Task<int> StockAsync(string sku) =>
        (await Http.GetFromJsonAsync<JsonElement>($"/inventory/{sku}")).GetProperty("available").GetInt32();

    public async Task<List<string>> NotificationsAsync(string email) =>
        (await Http.GetFromJsonAsync<JsonElement>($"/notifications?email={email}"))
            .EnumerateArray().Select(n => n.GetProperty("subject").GetString()!).ToList();

    public void Dispose()
    {
        Http.Dispose();
        _factory.Dispose();
    }
}
