using TodoApp.Domain.Todos;

namespace TodoApp.Application.Abstractions;

/// <summary>Output port. Defined by the use cases (inner layer), implemented by Infrastructure (outer layer).</summary>
public interface ITodoRepository
{
    Task AddAsync(TodoItem item, CancellationToken ct);
    Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TodoItem>> ListAsync(CancellationToken ct);
    Task UpdateAsync(TodoItem item, CancellationToken ct);
}
