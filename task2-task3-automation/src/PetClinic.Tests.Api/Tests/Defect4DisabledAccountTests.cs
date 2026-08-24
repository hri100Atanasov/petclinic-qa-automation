using System.Net;
using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;

namespace PetClinic.Tests.Api.Tests;

/// <summary>
/// Defect #4 (test-plan.md §8): a disabled account can still authenticate. The
/// login response already correctly reports enabled: false — the defect is
/// narrowly that nothing checks it at authentication time — so that field is
/// supporting evidence here, not the primary assertion.
/// </summary>
[TestFixture]
public class Defect4DisabledAccountTests
{
    private PetClinicApiClient _client = null!;

    [SetUp]
    public void SetUp() => _client = new PetClinicApiClient();

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task Disabled_Account_Cannot_Log_In()
    {
        var response = await _client.LoginAsync(
            SeedAccounts.FormerStaff.Username, SeedAccounts.FormerStaff.Password);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized).Or.EqualTo(HttpStatusCode.Forbidden),
                $"A disabled account should be rejected at login, but got {response.StatusCode}.");
            Assert.That(response.Data?.User.Enabled, Is.False,
                "Supporting evidence: the account's own record does correctly report enabled: false.");
        });
    }
}
