using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shop.Application;

namespace Shop.Tests;

public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<CustomerDto> NewCustomer(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/customers", new { name = "Ana" })).Content.ReadFromJsonAsync<CustomerDto>())!;

    [Fact]
    public async Task Customer_PlaceOrder_Get_Cancel_RoundTrip()
    {
        var client = factory.CreateClient();
        var customer = await NewCustomer(client);

        var placed = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = customer.Id,
            lines = new[] { new { sku = "BOOK", quantity = 2, unitPrice = 60m } }
        });
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = (await placed.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(12m, order.Discount);   // 10% bulk on 120
        Assert.Equal(108m, order.Total);

        var fetched = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}");
        Assert.Equal("Placed", fetched!.Status);

        var cancelled = await client.PostAsync($"/api/orders/{order.Id}/cancel", null);
        Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<OrderDto>())!.Status);
    }

    [Fact]
    public async Task PlaceOrder_WithoutLines_Returns422()
    {
        var client = factory.CreateClient();
        var customer = await NewCustomer(client);

        var response = await client.PostAsJsonAsync("/api/orders", new { customerId = customer.Id, lines = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_Unknown_Returns404()
    {
        var response = await factory.CreateClient().GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
