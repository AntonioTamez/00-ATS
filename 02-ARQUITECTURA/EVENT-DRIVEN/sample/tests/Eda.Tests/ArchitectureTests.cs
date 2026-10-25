using System.Reflection;
using Eda.Inventory;
using Eda.Messaging;
using Eda.Notifications;
using Eda.Orders;

namespace Eda.Tests;

/// <summary>The event-driven design rules as executable checks.</summary>
public class ArchitectureTests
{
    private sealed record Participant(string Name, Assembly Assembly);

    private static readonly Participant[] Participants =
    [
        new("Orders", typeof(OrdersModule).Assembly),
        new("Inventory", typeof(InventoryModule).Assembly),
        new("Notifications", typeof(NotificationsModule).Assembly),
    ];

    private static readonly Assembly Messaging = typeof(IMessageBus).Assembly;

    public static TheoryData<string> Names => new(Participants.Select(p => p.Name));

    private static Participant Get(string name) => Participants.Single(p => p.Name == name);

    private static IEnumerable<string> EdaRefs(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Eda."));

    [Theory]
    [MemberData(nameof(Names))]
    public void AParticipant_KnowsOnlyTheMessagingPlumbing_NeverAnotherParticipant(string name) =>
        Assert.Equal(["Eda.Messaging"], EdaRefs(Get(name).Assembly));

    [Fact]
    public void TheMessagingPlumbing_KnowsNoParticipant() =>
        Assert.Empty(EdaRefs(Messaging));

    [Theory]
    [MemberData(nameof(Names))]
    public void AParticipant_ExposesOnlyItsModuleEntryPoint(string name) =>
        Assert.Equal([$"{name}Module"], Get(name).Assembly.GetExportedTypes().Select(t => t.Name));

    [Theory]
    [MemberData(nameof(Names))]
    public void AParticipant_NeverPublishesToTheBrokerDirectly_OnlyThroughItsOutbox(string name)
    {
        var offenders = Get(name).Assembly.GetTypes()
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IMessageBus) || p.ParameterType == typeof(InMemoryBroker))))
            .Select(t => t.Name);

        Assert.Empty(offenders);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TheMessageTypesAParticipantReads_AreItsOwnPrivateCopies(string name)
    {
        var messageTypes = Get(name).Assembly.GetTypes().Where(t => t.Name.EndsWith("Message")).ToList();

        Assert.All(messageTypes, t => Assert.False(t.IsVisible, $"{t.Name} would be shareable"));
    }

    [Fact]
    public void ADataStoreOrQueue_IsNeverVisibleOutsideItsParticipant()
    {
        var suffixes = new[] { "Store", "Log", "Outbox", "Inbox" };
        var exposed = Participants.SelectMany(p => p.Assembly.GetTypes())
            .Where(t => suffixes.Any(s => t.Name.EndsWith(s)) && t.IsVisible)
            .Select(t => t.FullName);

        Assert.Empty(exposed);
    }

    [Fact]
    public void EveryParticipantHasItsOwnOutboxAndInbox_NeverTheSharedBaseClassesDirectly()
    {
        foreach (var participant in Participants)
        {
            var types = participant.Assembly.GetTypes();
            Assert.DoesNotContain(types, t => t == typeof(Outbox) || t == typeof(Inbox));
            Assert.Contains(types, t => t.BaseType == typeof(Inbox));           // every participant consumes: it has an inbox
        }
    }
}
