using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;

namespace PetClinic.Tests.Api.Tests;

/// <summary>
/// Defect #1 (test-plan.md §8): tax is computed on the raw subtotal instead of the
/// post-discount taxable amount. Uses a partial (20%) discount rather than 100% —
/// a 100% discount collapses both bases to the same taxable amount of 0, which
/// would mask the bug; a partial discount gives two distinct nonzero expected
/// values (8.00 correct vs 10.00 actual), a clearer signal.
/// </summary>
[TestFixture]
public class Defect1TaxCalculationTests
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
    public async Task Tax_Is_Computed_On_The_Taxable_Amount_Not_The_Subtotal()
    {
        var created = await _client.CreateInvoiceAsync(taxRate: 0.10m, discountPct: 20m);
        var withItem = await _client.AddItemAsync(created.Data!.Id, "Consultation", "SERVICE", 1, 100m);
        var totals = withItem.Data!.Totals;

        // subtotal 100.00, discount 20% -> discountAmount 20.00, taxableAmount 80.00.
        // Correct tax: 80.00 * 10% = 8.00. Actual (Defect #1): 100.00 * 10% = 10.00.
        Assert.Multiple(() =>
        {
            Assert.That(totals.Subtotal, Is.EqualTo(100.00m));
            Assert.That(totals.DiscountAmount, Is.EqualTo(20.00m));
            Assert.That(totals.TaxableAmount, Is.EqualTo(80.00m));
            Assert.That(totals.TaxAmount, Is.EqualTo(8.00m),
                $"Tax should be computed on the taxable amount (80.00), not the subtotal (100.00), but was {totals.TaxAmount}.");
        });
    }
}
