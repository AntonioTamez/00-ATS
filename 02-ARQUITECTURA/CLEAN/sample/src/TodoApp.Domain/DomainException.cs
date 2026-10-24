namespace TodoApp.Domain;

/// <summary>A business rule was violated. Raised by entities; never depends on HTTP or persistence.</summary>
public sealed class DomainException(string message) : Exception(message);
