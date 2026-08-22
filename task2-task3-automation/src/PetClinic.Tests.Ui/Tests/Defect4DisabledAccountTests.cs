using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Ui.Pages;
using PetClinic.Tests.Ui.Setup;

namespace PetClinic.Tests.Ui.Tests;

/// <summary>
/// Defect #4 (Task 1, test-plan.md §8): a deactivated account can still
/// authenticate. This is the UI-level reproduction — logging in through the
/// real login form, not a direct API call.
/// </summary>
[TestFixture]
public class Defect4DisabledAccountTests : PetClinicPageTest
{
    [Test]
    public async Task Disabled_Account_Cannot_Log_In()
    {
        var (username, password) = SeedAccounts.FormerStaff;

        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(username, password);

        // Expected: login is rejected and no session is established.
        // Actual (Defect #4): the account logs in successfully despite being disabled.
        await Expect(loginPage.SignOutButton).Not.ToBeVisibleAsync();
    }
}
