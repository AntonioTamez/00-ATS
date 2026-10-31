namespace Clinic.SharedKernel;

/// <summary>A business rule was broken. The web layer will show it as a 422; the rule itself knows nothing of HTTP.</summary>
public sealed class RuleViolationException(string message) : Exception(message);

public sealed class NotFoundException(string message) : Exception(message);
