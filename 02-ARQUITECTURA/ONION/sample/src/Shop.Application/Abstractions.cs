namespace Shop.Application;

/// <summary>Defined by the application ring, implemented by Infrastructure (outer ring).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class NotFoundException(string message) : Exception(message);
