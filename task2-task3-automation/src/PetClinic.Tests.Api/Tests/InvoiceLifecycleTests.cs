using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;

namespace PetClinic.Tests.Api.Tests;

/// <summary>
/// Full invoice lifecycle (create -> add items -> issue -> pay in full), asserting
/// every financial field individually and combined. Uses two line items so this
/// also automates S2 (multi-item subtotal). Deliberately includes a nonzero
/// discount alongside tax, so — unlike Defect1TaxCalculationTests' minimal,
/// isolated reproduction — this test shows Defect #1's real downstream
/// consequence: paid with the mathematically-correct total (93.50) rather than
/// the API's own inflated figure (95.00), the invoice cannot actually reach a
/// clean PAID/zero-balance state under correct accounting. Those cascading
/// failures (taxAmount, total, balance, status) are expected and share Defect #1's
/// root cause — not independent defects.
/// </summary>
[TestFixture]
public class InvoiceLifecycleTests
{
    private PetClinicApiClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _client = new PetClinicApiClient();
        await _client.AuthenticateAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);
    }

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task Full_Lifecycle_Computes_Every_Financial_Field_Correctly()
    {
        // Run the whole lifecycle first, unconditionally — Assert.Multiple throws
        // once its block ends if anything inside failed, which would otherwise
        // abort the test before issue/pay ever ran.
        var created = await _client.CreateInvoiceAsync(taxRate: 0.10m, discountPct: 15m);
        var invoiceId = created.Data!.Id;

        await _client.AddItemAsync(invoiceId, "Consultation", "SERVICE", quantity: 2, unitPrice: 30m);
        var withItems = await _client.AddItemAsync(invoiceId, "Medication", "PRODUCT", quantity: 1, unitPrice: 40m);
        var preIssueTotals = withItems.Data!.Totals;

        await _client.IssueInvoiceAsync(invoiceId);

        // Pay the mathematically-correct total (93.50), not whatever the API itself
        // reports, to surface the real consequence of Defect #1 rather than
        // re-checking the same isolated field the dedicated Defect #1 test covers.
        var paid = await _client.PayInvoiceAsync(invoiceId, 93.50m, "CASH");
        var finalTotals = paid.Data!.Totals;

        // subtotal = (2 * 30.00) + (1 * 40.00) = 100.00
        // discount 15% -> discountAmount 15.00, taxableAmount 85.00
        // correct tax: 85.00 * 10% = 8.50 -> correct total 93.50
        Assert.Multiple(() =>
        {
            Assert.That(preIssueTotals.Subtotal, Is.EqualTo(100.00m), "subtotal");
            Assert.That(preIssueTotals.DiscountAmount, Is.EqualTo(15.00m), "discount amount");
            Assert.That(preIssueTotals.TaxableAmount, Is.EqualTo(85.00m), "taxable amount");
            Assert.That(preIssueTotals.TaxAmount, Is.EqualTo(8.50m),
                $"tax amount (Defect #1: expected 8.50 on the taxable amount, actual {preIssueTotals.TaxAmount})");
            Assert.That(preIssueTotals.Total, Is.EqualTo(93.50m),
                $"total (Defect #1 cascade: expected 93.50, actual {preIssueTotals.Total})");
            Assert.That(preIssueTotals.Total, Is.EqualTo(preIssueTotals.TaxableAmount + 8.50m),
                "combined discount+tax: total should equal taxableAmount + the correctly-computed tax");

            Assert.That(finalTotals.AmountPaid, Is.EqualTo(93.50m), "amount paid");
            Assert.That(finalTotals.Balance, Is.EqualTo(0.00m),
                $"balance (Defect #1 cascade: expected 0.00 once the correct total is paid, actual {finalTotals.Balance})");
            Assert.That(paid.Data!.Status, Is.EqualTo("PAID"),
                $"status (Defect #1 cascade: expected PAID, actual {paid.Data.Status})");
        });
    }
}
