using System.Reflection;
using System.Runtime.CompilerServices;
using Hotel.Domain;
using Hotel.Domain.Common;

namespace Hotel.Tests;

/// <summary>
/// DDD tactical rules as executable tests. If someone breaks an aggregate boundary,
/// makes a value object mutable or exposes a public constructor on an entity, the build goes red.
/// </summary>
public class DddRulesTests
{
    private static readonly Assembly DomainAssembly = typeof(IValueObject).Assembly;
    private static readonly Type[] DomainTypes = DomainAssembly.GetTypes().Where(t => !t.IsNested || t.IsNestedPublic).ToArray();

    private static bool Inherits(Type type, Type openGeneric)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == openGeneric) return true;
        return false;
    }

    private static IEnumerable<Type> AggregateRoots => DomainTypes.Where(t => !t.IsAbstract && Inherits(t, typeof(AggregateRoot<>)));
    private static IEnumerable<Type> Entities => DomainTypes.Where(t => !t.IsAbstract && Inherits(t, typeof(Entity<>)));
    private static IEnumerable<Type> ValueObjects => DomainTypes.Where(t => typeof(IValueObject).IsAssignableFrom(t) && !t.IsInterface);

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    private static IEnumerable<PropertyInfo> PubliclyMutable(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true } set && !IsInitOnly(set));

    // --- sanity: the rules below must be examining something

    [Fact]
    public void TheModelContainsAggregatesEntitiesAndValueObjects()
    {
        Assert.Equal(2, AggregateRoots.Count());   // Room, Reservation
        Assert.Contains(typeof(Hotel.Domain.Reservations.Charge), Entities);
        Assert.True(ValueObjects.Count() >= 5);    // Money, DateRange, GuestInfo, RoomId, ReservationId
    }

    // --- aggregates and entities

    [Fact]
    public void Entities_HaveNoPublicConstructors_SoTheyAreCreatedThroughTheirRootOrFactory() =>
        Assert.All(Entities, t => Assert.Empty(t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));

    [Fact]
    public void Entities_HaveNoPublicSetters_SoStateChangesOnlyThroughBehavior() =>
        Assert.All(Entities, t => Assert.Empty(PubliclyMutable(t)));

    [Fact]
    public void AggregateRoots_ReferenceOtherAggregatesByIdentityOnly()
    {
        var roots = AggregateRoots.ToHashSet();

        foreach (var root in roots)
            foreach (var property in root.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var types = new[] { property.PropertyType }.Concat(
                    property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments() : []);
                Assert.False(types.Any(roots.Contains),
                    $"{root.Name}.{property.Name} holds another aggregate root; hold its id instead.");
            }
    }

    // --- value objects

    [Fact]
    public void ValueObjects_AreImmutable() =>
        Assert.All(ValueObjects, t => Assert.Empty(PubliclyMutable(t)));

    [Fact]
    public void ValueObjects_HaveNoPublicMutableFields() =>
        Assert.All(ValueObjects, t =>
            Assert.True(t.GetFields(BindingFlags.Public | BindingFlags.Instance).All(f => f.IsInitOnly),
                $"{t.Name} exposes a mutable public field."));

    [Fact]
    public void ValueObjects_AreStructsOrSealed() =>
        Assert.All(ValueObjects, t => Assert.True(t.IsValueType || t.IsSealed, $"{t.Name} must be a struct or sealed."));

    // --- domain events

    [Fact]
    public void DomainEvents_AreImmutable()
    {
        var events = DomainTypes.Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsInterface).ToList();

        Assert.NotEmpty(events);
        Assert.All(events, t => Assert.Empty(PubliclyMutable(t)));
    }

    // --- repositories

    [Fact]
    public void RepositoryInterfaces_AreDeclaredPerAggregateRoot_ThroughTheGenericContract()
    {
        var repositories = DomainTypes.Where(t => t.IsInterface && t.Name.EndsWith("Repository")).ToList();

        Assert.NotEmpty(repositories);
        Assert.All(repositories, r => Assert.Contains(r.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<,>)));
    }

    // --- layer dependencies

    private static List<string> HotelRefs(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Hotel.")).ToList();

    [Fact]
    public void Domain_DependsOnNoOtherProjectOrFramework()
    {
        var refs = DomainAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.Empty(HotelRefs(DomainAssembly));
        Assert.DoesNotContain(refs, n => n.StartsWith("Microsoft.AspNetCore") || n.StartsWith("Microsoft.EntityFrameworkCore"));
    }

    [Fact]
    public void Application_DependsOnlyOnTheDomain() =>
        Assert.Equal(["Hotel.Domain"], HotelRefs(typeof(Hotel.Application.IClock).Assembly));

    [Fact]
    public void Infrastructure_DoesNotDependOnTheApi() =>
        Assert.DoesNotContain(HotelRefs(typeof(Hotel.Infrastructure.DependencyInjection).Assembly), n => n == "Hotel.Api");
}
