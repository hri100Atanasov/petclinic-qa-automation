namespace PetClinic.Tests.Shared.Configuration;

/// <summary>
/// Central place to read where the AUT lives. Defaults match the AUT's own README
/// (localhost:8081 UI, localhost:8080 API) for running the suites directly on a
/// dev machine. When containerized, the Docker Compose file overrides these to
/// point at host.docker.internal instead — see docker/docker-compose.yml.
/// </summary>
public static class TestSettings
{
    /// <summary>
    /// Where the UI is reachable from this .NET process's own network stack —
    /// used only for the pre-flight health check. In Docker this is overridden
    /// to host.docker.internal, same as ApiBaseUrl.
    /// </summary>
    public static string UiBaseUrl =>
        GetEnvOrDefault("UI_BASE_URL", "http://localhost:8081");

    /// <summary>
    /// The URL Playwright actually navigates the browser to. Deliberately kept
    /// separate from UiBaseUrl: PetClinic's backend rejects the CORS Origin
    /// "host.docker.internal" (403 Invalid CORS request) but accepts "localhost",
    /// so the browser must always address the app as localhost. When containerized,
    /// PetClinicPageTest additionally tells Chromium (via --host-resolver-rules) to
    /// resolve "localhost" to host.docker.internal's real address — see its doc
    /// comment for the full explanation.
    /// </summary>
    public static string UiBrowserUrl =>
        GetEnvOrDefault("UI_BROWSER_URL", "http://localhost:8081");

    public static string ApiBaseUrl =>
        GetEnvOrDefault("API_BASE_URL", "http://localhost:8080");

    private static string GetEnvOrDefault(string variable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
