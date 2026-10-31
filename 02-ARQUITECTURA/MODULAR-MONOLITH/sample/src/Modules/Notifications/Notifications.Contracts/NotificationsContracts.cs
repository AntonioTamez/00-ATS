namespace Notifications.Contracts;

public sealed record NotificationDto(DateTimeOffset SentAt, string To, string Subject);

/// <summary>The public face of the Notifications module. Nobody calls it to SEND: it reacts to events.</summary>
public interface INotificationsApi
{
    Task<IReadOnlyList<NotificationDto>> ListForAsync(string email, CancellationToken cancellationToken = default);
}
