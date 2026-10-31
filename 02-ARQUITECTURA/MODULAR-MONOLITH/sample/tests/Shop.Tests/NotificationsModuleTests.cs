using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Contracts;
using Notifications.Core;
using Ordering.Contracts;
using Shop.SharedKernel;

namespace Shop.Tests;

/// <summary>Notifications alone: we play the role of Ordering by publishing its events on the real bus.</summary>
public class NotificationsModuleTests
{
    private static (IEventBus Bus, INotificationsApi Api) Create()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddSharedKernel()
            .AddNotificationsModule()
            .BuildServiceProvider();
        return (provider.GetRequiredService<IEventBus>(),
                provider.CreateScope().ServiceProvider.GetRequiredService<INotificationsApi>());
    }

    [Fact]
    public async Task OrderPlaced_SendsConfirmation_ToTheCustomer()
    {
        var (bus, api) = Create();

        await bus.PublishAsync(new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), "ana@x.com", 62m));

        var sent = Assert.Single(await api.ListForAsync("ana@x.com"));
        Assert.Contains("confirmed", sent.Subject);
        Assert.Contains("62.00", sent.Subject);
    }

    [Fact]
    public async Task OrderCancelled_SendsCancellationNotice()
    {
        var (bus, api) = Create();

        await bus.PublishAsync(new OrderCancelled(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), "ana@x.com"));

        Assert.Contains("cancelled", Assert.Single(await api.ListForAsync("ana@x.com")).Subject);
    }

    [Fact]
    public async Task List_OnlyReturnsTheRequestedCustomersNotifications()
    {
        var (bus, api) = Create();
        await bus.PublishAsync(new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), "ana@x.com", 1m));

        Assert.Empty(await api.ListForAsync("bob@x.com"));
    }

    [Fact]
    public async Task Publishing_WithNoSubscribers_DoesNotFail()
    {
        var bus = new InMemoryEventBus(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<InMemoryEventBus>.Instance);

        await bus.PublishAsync(new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), "ana@x.com", 1m));
    }
}
