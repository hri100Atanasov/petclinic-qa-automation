# PetClinic Pro — QA Automation Submission

QA Engineer take-home submission against **PetClinic Pro**, a vet practice management app. Every
task in the brief is optional; this is what was attempted, why, and how to run it.

## Tasks attempted

| Task | Status | Where |
|---|---|---|
| 1 — Test plan and scenarios | Done — Billing module | [`task1-test-plan/`](task1-test-plan/test-plan.md) |
| 2 — UI automation | Done — Playwright + NUnit | [`task2-task3-automation/`](task2-task3-automation/README.md) |
| 3 — API automation | Done — RestSharp + NUnit | [`task2-task3-automation/`](task2-task3-automation/README.md) |
| 4 — Performance test | **Not attempted** | — |
| 5 — Setup guide and execution instructions | Done | this file |

Task 4 wasn't attempted — time was spent going deeper on Tasks 1–3 instead (six automated defect
reproductions, RBAC across four roles at both layers, and a test-data fixture that had to be
reworked twice after it broke itself — see "Known issues" below) rather than adding a fourth,
shallower task.

## Repository layout

```
petclinic-qa-automation/
├── README.md                    ← you are here (Task 5)
├── PROMPTS.md                   ← AI-usage log (verbatim prompts, what was kept/changed/caught)
├── task1-test-plan/             ← Task 1: test plan + scenarios (Billing module)
│   ├── test-plan.md
│   └── scenarios-full.md
└── task2-task3-automation/      ← Tasks 2 & 3: UI + API automation (one .NET solution)
    ├── README.md                ← detailed setup/run/coverage docs for this project specifically
    └── src/
```

`task2-task3-automation/README.md` is the detailed reference for that project (prerequisites,
every environment variable, full defect/coverage tables, design notes). This file gets you from a
clean machine to a running suite without needing to open it — but it's there for anything this file
only summarizes.

## Prerequisites

- **Docker Desktop** (macOS or Windows), with Compose — this is the only hard requirement. No
  .NET SDK needed on the host if you run everything through Docker.
