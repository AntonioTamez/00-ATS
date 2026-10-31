namespace TodoApp.Domain.Todos;

/// <summary>Entity (aggregate root): owns its identity and enforces its own business rules.</summary>
public sealed class TodoItem
{
    public const int MaxTitleLength = 100;

    public Guid Id { get; }
    public string Title { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public bool IsCompleted => CompletedAt is not null;

    private TodoItem(Guid id, string title, DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        CreatedAt = createdAt;
    }

    public static TodoItem Create(string title, DateTimeOffset now)
    {
        var normalized = title?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
            throw new DomainException("A todo title is required.");
        if (normalized.Length > MaxTitleLength)
            throw new DomainException($"A todo title cannot exceed {MaxTitleLength} characters.");

        return new TodoItem(Guid.NewGuid(), normalized, now);
    }

    public void Complete(DateTimeOffset now)
    {
        if (IsCompleted)
            throw new DomainException("The todo is already completed.");

        CompletedAt = now;
    }
}
