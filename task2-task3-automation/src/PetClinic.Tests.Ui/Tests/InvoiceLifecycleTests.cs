using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Ui.Pages;
using PetClinic.Tests.Ui.Setup;

namespace PetClinic.Tests.Ui.Tests;

/// <summary>
/// S1 (Task 1, scenarios-full.md): the full invoice lifecycle — create, issue,
/// pay — driven entirely through the real UI forms. This is the positive
/// baseline: proves the rendering pipeline surfaces correct computed values,
/// not just that the API returns them.
/// </summary>
[TestFixture]
public class InvoiceLifecycleTests : PetClinicPageTest
{
    [Test]
    public async Task Receptionist_Can_Create_Issue_And_Pay_An_Invoice()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(SeedAccounts.Reception.Username, SeedAccounts.Reception.Password);
        await Expect(loginPage.SignOutButton).ToBeVisibleAsync();

        var listPage = new InvoiceListPage(Page);
        await listPage.NavigateAsync(TestSettings.UiBrowserUrl);
        var invoiceId = await listPage.CreateDraftInvoiceAsync(
            SharedTestOwner.Owner.FullName, taxRate: 0.10m, discountPct: 0m);

        var detailPage = new InvoiceDetailPage(Page);
        await detailPage.NavigateAsync(TestSettings.UiBrowserUrl, invoiceId);
        await Expect(detailPage.Status).ToHaveTextAsync("DRAFT");

        await detailPage.AddLineItemAsync("Annual checkup", "SERVICE", 1, 100m);

        await Expect(detailPage.Subtotal).ToHaveTextAsync("$100");
        await Expect(detailPage.TaxableAmount).ToHaveTextAsync("$100");
        await Expect(detailPage.TaxAmount).ToHaveTextAsync("$10");
        await Expect(detailPage.Total).ToHaveTextAsync("$110");

        await detailPage.IssueInvoiceAsync();
        await Expect(detailPage.Status).ToHaveTextAsync("ISSUED");
        await Expect(detailPage.Balance).ToHaveTextAsync("$110");

        await detailPage.RecordPaymentAsync(110m, "CASH");
        await Expect(detailPage.Status).ToHaveTextAsync("PAID");
        await Expect(detailPage.Paid).ToHaveTextAsync("$110");
        await Expect(detailPage.Balance).ToHaveTextAsync("$0");
    }
}
