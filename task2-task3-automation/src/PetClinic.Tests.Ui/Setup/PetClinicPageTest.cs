using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace PetClinic.Tests.Ui.Setup;

/// <summary>
/// Base class for every UI test. Handles a Docker-specific quirk: PetClinic's
/// frontend calls its API via same-origin relative paths (e.g. "/api/auth/login"),
/// and its backend enforces a strict CORS allow-list that accepts an Origin of
/// "localhost" but rejects "host.docker.internal" with 403 Invalid CORS request
/// (confirmed directly: curl with Origin: http://host.docker.internal:8081 -> 403,
/// same request with Origin: http://localhost:8081 -> 200).
///
/// So navigating the browser straight to http://host.docker.internal:8081 breaks
/// login inside the container even though the page itself loads fine. The fix:
/// keep navigating to http://localhost:8081 (TestSettings.UiBrowserUrl's default,
/// unchanged between local and Docker runs) and instead tell Chromium, via
/// --host-resolver-rules, to resolve "localhost" to host.docker.internal's real
/// address. The browser then believes it's genuinely on localhost — matching
/// what a real user's local browser does — while its network layer actually
/// reaches the host machine. Only applied when PLAYWRIGHT_RESOLVE_LOCALHOST_TO
/// is set (i.e. inside the Docker image); a plain local run leaves Chromium's
/// default resolution untouched.
/// </summary>
public abstract class PetClinicPageTest : PageTest
{
    public override async Task<BrowserTypeLaunchOptions?> LaunchOptionsAsync()
    {
        var options = await base.LaunchOptionsAsync() ?? new BrowserTypeLaunchOptions();

        var resolveTarget = Environment.GetEnvironmentVariable("PLAYWRIGHT_RESOLVE_LOCALHOST_TO");
        if (!string.IsNullOrWhiteSpace(resolveTarget))
        {
            var args = options.Args ?? [];
            options.Args = [.. args, $"--host-resolver-rules=MAP localhost {resolveTarget}"];
        }

        return options;
    }
}
