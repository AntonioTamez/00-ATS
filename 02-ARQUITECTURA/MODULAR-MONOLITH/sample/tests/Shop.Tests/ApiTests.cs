using System.Net;
using System.Net.Http.Json;
using Catalog.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Ordering.Contracts;
using Shop.SharedKernel;

namespace Shop.Tests;

/// <summary>The whole monolith wired by the host: modules cooperate through contracts and events.</summary>
public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static object Order(string email, params (string Sku, int Quantity)[] lines) =>
        new { customerEmail = email, lines = lines.Select(l => new { sku = l.Sku, quantity = l.Quantity }) };

    private static async Task<int> StockAsync(HttpClient client, string sku) =>
        (await client.GetFromJsonAsync<ProductInfo>($"/catalog/products/{sku}"))!.Stock;

    [Fact]
    public async Task Catalog_ListsProducts()
    {
        var products = await factory.CreateClient().GetFromJsonAsync<List<ProductInfo>>("/catalog/products");

        Assert.Equal(3, products!.Count);
    }

    [Fact]
    public async Task Catalog_UnknownProduct_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/catalog/products/NOPE")).StatusCode);

    [Fact]
    public async Task PlacingAnOrder_ReservesStock_AndNotifiesTheCustomer_AcrossThreeModules()
    {
        using var app = factory.WithWebHostBuilder(_ => { });
        var client = app.CreateClient();
        var before = await StockAsync(client, "BOOK-1");

        var response = await client.PostAsJsonAsync("/orders", Order("flow@x.com", ("BOOK-1", 2)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(60m, order.Total);
        Assert.Equal($"/orders/{order.Id}", response.Headers.Location!.ToString());
        Assert.Equal(before - 2, await StockAsync(client, "BOOK-1"));

        var notifications = await client.GetFromJsonAsync<List<NotificationDto>>("/notifications?email=flow@x.com");
        Assert.Contains("confirmed", Assert.Single(notifications!).Subject);
    }

    [Fact]
    public async Task CancellingAnOrder_ReturnsStock_AndNotifies()
    {
        using var app = factory.WithWebHostBuilder(_ => { });
        var client = app.CreateClient();
        var before = await StockAsync(client, "PEN-1");
        var order = (await (await client.PostAsJsonAsync("/orders", Order("cancel@x.com", ("PEN-1", 5))))
            .Content.ReadFromJsonAsync<OrderDto>())!;

        var cancel = await client.PostAsync($"/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(before, await StockAsync(client, "PEN-1"));
        var subjects = (await client.GetFromJsonAsync<List<NotificationDto>>("/notifications?email=cancel@x.com"))!
            .Select(n => n.Subject).ToList();
        Assert.Equal(2, subjects.Count);
        Assert.Contains(subjects, s => s.Contains("cancelled"));
    }

    [Fact]
    public async Task CancellingTwice_Returns409()
    {
        using var app = factory.WithWebHostBuilder(_ => { });
        var client = app.CreateClient();
        var order = (await (await client.PostAsJsonAsync("/orders", Order("twice@x.com", ("PEN-1", 1))))
            .Content.ReadFromJsonAsync<OrderDto>())!;
        await client.PostAsync($"/orders/{order.Id}/cancel", null);

        var second = await client.PostAsync($"/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task OrderWithoutStock_Returns422_AndLeavesCatalogUntouched()
    {
        using var app = factory.WithWebHostBuilder(_ => { });
        var client = app.CreateClient();
        var before = await StockAsync(client, "BOOK-1");

        var response = await client.PostAsJsonAsync("/orders", Order("nostock@x.com", ("BOOK-1", 1), ("MUG-1", 999)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(before, await StockAsync(client, "BOOK-1"));
        Assert.Empty((await client.GetFromJsonAsync<List<NotificationDto>>("/notifications?email=nostock@x.com"))!);
    }

    [Fact]
    public async Task UnknownOrder_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/orders/{Guid.NewGuid()}")).StatusCode);

    [Fact]
    public async Task AFailingSubscriber_DoesNotBreakTheOrder_NorTheOtherSubscribers()
    {
        using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
            services.AddScoped<IIntegrationEventHandler<OrderPlaced>, ExplodingHandler>()));
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", Order("boom@x.com", ("PEN-1", 1)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<List<NotificationDto>>("/notifications?email=boom@x.com"))!);
    }

    private sealed class ExplodingHandler : IIntegrationEventHandler<OrderPlaced>
    {
        public Task HandleAsync(OrderPlaced integrationEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("subscriber crashed");
    }
}
