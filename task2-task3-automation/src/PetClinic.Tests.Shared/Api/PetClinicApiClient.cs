using PetClinic.Tests.Shared.Configuration;
using RestSharp;

namespace PetClinic.Tests.Shared.Api;

/// <summary>
/// Thin RestSharp wrapper over the billing API. Shared between the UI and API test
/// projects: in the API suite these calls are the system under test; in the UI
/// suite they're test setup (seeding a fixture invoice faster and more
/// deterministically than driving the UI through steps that aren't themselves
/// under test). Living here instead of being duplicated in both projects keeps
/// login, invoice creation, and the response models to a single implementation.
/// </summary>
public sealed class PetClinicApiClient : IDisposable
{
    private readonly RestClient _client = new(TestSettings.ApiBaseUrl);
    private string? _token;

    public Task<RestResponse<LoginResponse>> LoginAsync(string username, string password) =>
        _client.ExecuteAsync<LoginResponse>(new RestRequest("/api/auth/login", Method.Post)
            .AddJsonBody(new { username, password }));

    public async Task AuthenticateAsync(string username, string password)
    {
        var response = await LoginAsync(username, password);
        if (response.Data is null)
        {
            throw new InvalidOperationException(
                $"Login as '{username}' failed unexpectedly during test setup: {response.StatusCode} {response.Content}");
        }

        _token = response.Data.Token;
    }

    public Task<RestResponse<InvoiceResponse>> CreateInvoiceAsync(
        int ownerId, decimal taxRate, decimal discountPct) =>
        Execute<InvoiceResponse>("/api/invoices", Method.Post, new { ownerId, taxRate, discountPct });

    public Task<RestResponse<InvoiceResponse>> AddItemAsync(
        int invoiceId, string description, string itemType, int quantity, decimal unitPrice) =>
        Execute<InvoiceResponse>($"/api/invoices/{invoiceId}/items", Method.Post,
            new { description, itemType, quantity, unitPrice });

    public Task<RestResponse<InvoiceResponse>> IssueInvoiceAsync(int invoiceId) =>
        Execute<InvoiceResponse>($"/api/invoices/{invoiceId}/issue", Method.Post);

    public Task<RestResponse<InvoiceResponse>> PayInvoiceAsync(int invoiceId, decimal amount, string method) =>
        Execute<InvoiceResponse>($"/api/invoices/{invoiceId}/payments", Method.Post, new { amount, method });

    public Task<RestResponse<InvoiceResponse>> VoidInvoiceAsync(int invoiceId) =>
        Execute<InvoiceResponse>($"/api/invoices/{invoiceId}/void", Method.Post);

    public Task<RestResponse<InvoiceResponse>> GetInvoiceAsync(int invoiceId) =>
        Execute<InvoiceResponse>($"/api/invoices/{invoiceId}", Method.Get);

    public Task<RestResponse<InvoiceListResponse>> ListInvoicesAsync(int page, int size, string? status = null)
    {
        var request = Authorized("/api/invoices", Method.Get)
            .AddQueryParameter("page", page)
            .AddQueryParameter("size", size);
        if (status is not null)
        {
            request.AddQueryParameter("status", status);
        }

        return _client.ExecuteAsync<InvoiceListResponse>(request);
    }

    /// <summary>
    /// Creates a draft invoice with one line item — the common precondition most
    /// tests here need, built with whatever role this client is currently
    /// authenticated as.
    /// </summary>
    public async Task<InvoiceResponse> CreateDraftInvoiceWithItemAsync(
        int ownerId = 6, decimal taxRate = 0.10m, decimal discountPct = 0m, decimal unitPrice = 100m)
    {
        var created = await CreateInvoiceAsync(ownerId, taxRate, discountPct);
        var withItem = await AddItemAsync(created.Data!.Id, "Consultation", "SERVICE", 1, unitPrice);
        return withItem.Data!;
    }

    /// <summary>Same as <see cref="CreateDraftInvoiceWithItemAsync"/>, additionally issued.</summary>
    public async Task<InvoiceResponse> CreateIssuedInvoiceAsync(
        int ownerId = 6, decimal taxRate = 0.10m, decimal discountPct = 0m, decimal unitPrice = 100m)
    {
        var invoice = await CreateDraftInvoiceWithItemAsync(ownerId, taxRate, discountPct, unitPrice);
        var issued = await IssueInvoiceAsync(invoice.Id);
        return issued.Data!;
    }

    private Task<RestResponse<T>> Execute<T>(string resource, Method method, object? body = null)
    {
        var request = Authorized(resource, method);
        if (body is not null)
        {
            request.AddJsonBody(body);
        }

        return _client.ExecuteAsync<T>(request);
    }

    private RestRequest Authorized(string resource, Method method)
    {
        var request = new RestRequest(resource, method);
        if (_token is not null)
        {
            request.AddHeader("Authorization", $"Bearer {_token}");
        }

        return request;
    }

    public void Dispose() => _client.Dispose();
}
