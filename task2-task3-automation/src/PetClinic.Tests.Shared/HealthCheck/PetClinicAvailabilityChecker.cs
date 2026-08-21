using System.Net.Http.Json;

namespace PetClinic.Tests.Shared.HealthCheck;

/// <summary>
/// Single-shot readiness check (no retry, no polling) run once per test assembly
/// before any test executes. If the AUT isn't reachable, it fails fast with a
/// message telling the operator how to start it, rather than letting every
/// individual test fail with its own connection-refused error.
/// </summary>
public static class PetClinicAvailabilityChecker
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Checks the UI is serving something at its root path.</summary>
    public static async Task EnsureUiReachableAsync(string uiBaseUrl)
    {
        using var client = new HttpClient { Timeout = Timeout };
        try
        {
            var response = await client.GetAsync(uiBaseUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw NotRunning(uiBaseUrl, $"responded with HTTP {(int)response.StatusCode}");
            }
        }
        catch (Exception ex) when (ex is not PetClinicNotRunningException)
        {
            throw NotRunning(uiBaseUrl, ex.Message);
        }
    }

    /// <summary>Checks the API's /actuator/health endpoint reports UP.</summary>
    public static async Task EnsureApiHealthyAsync(string apiBaseUrl)
    {
        var healthUrl = $"{apiBaseUrl.TrimEnd('/')}/actuator/health";
        using var client = new HttpClient { Timeout = Timeout };
        try
        {
            var health = await client.GetFromJsonAsync<HealthResponse>(healthUrl);
            if (health?.Status != "UP")
            {
                throw NotRunning(healthUrl, $"reported status '{health?.Status ?? "<none>"}' instead of 'UP'");
            }
        }
        catch (Exception ex) when (ex is not PetClinicNotRunningException)
        {
            throw NotRunning(healthUrl, ex.Message);
        }
    }

    private static PetClinicNotRunningException NotRunning(string url, string reason) =>
        new($"""

            ============================================================
             PetClinic Pro does not appear to be running.
             Checked: {url}
             Reason:  {reason}

             Start it first, then re-run the tests:
               cd qa-test-automation-task
               docker compose up

             Wait for the log to settle, then run this test suite again.
            ============================================================
            """);

    private sealed record HealthResponse(string? Status);
}
