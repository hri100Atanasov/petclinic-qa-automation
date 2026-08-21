using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Shared.HealthCheck;

// Deliberately no namespace: NUnit only applies a SetUpFixture's OneTimeSetUp to
// tests in the same namespace or a descendant of it. Declaring it in the global
// namespace makes it apply to every test in this assembly regardless of which
// namespace they live in, avoiding that footgun.

/// <summary>
/// Runs once before any API test. Only checks the API's health endpoint —
/// that's what's relevant to this suite. If it fails, NUnit reports it as a
/// fatal setup error carrying the message from PetClinicAvailabilityChecker,
/// and no individual tests run at all.
/// </summary>
[SetUpFixture]
public class AssemblySetup
{
    [OneTimeSetUp]
    public async Task EnsureAppIsRunning()
    {
        await PetClinicAvailabilityChecker.EnsureApiHealthyAsync(TestSettings.ApiBaseUrl);
    }
}
