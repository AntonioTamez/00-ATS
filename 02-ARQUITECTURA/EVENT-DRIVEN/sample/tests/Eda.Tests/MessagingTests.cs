using System.Text.Json;
using Eda.Messaging;

namespace Eda.Tests;

/// <summary>The plumbing alone: broker, outbox, inbox, pump. No business participant involved.</summary>
public class MessagingTests
{
    [Fact]
    public async Task APublishedMessage_ReachesEverySubscriberOfThatType()
    {
        var broker = Kit.NewBroker();
        var received = new List<string>();
        broker.Subscribe("a", "t.happened", (_, _) => { received.Add("a"); return Task.CompletedTask; });
        broker.Subscribe("b", "t.happened", (_, _) => { received.Add("b"); return Task.CompletedTask; });

        await broker.PublishAsync(Kit.NewMessage("t.happened"));
        await broker.DeliverRoundAsync();

        Assert.Equal(["a", "b"], received.Order());
    }

    [Fact]
    public async Task ASubscriber_DoesNotReceiveTypesItDidNotSubscribeTo()
    {
        var broker = Kit.NewBroker();
        var calls = 0;
        broker.Subscribe("a", "t.happened", (_, _) => { calls++; return Task.CompletedTask; });

        await broker.PublishAsync(Kit.NewMessage("t.other"));
        await broker.DeliverRoundAsync();

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Delivery_IsAsynchronous_NothingIsHandledUntilTheBrokerDelivers()
    {
        var broker = Kit.NewBroker();
        var calls = 0;
        broker.Subscribe("a", "t.happened", (_, _) => { calls++; return Task.CompletedTask; });

        await broker.PublishAsync(Kit.NewMessage("t.happened"));

        Assert.Equal(0, calls);
        Assert.Equal(1, broker.Pending("a"));
        await broker.DeliverRoundAsync();
        Assert.Equal(1, calls);
        Assert.Equal(0, broker.Pending("a"));
    }

    [Fact]
    public async Task AFailingHandler_IsRetried_AndSucceedsWhenItRecovers()
    {
        var broker = Kit.NewBroker();
        var calls = 0;
        broker.Subscribe("a", "t.happened", (_, _) => ++calls < 3 ? throw new InvalidOperationException("flaky") : Task.CompletedTask);

        await broker.PublishAsync(Kit.NewMessage("t.happened"));
        for (var round = 0; round < 5; round++) await broker.DeliverRoundAsync();

        Assert.Equal(3, calls);
        Assert.Empty(broker.DeadLetters);
    }

    [Fact]
    public async Task AMessageThatAlwaysFails_GoesToTheDeadLetterList_AfterMaxAttempts()
    {
        var broker = Kit.NewBroker();
        var calls = 0;
        broker.Subscribe("a", "t.happened", (_, _) => { calls++; throw new InvalidOperationException("boom"); });

        await broker.PublishAsync(Kit.NewMessage("t.happened"));
        for (var round = 0; round < 10; round++) await broker.DeliverRoundAsync();

        Assert.Equal(InMemoryBroker.MaxAttempts, calls);
        var dead = Assert.Single(broker.DeadLetters);
        Assert.Equal(("a", "boom", InMemoryBroker.MaxAttempts), (dead.Subscriber, dead.Error, dead.Attempts));
    }

    [Fact]
    public async Task APoisonMessage_DoesNotBlockTheOnesBehindIt()
    {
        var broker = Kit.NewBroker();
        var handled = new List<string>();
        broker.Subscribe("a", "t.happened", (m, _) =>
        {
            if (m.CorrelationId == "poison") throw new InvalidOperationException("cannot process");
            handled.Add(m.CorrelationId);
            return Task.CompletedTask;
        });

        await broker.PublishAsync(Kit.NewMessage("t.happened", correlation: "poison"));
        await broker.PublishAsync(Kit.NewMessage("t.happened", correlation: "good"));
        for (var round = 0; round < 10; round++) await broker.DeliverRoundAsync();

        Assert.Equal(["good"], handled);
        Assert.Single(broker.DeadLetters);
    }

    [Fact]
    public async Task ADuplicateDelivery_RunsTheHandlerTwice_WhenThereIsNoInbox()
    {
        var broker = Kit.NewBroker();
        broker.DuplicateDeliveries = true;
        var calls = 0;
        broker.Subscribe("a", "t.happened", (_, _) => { calls++; return Task.CompletedTask; });

        await broker.PublishAsync(Kit.NewMessage("t.happened"));
        await broker.DeliverRoundAsync();

        Assert.Equal(2, calls);        // at-least-once delivery: this is the problem the inbox solves
    }

    [Fact]
    public async Task AnInbox_MakesADuplicateDeliveryHarmless()
    {
        var broker = Kit.NewBroker();
        broker.DuplicateDeliveries = true;
        var calls = 0;
        broker.SubscribeOnce("a", "t.happened", new TestInbox(), (_, _) => { calls++; return Task.CompletedTask; });

        await broker.PublishAsync(Kit.NewMessage("t.happened"));
        await broker.DeliverRoundAsync();

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AnInbox_DoesNotMarkAFailedMessage_SoTheRetryRunsAgain()
    {
        var inbox = new TestInbox();
        var attempts = 0;
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inbox.ProcessOnceAsync(id, () => { attempts++; throw new InvalidOperationException(); }));
        var second = await inbox.ProcessOnceAsync(id, () => { attempts++; return Task.CompletedTask; });
        var third = await inbox.ProcessOnceAsync(id, () => { attempts++; return Task.CompletedTask; });

        Assert.True(second);
        Assert.False(third);
        Assert.Equal(2, attempts);
        Assert.Equal(1, inbox.Count);
    }

    [Fact]
    public async Task APausedSubscriber_AccumulatesMessages_AndCatchesUpWhenItResumes()
    {
        var broker = Kit.NewBroker();
        var calls = 0;
        broker.Subscribe("a", "t.happened", (_, _) => { calls++; return Task.CompletedTask; });
        broker.Pause("a");

        await broker.PublishAsync(Kit.NewMessage("t.happened"));
        await broker.PublishAsync(Kit.NewMessage("t.happened"));
        await broker.DeliverRoundAsync();
        Assert.Equal((0, 2), (calls, broker.Pending("a")));      // the publisher did not have to wait for anybody

        broker.Resume("a");
        await broker.DeliverRoundAsync();
        Assert.Equal((2, 0), (calls, broker.Pending("a")));
    }

    [Fact]
    public async Task APublishedMessage_IsOnlyQueuedForSubscribersThatExistedAtThatMoment()
    {
        var broker = Kit.NewBroker();
        await broker.PublishAsync(Kit.NewMessage("t.happened"));
        var calls = 0;

        broker.Subscribe("late", "t.happened", (_, _) => { calls++; return Task.CompletedTask; });
        await broker.DeliverRoundAsync();

        Assert.Equal(0, calls);        // this broker is a queue per subscriber, not an event log you can replay
    }

    // ---------- outbox ----------

    [Fact]
    public async Task Outbox_KeepsMessagesPending_UntilTheRelayPublishesThem()
    {
        var broker = Kit.NewBroker();
        var calls = 0;
        broker.Subscribe("a", "t.happened", (_, _) => { calls++; return Task.CompletedTask; });
        var outbox = new TestOutbox();
        outbox.Add(Kit.NewMessage("t.happened"));

        Assert.Single(outbox.Pending());
        Assert.Equal(0, broker.Pending("a"));                    // saved, but nobody has been told yet

        var published = await new OutboxRelay(outbox, broker).PublishPendingAsync();

        Assert.Equal(1, published);
        Assert.Empty(outbox.Pending());
        Assert.Equal(1, broker.Pending("a"));
    }

    [Fact]
    public async Task Outbox_PublishesEachMessageOnce_WhenTheRelayRunsAgain()
    {
        var broker = Kit.NewBroker();
        var outbox = new TestOutbox();
        outbox.Add(Kit.NewMessage("t.happened"));
        var relay = new OutboxRelay(outbox, broker);

        Assert.Equal(1, await relay.PublishPendingAsync());
        Assert.Equal(0, await relay.PublishPendingAsync());
    }

    [Fact]
    public async Task Outbox_WhenTheBrokerIsDown_KeepsTheMessagesAndSendsThemLater()
    {
        var broker = Kit.NewBroker();
        broker.Subscribe("a", "t.happened", (_, _) => Task.CompletedTask);
        var outbox = new TestOutbox();
        outbox.Add(Kit.NewMessage("t.happened"));
        var relay = new OutboxRelay(outbox, broker);

        broker.Offline = true;
        Assert.Equal(0, await relay.PublishPendingAsync());
        Assert.Single(outbox.Pending());

        broker.Offline = false;
        Assert.Equal(1, await relay.PublishPendingAsync());
        Assert.Empty(outbox.Pending());
    }

    [Fact]
    public void Outbox_Atomically_RunsStateChangeAndMessageTogether()
    {
        var outbox = new TestOutbox();
        var saved = new List<string>();

        outbox.Atomically(() =>
        {
            saved.Add("order-1");
            outbox.Add(Kit.NewMessage("orders.order-placed.v1"));
        });

        Assert.Equal((1, 1), (saved.Count, outbox.Pending().Count));
    }

    // ---------- message format ----------

    [Fact]
    public void Message_ReadsOnlyWhatTheConsumerNeeds_IgnoringUnknownFields()
    {
        var message = new Message(Guid.NewGuid(), "t", """{"orderId":"5c1b2f0e-2f64-4b8e-bb0e-1f2f2f2f2f2f","extra":{"x":1},"newField":42}""",
            DateTimeOffset.UtcNow, "c");

        var body = message.Read<Body>();

        Assert.Equal(Guid.Parse("5c1b2f0e-2f64-4b8e-bb0e-1f2f2f2f2f2f"), body.OrderId);
    }

    [Fact]
    public void Message_WithAMalformedPayload_ThrowsOnRead() =>
        Assert.ThrowsAny<JsonException>(() => new Message(Guid.NewGuid(), "t", "{not json", DateTimeOffset.UtcNow, "c").Read<Body>());

    private sealed record Body(Guid OrderId);

    // ---------- pump ----------

    [Fact]
    public async Task Pump_FollowsAChainOfMessages_UntilEverythingIsQuiet()
    {
        var broker = Kit.NewBroker();
        var outbox = new TestOutbox();
        var last = new List<string>();
        broker.Subscribe("first", "step.one", (m, _) => { outbox.Add(Kit.NewMessage("step.two", correlation: m.CorrelationId)); return Task.CompletedTask; });
        broker.Subscribe("second", "step.two", (m, _) => { last.Add(m.CorrelationId); return Task.CompletedTask; });
        outbox.Add(Kit.NewMessage("step.one", correlation: "chain"));
        var pump = new Pump([new OutboxRelay(outbox, broker)], broker);

        var moved = await pump.RunUntilIdleAsync();

        Assert.Equal(["chain"], last);
        Assert.True(moved >= 4);
    }

    [Fact]
    public async Task Pump_WithNothingToDo_ReturnsZero()
    {
        var broker = Kit.NewBroker();

        Assert.Equal(0, await new Pump([], broker).RunUntilIdleAsync());
    }
}
