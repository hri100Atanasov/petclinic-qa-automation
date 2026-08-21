using System.Net;
using PetClinic.Tests.Shared.Configuration;
using RestSharp;

namespace PetClinic.Tests.Api.Tests;

[TestFixture]
public class LoginTests
{
    private RestClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _client = new RestClient(TestSettings.ApiBaseUrl);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
    }

    [Test]
    public async Task Admin_Can_Log_In_And_Receive_A_Bearer_Token()
    {
        var request = new RestRequest("/api/auth/login", Method.Post)
            .AddJsonBody(new
            {
                username = TestSettings.AdminUsername,
                password = TestSettings.AdminPassword
            });

        var response = await _client.ExecuteAsync<LoginResponse>(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), response.Content);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data!.Token, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Data.TokenType, Is.EqualTo("Bearer"));
        Assert.That(response.Data.User.Username, Is.EqualTo(TestSettings.AdminUsername));
        Assert.That(response.Data.User.Enabled, Is.True);
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = "";
        public string TokenType { get; set; } = "";
        public UserInfo User { get; set; } = new();
    }

    private sealed class UserInfo
    {
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public bool Enabled { get; set; }
    }
}
