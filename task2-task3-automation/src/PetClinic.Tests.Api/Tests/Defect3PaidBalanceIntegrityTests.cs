using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;

namespace PetClinic.Tests.Api.Tests;

/// <summary>
/// Defect #3 / S15 (test-plan.md §8 and §9): a PAID invoice should always have a
/// zero balance. A system-wide sweep over every invoice currently in PAID status,
/// not a lookup of specific invoice numbers, so it stays valid regardless of what
/// the current invoice set contains — same approach as S15. Earmarked for Task 3
/// since Task 2's UI suite scoped this out as API/data-integrity territory.
/// </summary>
[TestFixture]
public class Defect3PaidBalanceIntegrityTests
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
    public async Task Every_Paid_Invoice_Has_A_Zero_Balance()
    {
        var response = await _client.ListInvoicesAsync(page: 0, size: 500, status: "PAID");
        var paidInvoices = response.Data!.Content;

        var violations = paidInvoices
            .Where(invoice => invoice.Balance != 0.00m)
            .Select(invoice => $"{invoice.InvoiceNo} (balance {invoice.Balance})")
            .ToList();

        Assert.That(violations, Is.Empty,
            $"PAID invoices must have a zero balance. Violating invoices: {string.Join(", ", violations)}");
    }
}
