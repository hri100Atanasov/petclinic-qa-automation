# Task 2 & 3 — UI and API Test Automation

One dockerized .NET solution covering both UI automation (Task 2, Playwright) and API automation
(Task 3, RestSharp) against PetClinic Pro's Billing module, runnable independently or together.

This README covers this project specifically. A consolidated top-level setup guide for the whole
repo (Task 5) will reference this doc rather than duplicate it.

## Prerequisites

**Via Docker:**
- Docker Desktop (macOS or Windows) with Compose. No .NET SDK needed on the host.

**Outside Docker, directly on the host:**
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Playwright's browser binaries, installed once: after the first `dotnet build`, run the
  generated install script for the UI project —
  `pwsh src/PetClinic.Tests.Ui/bin/Debug/net10.0/playwright.ps1 install chromium --with-deps`
  (or the `.sh` equivalent Playwright generates on macOS/Linux).

**Either way:** PetClinic Pro itself must already be running separately — see
[`qa-test-automation-task/README.md`](../../qa-test-automation-task/README.md). This project
never starts or manages PetClinic's lifecycle; it only checks it's reachable before testing
against it.

## Running the tests

Two equivalent ways to run — pick one.

**1. Docker Compose directly**

doesn't open the report automatically, report is saved to `./testresults/`

```bash
docker compose run --rm tests ui    # UI suite only
docker compose run --rm tests api   # API suite only
docker compose run --rm tests all   # both (`docker compose up` also works)
```

To auto-open the report after a direct Docker run, chain it in PowerShell 7+ (`pwsh`):
- **Windows** — built in.
- **macOS** — install via `brew install --cask powershell`. The commands below use `Invoke-Item`
  rather than `Start-Process`, since only the former opens documents with their default app on
  macOS/Linux.

```powershell
docker compose run --rm tests ui; if (Test-Path testresults/ui-report.html) { Invoke-Item testresults/ui-report.html }
docker compose run --rm tests api; if (Test-Path testresults/api-report.html) { Invoke-Item testresults/api-report.html }
docker compose run --rm tests all; Get-ChildItem testresults/*-report.html | ForEach-Object { Invoke-Item $_.FullName }
```

**2. The runner (`PetClinic.Tests.Runner`)** — recommended day-to-day. Opens the HTML report
automatically when the run finishes; add `--no-open` to skip this (also skipped automatically
when a `CI` environment variable is set).

```bash
dotnet run --project src/PetClinic.Tests.Runner -- ui             # UI suite only, local
dotnet run --project src/PetClinic.Tests.Runner -- api            # API suite only, local
dotnet run --project src/PetClinic.Tests.Runner -- all            # both, local
dotnet run --project src/PetClinic.Tests.Runner -- all --docker   # both, executed inside Docker
```



Under `all`, both suites always run to completion — a UI failure doesn't skip the API suite — and
the run exits non-zero if **either** suite failed.

## What a run looks like

Console output is intentionally terse: no per-test pass/fail lines, no assertion messages, no stack
traces — just build/restore, a one-line result per suite, and the final summary. Full detail
(assertion messages, stack traces, per-test duration) lives in the `.trx`/HTML report instead (see
below), not on the console. This is `docker compose run --rm tests all`:

```
=== Running UI tests (Playwright) ===
Test run for /app/src/PetClinic.Tests.Ui/bin/Release/net10.0/PetClinic.Tests.Ui.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
WARNING: Overwriting results file: /app/testresults/ui-results.trx
Results File: /app/testresults/ui-results.trx
Html test results file : /app/testresults/ui-report.html

Failed!  - Failed:     4, Passed:     6, Skipped:     0, Total:    10, Duration: 29 s - PetClinic.Tests.Ui.dll (net10.0)

=== Running API tests (RestSharp) ===
Test run for /app/src/PetClinic.Tests.Api/bin/Release/net10.0/PetClinic.Tests.Api.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
WARNING: Overwriting results file: /app/testresults/api-results.trx
Results File: /app/testresults/api-results.trx
Html test results file : /app/testresults/api-report.html

Failed!  - Failed:     4, Passed:    10, Skipped:     0, Total:    14, Duration: 2 s - PetClinic.Tests.Api.dll (net10.0)

=== Summary ===
UI suite:  FAILED
API suite: FAILED
TRX + HTML reports written to ./testresults
```

