using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Ui.Pages;
using PetClinic.Tests.Ui.Setup;

namespace PetClinic.Tests.Ui.Tests;

/// <summary>
/// Defect #5 (Task 1, test-plan.md §8): the invoice list's Next control stays
/// active past the last page. Confirmed during exploration that it has no upper
/// bound at all — repeated clicking shows "Page 21 of 15" with "No invoices
/// found." This test walks to the true last page (parsed from the page
/// indicator, not hardcoded) and checks Next there specifically.
/// </summary>
[TestFixture]
public class Defect5PaginationTests : PetClinicPageTest
{
    [Test]
    public async Task Next_Button_Is_Disabled_On_The_Last_Page()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(SeedAccounts.Reception.Username, SeedAccounts.Reception.Password);
        await Expect(loginPage.SignOutButton).ToBeVisibleAsync();

        var listPage = new InvoiceListPage(Page);
        await listPage.NavigateAsync(TestSettings.UiBrowserUrl);

        var (current, total) = await listPage.GetPageInfoAsync();
        for (var i = current; i < total; i++)
        {
            await listPage.ClickNextAsync();
        }

        var (finalCurrent, finalTotal) = await listPage.GetPageInfoAsync();
        Assert.That(finalCurrent, Is.EqualTo(finalTotal), "Should have landed exactly on the last page.");

        // Expected: Next is disabled once there are no further pages.
        // Actual (Defect #5): Next stays enabled indefinitely past the last page.
        await Expect(listPage.NextButton).ToBeDisabledAsync();
    }
}
