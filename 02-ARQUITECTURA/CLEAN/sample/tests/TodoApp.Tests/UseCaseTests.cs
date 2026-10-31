using TodoApp.Application.Abstractions;
using TodoApp.Application.Todos;
using TodoApp.Domain.Todos;

namespace TodoApp.Tests;

/// <summary>Use cases are tested with hand-written fakes: no web server, no database.</summary>
public class UseCaseTests
{
    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class FakeRepository : ITodoRepository
    {
        public Dictionary<Guid, TodoItem> Items { get; } = new();
        public Task AddAsync(TodoItem item, CancellationToken ct) { Items[item.Id] = item; return Task.CompletedTask; }
        public Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.GetValueOrDefault(id));
        public Task<IReadOnlyList<TodoItem>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<TodoItem>>(Items.Values.ToList());
        public Task UpdateAsync(TodoItem item, CancellationToken ct) { Items[item.Id] = item; return Task.CompletedTask; }
    }

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateTodo_PersistsAndReturnsDto()
    {
        var repo = new FakeRepository();

        var dto = await new CreateTodo(repo, new FakeClock(Now)).ExecuteAsync(new CreateTodoCommand("Write docs"));

        Assert.Equal("Write docs", dto.Title);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task CompleteTodo_MarksItemCompleted()
    {
        var repo = new FakeRepository();
        var created = await new CreateTodo(repo, new FakeClock(Now)).ExecuteAsync(new CreateTodoCommand("Task"));

        var dto = await new CompleteTodo(repo, new FakeClock(Now.AddDays(1))).ExecuteAsync(created.Id);

        Assert.True(dto.IsCompleted);
        Assert.Equal(Now.AddDays(1), dto.CompletedAt);
    }

    [Fact]
    public async Task CompleteTodo_UnknownId_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CompleteTodo(new FakeRepository(), new FakeClock(Now)).ExecuteAsync(Guid.NewGuid()));

    [Fact]
    public async Task ListTodos_ReturnsOldestFirst()
    {
        var repo = new FakeRepository();
        await new CreateTodo(repo, new FakeClock(Now.AddHours(2))).ExecuteAsync(new CreateTodoCommand("second"));
        await new CreateTodo(repo, new FakeClock(Now)).ExecuteAsync(new CreateTodoCommand("first"));

        var list = await new ListTodos(repo).ExecuteAsync();

        Assert.Equal(["first", "second"], list.Select(t => t.Title));
    }
}
