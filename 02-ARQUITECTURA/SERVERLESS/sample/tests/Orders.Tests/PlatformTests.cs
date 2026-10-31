using Faas.Runtime;
using static Orders.Tests.Support;

namespace Orders.Tests;

/// <summary>The platform on its own, with functions invented here: it needs no orders to be tested.</summary>
public class PlatformTests
{
    private static FunctionOptions Options(int delivery = 3, int concurrency = 1, int timeoutMs = 2000) => new()
    {
        MaxDeliveryCount = delivery,
        MaxConcurrency = concurrency,
        Timeout = TimeSpan.FromMilliseconds(timeoutMs),
    };

    private static Task<Outputs> Done() => Task.FromResult(Outputs.None);

    // ---------- HTTP ----------

    [Fact]
    public async Task HttpRequestsAreRoutedByMethodAndPath_AndUnknownRoutesAre404()
    {
        var host = new FunctionHost(new InMemoryMessageBus())
            .AddHttp("hello", "GET", "/hello", () => new HttpFn((_, _) => Task.FromResult(new HttpResponse(200, "hi"))));

        Assert.Equal(200, (await host.InvokeHttpAsync(new HttpRequest("get", "/hello", ""))).Status);
        Assert.Equal(404, (await host.InvokeHttpAsync(new HttpRequest("POST", "/hello", ""))).Status);
        Assert.Equal(404, (await host.InvokeHttpAsync(new HttpRequest("GET", "/nope", ""))).Status);
    }

    [Fact]
    public async Task AThrowingHttpFunctionBecomes500_AndItsMessageIsNeverLeaked()
    {
        var host = new FunctionHost(new InMemoryMessageBus())
            .AddHttp("bad", "GET", "/bad", () => new HttpFn((_, _) => throw new InvalidOperationException("db password is hunter2")));

        var response = await host.InvokeHttpAsync(new HttpRequest("GET", "/bad", ""));

        Assert.Equal(500, response.Status);
        Assert.DoesNotContain("hunter2", response.Body);
        var record = Assert.Single(host.Invocations);
        Assert.Equal((Outcome.Failed, "db password is hunter2"), (record.Outcome, record.Error));
    }

    [Fact]
    public async Task AnHttpFunctionThatIsTooSlowBecomes504_EvenIfItIgnoresTheCancellationToken()
    {
        var host = new FunctionHost(new InMemoryMessageBus())
            .AddHttp("slow", "GET", "/slow", () => new HttpFn(async (_, _) =>
            {
                await Task.Delay(400);
                return new HttpResponse(200, "late");
            }), Options(timeoutMs: 50));

        var response = await host.InvokeHttpAsync(new HttpRequest("GET", "/slow", ""));

        Assert.Equal(504, response.Status);
        Assert.Equal(Outcome.TimedOut, Assert.Single(host.Invocations).Outcome);
    }