("A total of 1 test files matched the specified pattern" refers to the one compiled test assembly
per suite, not the test count — the real counts are in the `Failed!`/`Passed!` line: `Total: 10` for
UI, `Total: 14` for API.) The runner (`dotnet run --project src/PetClinic.Tests.Runner -- all`)
produces the same per-suite block, then opens both HTML reports automatically unless `--no-open` is
passed.

Every run writes, per suite, to `./testresults/`:
- `{ui,api}-results.trx` — the raw VSTest result file
- `{ui,api}-report.html` — a self-contained HTML report (pass/fail counts, per-test list with
  duration, full error/stack trace on failure) via VSTest's built-in `html` logger. Opens directly
  in a browser — no server needed.

### The UI suite is expected to fail right now — that's by design

Four UI tests deliberately reproduce known, confirmed defects from Task 1
(`../task1-test-plan/test-plan.md` §8) and are written to assert the *correct* behavior, not the
app's current behavior — so they fail until the underlying bug is fixed, and start passing (with
no test changes needed) the moment it is:

| Test | Defect | Asserts |
|---|---|---|
| `Defect1TaxCalculationTests` | #1 — tax computed on subtotal, not taxable amount | A 100%-discounted invoice's tax should be $0 |
| `Defect2OverpaymentTests` | #2 — an invoice can be overpaid | Balance should never go negative after an overpayment |
| `Defect4DisabledAccountTests` | #4 — a disabled account can still authenticate | `former.staff` (disabled) should be rejected at login |
| `Defect5PaginationTests` | #5 — pagination `Next` stays active past the last page | `Next` should be disabled on the true last page |

The other 6 tests (login, the full invoice lifecycle, and 4 RBAC checks) pass and are expected to
keep passing. This mirrors Task 1's own exit criteria: the module doesn't exit "green," it exits
with a known, documented, regression-testable defect list — the same philosophy applied to
automation instead of manual scenarios.

### The API suite is also expected to fail right now — same reason, deeper layer

Five API tests fail today, all rooted in the same three confirmed defects — API-layer isolation
gives more precision than the UI tests can (exact decimal boundaries, direct JSON field checks, a
system-wide data sweep) but doesn't change which underlying bugs they trace back to:

| Test | Defect | Asserts |
|---|---|---|
| `Defect1TaxCalculationTests.Tax_Is_Computed_On_The_Taxable_Amount_Not_The_Subtotal` | #1 — tax computed on subtotal, not taxable amount | A 20%-discounted invoice's tax should be 8.00 (10% of the 80.00 taxable amount), not 10.00 |
| `Defect2OverpaymentTests.Overpaying_By_One_Cent_Does_Not_Leave_A_Negative_Balance` | #2 — an invoice can be overpaid | Paying 2.01 against a 2.00 balance should not leave a negative balance |
| `Defect3PaidBalanceIntegrityTests.Every_Paid_Invoice_Has_A_Zero_Balance` | #3 — PAID invoices with a non-zero balance | Every invoice currently in PAID status should have balance 0.00 (system-wide sweep, not specific invoice numbers — currently finds `INV-2024-0003` and `INV-2024-0004`) |
| `Defect4DisabledAccountTests.Disabled_Account_Cannot_Log_In` | #4 — a disabled account can still authenticate | `former.staff` (disabled) should be rejected at login |
| `InvoiceLifecycleTests.Full_Lifecycle_Computes_Every_Financial_Field_Correctly` | #1 (cascade) | Paid with the mathematically-correct total rather than the API's own inflated figure, a discounted+taxed invoice should reach PAID/balance 0.00 — it doesn't, because Defect #1 has already thrown off `taxAmount`, `total`, and everything computed from them |

The other 9 tests (login, RBAC across all four roles, the two passing overpayment boundary cases,
and the pagination contract check) pass and are expected to keep passing.

