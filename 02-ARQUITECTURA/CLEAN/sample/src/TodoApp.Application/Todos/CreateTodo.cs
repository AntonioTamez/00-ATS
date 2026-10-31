using TodoApp.Application.Abstractions;
using TodoApp.Domain.Todos;

namespace TodoApp.Application.Todos;

public sealed record CreateTodoCommand(string Title);

/// <summary>Use case: orchestrates entities and ports. Contains no business rules and no I/O details.</summary>
public sealed class CreateTodo(ITodoRepository repository, IClock clock)
{
    public async Task<TodoDto> ExecuteAsync(CreateTodoCommand command, CancellationToken ct = default)
    {
        var item = TodoItem.Create(command.Title, clock.UtcNow);
        await repository.AddAsync(item, ct);
        return TodoDto.From(item);
    }
}
