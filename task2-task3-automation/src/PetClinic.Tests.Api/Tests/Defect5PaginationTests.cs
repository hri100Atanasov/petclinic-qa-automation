using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;

namespace PetClinic.Tests.Api.Tests;

/// <summary>
/// Defect #5 (test-plan.md §8) is UI-only: Task 1 already confirmed the API's own
/// "last" flag is correct on the true last page — the bug is that the UI's Next
/// button ignores it. So unlike the other Defect tests in this suite, this one is
/// a regression guard/contract check and is expected to PASS, isolating that the
/// defect lives entirely in the UI layer (covered separately by Defect5PaginationTests
/// in PetClinic.Tests.Ui).
/// </summary>
[TestFixture]
public class Defect5PaginationTests
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
    public async Task Api_Reports_Last_True_On_The_True_Last_Page()
    {
        // Guarantee at least one invoice exists regardless of ambient DB state —
        // on a genuinely empty table, totalPages is 0, and requesting page -1
        // below would 500 instead of exercising the "last" flag at all.
        await _client.CreateDraftInvoiceWithItemAsync();

        var firstPage = await _client.ListInvoicesAsync(page: 0, size: 10);
        var totalPages = firstPage.Data!.TotalPages;

        var lastPage = await _client.ListInvoicesAsync(page: totalPages - 1, size: 10);

        Assert.That(lastPage.Data!.Last, Is.True,
            $"Page {totalPages - 1} of {totalPages} should be flagged as the last page.");
    }
}
