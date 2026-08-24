using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Shared.HealthCheck;

// Deliberately no namespace: NUnit only applies a SetUpFixture's OneTimeSetUp to
// tests in the same namespace or a descendant of it. Declaring it in the global
// namespace makes it apply to every test in this assembly regardless of which
// namespace they live in, avoiding that footgun.

/// <summary>
/// Runs once before any UI test. Checks both the UI and the API: several UI
/// tests (RBAC, Defect #1, Defect #2, Defect #5) seed their fixtures by calling
/// the API directly (see PetClinic.Tests.Shared.Api.PetClinicApiClient), so a
/// UI-only check would pass while the API is down and those tests would then
/// fail on a raw connection error instead of this fatal, readable setup error.
/// If either check fails, NUnit reports it as a fatal setup error carrying the
/// message from PetClinicAvailabilityChecker, and no individual tests run at
/// all.
/// </summary>
[SetUpFixture]
public class AssemblySetup
{
    [OneTimeSetUp]
    public async Task EnsureAppIsRunning()
    {
        await PetClinicAvailabilityChecker.EnsureUiReachableAsync(TestSettings.UiBaseUrl);
        await PetClinicAvailabilityChecker.EnsureApiHealthyAsync(TestSettings.ApiBaseUrl);
    }
}
