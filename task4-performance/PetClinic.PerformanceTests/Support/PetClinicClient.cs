using System.Net.Http.Json;
using PetClinic.PerformanceTests.Config;

namespace PetClinic.PerformanceTests.Support;

/// <summary>
/// Minimal HTTP helper used only for one-time setup (readiness check, admin
/// login, owner pool, receptionist pool, the shared invoice fixture Test 2
/// pays against) before any timed load simulation starts. The timed
/// scenarios talk to the API directly with their own HttpClient (one per
/// pooled receptionist session), not through this class, so setup cost is
/// never counted in the reported latency/percentiles.
/// </summary>
public sealed class PetClinicClient(HttpClient http)
{
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await http.GetAsync("/actuator/health");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadAsStringAsync();
            return body.Contains("\"status\":\"UP\"");
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> LoginAsync(string username, string password)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    public async Task<int> CreateOwnerAsync(string token, string lastNameSuffix)
    {
        var request = Authorized(HttpMethod.Post, "/api/owners", token, new
        {
            firstName = "PerfTest",
            lastName = $"AAAPerfOwner{lastNameSuffix}",
            address = "1 Load Test St",
            city = "Testville",
            telephone = $"555{Random.Shared.Next(1000000, 9999999)}",
            email = $"perf.owner.{lastNameSuffix}@example.test"
        });

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OwnerResponse>();
        return body!.Id;
    }

    /// <summary>Creates a RECEPTIONIST account (admin-only endpoint) and
    /// immediately logs in as it, returning that account's own bearer
    /// token — this is the token every pooled session in the load
    /// scenarios actually uses, not the admin's.</summary>
    public async Task<(string Username, string Token)> CreateReceptionistAsync(string adminToken, string suffix)
    {
        var username = $"perf.reception.{suffix}";
        const string password = "PerfTest123!";

        var request = Authorized(HttpMethod.Post, "/api/users", adminToken, new
        {
            username,
            password,
            fullName = $"Perf Reception {suffix}",
            email = $"perf.reception.{suffix}@example.test",
            role = "RECEPTIONIST"
        });

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var token = await LoginAsync(username, password);
        return (username, token);
    }

    public async Task<int> CreateInvoiceAsync(string token, int ownerId, decimal taxRate, decimal discountPct)
    {
        var request = Authorized(HttpMethod.Post, "/api/invoices", token,
            new { ownerId, taxRate, discountPct });
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        return body!.Id;
    }

    public async Task AddItemAsync(string token, int invoiceId, string description, string itemType, int quantity, decimal unitPrice)
    {
        var request = Authorized(HttpMethod.Post, $"/api/invoices/{invoiceId}/items", token,
            new { description, itemType, quantity, unitPrice });
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task IssueInvoiceAsync(string token, int invoiceId)
    {
        var request = Authorized(HttpMethod.Post, $"/api/invoices/{invoiceId}/issue", token);
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<InvoiceResponse> GetInvoiceAsync(string token, int invoiceId)
    {
        var request = Authorized(HttpMethod.Get, $"/api/invoices/{invoiceId}", token);
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Authorization", $"Bearer {token}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return request;
    }
}
