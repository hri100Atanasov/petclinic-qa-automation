using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;

namespace PetClinic.Tests.Api.Tests;

/// <summary>
/// Defect #2 (test-plan.md §8): a payment exceeding the outstanding balance is
/// accepted in full instead of being rejected. Boundary value testing to 2 decimal
/// places against a clean 2.00 invoice (0% tax, 0% discount) so the boundary isn't
/// contaminated by Defect #1. Confirmed live: paying exactly the balance reaches
/// PAID/0.00; paying 0.01 under it reaches PARTIALLY_PAID/0.01 without incorrectly
/// flipping to PAID — both used as the expected values below.
/// </summary>
[TestFixture]
public class Defect2OverpaymentTests
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

    private async Task<InvoiceResponse> CreateTwoDollarInvoiceAsync() =>
        await _client.CreateIssuedInvoiceAsync(taxRate: 0m, discountPct: 0m, unitPrice: 2.00m);

    [TestCase(1.99, 0.01, "PARTIALLY_PAID")]
    [TestCase(2.00, 0.00, "PAID")]
    public async Task Paying_At_Or_Under_The_Balance_Produces_The_Correct_Balance_And_Status(
        decimal amount, decimal expectedBalance, string expectedStatus)
    {
        var invoice = await CreateTwoDollarInvoiceAsync();

        var response = await _client.PayInvoiceAsync(invoice.Id, amount, "CASH");

        Assert.Multiple(() =>
        {
            Assert.That(response.Data!.Totals.Balance, Is.EqualTo(expectedBalance));
            Assert.That(response.Data.Status, Is.EqualTo(expectedStatus));
        });
    }

    [Test]
    public async Task Overpaying_By_One_Cent_Does_Not_Leave_A_Negative_Balance()
    {
        var invoice = await CreateTwoDollarInvoiceAsync();

        var response = await _client.PayInvoiceAsync(invoice.Id, 2.01m, "CASH");

        Assert.That(response.Data!.Totals.Balance, Is.GreaterThanOrEqualTo(0m),
            $"Balance should never be negative, but was {response.Data.Totals.Balance}.");
    }
}
