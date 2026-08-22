using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Ui.Fixtures;
using PetClinic.Tests.Ui.Pages;
using PetClinic.Tests.Ui.Setup;

namespace PetClinic.Tests.Ui.Tests;

/// <summary>
/// Defect #1 (Task 1, test-plan.md §8): tax is computed on the subtotal instead
/// of the taxable (post-discount) amount. Fixtures are created directly via the
/// API for speed/determinism; the actual assertion reads the rendered totals in
/// the UI, since that's what a user actually sees.
/// </summary>
[TestFixture]
public class Defect1TaxCalculationTests : PetClinicPageTest
{
    [Test]
    public async Task Tax_Is_Computed_On_Taxable_Amount_Not_Subtotal()
    {
        using var testData = new InvoiceTestData();
        var fullDiscountInvoice = await testData.CreateDraftInvoiceWithItemAsync(taxRate: 0.10m, discountPct: 100m, unitPrice: 100m);

        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(SeedAccounts.Reception.Username, SeedAccounts.Reception.Password);
        await Expect(loginPage.SignOutButton).ToBeVisibleAsync();

        var detailPage = new InvoiceDetailPage(Page);
        await detailPage.NavigateAsync(TestSettings.UiBrowserUrl, fullDiscountInvoice.Id);

        // Subtotal 100.00, 100% discount -> taxable amount is 0.00.
        await Expect(detailPage.TaxableAmount).ToHaveTextAsync("$0");

        // Expected: 10% of the 0.00 taxable amount = $0 tax.
        // Actual (Defect #1): tax is computed on the 100.00 subtotal instead, so
        // this renders as $10 even though nothing is actually taxable.
        await Expect(detailPage.TaxAmount).ToHaveTextAsync("$0");
    }
}
