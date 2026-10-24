using System.Collections.Concurrent;
using TodoApp.Application.Abstractions;
using TodoApp.Domain.Todos;

namespace TodoApp.Infrastructure.Persistence;

/// <summary>Adapter: implements the port with a dictionary. Swap for EF Core/Dapper without touching inner layers.</summary>
internal sealed class InMemoryTodoRepository : ITodoRepository
{
    private readonly ConcurrentDictionary<Guid, TodoItem> _store = new();

    public Task AddAsync(TodoItem item, CancellationToken ct)
    {
        _store[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<TodoItem>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TodoItem>>(_store.Values.ToList());

    public Task UpdateAsync(TodoItem item, CancellationToken ct)
    {
        _store[item.Id] = item;
        return Task.CompletedTask;
    }
}
