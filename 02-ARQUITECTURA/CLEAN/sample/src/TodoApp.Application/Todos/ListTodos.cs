using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Todos;

public sealed class ListTodos(ITodoRepository repository)
{
    public async Task<IReadOnlyList<TodoDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var items = await repository.ListAsync(ct);
        return items.OrderBy(i => i.CreatedAt).Select(TodoDto.From).ToList();
    }
}
