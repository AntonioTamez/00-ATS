using System.Reflection;
using System.Runtime.CompilerServices;
using Bank.Commands;
using Bank.Domain;
using Bank.Infrastructure;
using Bank.Queries;

namespace Bank.Tests;

/// <summary>The CQRS separation as executable rules.</summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Account).Assembly;
    private static readonly Assembly Commands = typeof(ICommand<>).Assembly;
    private static readonly Assembly Queries = typeof(IQuery<>).Assembly;
    private static readonly Assembly Infrastructure = typeof(IProjector).Assembly;

    private static IEnumerable<string> BankRefs(Assembly a) =>
        a.GetReferencedAssemblies().Select(r => r.Name!).Where(n => n.StartsWith("Bank."));

    [Fact]
    public void ReadSide_KnowsNothingOfTheWriteSide() =>
        Assert.Empty(BankRefs(Queries));

    [Fact]
    public void WriteSide_KnowsTheDomain_ButNotTheReadSide() =>
        Assert.Equal(["Bank.Domain"], BankRefs(Commands));

    [Fact]
    public void Domain_ReferencesNoOtherProject() =>
        Assert.Empty(BankRefs(Domain));

    [Fact]
    public void Infrastructure_DoesNotReferenceTheApi_NorTheCommandSide()
    {
        var refs = BankRefs(Infrastructure).ToList();

        Assert.DoesNotContain("Bank.Api", refs);
        Assert.DoesNotContain("Bank.Commands", refs);
    }

    [Fact]
    public void EveryCommand_IsASealedRecordNamedCommand_LivingInTheCommandSide()
    {
        var commands = AllTypes().Where(t => t.GetInterfaces().Any(i => IsGeneric(i, typeof(ICommand<>)))).ToList();

        Assert.NotEmpty(commands);
        Assert.All(commands, t =>
        {
            Assert.EndsWith("Command", t.Name);
            Assert.True(t.IsSealed);
            Assert.Equal(Commands, t.Assembly);
        });
    }

    [Fact]
    public void EveryQuery_IsASealedRecordNamedQuery_LivingInTheReadSide()
    {
        var queries = AllTypes().Where(t => t.GetInterfaces().Any(i => IsGeneric(i, typeof(IQuery<>)))).ToList();

        Assert.NotEmpty(queries);
        Assert.All(queries, t =>
        {
            Assert.EndsWith("Query", t.Name);
            Assert.True(t.IsSealed);
            Assert.Equal(Queries, t.Assembly);
        });
    }

    [Fact]
    public void EveryCommandAndQuery_HasExactlyOneHandler()
    {
        var messages = AllTypes()
            .Where(t => t.GetInterfaces().Any(i => IsGeneric(i, typeof(ICommand<>)) || IsGeneric(i, typeof(IQuery<>))))
            .ToList();
        var handled = AllTypes()
            .SelectMany(t => t.GetInterfaces())
            .Where(i => IsGeneric(i, typeof(ICommandHandler<,>)) || IsGeneric(i, typeof(IQueryHandler<,>)))
            .Select(i => i.GetGenericArguments()[0])
            .ToList();

        Assert.All(messages, m => Assert.Single(handled, h => h == m));
    }

    [Fact]
    public void CommandHandlers_ReturnOnlyAnIdOrNothing()
    {
        var results = AllTypes()
            .SelectMany(t => t.GetInterfaces())
            .Where(i => IsGeneric(i, typeof(ICommandHandler<,>)))
            .Select(i => i.GetGenericArguments()[1])
            .ToList();

        Assert.All(results, r => Assert.True(r == typeof(Guid) || r == typeof(Unit), $"Command returns {r.Name}"));
    }

    [Fact]
    public void ReadStore_OnlyOffersQuestions()
    {
        var methods = typeof(IAccountReadStore).GetMethods();

        Assert.All(methods, m =>
        {
            Assert.True(m.Name.StartsWith("Get") || m.Name.StartsWith("List"), $"{m.Name} is not a question");
            Assert.Equal(typeof(Task), m.ReturnType.BaseType);
        });
    }

    [Fact]
    public void QueryHandlers_DependOnlyOnTheReadStore()
    {
        var handlers = Queries.GetTypes().Where(t => t.GetInterfaces().Any(i => IsGeneric(i, typeof(IQueryHandler<,>))));

        Assert.All(handlers, h =>
            Assert.All(h.GetConstructors().Single().GetParameters(),
                p => Assert.Equal(typeof(IAccountReadStore), p.ParameterType)));
    }

    [Fact]
    public void ReadModelDtos_AreImmutable()
    {
        var dtos = Queries.GetExportedTypes().Where(t => t.Name is "AccountSummary" or "TransactionItem");

        Assert.All(dtos, t => Assert.All(t.GetProperties(), p =>
            Assert.True(p.SetMethod is null || p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)),
                $"{t.Name}.{p.Name} is mutable")));
    }

    [Fact]
    public void Account_HasNoPublicConstructorsNorSetters()
    {
        Assert.Empty(typeof(Account).GetConstructors());
        Assert.All(typeof(Account).GetProperties(), p => Assert.True(p.SetMethod is null || !p.SetMethod.IsPublic, p.Name));
    }

    [Fact]
    public void OnlyTheProjector_CanChangeTheReadModel()
    {
        // The store that can be mutated is not even visible outside Bank.Infrastructure.
        var store = Infrastructure.GetTypes().Single(t => t.Name == "ReadModelStore");

        Assert.False(store.IsVisible);
    }

    private static IEnumerable<Type> AllTypes() => Commands.GetTypes().Concat(Queries.GetTypes());

    private static bool IsGeneric(Type type, Type definition) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == definition;
}
