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

## What a passing run looks like

```
=== Running UI tests (Playwright) ===
  Passed Admin_Can_Log_In_And_Reach_The_Dashboard [1 s]
Test Run Successful.
Total tests: 1
     Passed: 1

=== Running API tests (RestSharp) ===
  Passed Admin_Can_Log_In_And_Receive_A_Bearer_Token [203 ms]
Test Run Successful.
Total tests: 1
     Passed: 1

=== Summary ===
UI suite:  PASSED
API suite: PASSED
TRX + HTML reports written to ./testresults
```

Every run writes, per suite, to `./testresults/`:
- `{ui,api}-results.trx` — the raw VSTest result file
- `{ui,api}-report.html` — a self-contained HTML report (pass/fail counts, per-test list with
  duration, full error/stack trace on failure) via VSTest's built-in `html` logger. Opens directly
  in a browser — no server needed.

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

The UI suite checks only the UI's reachability; the API suite checks only the API's
`/actuator/health` endpoint — each suite gates on what's relevant to it, not on the other surface.

## Stack

- **.NET 10**, **NUnit 4** as the test runner for both suites
- **UI (Task 2):** [Playwright for .NET](https://playwright.dev/dotnet/) — `Microsoft.Playwright` /
  `Microsoft.Playwright.NUnit`, pinned to **1.62.0**
- **API (Task 3):** [RestSharp](https://restsharp.dev/) — pinned to **114.0.0**
- **Docker image:** `mcr.microsoft.com/playwright/dotnet:v1.62.0-noble` (.NET 10 SDK + browsers
  pre-installed matching Playwright 1.62.0 exactly)
- **Reporting:** console output, `.trx`, and an HTML report per suite via VSTest's built-in `html`
  logger — no extra NuGet package needed

## Configuration (environment variables)

All have working defaults; only change them if PetClinic runs somewhere other than the default
local Docker Compose setup. Copy `.env.example` to `.env` and edit it — Docker Compose picks it up
automatically.

| Variable | Default (local/no Docker) | Default (Docker) | Purpose |
|---|---|---|---|
| `UI_BASE_URL` | `http://localhost:8081` | `http://host.docker.internal:8081` | Where the UI readiness check looks |
| `API_BASE_URL` | `http://localhost:8080` | `http://host.docker.internal:8080` | Where the API readiness check and all API tests point |
| `PETCLINIC_ADMIN_USERNAME` / `PETCLINIC_ADMIN_PASSWORD` | `admin` / `admin123` | same | Seed admin credentials, per the AUT's README |
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
- **`dotnet restore`/`dotnet build` only accept a single project path each** — passing two
  (`dotnet build a.csproj b.csproj`) fails with an MSBuild "switch syntax" error. The Dockerfile
  runs them once per project instead of trying to pass both at once. Only matters if you're
  editing the Dockerfile.
- **Scope right now:** one smoke test per suite (login), proving the whole pipeline — Docker
  build, cross-platform networking, readiness gating, both runners, HTML/`.trx` reporting — works
  end-to-end. The real Task 2/3 scenario suites come next, on top of this foundation.