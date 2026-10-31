namespace Helpdesk.Api.Common;

public enum TicketStatus { Open, Assigned, Closed }

/// <summary>
/// The only truly shared model. Slices that change a ticket go through its methods,
/// so the business rules live in one place and are not copied into each slice.
/// </summary>
public sealed class Ticket
{
    public const int MaxTitleLength = 100;

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public string? Assignee { get; private set; }
    public string? Resolution { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    private Ticket() { }   // for EF Core

    public static Ticket Open(string title, string? description, DateTimeOffset now)
    {
        var normalized = title?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new DomainException("A ticket title is required.");
        if (normalized.Length > MaxTitleLength)
            throw new DomainException($"A ticket title cannot exceed {MaxTitleLength} characters.");

        return new Ticket
        {
            Id = Guid.NewGuid(),
            Title = normalized,
            Description = description?.Trim() ?? string.Empty,
            Status = TicketStatus.Open,
            OpenedAt = now
        };
    }

    public void Assign(string assignee)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException("A closed ticket cannot be assigned.");
        if (string.IsNullOrWhiteSpace(assignee))
            throw new DomainException("An assignee is required.");

        Assignee = assignee.Trim();
        Status = TicketStatus.Assigned;
    }

    public void Close(string resolution, DateTimeOffset now)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException("The ticket is already closed.");
        if (string.IsNullOrWhiteSpace(resolution))
            throw new DomainException("A resolution is required to close a ticket.");

        Resolution = resolution.Trim();
        ClosedAt = now;
        Status = TicketStatus.Closed;
    }
}
