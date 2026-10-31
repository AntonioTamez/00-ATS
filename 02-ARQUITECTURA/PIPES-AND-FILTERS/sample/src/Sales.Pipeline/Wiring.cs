using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Sales.Pipeline;

/// <summary>The two mechanisms the pipeline adds around filters: failure attribution and the buffered pipe.</summary>
internal static class Wiring
{
    /// <summary>
    /// Wraps a filter's output so that anything it throws is reported as a <see cref="PipelineException"/> naming it.
    /// Cancellation and failures already attributed to an upstream filter pass through untouched.
    /// </summary>
    public static async IAsyncEnumerable<T> Guard<T>(
        string filter, IAsyncEnumerable<T> stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not PipelineException)
            {
                throw new PipelineException(filter, ex);
            }

            if (!hasNext)
                yield break;
            yield return enumerator.Current;
        }
    }

    /// <summary>
    /// A pipe with a bounded buffer: the upstream stages run on their own task, at most <paramref name="capacity"/>
    /// items ahead of the consumer. When the buffer is full the producer waits (backpressure).
    /// </summary>
    public static async IAsyncEnumerable<T> Buffer<T>(
        IAsyncEnumerable<T> source, int capacity, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var producer = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(stop.Token))
                    await channel.Writer.WriteAsync(item, stop.Token);
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        });

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
                yield return item;
        }
        finally
        {
            await stop.CancelAsync();
            await producer;
        }
    }
}
