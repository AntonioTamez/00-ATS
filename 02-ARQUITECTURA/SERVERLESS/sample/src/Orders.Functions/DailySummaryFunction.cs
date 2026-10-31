using System.Text.Json;
using Faas.Runtime;

namespace Orders.Functions;

/// <summary>
/// Timer trigger. Counts the orders by status and publishes the summary to "reports". It computes everything from
/// the store each time, so running it twice, or after a missed run, gives the same answer: it is idempotent.
/// </summary>
public sealed class DailySummaryFunction : ITimerFunction
{
    private readonly IOrderStore _store;

    public DailySummaryFunction(IOrderStore store) => _store = store;

    public async Task<Outputs> HandleAsync(TimerInfo timer, FunctionContext context)
    {
        var orders = await _store.ListAsync();
        var summary = new
        {
            date = context.Now.ToString("yyyy-MM-dd"),
            received = orders.Count(o => o.Status == OrderStatus.Received),
            reserved = orders.Count(o => o.Status == OrderStatus.Reserved),
            outOfStock = orders.Count(o => o.Status == OrderStatus.OutOfStock),
        };

        return Outputs.Send(new OutgoingMessage("reports", JsonSerializer.Serialize(summary)));
    }
}
