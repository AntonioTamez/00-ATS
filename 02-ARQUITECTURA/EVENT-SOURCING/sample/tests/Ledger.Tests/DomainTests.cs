using Ledger.Domain;

namespace Ledger.Tests;

/// <summary>
/// Event-sourced tests read as GIVEN (past events) / WHEN (a command) / THEN (new events, or a refusal).
/// No database, no queue, no projection: just the business logic.
/// </summary>
public class DomainTests
{
    private static StockItem Given(params IDomainEvent[] history) => StockItem.Rehydrate(history);

    private static readonly ItemRegistered Registered = new("BOOK-1", "Clean Architecture");

    // ---------- register ----------

    [Fact]
    public void Registering_RaisesItemRegistered_WithANormalizedSku()
    {
        var item = StockItem.Register("  book-1 ", "  Clean Architecture ");

        Assert.Equal(new ItemRegistered("BOOK-1", "Clean Architecture"), Assert.Single(item.Uncommitted));
        Assert.Equal(("BOOK-1", 0, 1), (item.Sku, item.OnHand, item.Version));
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("SKU", " ")]
    public void RegisteringWithoutSkuOrName_IsRefused(string sku, string name) =>
        Assert.Throws<DomainException>(() => StockItem.Register(sku, name));

    // ---------- receive ----------

    [Fact]
    public void Receiving_RaisesStockReceived_AndIncreasesTheStock()
    {
        var item = Given(Registered);

        item.Receive(10, "  Acme ");

        Assert.Equal(new StockReceived(10, "Acme"), Assert.Single(item.Uncommitted));
        Assert.Equal(10, item.OnHand);
    }

    [Fact]
    public void ABlankSupplier_IsRecordedAsNoSupplier()
    {
        var item = Given(Registered);

        item.Receive(1, "  ");

        Assert.Null(((StockReceived)Assert.Single(item.Uncommitted)).Supplier);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ReceivingANonPositiveQuantity_IsRefused_AndRaisesNothing(int quantity)
    {
        var item = Given(Registered);

        Assert.Throws<DomainException>(() => item.Receive(quantity, null));

        Assert.Empty(item.Uncommitted);
        Assert.Equal(0, item.OnHand);
    }

    // ---------- ship ----------

    [Fact]
    public void Shipping_WithinTheStock_RaisesStockShipped()
    {
        var item = Given(Registered, new StockReceived(10, null));

        item.Ship(4, "ORDER-9");

        Assert.Equal(new StockShipped(4, "ORDER-9"), Assert.Single(item.Uncommitted));
        Assert.Equal(6, item.OnHand);
    }

    [Fact]
    public void Shipping_MoreThanOnHand_IsRefused_AndChangesNothing()
    {
        var item = Given(Registered, new StockReceived(3, null));

        var ex = Assert.Throws<DomainException>(() => item.Ship(4, "ORDER-9"));

        Assert.Contains("only 3 on hand", ex.Message);
        Assert.Empty(item.Uncommitted);
        Assert.Equal(3, item.OnHand);
    }

    [Fact]
    public void Shipping_TheExactStock_IsAllowed()
    {
        var item = Given(Registered, new StockReceived(3, null));

        item.Ship(3, "ORDER-9");

        Assert.Equal(0, item.OnHand);
    }

    [Fact]
    public void Shipping_NeedsAReference_AndAPositiveQuantity()
    {
        var item = Given(Registered, new StockReceived(3, null));

        Assert.Throws<DomainException>(() => item.Ship(1, " "));
        Assert.Throws<DomainException>(() => item.Ship(0, "REF"));
    }

    // ---------- adjust: corrections are new events ----------

    [Fact]
    public void AnAdjustment_IsANewEventWithAReason_NotAnEditOfThePast()
    {
        var item = Given(Registered, new StockReceived(10, null));

        item.Adjust(-2, "2 units damaged in the warehouse");

        Assert.Equal(new StockAdjusted(-2, "2 units damaged in the warehouse"), Assert.Single(item.Uncommitted));
        Assert.Equal(8, item.OnHand);
        Assert.Equal(3, item.Version);                       // the history grew; nothing was rewritten
    }

    [Fact]
    public void AnAdjustment_CannotBeZero_NeedsAReason_AndCannotGoNegative()
    {
        var item = Given(Registered, new StockReceived(5, null));

        Assert.Throws<DomainException>(() => item.Adjust(0, "x"));
        Assert.Throws<DomainException>(() => item.Adjust(1, " "));
        Assert.Throws<DomainException>(() => item.Adjust(-6, "too much"));
        Assert.Empty(item.Uncommitted);
    }

    // ---------- discontinue ----------

    [Fact]
    public void AnItemWithStock_CannotBeDiscontinued()
    {
        var item = Given(Registered, new StockReceived(1, null));

        Assert.Throws<DomainException>(() => item.Discontinue());
    }

    [Fact]
    public void ADiscontinuedItem_RefusesEveryCommand()
    {
        var item = Given(Registered, new ItemDiscontinued());

        Assert.True(item.Discontinued);
        Assert.Throws<DomainException>(() => item.Receive(1, null));
        Assert.Throws<DomainException>(() => item.Ship(1, "R"));
        Assert.Throws<DomainException>(() => item.Adjust(1, "r"));
        Assert.Throws<DomainException>(() => item.Discontinue());
    }

    // ---------- rebuilding state ----------

    [Fact]
    public void Version_CountsEveryEvent_WhetherReplayedOrRaised()
    {
        var item = Given(Registered, new StockReceived(5, null), new StockShipped(1, "R"));
        Assert.Equal(3, item.Version);

        item.Receive(1, null);

        Assert.Equal(4, item.Version);
    }

    [Fact]
    public void ReplayingTheSameHistory_AlwaysGivesTheSameState()
    {
        IDomainEvent[] history = [Registered, new StockReceived(10, "A"), new StockShipped(3, "R1"), new StockAdjusted(-1, "lost")];

        var first = Given(history).ToSnapshot();
        var second = Given(history).ToSnapshot();

        Assert.Equal(first, second);
        Assert.Equal(6, first.OnHand);
    }

    [Fact]
    public void ASnapshotPlusTheTail_EqualsReplayingEverything()
    {
        IDomainEvent[] history =
            [Registered, new StockReceived(10, "A"), new StockShipped(3, "R1"), new StockAdjusted(-1, "lost"), new StockReceived(4, null), new StockShipped(2, "R2")];
        var snapshotAtThree = Given(history[..3]).ToSnapshot();

        var viaSnapshot = StockItem.Rehydrate(snapshotAtThree, history[3..]).ToSnapshot();
        var viaFullReplay = Given(history).ToSnapshot();

        Assert.Equal(viaFullReplay, viaSnapshot);
    }

    [Fact]
    public void ReplayingHistory_DoesNotRevalidateIt_BecauseEventsAreFacts()
    {
        // A past event that today's rules would refuse still happened. Replay must not throw.
        var item = Given(Registered, new StockShipped(5, "OLD-SHIPMENT"));

        Assert.Equal(-5, item.OnHand);
        Assert.Empty(item.Uncommitted);                      // replayed events are not "new"
    }

    [Fact]
    public void PullUncommitted_HandsOverTheNewEventsOnce()
    {
        var item = StockItem.Register("A", "Item");
        item.Receive(2, null);

        Assert.Equal(2, item.PullUncommitted().Count);
        Assert.Empty(item.PullUncommitted());
    }
}
