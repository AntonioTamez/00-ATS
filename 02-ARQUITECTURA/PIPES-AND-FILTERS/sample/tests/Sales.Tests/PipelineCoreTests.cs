using Sales.Pipeline;

namespace Sales.Tests;

/// <summary>
/// The pipeline core, tested with filters invented here (numbers in, numbers out): it needs no sales domain.
/// </summary>
public class PipelineCoreTests
{
    private static Map<int, int> Plus(string name, int n) => new(name, x => x + n);

    [Fact]
    public async Task FiltersRunInTheOrderTheyWereAdded()
    {
        var pipeline = Pipelines.Of<int>().Then(new Map<int, int>("double", x => x * 2)).Then(Plus("inc", 1));

        var output = await pipeline.Start(new[] { 1, 2, 3 }.ToAsyncEnumerable()).Output.ToListAsync();

        Assert.Equal(["double", "inc"], pipeline.Stages);
        Assert.Equal([3, 5, 7], output);
    }

    [Fact]
    public async Task ReorderingTwoFiltersWithMatchingSchemas_ChangesOnlyWhatTheOrderShouldChange()
    {
        var doubleThenInc = Pipelines.Of<int>().Then(new Map<int, int>("double", x => x * 2)).Then(Plus("inc", 1));
        var incThenDouble = Pipelines.Of<int>().Then(Plus("inc", 1)).Then(new Map<int, int>("double", x => x * 2));

        Assert.Equal([3, 5], await doubleThenInc.Start(new[] { 1, 2 }.ToAsyncEnumerable()).Output.ToListAsync());
        Assert.Equal([4, 6], await incThenDouble.Start(new[] { 1, 2 }.ToAsyncEnumerable()).Output.ToListAsync());
    }

    [Fact]
    public async Task FiltersCanChangeTheItemType_AsLongAsEachInputMatchesThePreviousOutput()
    {
        var pipeline = Pipelines.Of<int>()
            .Then(new Map<int, string>("to-text", x => $"#{x}"))
            .Then(new Map<string, int>("length", s => s.Length));

        Assert.Equal([2, 3], await pipeline.Start(new[] { 7, 42 }.ToAsyncEnumerable()).Output.ToListAsync());
    }

    [Fact]
    public async Task NothingIsReadFromTheSourceUntilSomeoneEnumeratesTheOutput()
    {
        var probe = new Probe();
        var run = Pipelines.Of<int>().Then(Plus("inc", 1)).Start(probe.Source(10));

        Assert.Equal(0, probe.Produced);

        await using var enumerator = run.Output.GetAsyncEnumerator();
        await enumerator.MoveNextAsync();
        Assert.Equal(1, probe.Produced);
    }

    [Fact]
    public async Task AskingForTwoItemsOutOfAThousand_PullsAtMostThree_AndReleasesTheSource()
    {
        var probe = new Probe();
        var run = Pipelines.Of<int>().Then(Plus("a", 1)).Then(Plus("b", 1)).Start(probe.Source(1000));

        var firstTwo = await run.Output.Take(2).ToListAsync();

        Assert.Equal([2, 3], firstTwo);
        Assert.InRange(probe.Produced, 2, 3);
        Assert.True(probe.Disposed, "the source must be disposed when the consumer stops early");
    }

    [Fact]
    public async Task EachItemTravelsThroughAllTheFiltersBeforeTheNextOneIsRead()
    {
        var probe = new Probe();
        var run = Pipelines.Of<int>().Then(Plus("a", 1)).Then(Plus("b", 1)).Then(Plus("c", 1)).Start(probe.Source(5000));

        await foreach (var _ in run.Output)
            probe.Received();

        Assert.Equal(5000, probe.Consumed);
        Assert.Equal(1, probe.MaxInFlight);
    }

    [Fact]
    public async Task ARefusedItemIsRecordedWithTheNameOfTheFilterAndLeavesThePipe()
    {
        var run = Pipelines.Of<int>()
            .Then(new RejectIf<int>("no-odd", x => x % 2 == 1, "odd"))
            .Start(new[] { 1, 2, 3, 4 }.ToAsyncEnumerable());

        var output = await run.Output.ToListAsync();

        Assert.Equal([2, 4], output);
        Assert.Equal(
            [new Rejection("no-odd", "item 1", "odd"), new Rejection("no-odd", "item 3", "odd")],
            run.Rejections);
    }

