# PetClinic Pro — QA Automation Submission

QA Engineer take-home submission against **PetClinic Pro**, a vet practice management app. Every
task in the brief is optional; this is what was attempted, why, and how to run it.

## Tasks attempted

| Task | Status | Where |
|---|---|---|
| 1 — Test plan and scenarios | Done — Billing module | [`task1-test-plan/`](task1-test-plan/test-plan.md) |
| 2 — UI automation | Done — Playwright + NUnit | [`task2-task3-automation/`](task2-task3-automation/README.md) |
| 3 — API automation | Done — RestSharp + NUnit | [`task2-task3-automation/`](task2-task3-automation/README.md) |
| 4 — Performance test | Done — NBomber | [`task4-performance/`](task4-performance/SUMMARY.md) |
| 5 — Setup guide and execution instructions | Done | this file |

All five tasks were attempted. Task 4 is kept deliberately self-contained — its own report, defect
list, and AI-usage log under `task4-performance/` — because it uses a different tool (NBomber) and a
different methodology (concurrent load) from Tasks 2/3, and its two findings are concurrency defects
that the other suites structurally cannot reproduce.

## Repository layout

```
petclinic-qa-automation/
├── README.md                    ← you are here (Task 5)
├── PROMPTS-SUMMARY.md           ← the "what the model got wrong" passages from both logs, verbatim
├── PROMPTS-TASK-1-2-3.md        ← AI-usage log for Tasks 1/2/3/5 (verbatim prompts, in order)
├── task1-test-plan/             ← Task 1: test plan + scenarios (Billing module)
│   ├── test-plan.md
│   └── scenarios-full.md
├── task2-task3-automation/      ← Tasks 2 & 3: UI + API automation (one .NET solution)
│   ├── README.md                ← detailed setup/run/coverage docs for this project specifically
│   └── src/
└── task4-performance/           ← Task 4: load/concurrency testing (NBomber)
    ├── README.md                ← how to run the performance suite
    ├── SUMMARY.md               ← load model, results, conclusions
    ├── DEFECTS.md               ← the two concurrency defects found (#8, #9)
    ├── PROMPTS-TASK-4.md        ← Task 4's own AI-usage log
    ├── reports-cited/           ← the raw NBomber runs every quoted number comes from
    └── PetClinic.PerformanceTests/
```

`task2-task3-automation/README.md` is the detailed reference for that project (prerequisites,
every environment variable, full defect/coverage tables, design notes). This file gets you from a
clean machine to a running suite without needing to open it — but it's there for anything this file
only summarizes.

## Defects found

Nine confirmed defects, all in Billing. #1–#7 came from Task 1's exploratory pass — full
reproduction detail in [`task1-test-plan/test-plan.md`](task1-test-plan/test-plan.md) §8. #8–#9 came
from Task 4's concurrency testing, which every other suite here structurally could not have found:
each of them issues one request at a time by construction, and both defects need two requests to
overlap. Detail in [`task4-performance/DEFECTS.md`](task4-performance/DEFECTS.md).

| # | Defect | Layer | Automated regression test |
|---|---|---|---|
| 1 | Tax computed on the subtotal instead of the discounted taxable amount | API | UI + API |
| 2 | Invoices can be overpaid — balance goes negative, status stays `PARTIALLY_PAID` | API | UI + API |
| 3 | `PAID` invoices carrying a non-zero balance | API/data | API (system-wide sweep) |
| 4 | Disabled account (`former.staff`) can still authenticate and use billing | API | UI + API |
| 5 | Pagination `Next` stays enabled past the last page | UI only | UI |
| 6 | Invoice-form owner dropdown capped at the first 100 owners, no search or paging | UI only | **none** — see Known issues |
| 7 | Due date renders one day early for any viewer in a timezone behind UTC | UI only | UI (2 of 4 timezone cases) |
| 8 | Invoice-number race — concurrent creates collide on a unique constraint | API | Task 4, Tests 1/4/6 |
| 9 | Concurrent payments leave a fully paid invoice stuck at `PARTIALLY_PAID` | API | Task 4, Test 2 |

Two things worth pulling out of that table. **#9 is probably the same root cause as #3**, seen from
the other side of the same race — balance correct while status is wrong, versus status correct while
balance is wrong; Task 1 could never reproduce #3 on demand, and #9 is the first evidence of a
mechanism that would explain how it arises at all (reasoning in `DEFECTS.md`). And **#8 needs no load
at all** — two simultaneous invoice creations failed one of the two in 12 of 12 trials, which is the
minimum concurrency a real clinic can experience.

