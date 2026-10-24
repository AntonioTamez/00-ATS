using TodoApp.Domain.Todos;

namespace TodoApp.Application.Todos;

/// <summary>Plain data that crosses the boundary. Entities never leave the Application layer.</summary>
public sealed record TodoDto(Guid Id, string Title, bool IsCompleted, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt)
{
    internal static TodoDto From(TodoItem item) =>
        new(item.Id, item.Title, item.IsCompleted, item.CreatedAt, item.CompletedAt);
}
