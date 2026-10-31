using System.Net;
using System.Net.Http.Json;
using Eda.Messaging;

namespace Eda.Tests;

/// <summary>The three participants together, talking only through messages.</summary>
public class FlowTests : IDisposable
{
    private readonly App _app = new();

    public void Dispose() => _app.Dispose();

    // ---------- the happy path and eventual consistency ----------

    [Fact]
    public async Task PlacingAnOrder_Returns202Pending_AndNothingElseHasHappenedYet()
    {
        var response = await _app.Http.PostAsJsonAsync("/orders", new { customerEmail = "ana@x.com", sku = "BOOK-1", quantity = 2 });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.StartsWith("/orders/", response.Headers.Location!.ToString());
        Assert.Equal(10, await _app.StockAsync("BOOK-1"));             // Inventory has not even heard about it
        Assert.Empty(await _app.NotificationsAsync("ana@x.com"));
    }

    [Fact]
    public async Task OnceTheMessagesFlow_TheOrderIsConfirmed_StockDrops_AndTheCustomerIsNotified()
    {
        var id = await _app.PlaceAsync("ana@x.com", "BOOK-1", 2);
        Assert.Equal("Pending", await _app.StatusAsync(id));           // eventual consistency: not decided yet

        await _app.Settle();

        Assert.Equal("Confirmed", await _app.StatusAsync(id));
        Assert.Equal(8, await _app.StockAsync("BOOK-1"));
        var notifications = await _app.NotificationsAsync("ana@x.com");
        Assert.Contains("confirmed", Assert.Single(notifications));
    }

    [Fact]
    public async Task WithoutEnoughStock_TheOrderIsRejected_WithTheReason_AndTheCustomerIsTold()
    {
        var id = await _app.PlaceAsync("ana@x.com", "MUG-1", 99);

        await _app.Settle();

        var order = await _app.GetOrderAsync(id);
        Assert.Equal("Rejected", order.GetProperty("status").GetString());
        Assert.Contains("Not enough stock", order.GetProperty("reason").GetString());
        Assert.Equal(5, await _app.StockAsync("MUG-1"));
        Assert.Contains("could not be completed", Assert.Single(await _app.NotificationsAsync("ana@x.com")));
    }

