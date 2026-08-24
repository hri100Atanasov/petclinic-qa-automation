using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Shared.HealthCheck;

// Deliberately no namespace: NUnit only applies a SetUpFixture's OneTimeSetUp to
// tests in the same namespace or a descendant of it. Declaring it in the global
// namespace makes it apply to every test in this assembly regardless of which
// namespace they live in, avoiding that footgun.

/// <summary>
/// Runs once before any API test. Checks the API's health endpoint, then
/// creates the one shared owner (SharedTestOwner) this suite's tests reuse. If
/// either step fails, NUnit reports it as a fatal setup error and no individual
/// tests run at all.
/// </summary>
[SetUpFixture]
public class AssemblySetup
{
    [OneTimeSetUp]
    public async Task EnsureAppIsRunning()
    {
        await PetClinicAvailabilityChecker.EnsureApiHealthyAsync(TestSettings.ApiBaseUrl);

        using var client = new PetClinicApiClient();
        await client.AuthenticateAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);
        SharedTestOwner.Owner = await client.CreateOwnerWithPetAsync();
    }
}
