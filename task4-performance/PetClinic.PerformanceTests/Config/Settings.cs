namespace PetClinic.PerformanceTests.Config;

/// <summary>
/// Where the AUT lives, and the fixed admin seed account used to set up test
/// fixtures (owner pool, receptionist pool). Standalone from
/// task2-task3-automation's TestSettings/SeedAccounts on purpose: Task 4 uses
/// a different tool (NBomber, not RestSharp/Playwright) and is meant to be
/// reviewable/runnable on its own, without pulling in that other solution's
/// project references.
/// </summary>
public static class Settings
{
    public static string ApiBaseUrl =>
        GetEnvOrDefault("API_BASE_URL", "http://localhost:8080");

    /// <summary>Number of owners created once before any load starts, then
    /// reused (randomly picked) by every invoice-creation iteration — avoids
    /// growing the owner table by one row per request, and avoids the
    /// UI-layer 100-owner dropdown cap (Defect #6) entirely, since this
    /// suite talks to the API directly.</summary>
    public const int OwnerPoolSize = 10;

    /// <summary>Number of RECEPTIONIST accounts seeded once before any load
    /// starts. Each concurrent virtual user in every scenario is pinned to
    /// exactly one of these accounts for its whole lifetime in the test (via
    /// NBomber's ClientPool + ScenarioInfo.InstanceNumber), rather than
    /// picking a random account per request — closer to how one real
    /// front-desk session actually behaves, and gives every test a
    /// consistent "10 concurrent users" story.</summary>
    public const int ReceptionistPoolSize = 10;

    /// <summary>Test 2 only: how many concurrent payments are fired at the one
    /// shared invoice, and the amount of each. Deliberately chosen so
    /// PaymentCount * PaymentAmount == the invoice's total, so a correct system
    /// must land on exactly balance 0.00 / status PAID.</summary>
    public const int PaymentCount = 10;

    public const decimal PaymentAmount = 10.00m;

    public const decimal Test2InvoiceTotal = PaymentCount * PaymentAmount;

    /// <summary>Test 5 only: the arrival rate the read ramp climbs to, and how
    /// long it takes to get there. An open model — the rate is imposed rather
    /// than gated by user think time — because a closed model with think time
    /// cannot exceed users/think-time requests per second and so can never
    /// reach a capacity limit.
    ///
    /// 200 rather than a higher number on purpose. The connection pool starts
    /// queuing at roughly 200 req/s, so this crosses the knee with only mild
    /// overshoot. An earlier 400 req/s version overshot far enough that
    /// thousands of requests sat in ~30s timeouts, which saturated the *host's*
    /// Docker port-forwarding path (not the application — verified healthy from
    /// inside the Docker network throughout) and required recreating the
    /// containers to clear. Raising this materially risks measuring the load
    /// generator's host rather than the server.</summary>
    public const int RampToRps = 200;

    public static readonly TimeSpan RampDuration = TimeSpan.FromSeconds(60);

    /// <summary>Test 6 only: a fixed write arrival rate, held for
    /// WriteRateDuration. Overridable per run (WRITE_RATE_RPS) so the same test
    /// can be executed at several rates to build a scalability curve. Kept
    /// deliberately low — the write path fails at a concurrency of 2, so the
    /// whole curve is measurable well below any rate that would stress the load
    /// generator's own host.</summary>
    public static int WriteRateRps =>
        int.TryParse(Environment.GetEnvironmentVariable("WRITE_RATE_RPS"), out var v) ? v : 10;

    public static readonly TimeSpan WriteRateDuration = TimeSpan.FromSeconds(20);

    public const string AdminUsername = "admin";
    public const string AdminPassword = "admin123";

    private static string GetEnvOrDefault(string variable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
