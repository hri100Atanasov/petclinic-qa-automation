using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Ui.Pages;
using PetClinic.Tests.Ui.Setup;

namespace PetClinic.Tests.Ui.Tests;

/// <summary>
/// Checks a new angle Task 1 didn't cover: Task 1 confirmed the API rejects
/// unauthorized writes with 403 for READONLY/VET (test-plan.md §9, S13). This
/// checks whether the UI actually hides those actions for unauthorized roles,
/// or renders a control that would then fail if clicked — a UX/authorization-
/// awareness gap distinct from the already-confirmed API-level enforcement.
/// </summary>
[TestFixture]
public class RbacTests : PetClinicPageTest
{
    private PetClinicApiClient _apiClient = null!;

    [SetUp]
    public async Task SetUp()
    {
        _apiClient = new PetClinicApiClient();
        await _apiClient.AuthenticateAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);
    }

    [TearDown]
    public void TearDown() => _apiClient.Dispose();

    [TestCase(SeedAccounts.Auditor.Username, SeedAccounts.Auditor.Password)]
    [TestCase(SeedAccounts.Vet.Username, SeedAccounts.Vet.Password)]
    public async Task ReadOnly_Roles_See_No_Invoice_Manipulation_Controls(string username, string password)
    {
        var draftWithItem = await _apiClient.CreateDraftInvoiceWithItemAsync();
        var issuedInvoice = await _apiClient.CreateIssuedInvoiceAsync();

        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(username, password);
        await Expect(loginPage.SignOutButton).ToBeVisibleAsync();

        var listPage = new InvoiceListPage(Page);
        await listPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await Expect(listPage.CreateInvoiceButton).Not.ToBeVisibleAsync();

        var detailPage = new InvoiceDetailPage(Page);
        await detailPage.NavigateAsync(TestSettings.UiBrowserUrl, draftWithItem.Id);
        await Expect(detailPage.AddLineItemButton).Not.ToBeVisibleAsync();
        await Expect(detailPage.IssueButton).Not.ToBeVisibleAsync();

        await detailPage.NavigateAsync(TestSettings.UiBrowserUrl, issuedInvoice.Id);
        await Expect(detailPage.PayButton).Not.ToBeVisibleAsync();
        await Expect(detailPage.VoidButton).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Receptionist_Can_Create_Add_Items_Issue_And_Pay_But_Not_Void()
    {
        var draftWithItem = await _apiClient.CreateDraftInvoiceWithItemAsync();
        var issuedInvoice = await _apiClient.CreateIssuedInvoiceAsync();

        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(SeedAccounts.Reception.Username, SeedAccounts.Reception.Password);
        await Expect(loginPage.SignOutButton).ToBeVisibleAsync();

        var listPage = new InvoiceListPage(Page);
        await listPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await Expect(listPage.CreateInvoiceButton).ToBeVisibleAsync();

        var detailPage = new InvoiceDetailPage(Page);
        await detailPage.NavigateAsync(TestSettings.UiBrowserUrl, draftWithItem.Id);
        await Expect(detailPage.AddLineItemButton).ToBeVisibleAsync();
        await Expect(detailPage.IssueButton).ToBeVisibleAsync();

        await detailPage.NavigateAsync(TestSettings.UiBrowserUrl, issuedInvoice.Id);
        await Expect(detailPage.PayButton).ToBeVisibleAsync();
        await Expect(detailPage.VoidButton).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task Admin_Has_Receptionist_Access_Plus_Void()
    {
        var issuedInvoice = await _apiClient.CreateIssuedInvoiceAsync();

        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);
        await Expect(loginPage.SignOutButton).ToBeVisibleAsync();

        var listPage = new InvoiceListPage(Page);
        await listPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await Expect(listPage.CreateInvoiceButton).ToBeVisibleAsync();

        var detailPage = new InvoiceDetailPage(Page);
        await detailPage.NavigateAsync(TestSettings.UiBrowserUrl, issuedInvoice.Id);
        await Expect(detailPage.PayButton).ToBeVisibleAsync();
        await Expect(detailPage.VoidButton).ToBeVisibleAsync();
    }
}