The lifecycle test's failures aren't a 6th defect — `taxAmount`, `total`, `balance`, and `status`
all fail there for the same root cause Defect #1 already covers, just observed at the end of a
realistic multi-step flow instead of in isolation. `Defect1TaxCalculationTests` is the minimal,
isolated reproduction; the lifecycle test shows the same bug's actual downstream consequence — a
correctly-discounted-and-taxed invoice can't reach a clean paid state, because the app is still
asking for and crediting the wrong amount at every step.

`Defect5PaginationTests` in this suite is **not** in the table above and is expected to **pass** —
Task 1 already established the API's own `last` flag is correct and Defect #5 is UI-only, so this
test is a regression guard confirming the API's contract, not a reproduction. Likewise
`RbacTests.Receptionist_Cannot_Void_Via_Api` passes: see "Known issues" below for why that
correlates with a change from what this README previously said.

## If PetClinic Pro isn't running

Both suites check readiness once, before any test runs (no retry/polling). If the app isn't
reachable, you get one clear message instead of a wall of connection-refused failures:

```
============================================================
 PetClinic Pro does not appear to be running.
 Checked: http://host.docker.internal:8080/actuator/health
 Reason:  Network is unreachable (host.docker.internal:8080)

 Start it first, then re-run the tests:
   cd qa-test-automation-task
   docker compose up

 Wait for the log to settle, then run this test suite again.
============================================================
```

