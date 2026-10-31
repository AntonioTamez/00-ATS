namespace Sales.Pipeline;

/// <summary>The rejections of ONE run. Every run gets its own log, so runs never see each other's rejections.</summary>
internal sealed class RejectLog
{
    private readonly List<Rejection> _items = [];
    private readonly Lock _gate = new();

    public IRejectSink For(string filter) => new Sink(this, filter);

    public IReadOnlyList<Rejection> Snapshot()
    {
        lock (_gate)
            return [.. _items];
    }

    private void Add(Rejection rejection)
    {
        lock (_gate)
            _items.Add(rejection);
    }

    private sealed class Sink(RejectLog log, string filter) : IRejectSink
    {
        public void Reject(string item, string reason) => log.Add(new Rejection(filter, item, reason));
    }
}
