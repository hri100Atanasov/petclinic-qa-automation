using System.Globalization;
using Microsoft.Playwright;
using PetClinic.Tests.Shared.Api;
using PetClinic.Tests.Shared.Configuration;
using PetClinic.Tests.Ui.Pages;
using PetClinic.Tests.Ui.Setup;

namespace PetClinic.Tests.Ui.Tests;

/// <summary>
/// PetClinic stores and returns dates in UTC (per the AUT's own README), and
/// dueDate specifically is a date-only value with no time component (e.g.
/// "2026-09-24") — a classic source of an off-by-one-day bug if the frontend
/// parses that bare date string as a UTC instant (JavaScript's Date treats a
/// bare "YYYY-MM-DD" as UTC midnight) and then formats it in the browser's
/// *local* timezone: a viewer far enough behind UTC would see the calendar day
/// roll back by one, since UTC midnight on the 24th is still the evening of the
/// 23rd everywhere west of UTC.
///
/// Each test case opens its own browser context with a specific TimezoneId
/// (Playwright's context-level timezone emulation), rather than relying on the
/// suite's UTC default (see PetClinicPageTest), to check whether the rendered
/// due date still matches the API's stored value regardless of viewer timezone.
/// Fixed-offset zones only (no DST) — Honolulu and Kiritimati don't observe it —
/// so the expected result doesn't depend on when this test happens to run.
/// </summary>
[TestFixture]
public class DueDateTimezoneTests : PetClinicPageTest
{
    private InvoiceResponse _invoice = null!;

    [OneTimeSetUp]
    public async Task CreateInvoiceOnce()
    {
        using var client = new PetClinicApiClient();
        await client.AuthenticateAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);
        _invoice = await client.CreateIssuedInvoiceAsync();
    }

    [TestCase("UTC")]
    [TestCase("Atlantic/Cape_Verde")] // UTC-1, fixed offset, no DST -- smallest possible negative offset
    [TestCase("Pacific/Honolulu")] // UTC-10, fixed offset, no DST
    [TestCase("Pacific/Kiritimati")] // UTC+14, fixed offset, no DST
    public async Task Due_Date_Matches_The_Stored_Value_Regardless_Of_Viewer_Timezone(string timezoneId)
    {
        // Locale pinned alongside the timezone -- this override bypasses
        // PetClinicPageTest's ContextOptions() default entirely (NewContext
        // takes its own fresh options), so without repeating it here the
        // rendered date format would depend on the host machine's OS locale.
        await using var context = await NewContext(new BrowserNewContextOptions
        {
            TimezoneId = timezoneId,
            Locale = "en-US"
        });
        var page = await context.NewPageAsync();

        var loginPage = new LoginPage(page);
        await loginPage.NavigateAsync(TestSettings.UiBrowserUrl);
        await loginPage.LoginAsync(SeedAccounts.Admin.Username, SeedAccounts.Admin.Password);

        var detailPage = new InvoiceDetailPage(page);
        await detailPage.NavigateAsync(TestSettings.UiBrowserUrl, _invoice.Id);

        var renderedText = (await detailPage.DueDate.InnerTextAsync()).Trim();
        var renderedDate = DateOnly.ParseExact(renderedText, "M/d/yyyy", CultureInfo.InvariantCulture);
        var storedDate = DateOnly.ParseExact(_invoice.DueDate!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        Assert.That(renderedDate, Is.EqualTo(storedDate),
            $"Due date rendered as '{renderedText}' in timezone '{timezoneId}', but the invoice's " +
            $"stored due date is '{_invoice.DueDate}' — these should always be the same calendar day.");
    }
}
