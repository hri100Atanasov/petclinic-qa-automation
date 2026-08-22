using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PetClinic.Tests.Ui.Pages;

public class InvoiceListPage(IPage page)
{
    public ILocator CreateInvoiceButton => page.GetByTestId("invoice-create-button");
    public ILocator StatusFilter => page.GetByTestId("invoice-status-filter");
    public ILocator InvoicesTable => page.GetByTestId("invoices-table");
    private ILocator PageIndicator => page.GetByTestId("page-indicator");
    public ILocator NextButton => page.GetByTestId("page-next");
    public ILocator PreviousButton => page.GetByTestId("page-prev");

    // New-invoice modal
    private ILocator NewInvoiceModal => page.GetByTestId("invoice-modal");
    private ILocator OwnerSelect => page.GetByTestId("invoice-owner-select");
    private ILocator TaxRateInput => page.GetByTestId("invoice-taxrate-input");
    private ILocator DiscountInput => page.GetByTestId("invoice-discount-input");
    private ILocator SaveNewInvoiceButton => page.GetByTestId("invoice-save");

    public Task NavigateAsync(string baseUrl) => page.GotoAsync($"{baseUrl}/invoices");

    private ILocator InvoiceLink(int invoiceId) => page.GetByTestId($"invoice-link-{invoiceId}");

    public Task OpenInvoiceAsync(int invoiceId) => InvoiceLink(invoiceId).ClickAsync();

    /// <summary>
    /// Clicks Next and waits for the page indicator's current-page number to
    /// actually change before returning — clicking several times in a tight
    /// loop with no wait between clicks was observed to leave the page stuck
    /// on page 1 (the app's own state update lagging the clicks). This is a
    /// neutral synchronization wait, not a pass/fail assertion: if the page
    /// number never changes, this just returns quietly and lets the caller's
    /// own assertion report the mismatch.
    /// </summary>
    public async Task ClickNextAsync()
    {
        var before = await GetPageInfoAsync();
        await NextButton.ClickAsync();
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var after = await GetPageInfoAsync();
            if (after.Current != before.Current)
            {
                return;
            }
            await page.WaitForTimeoutAsync(150);
        }
    }

    public async Task<(int Current, int Total)> GetPageInfoAsync()
    {
        var text = await PageIndicator.InnerTextAsync();
        var match = Regex.Match(text, @"Page (\d+) of (\d+)");
        return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
    }

    /// <summary>
    /// Opens the new-invoice form, fills it in, saves, and returns the created
    /// invoice's numeric id — read directly from the POST /api/invoices response,
    /// not scraped from the list's top row. Reading the DOM instead (e.g. "the
    /// newest invoice is whichever one is on top") was observed to be unreliable:
    /// it sometimes resolved to a different, pre-existing invoice, since nothing
    /// guarantees the list has re-rendered with exactly the new row — or only the
    /// new row — by the time it's read. The API response has no such ambiguity.
    /// </summary>
    public async Task<int> CreateDraftInvoiceAsync(string ownerName, decimal? taxRate = null, decimal? discountPct = null)
    {
        await CreateInvoiceButton.ClickAsync();
        await OwnerSelect.SelectOptionAsync(new SelectOptionValue { Label = ownerName });
        if (taxRate is not null)
        {
            await TaxRateInput.FillAsync(taxRate.Value.ToString(CultureInfo.InvariantCulture));
        }
        if (discountPct is not null)
        {
            await DiscountInput.FillAsync(discountPct.Value.ToString(CultureInfo.InvariantCulture));
        }

        var response = await page.RunAndWaitForResponseAsync(
            () => SaveNewInvoiceButton.ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/api/invoices"));

        await NewInvoiceModal.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

        var body = await response.JsonAsync();
        return body!.Value.GetProperty("id").GetInt32();
    }
}
