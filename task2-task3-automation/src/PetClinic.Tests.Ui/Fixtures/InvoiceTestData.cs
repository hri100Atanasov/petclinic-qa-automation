using System.Net.Http.Json;
using PetClinic.Tests.Shared.Configuration;

namespace PetClinic.Tests.Ui.Fixtures;

public sealed record InvoiceFixture(int Id, string InvoiceNo);

/// <summary>
/// Creates invoice fixtures directly against the API (as admin) so UI tests can
/// start from a known state without driving the UI through setup steps that
/// aren't themselves under test — faster and more deterministic than repeating
/// those steps through the browser every time.
/// </summary>
public sealed class InvoiceTestData : IDisposable
{
    private const int DefaultOwnerId = 6; // Jean Coleman — stable across the seed data

    private readonly HttpClient _client = new() { BaseAddress = new Uri(TestSettings.ApiBaseUrl) };
    private string? _adminToken;

    private async Task<string> GetAdminTokenAsync()
    {
        if (_adminToken is not null)
        {
            return _adminToken;
        }

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = TestSettings.AdminUsername,
            password = TestSettings.AdminPassword
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        _adminToken = body!.Token;
        _client.DefaultRequestHeaders.Authorization = new("Bearer", _adminToken);
        return _adminToken;
    }

    private async Task<InvoiceFixture> CreateDraftInvoiceAsync(
        decimal taxRate = 0.10m, decimal discountPct = 0m, int ownerId = DefaultOwnerId)
    {
        await GetAdminTokenAsync();
        var response = await _client.PostAsJsonAsync("/api/invoices", new { ownerId, taxRate, discountPct });
        response.EnsureSuccessStatusCode();
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        return new InvoiceFixture(invoice!.Id, invoice.InvoiceNo);
    }

    public async Task<InvoiceFixture> CreateDraftInvoiceWithItemAsync(
        decimal taxRate = 0.10m, decimal discountPct = 0m, decimal unitPrice = 100m, int ownerId = DefaultOwnerId)
    {
        var invoice = await CreateDraftInvoiceAsync(taxRate, discountPct, ownerId);
        await _client.PostAsJsonAsync($"/api/invoices/{invoice.Id}/items", new
        {
            description = "Consultation",
            itemType = "SERVICE",
            quantity = 1,
            unitPrice
        });
        return invoice;
    }

    public async Task<InvoiceFixture> CreateIssuedInvoiceAsync(
        decimal taxRate = 0.10m, decimal discountPct = 0m, decimal unitPrice = 100m, int ownerId = DefaultOwnerId)
    {
        var invoice = await CreateDraftInvoiceWithItemAsync(taxRate, discountPct, unitPrice, ownerId);
        var issueResponse = await _client.PostAsync($"/api/invoices/{invoice.Id}/issue", null);
        issueResponse.EnsureSuccessStatusCode();
        return invoice;
    }

    public void Dispose() => _client.Dispose();

    private sealed class LoginResponse
    {
        public string Token { get; set; } = "";
    }

    private sealed class InvoiceResponse
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = "";
    }
}