    [Fact]
    public async Task EachRunHasItsOwnRejections()
    {
        var pipeline = Pipelines.Of<int>().Then(new RejectIf<int>("no-odd", x => x % 2 == 1, "odd"));
        var first = pipeline.Start(new[] { 1, 2 }.ToAsyncEnumerable());
        var second = pipeline.Start(new[] { 4, 6 }.ToAsyncEnumerable());

        await first.Output.ToListAsync();
        await second.Output.ToListAsync();

        Assert.Single(first.Rejections);
        Assert.Empty(second.Rejections);
    }

    [Fact]
    public async Task AFilterThatThrows_FailsTheWholeRun_AndTheErrorNamesThatFilter()
    {
        var run = Pipelines.Of<int>()
            .Then(Plus("first", 0))
            .Then(new FailAt("boom", 2))
            .Then(Plus("last", 0))
            .Start(new[] { 1, 2, 3 }.ToAsyncEnumerable());

        var failure = await Assert.ThrowsAsync<PipelineException>(async () => await run.Output.ToListAsync());

        Assert.Equal("boom", failure.Filter);
        Assert.Equal("boom", Assert.IsType<InvalidOperationException>(failure.InnerException).Message);
    }

    [Fact]
    public async Task AFailingSource_IsBlamedOnTheFirstFilterThatPullsFromIt()
    {
        var run = Pipelines.Of<int>().Then(Plus("first", 0)).Then(Plus("second", 0)).Start(FailingSource());

        var failure = await Assert.ThrowsAsync<PipelineException>(async () => await run.Output.ToListAsync());

        Assert.Equal("first", failure.Filter);
        Assert.IsType<IOException>(failure.InnerException);
    }

    private static async IAsyncEnumerable<int> FailingSource()
    {
        yield return 1;
        await Task.Yield();
        throw new IOException("disk");
    }

    [Fact]
    public async Task CancellingStopsTheRun_WithoutWrappingTheCancellation()
    {
        using var cts = new CancellationTokenSource();
        var probe = new Probe();
        var run = Pipelines.Of<int>().Then(Plus("a", 0)).Start(probe.Source(100, cts.Token), cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var x in run.Output)
                if (x == 3)
                    cts.Cancel();
        });

        Assert.True(probe.Produced < 100);
    }

    [Fact]
    public void ANullFilterIsRefused() =>
        Assert.Throws<ArgumentNullException>(() => Pipelines.Of<int>().Then<int>(null!));

    // ---------- buffered pipes ----------

    [Fact]
    public async Task ABufferedPipe_LetsTheProducerRunAheadButOnlyByItsCapacity()
    {
        var probe = new Probe();
        var run = Pipelines.Of<int>().Buffer(4).Start(probe.Source(1000));

        await using var enumerator = run.Output.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        probe.Received();
        await Task.Delay(300);

        Assert.InRange(probe.Produced, 4, 4 + 3);
    }

    [Fact]
    public async Task ABufferedPipe_StopsTheProducerWhenTheConsumerWalksAway()
    {
        var probe = new Probe();
        var run = Pipelines.Of<int>().Buffer(4).Start(probe.Source(1000));

        await foreach (var _ in run.Output)
            break;

        Assert.True(probe.Disposed, "the producer must release the source when the consumer stops");
        Assert.True(probe.Produced < 1000);
    }

    [Fact]
    public async Task ABufferedPipe_KeepsEveryItemAndTheirOrder()
    {
        var run = Pipelines.Of<int>().Buffer(3).Then(Plus("inc", 1)).Start(Enumerable.Range(0, 1000).ToAsyncEnumerable());

        Assert.Equal(Enumerable.Range(1, 1000), await run.Output.ToListAsync());
    }

    [Fact]
    public async Task AFailureBeforeABufferedPipe_ReachesTheConsumer()
    {
        var run = Pipelines.Of<int>().Then(new FailAt("boom", 2)).Buffer(2).Start(new[] { 1, 2, 3 }.ToAsyncEnumerable());

        var failure = await Assert.ThrowsAsync<PipelineException>(async () => await run.Output.ToListAsync());

        Assert.Equal("boom", failure.Filter);
    }

    [Fact]
    public void TheBufferIsAStageOfThePipelineAndItsCapacityMustBePositive()
    {
        var pipeline = Pipelines.Of<int>().Then(Plus("a", 1)).Buffer(8).Then(Plus("b", 1));

        Assert.Equal(["a", "buffer(8)", "b"], pipeline.Stages);
        Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.Buffer(0));
    }
}
