using Hotel.Domain.Common;

namespace Hotel.Application;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}

/// <summary>Output port for telling guests things. Implemented by infrastructure (e-mail, log...).</summary>
public interface INotifier
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Reacts to a domain event. Handlers coordinate work across aggregates without sharing a transaction.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
