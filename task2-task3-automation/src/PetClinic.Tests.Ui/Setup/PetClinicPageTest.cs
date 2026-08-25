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
///
/// Also defaults every test's browser context to the UTC timezone. PetClinic
/// stores and returns dates in UTC but renders them in the browser's local
/// timezone (per the AUT's own README) -- without pinning this, a date-sensitive
/// assertion's correctness would depend on whatever timezone happens to be set
/// on the machine running the suite, which test-plan.md's entry criteria already
/// flagged as a manual assumption ("tester's browser/OS clock is set to UTC").
/// This makes that assumption an enforced default instead. DueDateTimezoneTests
/// deliberately overrides it per test case to check other timezones on purpose.
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

    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.TimezoneId = "UTC";
        // Also pin the locale: confirmed live that without this, Chromium falls
        // back to the host OS's regional format (e.g. "23.09.2026 г." on a
        // Bulgarian-locale machine, not "9/23/2026") -- the suite's correctness
        // shouldn't depend on which machine happens to run it, same reasoning
        // as pinning the timezone above.
        options.Locale = "en-US";
        return options;
    }
}
