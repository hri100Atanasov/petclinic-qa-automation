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
    // "dog", per GET /api/pet-types — reference/taxonomy data, not seeded test
    // data, so unlike owners it's fine to rely on this id being stable.
    private const int DogTypeId = 2;

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

    /// <summary>
    /// Creates an invoice. When <paramref name="ownerId"/> is omitted, uses
    /// <see cref="SharedTestOwner"/> — the one owner created once per test-assembly
    /// run — rather than a specific pre-seeded owner (e.g. id 6 / "Jean Coleman")
    /// that might not stay present or unmutated across runs, and rather than
    /// creating a fresh owner per call (see SharedTestOwner's doc comment for why:
    /// the invoice UI's owner dropdown only shows the first 100 owners).
    /// </summary>
    public Task<RestResponse<InvoiceResponse>> CreateInvoiceAsync(
        decimal taxRate, decimal discountPct, int? ownerId = null)
    {
        var resolvedOwnerId = ownerId ?? SharedTestOwner.Owner?.Id
            ?? throw new InvalidOperationException(
                "No owner id available: pass ownerId explicitly, or ensure SharedTestOwner.Owner " +
                "is set before any test runs (see each project's AssemblySetup.EnsureAppIsRunning).");
        return Execute<InvoiceResponse>("/api/invoices", Method.Post,
            new { ownerId = resolvedOwnerId, taxRate, discountPct });
    }

    private Task<RestResponse<OwnerResponse>> CreateOwnerAsync(
        string firstName, string lastName, string address, string city, string telephone, string email) =>
        Execute<OwnerResponse>("/api/owners", Method.Post,
            new { firstName, lastName, address, city, telephone, email });

    private Task<RestResponse<PetResponse>> AddPetAsync(int ownerId, string name, int typeId = DogTypeId) =>
        Execute<PetResponse>($"/api/owners/{ownerId}/pets", Method.Post, new { name, typeId });

    /// <summary>
    /// Creates a fresh owner with one pet. Telephone/email are synthesized to
    /// satisfy the API's validation (telephone: exactly 10 digits; email:
    /// well-formed) — the API doesn't enforce uniqueness on either, but a random
    /// suffix keeps each test run's fixture distinct and traceable regardless.
    ///
    /// lastName is prefixed "AAA" deliberately: the invoice UI's owner dropdown
    /// only shows the first 100 owners, sorted by lastName (Defect #6,
    /// test-plan.md §8), and this repo's own accumulated test-data owners alone
    /// already exceed 100 — confirmed live that "AAA..." sorts ahead of every
    /// real seed surname and every other synthetic owner this method has ever
    /// created, so this owner lands on the dropdown's first page regardless of
    /// how many other owners exist. Without this, only reducing how often
    /// owners get created (see SharedTestOwner) still leaves each one's
    /// visibility down to chance.
    /// </summary>
    public async Task<OwnerResponse> CreateOwnerWithPetAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var telephone = Random.Shared.Next(1000000000, 2000000000).ToString();

        var owner = await CreateOwnerAsync(
            firstName: "QA",
            lastName: $"AAAOwner{suffix}",
            address: "1 Test St",
            city: "Testville",
            telephone: telephone,
            email: $"qa.owner.{suffix}@example.test");

        await AddPetAsync(owner.Data!.Id, $"Pet{suffix}");

        return owner.Data;
    }

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
    /// authenticated as. Owned by the shared test owner unless
    /// <paramref name="ownerId"/> is given — see <see cref="CreateInvoiceAsync"/>.
    /// </summary>
    public async Task<InvoiceResponse> CreateDraftInvoiceWithItemAsync(
        decimal taxRate = 0.10m, decimal discountPct = 0m, decimal unitPrice = 100m, int? ownerId = null)
    {
        var created = await CreateInvoiceAsync(taxRate, discountPct, ownerId);
        var withItem = await AddItemAsync(created.Data!.Id, "Consultation", "SERVICE", 1, unitPrice);
        return withItem.Data!;
    }

    /// <summary>Same as <see cref="CreateDraftInvoiceWithItemAsync"/>, additionally issued.</summary>
    public async Task<InvoiceResponse> CreateIssuedInvoiceAsync(
        decimal taxRate = 0.10m, decimal discountPct = 0m, decimal unitPrice = 100m, int? ownerId = null)
    {
        var invoice = await CreateDraftInvoiceWithItemAsync(taxRate, discountPct, unitPrice, ownerId);
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
