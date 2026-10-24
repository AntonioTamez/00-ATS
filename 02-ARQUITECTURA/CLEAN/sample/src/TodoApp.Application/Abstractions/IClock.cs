namespace TodoApp.Application.Abstractions;

/// <summary>Output port for time, so use cases stay deterministic in tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
