namespace PetClinic.Tests.Shared.HealthCheck;

/// <summary>
/// Thrown by the OneTimeSetUp health check when the AUT isn't reachable, so the
/// console shows one clear, actionable message instead of a wall of per-test
/// connection-refused failures.
/// </summary>
public sealed class PetClinicNotRunningException(string message) : Exception(message);
