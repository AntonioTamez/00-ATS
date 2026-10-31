using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.AspNetCore.Mvc;
using Shop.ConsoleView;
using Shop.Model;
using Shop.Mvc.Controllers;
using Shop.Mvc.Models;
using Shop.ViewModels;

namespace Shop.Tests;

/// <summary>The presentation patterns' rules as executable checks.</summary>
public class ArchitectureTests
{
    private static readonly Assembly Model = typeof(Cart).Assembly;
    private static readonly Assembly Mvc = typeof(CartController).Assembly;
    private static readonly Assembly ViewModels = typeof(ShopViewModel).Assembly;
    private static readonly Assembly Console = typeof(ConsoleShopView).Assembly;

    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!);

    private static IEnumerable<string> ShopRefs(Assembly assembly) =>
        References(assembly).Where(n => n.StartsWith("Shop."));

    // ---------- the Model ----------

    [Fact]
    public void TheModel_KnowsNoUi_NorAnyOtherProject() =>
        Assert.Empty(ShopRefs(Model));

    [Fact]
    public void TheModel_UsesNoUiFrameworkAtAll() =>
        Assert.DoesNotContain(References(Model), n =>
            n.StartsWith("Microsoft.AspNetCore") || n.Contains("PresentationFramework") || n.Contains("Maui") || n.Contains("System.Windows.Forms"));

    // ---------- MVC ----------

    [Fact]
    public void Mvc_DependsOnTheModel_AndOnNothingElseOfOurs() =>
        Assert.Equal(["Shop.Model"], ShopRefs(Mvc));

    [Fact]
    public void ControllerActions_ReturnActionResults()
    {
        var actions = Mvc.GetTypes()
            .Where(t => typeof(Controller).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .ToList();

        Assert.NotEmpty(actions);
        Assert.All(actions, m => Assert.True(typeof(IActionResult).IsAssignableFrom(m.ReturnType), $"{m.DeclaringType!.Name}.{m.Name}"));
    }

    [Fact]
    public void MvcViewModels_ArePassiveData_NoNotifications_NoCommands_ImmutableProperties()
    {
        var viewModels = Mvc.GetExportedTypes().Where(t => t.Namespace == "Shop.Mvc.Models").ToList();

        Assert.NotEmpty(viewModels);
        Assert.All(viewModels, t =>
        {
            Assert.False(typeof(INotifyPropertyChanged).IsAssignableFrom(t), $"{t.Name} notifies changes: that is MVVM");
            Assert.DoesNotContain(t.GetProperties(), p => typeof(ICommand).IsAssignableFrom(p.PropertyType));
            Assert.All(t.GetProperties(), p =>
                Assert.True(p.SetMethod is null || p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)),
                    $"{t.Name}.{p.Name} is mutable"));
        });
    }

    [Fact]
    public void TheViews_ContainNoModelCode()
    {
        var views = FindViewsFolder();

        var offenders = Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("Shop.Model") || File.ReadAllText(f).Contains("@inject"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.NotEmpty(Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories));
        Assert.Empty(offenders);
    }

    // ---------- MVVM ----------

    [Fact]
    public void TheViewModel_DependsOnTheModel_AndOnNothingElseOfOurs() =>
        Assert.Equal(["Shop.Model"], ShopRefs(ViewModels));

    [Fact]
    public void TheViewModel_KnowsNoUiFramework_NorAnyConcreteView() =>
        Assert.DoesNotContain(References(ViewModels), n =>
            n.StartsWith("Microsoft.AspNetCore") || n.Contains("PresentationFramework") || n.Contains("PresentationCore")
            || n.Contains("Maui") || n.Contains("System.Windows.Forms") || n.Contains("WindowsBase") || n == "System.Console");

    [Fact]
    public void TheShopViewModel_IsActive_ItNotifiesChanges_AndExposesCommands()
    {
        Assert.True(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(ShopViewModel)));
        var commands = typeof(ShopViewModel).GetProperties().Where(p => typeof(ICommand).IsAssignableFrom(p.PropertyType)).ToList();

        Assert.True(commands.Count >= 3);
        // Exposed as the ICommand interface, never as the concrete class: the View must not care which one it is.
        Assert.All(commands, p => Assert.Equal(typeof(ICommand), p.PropertyType));
    }

    [Fact]
    public void AViewModelProperty_ThatChangesFromOutside_HasAPrivateOrNoSetter_UnlessTheViewWritesIt()
    {
        // Only the properties a user can edit (selection, quantity) are writable from the View; everything else is read-only to it.
        var writable = typeof(ShopViewModel).GetProperties().Where(p => p.SetMethod?.IsPublic == true).Select(p => p.Name).OrderBy(n => n);

        Assert.Equal([nameof(ShopViewModel.Quantity), nameof(ShopViewModel.SelectedProduct)], writable);
    }

    // ---------- the View ----------

    [Fact]
    public void TheConsoleView_NeverMentionsTheModelInItsSignatures()
    {
        // Signature-level check; the IL-level check (calls inside method bodies) was done with REA, see the guide.
        var viewType = typeof(ConsoleShopView);
        var members = viewType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        var modelTypes = Model.GetExportedTypes().ToHashSet();

        var usedTypes = members.SelectMany(m => m switch
        {
            FieldInfo f => [f.FieldType],
            PropertyInfo p => [p.PropertyType],
            MethodBase mb => mb.GetParameters().Select(p => p.ParameterType).Concat(mb is MethodInfo mi ? [mi.ReturnType] : []),
            _ => [],
        });

        Assert.DoesNotContain(usedTypes, modelTypes.Contains);
    }

    private static string FindViewsFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Shop.Mvc", "Views");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Could not find src/Shop.Mvc/Views.");
    }
}
