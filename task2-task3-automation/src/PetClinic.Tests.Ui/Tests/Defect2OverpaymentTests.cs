using System.Globalization;
using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Ui.Pages;
using PetClinic.Tests.Ui.Setup;

namespace PetClinic.Tests.Ui.Tests;

/// <summary>
/// Defect #2 (Task 1, test-plan.md §8): a payment exceeding the outstanding
/// balance is accepted in full, leaving the invoice with a negative balance
/// instead of being rejected or the excess handled as a credit. Per
/// scenarios-full.md S8, the correct fix is one of two product decisions
/// (reject outright, or accept and record the excess as a credit while
/// marking the invoice Paid) — this test asserts the one invariant common to
/// both: the balance must never go negative.
/// </summary>
[TestFixture]
public class Defect2OverpaymentTests : PetClinicPageTest
{
    [Test]
    public async Task Overpaying_An_Invoice_Does_Not_Leave_A_Negative_Balance()
    {
        using var apiClient = new PetClinicApiClient();
        await apiClient.AuthenticateAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);
        var invoice = await apiClient.CreateIssuedInvoiceAsync(taxRate: 0.10m, discountPct: 0m, unitPrice: 100m);
        // Total/balance: 110.00.

        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(SeedAccounts.Reception.Username, SeedAccounts.Reception.Password);
        await Expect(loginPage.SignOutButton).ToBeVisibleAsync();

        var detailPage = new InvoiceDetailPage(Page);
        await detailPage.NavigateAsync(TestSettings.UiBrowserUrl, invoice.Id);

        // Attempt to pay far more than the 110.00 balance.
        await detailPage.RecordPaymentAsync(500m, "CASH");

        var balanceText = await detailPage.Balance.InnerTextAsync();
        var balance = decimal.Parse(balanceText.Replace("$", ""), NumberStyles.Number, CultureInfo.InvariantCulture);

        // Expected: the balance should never go negative — either the overpayment
        // is rejected outright, or the excess is recorded as a credit while the
        // invoice is marked Paid with balance 0.00.
        // Actual (Defect #2 — FAIL): the payment is accepted in full and the
        // balance goes negative.
        Assert.That(balance, Is.GreaterThanOrEqualTo(0m), $"Balance should never be negative, but was '{balanceText}'.");
    }
}