One earlier finding was **retracted**: a Task 1 note that `reception`'s token could void an invoice
did not reproduce on re-verification and is documented as a correction rather than silently dropped —
see `task2-task3-automation/README.md`'s Known issues.

## Prerequisites

- **Docker Desktop 4.x** (macOS or Windows) with **Compose v2** — enough on its own for Tasks 2 and
  3, which need no .NET SDK on the host.
- **[.NET 10 SDK](https://dotnet.microsoft.com/download)** — required for **Task 4**, which is
  deliberately not containerized so the load generator doesn't compete with the application for the
  same Docker resource pool. It also lets you run Tasks 2/3 directly on the host for faster
  iteration; that path additionally needs a one-time **Playwright 1.62.0** browser install after the
  first build (`pwsh src/PetClinic.Tests.Ui/bin/Debug/net10.0/playwright.ps1 install chromium
  --with-deps`) — see `task2-task3-automation/README.md`'s Prerequisites section for the exact steps
  and the macOS/Linux equivalent.

So: Docker alone gets you Tasks 2 and 3. Task 4 needs the SDK either way.

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

All three suites default to `localhost:8080` (API) / `localhost:8081` (UI) — matching the setup
above, with no configuration needed. If PetClinic runs somewhere else (a different host, port, or
inside Docker where `localhost` means the container itself), override via environment variables:

| Variable | Default | Used by | Purpose |
|---|---|---|---|
| `API_BASE_URL` | `http://localhost:8080` | Tasks 2, 3, 4 | Where the API readiness check and every API test/fixture call point |
| `UI_BASE_URL` | `http://localhost:8081` | Task 2 | Where the UI readiness check looks |
| `UI_BROWSER_URL` | `http://localhost:8081` | Task 2 | Where Playwright actually navigates the browser (kept separate from `UI_BASE_URL` for a CORS-related reason — see `task2-task3-automation/README.md`'s Known issues) |

These three are about *where the application is*. Each project also has its own tuning knobs that
have nothing to do with locating it — Task 4's load rates, for one; those are documented in
[`task4-performance/README.md`](task4-performance/README.md) rather than here.

**`.env` is a Docker Compose file, and only that.** Copying `task2-task3-automation/.env.example` to
`.env` works for `docker compose run`, which picks it up automatically. It does **nothing** for the
local runner or for Task 4 — both read real environment variables, and neither loads a `.env` file.
For a host run, export the variables in your shell instead. Note too that `.env.example` ships the
*Docker* values (`host.docker.internal`); on the host you want `localhost`.

**One thing that's *not* an environment variable:** the seed account credentials (`admin`/`admin123`,
`reception`/`desk123`, etc.) are hardcoded in
[`SeedAccounts.cs`](task2-task3-automation/src/PetClinic.Tests.Shared/Configuration/SeedAccounts.cs),
since they're fixed accounts documented in the AUT's own README, not environment-specific config.
If you're pointing this at an instance with different seed accounts, that file is what to edit —
there's no env var for it.

## 3. Run the suites

**Tasks 2 & 3 (UI + API)** — from `task2-task3-automation/`. Two equivalent ways to run, pick one:

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

**Task 4 (performance)** — from `task4-performance/PetClinic.PerformanceTests/`. Requires the .NET
SDK on the host; deliberately not containerized, so the load generator doesn't compete with the
application for the same Docker resource pool:

```bash
dotnet run -c Release -- test1   # invoice creation under ramped load
dotnet run -c Release -- test2   # concurrent payments against one invoice
dotnet run -c Release -- test3   # read-heavy invoice list
dotnet run -c Release -- test4   # mixed read/write
dotnet run -c Release -- test5   # read ramp (finds where the DB connection pool saturates)
dotnet run -c Release -- test6   # write scalability at a fixed rate
dotnet run -c Release -- all     # all six, in order
```

[`task4-performance/README.md`](task4-performance/README.md) is the detailed reference for this
project — environment variables, what each run is expected to produce, test-data behaviour, and how
to reproduce the capped-resource runs `SUMMARY.md` quotes (via
[`docker-compose.resource-limits.yml`](task4-performance/docker-compose.resource-limits.yml), which
nothing in the default run path uses).

Every suite checks the app is reachable before running anything, and fails fast with a clear message
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

**Task 4 is different** — it isn't pass/fail in the same sense. Each test prints an NBomber stats
table (request counts, error rate, latency percentiles) and writes a timestamped HTML/CSV/Markdown
report to `task4-performance/PetClinic.PerformanceTests/reports/`. Tests 1 and 4 are *expected* to
show a single-digit-to-10% write error rate, and Test 2 is expected to end with a `FAIL — invoice #N
did not reach a consistent paid state` line: those are the two defects it exists to demonstrate.
Test 5 ramps read traffic until the database connection pool reaches its configured maximum; it
should complete with **0 errors** and visibly higher latency than the other tests — p95 anywhere from
~45ms to ~160ms depending on what else the machine is doing. That spread is itself part of the
finding rather than instability; `SUMMARY.md` reports all three runs and what separates them. Test 6 is *expected* to fail a
growing share of writes the harder it is driven — that curve is the result it exists to produce, and
`task4-performance/README.md` covers how the rate is set if you want to reproduce it.
See [`task4-performance/SUMMARY.md`](task4-performance/SUMMARY.md).

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
- **Task 4 creates noticeably more**: 10 owners and 10 RECEPTIONIST user accounts per run, plus one
  invoice per write request — roughly **435 create requests across a full `all` run, ~400 of them
  succeeding**, with Test 6 alone accounting for 200 at its default rate. Nothing depends on a clean
  baseline, but this is by far the suite most worth resetting after if you run it repeatedly.

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
already created invoices, and the owner-dropdown timing issue described above); see
`PROMPTS-TASK-1-2-3.md` for how each was caught and fixed. What's deliberately out of scope:

- **Task 4's concurrent-payment test is probabilistic** — it reproduces its defect in roughly 9 runs
  out of 10, since it is deliberately racing the application. A single passing run is not evidence
  the defect is fixed. See [`task4-performance/DEFECTS.md`](task4-performance/DEFECTS.md).
- **One confirmed defect has no automated regression test**: the owner-selection cap itself (the
  invoice UI's owner dropdown only shows the first 100 owners, no further pagination or search —
  documented as Defect #6 / S17 in the test plan) is real and reproduced manually, but wasn't turned
  into an automated Task 2/3 test — a candidate for follow-up.
- **S2/S3 and S10–S12** (multi-item totals beyond the lifecycle test, partial payments beyond the
  boundary cases already covered, and the zero/negative-payment, post-issue-immutability, and
  voided-invoice regression guards) were confirmed manually in Task 1 but not re-automated in Task
  2/3, since nothing in that exploration suggested they were at risk.
- **Task 4's read-ramp result varies more than the others.** Test 5 at 200 req/s crossed into
  connection-pool queuing in one of three runs and not in the other two (p95 161ms vs 62ms and 47ms).
  The pool reaching its maximum is consistent; the latency figure is not, and `SUMMARY.md` reports
  the range rather than the best single run. Raising `spring.datasource.hikari.maximum-pool-size` and
  re-running is the obvious next experiment, and is listed there as a follow-up.
- **`Defect5PaginationTests` gets slower as the database grows.** It clicks `Next` all the way to the
  true last page, so its runtime scales with the invoice count (~47 clicks at ~470 invoices). Repeated
  runs stay *correct* — see §4 — but on a heavily-used database this is the slowest UI test by a wide
  margin. `docker compose down -v` on the AUT brings it back down.
- **Task 4 exits non-zero whenever any request failed**, which for Tests 1, 4 and 6 is every run, by
  design — those failures are the defect it exists to demonstrate. Don't wire it into CI as a
  pass/fail gate without accounting for that.
- Environment/tooling specifics (CORS handling in Docker, Playwright/image version pinning, why the
  readiness check doesn't retry, why two separate flags were needed to quiet the console) are in
  `task2-task3-automation/README.md`'s "Known issues / design notes" — narrower than what belongs
  here.

## AI usage

Used throughout, with every finding verified against the running application rather than taken on
faith. Start with [`PROMPTS-SUMMARY.md`](PROMPTS-SUMMARY.md), which collects the "what the model got
wrong" passages from both logs verbatim in one place — that's the part the brief says it weighs most
heavily. The full logs behind it are [`PROMPTS-TASK-1-2-3.md`](PROMPTS-TASK-1-2-3.md) (Tasks 1/2/3/5)
and [`task4-performance/PROMPTS-TASK-4.md`](task4-performance/PROMPTS-TASK-4.md): every prompt in
order, what was kept versus rewritten, and what the model got wrong and how each was caught. Includes a case where a previously documented "confirmed" finding (reception's API token
could void an invoice) didn't reproduce on re-verification against the running app, and was
corrected rather than left standing.

Task 4 keeps its own log because it was built as a self-contained unit; the two logs together cover
the whole submission.

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