    [Fact]
    public async Task TheTimeoutAlsoCancelsTheTokenSoACooperativeFunctionCanStop()
    {
        var stopped = new TaskCompletionSource();
        var host = new FunctionHost(new InMemoryMessageBus())
            .AddHttp("slow", "GET", "/slow", () => new HttpFn(async (_, context) =>
            {
                try
                {
                    await Task.Delay(5000, context.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    stopped.SetResult();
                    throw;
                }

                return new HttpResponse(200, "late");
            }), Options(timeoutMs: 50));

        await host.InvokeHttpAsync(new HttpRequest("GET", "/slow", ""));

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AnHttpFunctionCanSendMessagesToAQueue_AndTheyArriveOnlyWhenItSucceeds()
    {
        var bus = new InMemoryMessageBus();
        var host = new FunctionHost(bus)
            .AddHttp("ok", "POST", "/ok", () => new HttpFn((_, _) =>
                Task.FromResult(new HttpResponse(202, "", Outputs.Send(new OutgoingMessage("work", "job-1"))))))
            .AddHttp("bad", "POST", "/bad", () => new HttpFn((_, _) => throw new InvalidOperationException("boom")));

        await host.InvokeHttpAsync(new HttpRequest("POST", "/bad", ""));
        Assert.Equal(0, bus.Pending("work"));

        await host.InvokeHttpAsync(new HttpRequest("POST", "/ok", ""));
        Assert.Equal("job-1", Assert.Single(bus.Peek("work")).Body);
    }

    // ---------- queues ----------

    [Fact]
    public async Task ASuccessfulMessageIsCompleted_NotRedelivered()
    {
        var bus = new InMemoryMessageBus();
        var host = new FunctionHost(bus).AddQueue("f", "in", () => new QueueFn((_, _) => Done()), Options());
        bus.Send("in", "x");

        var invocations = await host.DrainQueuesAsync();

        Assert.Equal(1, invocations);
        Assert.Equal(0, bus.Pending("in"));
        Assert.Empty(bus.DeadLetters("in"));
        Assert.Equal((Outcome.Succeeded, 1), (host.Invocations[0].Outcome, host.Invocations[0].Attempt));
    }

    [Fact]
    public async Task AFailingMessageIsRedelivered_WithGrowingAttempts_AndFinallyDeadLettered()
    {
        var bus = new InMemoryMessageBus();
        var host = new FunctionHost(bus)
            .AddQueue("f", "in", () => new QueueFn((_, _) => throw new InvalidOperationException("boom")), Options(delivery: 3));
        bus.Send("in", "x", "m1");

        var invocations = await host.DrainQueuesAsync();

        Assert.Equal(3, invocations);
        Assert.Equal([1, 2, 3], host.Invocations.Select(r => r.Attempt));
        Assert.All(host.Invocations, r => Assert.Equal(Outcome.Failed, r.Outcome));
        var dead = Assert.Single(bus.DeadLetters("in"));
        Assert.Equal(("m1", 3, "boom"), (dead.Message.Id, dead.Message.DeliveryCount, dead.Reason));
        Assert.Equal(0, bus.Pending("in"));
    }

    [Fact]
    public async Task ARetryCanSucceed_AndThenNothingIsDeadLettered()
    {
        var bus = new InMemoryMessageBus();
        var host = new FunctionHost(bus).AddQueue("f", "in", () => new QueueFn((_, context) =>
        {
            if (context.Attempt == 1)
                throw new InvalidOperationException("transient");
            return Done();
        }), Options());
        bus.Send("in", "x");

        await host.DrainQueuesAsync();

        Assert.Equal([Outcome.Failed, Outcome.Succeeded], host.Invocations.Select(r => r.Outcome));
        Assert.Empty(bus.DeadLetters("in"));
    }

    [Fact]
    public async Task ATimedOutMessageCountsAsAFailure()
    {
        var bus = new InMemoryMessageBus();
        var host = new FunctionHost(bus).AddQueue("f", "in", () => new QueueFn(async (_, _) =>
        {
            await Task.Delay(400);
            return Outputs.None;
        }), Options(delivery: 2, timeoutMs: 50));
        bus.Send("in", "x");

        await host.DrainQueuesAsync();

        Assert.Equal([Outcome.TimedOut, Outcome.TimedOut], host.Invocations.Select(r => r.Outcome));
        Assert.StartsWith("timed out after 50 ms", Assert.Single(bus.DeadLetters("in")).Reason);
    }

    [Fact]
    public async Task WhatAFunctionReturnsTooLate_IsNeverSent()
    {
        var bus = new InMemoryMessageBus();
        var host = new FunctionHost(bus).AddQueue("f", "in", () => new QueueFn(async (_, _) =>
        {
            await Task.Delay(300);
            return Outputs.Send(new OutgoingMessage("out", "late"));
        }), Options(delivery: 1, timeoutMs: 50));
        bus.Send("in", "x");

        await host.DrainQueuesAsync();
        await Task.Delay(500);

        Assert.Equal(0, bus.Pending("out"));
    }

    [Fact]
    public async Task TheSameMessageSentTwice_IsDeliveredTwice_ThePlatformDoesNotDeduplicate()
    {
        var bus = new InMemoryMessageBus();
        var seen = new List<string>();
        var host = new FunctionHost(bus).AddQueue("f", "in", () => new QueueFn((message, _) =>
        {
            lock (seen)
                seen.Add(message.Id);
            return Done();
        }), Options());
        bus.Send("in", "x", "same-id");
        bus.Send("in", "x", "same-id");

        await host.DrainQueuesAsync();

        Assert.Equal(["same-id", "same-id"], seen);
    }

    [Fact]
    public async Task FunctionsTalkOnlyThroughQueues_AndOneDrainFollowsTheWholeChain()
    {
        var bus = new InMemoryMessageBus();
        var received = new List<string>();
        var host = new FunctionHost(bus)
            .AddQueue("first", "a", () => new QueueFn((message, _) =>
                Task.FromResult(Outputs.Send(new OutgoingMessage("b", message.Body + "!")))), Options())
            .AddQueue("second", "b", () => new QueueFn((message, _) =>
            {
                received.Add(message.Body);
                return Done();
            }), Options());
        bus.Send("a", "hello");

        var invocations = await host.DrainQueuesAsync();

        Assert.Equal(2, invocations);
        Assert.Equal(["hello!"], received);
    }

    [Fact]
    public async Task AQueueWithoutAFunction_KeepsItsMessages()
    {
        var bus = new InMemoryMessageBus();
        var host = new FunctionHost(bus);
        bus.Send("nobody", "x");

        Assert.Equal(0, await host.DrainQueuesAsync());
        Assert.Equal(1, bus.Pending("nobody"));
    }

    // ---------- scale and instances ----------

    [Fact]
    public async Task MaxConcurrencyLimitsHowManyMessagesRunAtOnce()
    {
        var bus = new InMemoryMessageBus();
        var probe = new ConcurrencyProbe();
        var host = new FunctionHost(bus).AddQueue("f", "in", () => new QueueFn(async (_, _) =>
        {
            await probe.HoldAsync(100);
            return Outputs.None;
        }), Options(concurrency: 3));
        for (var i = 0; i < 9; i++)
            bus.Send("in", i.ToString());

        await host.DrainQueuesAsync();

        Assert.Equal(3, probe.Max);
        Assert.Equal(9, host.Invocations.Count);
    }

    [Fact]
    public async Task ByDefaultMessagesAreProcessedOneAtATime()
    {
        var bus = new InMemoryMessageBus();
        var probe = new ConcurrencyProbe();
        var host = new FunctionHost(bus).AddQueue("f", "in", () => new QueueFn(async (_, _) =>
        {
            await probe.HoldAsync(20);
            return Outputs.None;
        }));
        for (var i = 0; i < 4; i++)
            bus.Send("in", i.ToString());

        await host.DrainQueuesAsync();

        Assert.Equal(1, probe.Max);
    }

    [Fact]
    public async Task AWarmPlatformCreatesOneInstance_ARecyclingOneCreatesOnePerInvocation()
    {
        foreach (var (recycle, expected) in new[] { (false, 1), (true, 5) })
        {
            var bus = new InMemoryMessageBus();
            var created = 0;
            var host = new FunctionHost(bus, new HostOptions { RecycleInstances = recycle })
                .AddQueue("f", "in", () =>
                {
                    created++;
                    return new QueueFn((_, _) => Done());
                }, Options());
            for (var i = 0; i < 5; i++)
                bus.Send("in", i.ToString());

            await host.DrainQueuesAsync();

            Assert.Equal(expected, created);
            Assert.Equal(expected, host.ColdStarts("f"));
        }
    }

    // ---------- the invocation context ----------

    [Fact]
    public async Task EveryInvocationGetsItsOwnContext()
    {
        var bus = new InMemoryMessageBus();
        var contexts = new List<FunctionContext>();
        var host = new FunctionHost(bus, new HostOptions { Clock = new FixedClock(Now) }).AddQueue("f", "in", () => new QueueFn((_, context) =>
        {
            lock (contexts)
                contexts.Add(context);
            return Done();
        }), Options());
        for (var i = 0; i < 4; i++)
            bus.Send("in", i.ToString());

        await host.DrainQueuesAsync();

        Assert.Equal(4, contexts.Select(c => c.InvocationId).Distinct().Count());
        Assert.All(contexts, c => Assert.Equal((1, Now), (c.Attempt, c.Now)));
        Assert.Equal(contexts.Select(c => c.InvocationId), host.Invocations.Select(r => r.InvocationId));
    }

    // ---------- timers ----------

    [Fact]
    public async Task ATimerTickRunsTheFunction_ShowsTheClock_AndSendsItsOutputs()
    {
        var bus = new InMemoryMessageBus();
        var host = new FunctionHost(bus, new HostOptions { Clock = new FixedClock(Now) })
            .AddTimer("tick", () => new TimerFn((timer, _) =>
                Task.FromResult(Outputs.Send(new OutgoingMessage("reports", timer.ScheduledAt.ToString("O"))))));

        var record = await host.FireTimerAsync("tick");

        Assert.Equal(Outcome.Succeeded, record.Outcome);
        Assert.Equal(Now.ToString("O"), Assert.Single(bus.Peek("reports")).Body);
    }

    [Fact]
    public async Task AFailedTimerTickIsRecordedAndNotRetried()
    {
        var bus = new InMemoryMessageBus();
        var host = new FunctionHost(bus).AddTimer("tick", () => new TimerFn((_, _) => throw new InvalidOperationException("nope")));

        var record = await host.FireTimerAsync("tick");

        Assert.Equal((Outcome.Failed, "nope"), (record.Outcome, record.Error));
        Assert.Single(host.Invocations);
    }

    [Fact]
    public async Task AnUnknownTimerIsAnError() =>
        await Assert.ThrowsAsync<ArgumentException>(() => new FunctionHost(new InMemoryMessageBus()).FireTimerAsync("nope"));

    // ---------- registration ----------

    [Fact]
    public void FunctionNamesMustBeUnique() =>
        Assert.Throws<ArgumentException>(() => new FunctionHost(new InMemoryMessageBus())
            .AddQueue("f", "a", () => new QueueFn((_, _) => Done()))
            .AddTimer("f", () => new TimerFn((_, _) => Done())));

    [Fact]
    public void OptionsMustBePositive()
    {
        var host = new FunctionHost(new InMemoryMessageBus());
        Assert.Throws<ArgumentOutOfRangeException>(() => host.AddQueue("a", "q", () => new QueueFn((_, _) => Done()), new FunctionOptions { MaxConcurrency = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.AddQueue("b", "q", () => new QueueFn((_, _) => Done()), new FunctionOptions { MaxDeliveryCount = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.AddQueue("c", "q", () => new QueueFn((_, _) => Done()), new FunctionOptions { Timeout = TimeSpan.Zero }));
    }
}
