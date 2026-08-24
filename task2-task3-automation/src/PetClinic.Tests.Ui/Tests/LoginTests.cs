using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Ui.Pages;
using PetClinic.Tests.Ui.Setup;

namespace PetClinic.Tests.Ui.Tests;

[TestFixture]
public class LoginTests : PetClinicPageTest
{
    [Test]
    public async Task Admin_Can_Log_In_And_Reach_The_Dashboard()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);

        // "Sign out" only renders in the nav once a session is established, so it's
        // a reliable signal that login succeeded, regardless of which page loads first.
        await Expect(loginPage.SignOutButton).ToBeVisibleAsync();
    }
}
