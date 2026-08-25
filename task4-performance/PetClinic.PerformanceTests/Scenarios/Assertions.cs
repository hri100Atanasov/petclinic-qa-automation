using System.Net.Http.Json;
using System.Text.Json;
using PetClinic.PerformanceTests.Support;

namespace PetClinic.PerformanceTests.Scenarios;

/// <summary>Shared response-content assertions used by more than one
/// scenario, so "response assertions, not just status-code counting" is
/// enforced the same way everywhere the same kind of call is made.</summary>
internal static class Assertions
{
    public readonly record struct CheckResult(bool Ok, string? StatusCode, string? Message);

    public static async Task<CheckResult> ValidateCreatedInvoice(HttpResponseMessage response)
    {
        var statusCode = ((int)response.StatusCode).ToString();

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new CheckResult(false, statusCode, $"POST /api/invoices returned {(int)response.StatusCode}: {Truncate(body)}");
        }

        InvoiceResponse? invoice;
        try
        {
            invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        }
        catch (JsonException ex)
        {
            return new CheckResult(false, statusCode, $"Response body did not parse as an invoice: {ex.Message}");
        }

        if (invoice is null || invoice.Id <= 0)
        {
            return new CheckResult(false, statusCode, "Response had no valid invoice id");
        }
        if (invoice.Status != "DRAFT")
        {
            return new CheckResult(false, statusCode, $"Expected status DRAFT, got '{invoice.Status}'");
        }
        if (!invoice.InvoiceNo.StartsWith("INV-", StringComparison.Ordinal))
        {
            return new CheckResult(false, statusCode, $"Unexpected invoiceNo format: '{invoice.InvoiceNo}'");
        }
        if (invoice.Totals.Subtotal != 0.00m)
        {
            return new CheckResult(false, statusCode, $"Expected subtotal 0.00 for an item-less invoice, got {invoice.Totals.Subtotal}");
        }

        return new CheckResult(true, statusCode, null);
    }

    public static async Task<CheckResult> ValidateInvoiceList(HttpResponseMessage response, int requestedPage)
    {
        var statusCode = ((int)response.StatusCode).ToString();

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            return new CheckResult(false, statusCode, $"GET /api/invoices returned {(int)response.StatusCode}: {Truncate(body)}");
        }

        InvoiceListResponse? list;
        try
        {
            list = await response.Content.ReadFromJsonAsync<InvoiceListResponse>();
        }
        catch (JsonException ex)
        {
            return new CheckResult(false, statusCode, $"Response body did not parse as an invoice list: {ex.Message}");
        }

        if (list is null)
        {
            return new CheckResult(false, statusCode, "Response body was empty/null");
        }
        if (list.TotalElements < 0 || list.TotalPages < 0)
        {
            return new CheckResult(false, statusCode, $"Nonsensical pagination fields: totalElements={list.TotalElements}, totalPages={list.TotalPages}");
        }
        // Only pages within the reported range are guaranteed non-empty when
        // data exists — requesting a page past totalPages legitimately
        // returns an empty content array, not a defect.
        if (list.Content.Count == 0 && requestedPage < list.TotalPages)
        {
            return new CheckResult(false, statusCode,
                $"Requested page {requestedPage} of {list.TotalPages} but content array was empty (totalElements={list.TotalElements})");
        }

        return new CheckResult(true, statusCode, null);
    }

    public static string Truncate(string s) => s.Length > 300 ? s[..300] + "..." : s;
}
