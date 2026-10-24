namespace Users.Api.Contracts;

/// <summary>HTTP contract of a user. Owned by the API; independent from the domain and application models.</summary>
public sealed record UserResponse(Guid Id, string FullName, string Email, string Status, DateTimeOffset CreatedAt);
