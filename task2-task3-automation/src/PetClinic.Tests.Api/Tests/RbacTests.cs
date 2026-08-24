using System.Net;
using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;

namespace PetClinic.Tests.Api.Tests;

/// <summary>
/// API-layer counterpart to the UI's RbacTests (Task 2, test-plan.md S16): the UI
/// tests only prove the front end hides controls it shouldn't show, which says
/// nothing about whether the backend itself would actually reject the request if
/// one were sent anyway. This automates and extends S13 (READONLY/VET rejected)
/// to cover the full role matrix directly against the API.
/// </summary>
[TestFixture]
public class RbacTests
{
    private PetClinicApiClient _admin = null!;
    private InvoiceResponse _draftWithItem = null!;
    private InvoiceResponse _issuedInvoice = null!;

    [SetUp]
    public async Task SetUp()
    {
        _admin = new PetClinicApiClient();
        await _admin.AuthenticateAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);

        _draftWithItem = await _admin.CreateDraftInvoiceWithItemAsync();
        _issuedInvoice = await _admin.CreateIssuedInvoiceAsync();
    }

    [TearDown]
    public void TearDown() => _admin.Dispose();

    [TestCase(SeedAccounts.Auditor.Username, SeedAccounts.Auditor.Password)]
    [TestCase(SeedAccounts.Vet.Username, SeedAccounts.Vet.Password)]
    public async Task ReadOnly_Roles_Cannot_Perform_Any_Billing_Write(string username, string password)
    {
        using var client = new PetClinicApiClient();
        await client.AuthenticateAsync(username, password);

        var create = await client.CreateInvoiceAsync(taxRate: 0.10m, discountPct: 0m);
        var addItem = await client.AddItemAsync(_draftWithItem.Id, "Extra", "SERVICE", 1, 10m);
        var issue = await client.IssueInvoiceAsync(_draftWithItem.Id);
        var pay = await client.PayInvoiceAsync(_issuedInvoice.Id, 10m, "CASH");
        var voidInvoice = await client.VoidInvoiceAsync(_issuedInvoice.Id);

        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "create");
            Assert.That(addItem.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "add item");
            Assert.That(issue.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "issue");
            Assert.That(pay.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "pay");
            Assert.That(voidInvoice.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "void");
        });
    }

    [Test]
    public async Task Receptionist_Can_Create_Add_Items_Issue_And_Pay()
    {
        using var client = new PetClinicApiClient();
        await client.AuthenticateAsync(SeedAccounts.Reception.Username, SeedAccounts.Reception.Password);

        var create = await client.CreateInvoiceAsync(taxRate: 0.10m, discountPct: 0m);
        var addItem = await client.AddItemAsync(create.Data!.Id, "Consultation", "SERVICE", 1, 50m);
        var issue = await client.IssueInvoiceAsync(create.Data.Id);
        var pay = await client.PayInvoiceAsync(create.Data.Id, 55m, "CASH");

        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created), "create");
            Assert.That(addItem.StatusCode, Is.EqualTo(HttpStatusCode.OK), "add item");
            Assert.That(issue.StatusCode, Is.EqualTo(HttpStatusCode.OK), "issue");
            Assert.That(pay.StatusCode, Is.EqualTo(HttpStatusCode.OK), "pay");
            Assert.That(pay.Data!.Status, Is.EqualTo("PAID"));
        });
    }

    /// <summary>
    /// Reclassified after live re-verification (see PROMPTS.md Prompt 42): the
    /// README previously documented this as a confirmed 200/defect from Task 1,
    /// but re-testing against the currently running app returns 403 consistently
    /// on both DRAFT and ISSUED invoices, with an admin void succeeding normally
    /// as a positive control. This is now a regression guard, not a defect
    /// reproduction — it is expected to PASS.
    /// </summary>
    [Test]
    public async Task Receptionist_Cannot_Void_Via_Api()
    {
        using var client = new PetClinicApiClient();
        await client.AuthenticateAsync(SeedAccounts.Reception.Username, SeedAccounts.Reception.Password);

        var voidInvoice = await client.VoidInvoiceAsync(_issuedInvoice.Id);

        Assert.That(voidInvoice.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_Has_Full_Access_Including_Void()
    {
        var voidInvoice = await _admin.VoidInvoiceAsync(_issuedInvoice.Id);

        Assert.That(voidInvoice.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(voidInvoice.Data!.Status, Is.EqualTo("VOID"));
    }
}
