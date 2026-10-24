using TodoApp.Domain;
using TodoApp.Domain.Todos;

namespace TodoApp.Tests;

public class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsTitle_AndStartsPending()
    {
        var item = TodoItem.Create("  Buy milk  ", Now);

        Assert.Equal("Buy milk", item.Title);
        Assert.False(item.IsCompleted);
        Assert.Equal(Now, item.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyTitle(string title) =>
        Assert.Throws<DomainException>(() => TodoItem.Create(title, Now));

    [Fact]
    public void Create_RejectsTitleOverMaxLength() =>
        Assert.Throws<DomainException>(() => TodoItem.Create(new string('a', TodoItem.MaxTitleLength + 1), Now));

    [Fact]
    public void Complete_SetsCompletedAt()
    {
        var item = TodoItem.Create("Task", Now);

        item.Complete(Now.AddHours(1));

        Assert.True(item.IsCompleted);
        Assert.Equal(Now.AddHours(1), item.CompletedAt);
    }

    [Fact]
    public void Complete_Twice_Throws()
    {
        var item = TodoItem.Create("Task", Now);
        item.Complete(Now);

        Assert.Throws<DomainException>(() => item.Complete(Now));
    }
}
