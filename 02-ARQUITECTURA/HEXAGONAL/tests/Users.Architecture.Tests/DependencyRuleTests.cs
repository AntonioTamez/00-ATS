using System.Reflection;
using NetArchTest.Rules;
using Users.Api.Controllers;
using Users.Application.Ports.In;
using Users.Application.Ports.Out;
using Users.Domain.Users;
using Users.Infrastructure;

namespace Users.Architecture.Tests;

public sealed class DependencyRuleTests
{
    private const string Domain = "Users.Domain";
    private const string Application = "Users.Application";
    private const string Infrastructure = "Users.Infrastructure";
    private const string Api = "Users.Api";

    private static readonly Assembly DomainAssembly = typeof(User).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IListUsersQuery).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = typeof(UsersController).Assembly;

    [Fact]
    public void Domain_DependsOnNothingOutsideTheBcl()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api, "Microsoft.AspNetCore", "Microsoft.Extensions", "System.Net.Http")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Domain_HasNoAssemblyReferencesToOtherProjects() =>
        Assert.DoesNotContain(DomainAssembly.GetReferencedAssemblies(), a => a.Name!.StartsWith("Users.", StringComparison.Ordinal));

    [Fact]
    public void Application_DoesNotDependOnAdapters()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api, "Microsoft.AspNetCore")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(Api)
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Ports_AreInterfacesOwnedByApplication()
    {
        var ports = Types.InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespaceStartingWith("Users.Application.Ports")
            .GetTypes()
            .ToList();

        Assert.NotEmpty(ports);
        Assert.All(ports, t => Assert.True(t.IsInterface, $"{t.FullName} must be an interface."));
        Assert.Contains(typeof(IListUsersQuery), ports);
        Assert.Contains(typeof(IUserRepository), ports);
    }

    [Fact]
    public void Adapters_ImplementPortsDefinedInTheCore()
    {
        Assert.Contains(typeof(IUserRepository), typeof(Infrastructure.Persistence.InMemoryUserRepository).GetInterfaces());
        Assert.Empty(Types.InAssembly(InfrastructureAssembly).That().AreInterfaces().GetTypes());
    }

    [Fact]
    public void Controllers_DependOnlyOnDrivingPorts()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, "Users.Application.Ports.Out", "Users.Application.ListUsers.ListUsersHandler", Domain)
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void ApiContracts_DoNotExposeDomainTypes()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Users.Api.Contracts")
            .ShouldNot()
            .HaveDependencyOn(Domain)
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void User_HasNoPublicConstructorsOrSetters()
    {
        Assert.Empty(typeof(User).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(
            typeof(User).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            p => Assert.True(p.SetMethod is null || !p.SetMethod.IsPublic, $"{p.Name} must not have a public setter."));
    }

    [Fact]
    public void ValueObjectsAndAggregate_AreSealedAndImmutable()
    {
        foreach (var type in new[] { typeof(User), typeof(UserId), typeof(Email), typeof(FullName) })
        {
            Assert.True(type.IsSealed, $"{type.Name} must be sealed.");
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        }
    }

    private static void AssertSuccess(TestResult result) =>
        Assert.True(result.IsSuccessful, "Violations: " + string.Join(", ", result.FailingTypeNames ?? []));
}
