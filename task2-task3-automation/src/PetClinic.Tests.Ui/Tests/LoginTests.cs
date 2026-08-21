using Microsoft.Playwright;
using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Ui.Setup;

namespace PetClinic.Tests.Ui.Tests;

[TestFixture]
public class LoginTests : PetClinicPageTest
{
    [Test]
    public async Task Admin_Can_Log_In_And_Reach_The_Dashboard()
    {
        await Page.GotoAsync(TestSettings.UiBrowserUrl);

        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Username" })
            .FillAsync(TestSettings.AdminUsername);
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" })
            .FillAsync(TestSettings.AdminPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        // "Sign out" only renders in the nav once a session is established, so it's
        // a reliable signal that login succeeded, regardless of which page loads first.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }))
            .ToBeVisibleAsync();
    }
}