- To run the suites directly on the host instead (faster iteration than Docker): [.NET 10
  SDK](https://dotnet.microsoft.com/download), plus a one-time Playwright browser install after the
  first build (`pwsh src/PetClinic.Tests.Ui/bin/Debug/net10.0/playwright.ps1 install chromium
  --with-deps`) — see `task2-task3-automation/README.md`'s Prerequisites section for the exact
  steps and macOS/Linux equivalent.

## 1. Start the application under test

PetClinic Pro itself lives in a separate repository
([`qa-test-automation-task`](https://github.com/akotrulev/qa-test-automation-task)) — this
submission doesn't start or manage its lifecycle, only tests against it. Clone it as a **sibling**
directory to this one (the doc links below assume that layout):

```bash
cd ..
git clone https://github.com/akotrulev/qa-test-automation-task
cd qa-test-automation-task
docker compose up
```

Follow that repo's own README for the platform-specific image-loading step (it downloads Docker
images from a GitHub release before `compose up` will work) and the full accounts table. Once the
log settles:

| | |
|---|---|
| Web UI | <http://localhost:8081> |
| REST API | <http://localhost:8080/api> |
| Health check | <http://localhost:8080/actuator/health> |

## 2. Point the suites at the application

Both suites default to `localhost:8080` (API) / `localhost:8081` (UI) — matching the setup above
with no configuration needed. If PetClinic runs somewhere else (a different host, port, or inside
Docker where `localhost` means the container itself), override via environment variables — copy
`task2-task3-automation/.env.example` to `.env` and edit it, or set them directly:

| Variable | Default | Purpose |
|---|---|---|
| `API_BASE_URL` | `http://localhost:8080` | Where the API readiness check and every API test/fixture call point |
| `UI_BASE_URL` | `http://localhost:8081` | Where the UI readiness check looks |
| `UI_BROWSER_URL` | `http://localhost:8081` | Where Playwright actually navigates the browser (kept separate from `UI_BASE_URL` for a CORS-related reason — see `task2-task3-automation/README.md`'s Known issues) |

**One thing that's *not* an environment variable:** the seed account credentials (`admin`/`admin123`,
`reception`/`desk123`, etc.) are hardcoded in
[`SeedAccounts.cs`](task2-task3-automation/src/PetClinic.Tests.Shared/Configuration/SeedAccounts.cs),
since they're fixed accounts documented in the AUT's own README, not environment-specific config.
If you're pointing this at an instance with different seed accounts, that file is what to edit —
there's no env var for it.

## 3. Run the suites

All commands run from `task2-task3-automation/`. Two equivalent ways to run — pick one:

```bash
# Docker Compose directly — no .NET SDK needed on the host
docker compose run --rm tests ui     # UI suite only
docker compose run --rm tests api    # API suite only
docker compose run --rm tests all    # both

# The local runner — opens the HTML report automatically when done
dotnet run --project src/PetClinic.Tests.Runner -- ui             # UI suite only, local
dotnet run --project src/PetClinic.Tests.Runner -- api            # API suite only, local
dotnet run --project src/PetClinic.Tests.Runner -- all            # both, local
dotnet run --project src/PetClinic.Tests.Runner -- all --docker   # both, executed inside Docker
```

Both suites check the app is reachable before running anything, and fail fast with a clear message
(not a wall of connection errors) if it isn't — the fix is always "start PetClinic Pro, then re-run
the command."

### What a run looks like — and why it isn't green

**This is expected, not a sign something's broken:**

```
UI suite:  FAILED — 8 passed, 6 failed (of 14)
API suite: FAILED — 9 or 10 passed, 5 or 4 failed (of 14) — see note below
```

Both suites deliberately reproduce known, confirmed defects from the Task 1 test plan — each test
asserts the *correct* behavior, not the app's current behavior, so it fails today and will start
passing (no test changes needed) the moment the underlying bug is fixed. This mirrors Task 1's own
exit criteria: the module doesn't exit "green," it exits with a known, documented, regression-testable
defect list. The full defect-to-test mapping is in `task2-task3-automation/README.md`'s "What a run
looks like" section — the short version: tax miscalculation, invoice overpayment, and a disabled
account that can still log in, in both suites; the UI suite also reproduces the pagination defect
and (in 2 of 4 timezone cases) a due date that renders one day early for any viewer in a timezone
behind UTC; the API suite also reproduces the tax defect's downstream effect on a full paid invoice
lifecycle.

**The API suite's exact count varies, and that's expected too, not flakiness in the usual sense:**
one API test (`Every_Paid_Invoice_Has_A_Zero_Balance`) sweeps every PAID invoice in the database for
a non-zero balance — a real, confirmed defect, but one Task 1 could only reproduce on two specific
*original seed* invoices (`INV-2024-0003`/`0004`), not on a freshly created one. Whether that test
fails depends on whether those two invoices are currently present, which depends on whether
`qa-test-automation-task`'s Docker volume has been reset since they were last seeded (§4 below) —
present → 5 failures; reset away → 4. Every other failing test is self-contained and fails
deterministically regardless of what else is in the database.

Console output itself is intentionally terse (build lines, one summary line per suite, no per-test
detail) — full detail, including every assertion message and stack trace, is in the HTML report
written to `task2-task3-automation/testresults/{ui,api}-report.html` after every run (the local
runner opens these automatically).

## 4. Test data between runs, and resetting to a clean state

**Every run creates data and none of it is cleaned up automatically.** Specifically:

- Each suite creates one "shared" owner (with a pet) once per run, reused by every test in that run
  that needs an owner — so each run adds roughly one new owner per suite (two for `all`), not one
  per test. This was a deliberate fix: an earlier version created a fresh owner *per test*, which
  both grew the database quickly and (see the owner-selection defect below) made a newly created
  owner's visibility in the UI's owner dropdown a matter of chance once the table passed 100 rows.
  The current fixture always sorts to the very top of that dropdown regardless of how large the
  table gets, so **running the suite repeatedly is safe** — it won't recreate the flakiness that
  fix addressed.
- Invoices, line items, and payments created as test fixtures also accumulate — again, this doesn't
  affect correctness (no test depends on a specific total count), it just means the invoice list
  keeps growing.

**To return to the original seed state**, reset the AUT itself (this submission never does this for
you, since it isn't this repo's data to discard without being asked):

```bash
cd ../qa-test-automation-task
docker compose down -v
docker compose up
```

`down -v` removes the Docker volume the seed data lives in; without `-v` your accumulated test data
is kept across restarts.

## Known issues, flaky tests, and deliberate gaps

No currently-known flaky tests — three were found and fixed during this work rather than left as
known issues (a stale-DOM invoice-ID race, a pagination test that depended on other tests having
already created invoices, and the owner-dropdown timing issue described above); see `PROMPTS.md` for
how each was caught and fixed. What's deliberately out of scope:

- **Task 4 (performance testing)** — not attempted at all.
- **One confirmed defect has no automated regression test**: the owner-selection cap itself (the
  invoice UI's owner dropdown only shows the first 100 owners, no further pagination or search —
  documented as Defect #6 / S17 in the test plan) is real and reproduced manually, but wasn't turned
  into an automated Task 2/3 test — a candidate for follow-up.
- **S2/S3 and S10–S12** (multi-item totals beyond the lifecycle test, partial payments beyond the
  boundary cases already covered, and the zero/negative-payment, post-issue-immutability, and
  voided-invoice regression guards) were confirmed manually in Task 1 but not re-automated in Task
  2/3, since nothing in that exploration suggested they were at risk.
- Environment/tooling specifics (CORS handling in Docker, Playwright/image version pinning, why the
  readiness check doesn't retry, why two separate flags were needed to quiet the console) are in
  `task2-task3-automation/README.md`'s "Known issues / design notes" — narrower than what belongs
  here.

## AI usage

Used throughout, with every finding verified against the running application rather than taken on
faith — see [`PROMPTS.md`](PROMPTS.md) for the full, verbatim log: every prompt in order, what was
kept versus rewritten, and — the part that matters most — what the model got wrong and how each was
caught. Includes a case where a previously documented "confirmed" finding (reception's API token
could void an invoice) didn't reproduce on re-verification against the running app, and was
corrected rather than left standing.

## Assumptions

- **Directory layout**: this repo and `qa-test-automation-task` are sibling directories (the doc
  links above assume that). Functionally, only the environment variables in §2 matter — the layout
  assumption is purely a documentation convenience.
- **Billing module scope** (Task 1) and everything downstream from it (which defects mattered
  enough to automate, which scenarios were left as manual-only) — reasoning is in
  `task1-test-plan/test-plan.md` §1–§3, not repeated here.
- `taxRate` is a `[0, 1]` fraction while `discountPct` is a `[0, 100]` whole percentage — the two
  fields look similar but aren't scaled the same way; worth knowing before reading any test's fixture
  data.
- Seed data resets via `docker compose down -v` on the AUT's own compose file (§1 above) — not
  something this submission's suites do for you.
