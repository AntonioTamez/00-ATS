using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Todos;

public sealed class CompleteTodo(ITodoRepository repository, IClock clock)
{
    public async Task<TodoDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await repository.GetByIdAsync(id, ct)
                   ?? throw new NotFoundException($"Todo '{id}' was not found.");

        item.Complete(clock.UtcNow);
        await repository.UpdateAsync(item, ct);
        return TodoDto.From(item);
    }
}
