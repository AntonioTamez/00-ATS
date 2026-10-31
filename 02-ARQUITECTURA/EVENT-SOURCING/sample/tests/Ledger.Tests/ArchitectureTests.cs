using System.Reflection;
using System.Runtime.CompilerServices;
using Ledger.Domain;
using Ledger.Infrastructure;

namespace Ledger.Tests;

/// <summary>The event-sourcing design rules as executable checks.</summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(StockItem).Assembly;
    private static readonly Assembly Infrastructure = typeof(IEventStore).Assembly;

    private static IEnumerable<Type> EventTypes() =>
        Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

    [Fact]
    public void TheDomain_KnowsNothingOfStorage_NorOfAnyOtherProject() =>
        Assert.DoesNotContain(Domain.GetReferencedAssemblies().Select(a => a.Name!),
            n => n.StartsWith("Ledger.") || n.StartsWith("Microsoft.AspNetCore"));

    [Fact]
    public void TheInfrastructure_DependsOnTheDomain_AndOnNothingElseOfOurs() =>
        Assert.Equal(["Ledger.Domain"], Infrastructure.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Ledger.")));

    [Fact]
    public void ThereAreEventsToCheck() =>
        Assert.True(EventTypes().Count() >= 5);

    [Fact]
    public void EveryEvent_IsAnImmutableSealedRecord()
    {
        Assert.All(EventTypes(), t =>
        {
            Assert.True(t.IsSealed, $"{t.Name} must be sealed");
            Assert.All(t.GetProperties(), p =>
                Assert.True(p.SetMethod is null || p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)),
                    $"{t.Name}.{p.Name} is mutable"));
        });
    }

    [Fact]
    public void EveryEvent_IsNamedInThePastTense_BecauseItAlreadyHappened() =>
        Assert.All(EventTypes(), t => Assert.EndsWith("ed", t.Name));

    [Fact]
    public void EveryEvent_IsRegisteredInTheSerializer_OtherwiseItCouldNeverBeStored()
    {
        var registered = EventSerializer.CreateDefault().RegisteredTypes;

        Assert.Empty(EventTypes().Except(registered));
    }

    [Fact]
    public void TheAggregate_HasNoPublicConstructorsNorSetters_ItsStateChangesOnlyThroughEvents()
    {
        Assert.Empty(typeof(StockItem).GetConstructors());
        Assert.All(typeof(StockItem).GetProperties(), p => Assert.True(p.SetMethod is null || !p.SetMethod.IsPublic, p.Name));
    }

    [Fact]
    public void TheStoreInterface_CanOnlyAppendAndRead() =>
        Assert.All(typeof(IEventStore).GetMethods(), m => Assert.True(m.Name.StartsWith("Append") || m.Name.StartsWith("Read"), m.Name));

    [Fact]
    public void StoredEvents_AreImmutableRecords() =>
        Assert.All(typeof(StoredEvent).GetProperties(), p =>
            Assert.True(p.SetMethod is null || p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)), p.Name));
}