    [Fact]
    public async Task AnUnknownProduct_IsRejected()
    {
        var id = await _app.PlaceAsync("ana@x.com", "NOPE", 1);

        await _app.Settle();

        var order = await _app.GetOrderAsync(id);
        Assert.Equal("Rejected", order.GetProperty("status").GetString());
        Assert.Contains("Unknown product", order.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task TwoOrdersForTheLastUnits_OneWins_TheOtherIsRejected()
    {
        var first = await _app.PlaceAsync("one@x.com", "MUG-1", 3);
        var second = await _app.PlaceAsync("two@x.com", "MUG-1", 3);

        await _app.Settle();

        Assert.Equal("Confirmed", await _app.StatusAsync(first));
        Assert.Equal("Rejected", await _app.StatusAsync(second));
        Assert.Equal(2, await _app.StockAsync("MUG-1"));
    }

    // ---------- compensation ----------

    [Fact]
    public async Task CancellingAConfirmedOrder_ReturnsTheStock_ThroughAMessage_AndNotifies()
    {
        var id = await _app.PlaceAsync("ana@x.com", "BOOK-1", 3);
        await _app.Settle();
        Assert.Equal(7, await _app.StockAsync("BOOK-1"));

        var cancel = await _app.Http.PostAsync($"/orders/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(7, await _app.StockAsync("BOOK-1"));              // the stock comes back LATER, when the message is delivered
        await _app.Settle();

        Assert.Equal("Cancelled", await _app.StatusAsync(id));
        Assert.Equal(10, await _app.StockAsync("BOOK-1"));
        var notifications = await _app.NotificationsAsync("ana@x.com");
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.Contains("cancelled"));
    }

    [Fact]
    public async Task APendingOrder_CannotBeCancelled()
    {
        var id = await _app.PlaceAsync("ana@x.com", "BOOK-1", 1);

        var cancel = await _app.Http.PostAsync($"/orders/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        await _app.Settle();
        Assert.Equal("Confirmed", await _app.StatusAsync(id));         // the attempt left no trace
    }

    [Fact]
    public async Task CancellingTwice_ReturnsConflict_AndReleasesTheStockOnce()
    {
        var id = await _app.PlaceAsync("ana@x.com", "BOOK-1", 3);
        await _app.Settle();
        await _app.Http.PostAsync($"/orders/{id}/cancel", null);

        var second = await _app.Http.PostAsync($"/orders/{id}/cancel", null);
        await _app.Settle();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(10, await _app.StockAsync("BOOK-1"));
    }

    [Fact]
    public async Task CancellingAnUnknownOrder_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _app.Http.PostAsync($"/orders/{Guid.NewGuid()}/cancel", null)).StatusCode);

    // ---------- input validation ----------

    [Theory]
    [InlineData("", "BOOK-1", 1)]
    [InlineData("not-an-email", "BOOK-1", 1)]
    [InlineData("ana@x.com", "", 1)]
    [InlineData("ana@x.com", "BOOK-1", 0)]
    public async Task InvalidOrders_Return422_AndStartNothing(string email, string sku, int quantity)
    {
        var response = await _app.Http.PostAsJsonAsync("/orders", new { customerEmail = email, sku, quantity });
        await _app.Settle();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(10, await _app.StockAsync("BOOK-1"));
        Assert.Empty(_app.Broker.DeadLetters);
    }

    [Fact]
    public async Task AnUnknownOrder_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _app.Http.GetAsync($"/orders/{Guid.NewGuid()}")).StatusCode);

    // ---------- things that go wrong in the messaging layer ----------

    [Fact]
    public async Task WhenInventoryIsDown_OrdersKeepAcceptingOrders_AndTheyAreProcessedWhenItComesBack()
    {
        _app.Broker.Pause("inventory");

        var id = await _app.PlaceAsync("ana@x.com", "BOOK-1", 2);        // still 202: Orders does not depend on Inventory being up
        await _app.Settle();
        Assert.Equal("Pending", await _app.StatusAsync(id));
        Assert.Equal(1, _app.Broker.Pending("inventory"));

        _app.Broker.Resume("inventory");
        await _app.Settle();

        Assert.Equal("Confirmed", await _app.StatusAsync(id));
        Assert.Equal(8, await _app.StockAsync("BOOK-1"));
    }

    [Fact]
    public async Task WhenTheBrokerIsUnreachable_TheOrderIsStillAccepted_AndTheOutboxDeliversItLater()
    {
        _app.Broker.Offline = true;

        var id = await _app.PlaceAsync("ana@x.com", "BOOK-1", 2);        // the order and its message were saved together
        await _app.Settle();
        Assert.Equal("Pending", await _app.StatusAsync(id));
        Assert.Equal(10, await _app.StockAsync("BOOK-1"));

        _app.Broker.Offline = false;
        await _app.Settle();

        Assert.Equal("Confirmed", await _app.StatusAsync(id));
    }

    [Fact]
    public async Task DuplicateDeliveries_EverywhereInTheSystem_ProduceEachEffectOnce()
    {
        _app.Broker.DuplicateDeliveries = true;

        var id = await _app.PlaceAsync("ana@x.com", "BOOK-1", 2);
        await _app.Settle();

        Assert.Equal("Confirmed", await _app.StatusAsync(id));
        Assert.Equal(8, await _app.StockAsync("BOOK-1"));                // reserved once, not twice
        Assert.Single(await _app.NotificationsAsync("ana@x.com"));        // one email, not two
    }

    [Fact]
    public async Task APoisonMessage_EndsInTheDeadLetterList_AndTheNextOrderStillWorks()
    {
        await _app.Broker.PublishAsync(new Message(Guid.NewGuid(), "orders.order-placed.v1", "{ this is not json",
            DateTimeOffset.UtcNow, "poison"));

        var id = await _app.PlaceAsync("ana@x.com", "BOOK-1", 1);
        await _app.Settle();

        var dead = Assert.Single(_app.Broker.DeadLetters);
        Assert.Equal(("inventory", "poison"), (dead.Subscriber, dead.Message.CorrelationId));
        Assert.Equal("Confirmed", await _app.StatusAsync(id));
    }

    [Fact]
    public async Task AMessageOfAnUnknownType_IsSimplyIgnored()
    {
        await _app.Broker.PublishAsync(Kit.NewMessage("billing.invoice-created.v1", new { invoiceId = 1 }));
        await _app.Settle();

        Assert.Empty(_app.Broker.DeadLetters);
    }

    [Fact]
    public async Task ConsumersAreTolerantReaders_ExtraFieldsInAMessageDoNotBreakThem()
    {
        // A message built by hand, as a different (future) version of Orders might send it.
        await _app.Broker.PublishAsync(new Message(Guid.NewGuid(), "orders.order-confirmed.v1",
            """{"orderId":"5c1b2f0e-2f64-4b8e-bb0e-1f2f2f2f2f2f","customerEmail":"zed@x.com","loyaltyPoints":120,"meta":{"v":2}}""",
            DateTimeOffset.UtcNow, "x"));

        await _app.Settle();

        Assert.Contains("confirmed", Assert.Single(await _app.NotificationsAsync("zed@x.com")));
        Assert.Empty(_app.Broker.DeadLetters);
    }

    [Fact]
    public async Task AStockReservedMessage_ForAnOrderThatIsAlreadyDecided_ChangesNothing()
    {
        var id = await _app.PlaceAsync("ana@x.com", "MUG-1", 99);
        await _app.Settle();
        Assert.Equal("Rejected", await _app.StatusAsync(id));

        // A late, contradictory message arrives (with a NEW message id, so the inbox cannot help here).
        await _app.Broker.PublishAsync(new Message(Guid.NewGuid(), "inventory.stock-reserved.v1",
            $$"""{"orderId":"{{id}}"}""", DateTimeOffset.UtcNow, id.ToString()));
        await _app.Settle();

        Assert.Equal("Rejected", await _app.StatusAsync(id));            // the state machine refuses the impossible transition
        Assert.Single(await _app.NotificationsAsync("ana@x.com"));
    }
}