The API suite checks only the API's `/actuator/health` endpoint. The UI suite checks *both* the UI
and the API — several UI tests (RBAC, Defect #1, Defect #2) seed their fixtures with direct API
calls (see `PetClinic.Tests.Shared/Api/PetClinicApiClient.cs`), so a UI-only check would pass while
the API is down and those tests would then fail on a raw connection error instead of this message.

## Stack

- **.NET 10**, **NUnit 4** as the test runner for both suites
- **UI (Task 2):** [Playwright for .NET](https://playwright.dev/dotnet/) — `Microsoft.Playwright` /
  `Microsoft.Playwright.NUnit`, pinned to **1.62.0**
- **API (Task 3):** [RestSharp](https://restsharp.dev/) — pinned to **114.0.0**
- **Docker image:** `mcr.microsoft.com/playwright/dotnet:v1.62.0-noble` (.NET 10 SDK + browsers
  pre-installed matching Playwright 1.62.0 exactly)
- **Reporting:** console output, `.trx`, and an HTML report per suite via VSTest's built-in `html`
  logger — no extra NuGet package needed

## UI test coverage (Task 2)

Page Object Model (`src/PetClinic.Tests.Ui/Pages/`) — `LoginPage`, `InvoiceListPage`,
`InvoiceDetailPage` — one class per distinct page/URL the app has. Assertions live in the tests,
not the page objects. Fixtures for tests that don't create their own invoice through the UI are
seeded directly via the API, using the same `PetClinicApiClient` the API suite uses as its system
under test (`PetClinic.Tests.Shared/Api/`, see "API test coverage" below) — that's test *setup*
here, not what's under test, so it isn't bound by Task 1's UI-only exploratory methodology.

Scoped to the Billing module, anchored on Task 1's own risk findings rather than broad coverage:

- **Login + S1 (full lifecycle)** — the positive baseline; proves the UI's rendering pipeline
  surfaces correct computed values through real forms, not just that the API returns them.
- **Defects #1, #2, #4, #5** — confirmed Task 1 defects that are UI-observable or UI-exclusive
  (§5's Next button bug can't be caught any other way). See "The UI suite is expected to fail
  right now" above.
- **RBAC at the UI layer** — new ground Task 1 didn't cover. Task 1 confirmed the *API* rejects
  unauthorized writes (test-plan.md §9, S10-S13); these tests check whether the *UI* actually hides
  those controls for READONLY/VET, or renders one that would then fail — a distinct
  authorization-awareness risk from what's already proven at the API layer.

Left out deliberately: S2/S3 (multi-item totals, partial payments) — solid positive coverage, but
at the UI layer they mostly re-prove "the screen displays what the API already computed," which is
better-owned by Task 3 where more input combinations are cheap to test directly against the API.

## API test coverage (Task 3)

RestSharp (`src/PetClinic.Tests.Api/`), with a `PetClinicApiClient` wrapper and response model
classes living in `PetClinic.Tests.Shared/Api/` — not this project — since the UI suite's own
fixtures (RBAC, Defect #1, Defect #2) reuse the exact same client to seed invoices via the API
rather than duplicating login/invoice-creation logic in both projects. In the API suite these calls
are the system under test; in the UI suite they're setup (e.g. an admin-authenticated client
creating a fixture invoice for another role's test) — unlike Task 2, there's no separate
setup-vs-SUT boundary at this layer, only which project is calling the shared client and why.

Invoice fixtures use one owner created once per test-assembly run (`SharedTestOwner`, populated by
each project's `AssemblySetup.EnsureAppIsRunning`) rather than reusing a specific pre-seeded owner
(e.g. id 6 / "Jean Coleman") — no test's correctness depends on that owner still existing or being
unmutated by a prior run. Telephone (exactly 10 digits) and email (well-formed) are synthesized per
the API's own validation rules; a pet is added since an owner with no pets isn't representative of
what the AUT expects a real owner record to look like. The owner's `lastName` is deliberately
prefixed `AAA`: the invoice-creation UI's owner dropdown only shows the first 100 owners sorted by
`lastName`, with no further pagination or search (Defect #6, `../task1-test-plan/test-plan.md` §8) —
an earlier version of this fixture created a fresh, randomly-named owner per *test* rather than per
*run*, which both grew the owner table quickly and left each new owner's dropdown visibility down to
chance once the table passed 100 rows; sorting first regardless of table size, combined with
creating only one owner per run instead of one per test, fixes both.

- **Login + full lifecycle (S1, S2)** — `InvoiceLifecycleTests` runs create → add two line items →
  issue → pay in full, asserting every financial field (`subtotal`, `discountAmount`,
  `taxableAmount`, `taxAmount`, `total`, `amountPaid`, `balance`) individually and combined
  (`total == taxableAmount + tax`), on a multi-item invoice — closing S2 (multi-item subtotal) at
  the API layer, since no UI test does.
- **Defects #1, #2, #3, #4** — confirmed Task 1 defects, isolated directly against the API with more
  precision than the UI allows: exact decimal boundaries (Defect #2's three-case BVA to the cent),
  a minimal single-field reproduction (Defect #1), and a system-wide data-integrity sweep across
  every PAID invoice (Defect #3 / S15) rather than specific invoice numbers. See "The API suite is
  also expected to fail right now" above.
- **Defect #5, at the API layer** — confirms the API's own `last` pagination flag is correct
  (regression guard, expected to pass), isolating that the bug is UI-only.
- **RBAC at the API layer (dual coverage with S16/UI RBAC, deliberately)** — the UI tests only prove
  the front end *hides* controls it shouldn't show; they say nothing about whether the backend would
  actually reject the request if one were sent anyway. `RbacTests` sends the requests directly:
  READONLY/VET rejected (403) on every write action, RECEPTIONIST allowed to create/add
  items/issue/pay but rejected on void, ADMIN allowed everything including void.

Not automated here: S3 (partial payment) beyond what the boundary-value and lifecycle tests already
exercise, and S10-S12 (zero/negative payment, immutable line items post-issue, no payment on a
voided invoice) — all confirmed manually in Task 1 as working correctly and documented there as
regression guards, but not re-automated in this pass since nothing in Task 1 or Task 2's exploration
suggested they were at risk.

## Configuration (environment variables)

All have working defaults; only change them if PetClinic runs somewhere other than the default
local Docker Compose setup. Copy `.env.example` to `.env` and edit it — Docker Compose picks it up
automatically.

| Variable | Default (local/no Docker) | Default (Docker) | Purpose |
|---|---|---|---|
| `UI_BASE_URL` | `http://localhost:8081` | `http://host.docker.internal:8081` | Where the UI readiness check looks |
| `API_BASE_URL` | `http://localhost:8080` | `http://host.docker.internal:8080` | Where the API readiness check and all API tests point |
| `UI_BROWSER_URL` | `http://localhost:8081` | `http://localhost:8081` (**not** overridden) | Where Playwright actually navigates the browser — see Known Issues for why this is kept separate from `UI_BASE_URL` |
| `PLAYWRIGHT_RESOLVE_LOCALHOST_TO` | unset | `host.docker.internal` | Only meaningful in Docker — see Known Issues |

## Known issues / design notes

- **PetClinic's CORS policy rejects `host.docker.internal` as an Origin, but accepts
  `localhost`.** Confirmed directly: the same login request returns `200` with
  `Origin: http://localhost:8081` and `403 Invalid CORS request` with
  `Origin: http://host.docker.internal:8081`. Since the containerized browser needs
  `host.docker.internal` to reach the host machine at all, but must *present as* `localhost` for
  the app to accept it, the UI test's browser is launched with a Chromium
  `--host-resolver-rules=MAP localhost host.docker.internal` flag (see `PetClinicPageTest.cs`)
  rather than navigating to `host.docker.internal` directly. This is why `UI_BROWSER_URL` and
  `UI_BASE_URL` are separate settings — the former is what the browser addresses (must stay
  `localhost`), the latter is what this .NET process's own HTTP client uses for the readiness
  check (must be reachable from inside the container).
- **The `Microsoft.Playwright*` NuGet version and the `mcr.microsoft.com/playwright/dotnet` image
  tag must match exactly**, or browser/driver mismatches happen at runtime. Currently pinned to
  `1.62.0` / `v1.62.0-noble` in both the `.csproj` files and the `Dockerfile`'s
  `ARG PLAYWRIGHT_VERSION`. Bump both together, never just one.
- **No retry on the readiness check.** It checks once and fails fast with instructions;
  re-running the command after starting PetClinic is the expected workflow, not an automatic
  wait/retry loop.
- **Two separate flags are needed to keep the console quiet, not one.**
  `--logger "console;verbosity=quiet"` silences VSTest's own per-test output (assertion messages,
  stack traces), but `dotnet test` separately invokes MSBuild's terminal logger in `auto` mode,
  which — only when stdout is a real interactive terminal (not when piped, e.g. in CI or through a
  tool) — prints its own failed-test summary with the full stack trace again, as compiler-style
  `error TESTERROR:` diagnostics. `-tl:off` turns that off unconditionally. Dropping either flag
  brings stack traces back under some conditions but not others, depending on how the command is
  invoked — both are required together.
- **`dotnet restore`/`dotnet build` only accept a single project path each** — passing two
  (`dotnet build a.csproj b.csproj`) fails with an MSBuild "switch syntax" error. The Dockerfile
  runs them once per project instead of trying to pass both at once. Only matters if you're
  editing the Dockerfile.
- **The app's own state updates can lag its click handlers.** Clicking `Next` (or submitting a
  form) and immediately reading the DOM can observe stale state — e.g. clicking `Next` 14 times in
  a tight loop was observed to leave the page stuck on page 1. `LoginPage.LoginAsync` and
  `InvoiceListPage`'s `ClickNextAsync`/`CreateDraftInvoiceAsync` all wait for their action's actual
  effect (network idle, or the specific value that should have changed) before returning, rather
  than trusting that a click resolving means the app has caught up.
- **Correction to a Task 1 finding, caught while building Task 3:** the README previously
  documented that `reception`'s API token could void an invoice (`POST /api/invoices/{id}/void`
  returning 200), based on a Task 1 finding. Re-verified directly against the running app while
  building `RbacTests.Receptionist_Cannot_Void_Via_Api` — on both a DRAFT and a freshly-issued
  invoice, with an admin void succeeding normally as a positive control — and got a consistent
  `403 Forbidden` for reception both times. That earlier finding doesn't reproduce on the currently
  running app; the test is written (and passes) as a regression guard confirming the 403, not a
  defect reproduction. Left as a documented correction rather than silently dropped.
- **Scope right now:** login, the full invoice lifecycle (S1, S2), UI-observable and API-level Task
  1 defects (#1, #2, #3, #4, #5), and RBAC across all four roles at both the UI and API layers — see
  "UI test coverage" and "API test coverage" below for what's covered and why.