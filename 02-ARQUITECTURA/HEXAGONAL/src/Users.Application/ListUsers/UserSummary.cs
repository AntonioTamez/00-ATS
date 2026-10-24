namespace Users.Application.ListUsers;

/// <summary>Output model of the use case; deliberately not the domain aggregate.</summary>
public sealed record UserSummary(Guid Id, string FullName, string Email, string Status, DateTimeOffset CreatedAt);
