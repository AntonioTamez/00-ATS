using Helpdesk.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Tests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class TestDb
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A fresh, isolated in-memory database per test: slices are tested against the real DbContext.</summary>
    public static AppDbContext New() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"test-{Guid.NewGuid():N}").Options);
}
