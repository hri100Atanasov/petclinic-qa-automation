using System.Net;
using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;

namespace PetClinic.Tests.Api.Tests;

[TestFixture]
public class LoginTests
{
    private PetClinicApiClient _client = null!;

    [SetUp]
    public void SetUp() => _client = new PetClinicApiClient();

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task Admin_Can_Log_In_And_Receive_A_Bearer_Token()
    {
        var response = await _client.LoginAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), response.Content);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data!.Token, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Data.TokenType, Is.EqualTo("Bearer"));
        Assert.That(response.Data.User.Username, Is.EqualTo(SeedAccounts.Admin.Username));
        Assert.That(response.Data.User.Enabled, Is.True);
    }
}
