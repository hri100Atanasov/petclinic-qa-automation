# Prompt summary — what the model got wrong

This is a reading aid, not a replacement for the AI-usage log the brief asks for. The full logs,
with every prompt pasted verbatim and in order, are:

- [`PROMPTS-TASK-1-2-3.md`](PROMPTS-TASK-1-2-3.md) — Tasks 1, 2, 3 and 5 (Prompts 1–59)
- [`task4-performance/PROMPTS-TASK-4.md`](task4-performance/PROMPTS-TASK-4.md) — Task 4 (Prompts 1–10)

The brief singles out one thing as mattering most: *"What the model got wrong, and what you changed
after checking it against the running application."* Those passages exist in both logs, but they are
spread across roughly 1,700 lines, so this file collects the strongest of them **verbatim** — copied
unedited from the logs, in the order they happened, each labelled with where it came from so it can
be read in context.

Nothing here is summarised or rewritten. If a passage below disagrees with a log, the log is right.

The short version: the model invented a defect that did not exist, described a UI control that was
not on the screen, repeatedly attributed its own verification work to testing that never happened,
shipped four pieces of test code that compiled but did not do what they claimed, and published
performance figures that its own reports contradicted. Each was caught by running something against
the live application rather than by reading the output again.

---

## 1. It invented a defect, from an assumption it never tested

*(`PROMPTS-TASK-1-2-3.md`, Prompt 2)*

**What the model got wrong (Prompt 2):** while verifying Bug 1, the model tested `discountPct`
with values `1.0` and `0.5`, assuming (by analogy with `taxRate`, which the API had just rejected
at `10` with "must be less than or equal to 1.0000") that `discountPct` was also a `0–1` fraction.
It never independently tested `discountPct`'s own validation boundary before drawing that
conclusion. On that wrong assumption, `discountPct: 1.0` was read as "100% discount," and the
resulting `discountAmount: 1.00` on a 100.00 subtotal was reported as a new defect ("discount
computed two orders of magnitude too small").

I tested the actual validation errors myself — `discountPct: -10` → `"must be greater than or
equal to 0.00"`, `discountPct: 150` → `"must be less than or equal to 100.00"` — which shows
`discountPct` is a `0–100` whole-percentage field, not a `0–1` fraction. `discountPct: 1.0` was
therefore "1%," and `discountAmount: 1.00` (1% of 100.00) was the *correct* result. Told the model
to double-check and retract the finding.

**How it was caught, and the retraction** *(Prompt 3)*

**What the model got wrong / corrected after this prompt:** re-ran the same boundary checks
against the live API (`discountPct: -10` and `discountPct: 150`), got the same validation messages
the candidate reported, and confirmed `discountPct: 100` on a 100.00 subtotal produces
`discountAmount: 100.00` (correct). Retracted the false "Bug 6" finding from
`task1-test-plan/test-plan.md` — removed it from the risk table, the known-defects list, and the
scenario list, and repurposed scenario S7 from "targets defect #6" to a regression-guard positive
case (discount percentage is applied correctly) instead. Bug 1 (tax computed on subtotal, not
taxable amount) is unaffected and re-confirmed cleanly with the correct discount scale: a genuine
100% discount (`discountPct: 100`) still produces `taxAmount: 10.00` instead of the expected
`0.00`.

---

## 2. It attributed its own verification work to testing that never happened — four times

The single most persistent error in the whole submission, and the one that took four passes to
clear. The model had verified findings itself with `curl` and Swagger during the session, then wrote
the test plan as though that verification had been part of the exploratory testing being documented.

**First instance** *(`PROMPTS-TASK-1-2-3.md`, Prompt 4)*

**What the model got wrong:** §5 of the test plan claimed the exploratory testing methodology was
"API-first," inferred from the fact that the confirmed-defect writeups all cite raw API responses.
That inference was wrong — those API responses came from the candidate driving the UI and reading
the underlying requests/responses via browser network inspection (UI-first), not from crafting API
calls directly. Reworded §5 to describe UI-first testing with network-tab inspection as the actual
exploratory method, and separated that from the API being the more efficient *execution* surface
for the formal scenario writeups and Task 3 automation — those are two different things and the
first draft conflated them.

**Second instance — after the first was supposedly fixed** *(Prompt 5)*

**What the model got wrong:** even after Prompt 4's rework, §5 still included a bullet claiming
the API was used as "the primary surface once a defect is identified" to pin down exact boundaries
(e.g. sweeping `discountPct` through 0/50/100) — implying the candidate had done direct,
independent API testing during Task 1 exploration. That conflated the model's own verification
work in this session (curl calls made to confirm the reported bugs before writing the plan) with
the candidate's actual Task 1 methodology, which was UI-only with network inspection. Removed the
bullet and reworded §5/§8 to state plainly that direct API testing is deferred to Task 3, not
something that happened here.

**Third instance, and the proactive sweep that followed** *(Prompt 6)*

**What the model got wrong:** §5 still stated that Swagger UI "was used alongside this to confirm
field names, types, and the request/response schema being observed" as part of the candidate's
Task 1 exploration — another instance of the model's own verification activity (Swagger/OpenAPI
was used by the model in this session to understand the schema before writing the plan) bleeding
into the description of what the candidate actually did. Removed the sentence.

After this correction, proactively re-scanned the rest of the document for the same pattern (the
model's own curl-based verification bleeding into descriptions of the candidate's Task 1
methodology) rather than waiting for a fourth correction. Found and fixed three more instances:
§3's `taxRate`/`discountPct` scale note ("confirmed directly against the API" → reworded to state
the fact without claiming a testing method), the risk table's "Confirmed via API" label for Bug 3
(→ "Confirmed", matching the other rows), and defects #3/#4's write-ups, which described raw
`GET /api/invoices?status=PAID` calls and a token-based `GET /api/invoices` check — both reworded
to describe UI actions (filtering the invoice list, logging in and acting on invoices) with the
API response read via network inspection, consistent with §5/§8's stated methodology.

**Fourth instance, found later in the scenario list** *(Prompt 19)*

**What the model got wrong:** confirmed a real, still-unfixed instance of the same misattribution
pattern caught earlier for defects #3/#4 and the original S10 — S11 and S12's exact status/error
codes (`422 INVOICE_NOT_EDITABLE`, `422 PAYMENT_NOT_ALLOWED`) came from the model's own curl
testing during this session, not from the candidate's UI-first methodology stated in §5, and there
was no confirmation the candidate had independently observed the same codes via the UI. Not yet
fixed in the files at this point — the candidate asked for verification tooling first (Prompt 20)
before deciding how to resolve it.

---

## 3. It wrote test steps for a UI control that does not exist

*(`PROMPTS-TASK-1-2-3.md`, Prompt 13)*

**What the model got wrong:** S1 step 3 instructed the tester to "set... a due date 30 days out"
on the New Invoice form — a UI element that doesn't exist. Opened the running UI
(`localhost:8081`) to check: the New Invoice form only has Owner, Tax Rate, Discount %, and Notes.
Created a real draft invoice, added a line item, and issued it to observe actual behavior: the due
date is set automatically by the backend to 30 days after the issued date (issued 8/20/2026 → due
9/19/2026 in the test run) and is not user-editable anywhere in the UI. Corrected S1's steps and
expected result to describe this auto-set behavior instead of a manual entry step. Also answered
the AUT question ("Application Under Test," a standard QA acronym) and, since it wasn't
defined anywhere and only appeared three times, replaced it with the unabbreviated "the
application's README" throughout `test-plan.md` and `scenarios-full.md` rather than defining the
acronym on first use.

---

## 4. A previously "confirmed" defect did not reproduce, and was retracted

The strongest single example of checking a claim against the running application instead of
inheriting it. A finding documented in Task 1 and carried into the README turned out not to hold.

*(`PROMPTS-TASK-1-2-3.md`, Prompt 42)*

That verification surfaced a real discrepancy with the plan: item 1 assumed
`Receptionist_Cannot_Void_Via_Api` would **fail** today, per the README's "new finding" that
reception's token could void an invoice (200, confirmed during Task 1). Re-tested it directly —
twice, on both a DRAFT and a freshly-issued invoice, with an admin void as a positive control (200,
succeeded normally) — and got a consistent `403 Forbidden` for reception in both cases. The
documented Task 1/README finding does not reproduce on the currently running app. Rather than
building a test around a claim that no longer holds, reclassified this as a regression guard
(expected to **pass**, confirming the API correctly rejects it) and will correct the
`task2-task3-automation/README.md` "Known issues" section that documented the 200 finding, flagging
the correction transparently rather than quietly dropping it.

---

## 5. Test code that compiled, ran green, and did not do what it claimed

*(`PROMPTS-TASK-1-2-3.md`, Prompt 24)*

**What the model got wrong, caught by actually running it (not just written, but executed and
observed to fail before being fixed):**

1. **The `[SetUpFixture]` health-check gate silently never ran.** First implementation put
   `AssemblySetup` in a `*.Setup` namespace, sibling to the `*.Tests` namespace containing the
   actual tests. NUnit only applies a `SetUpFixture`'s `OneTimeSetUp` to tests in the *same or a
   descendant* namespace — sibling namespaces don't count. Confirmed by deliberately pointing
   `API_BASE_URL` at a dead port and observing the test itself fail with a raw connection error
   instead of the intended friendly "PetClinic isn't running" message. Fixed by declaring the
   fixture in the global namespace (no `namespace` line), which applies assembly-wide regardless
   of the tests' own namespace — then re-verified the gate actually fires with the friendly
   message, and that the happy path still passes.

2. **PetClinic's backend rejects the CORS `Origin: host.docker.internal` header (403 Invalid CORS
   request) but accepts `Origin: http://localhost:8081`.** Not anticipated going in — discovered
   only by actually running the UI test inside the Docker container: the login page loaded fine
   (proving `host.docker.internal` networking itself worked), but clicking "Sign in" produced a
   "Could not reach the server" error from the app's own UI. Added temporary request/response
   logging to the test to see the real failing call, which showed a same-origin relative
   `/api/auth/login` request (not a hardcoded `localhost:8080` call, as first assumed) returning
   `403`. Confirmed the root cause directly with curl, varying only the `Origin` header against
   the real API (`Origin: http://host.docker.internal:8081` → 403 "Invalid CORS request",
   `Origin: http://localhost:8081` → 200) before writing any fix, rather than guessing. Fixed by
   keeping the browser's navigation target as `http://localhost:8081` and adding a Chromium
   `--host-resolver-rules=MAP localhost host.docker.internal` launch flag (only when a new
   `PLAYWRIGHT_RESOLVE_LOCALHOST_TO` env var is set, i.e. only inside Docker) so the browser
   genuinely believes it's on `localhost` — matching what a real user's local browser does — while
   its network layer still reaches the host machine. This split `UI_BASE_URL` (used by the plain
   .NET health check, which has no such CORS constraint and does need `host.docker.internal` in
   Docker) from a new `UI_BROWSER_URL` (always `localhost`, for the reason above) — documented in
   both `PetClinicPageTest.cs` and the README.

3. **`dotnet new sln` on the .NET 10 SDK generates a `.slnx` file, not the classic `.sln`.** The
   Dockerfile's `COPY PetClinic.Automation.sln` failed with "not found" on the very first build
   attempt. Caught immediately by the Docker build itself; fixed by correcting the filename.

All three were caught by executing the actual pipeline (local `dotnet test`, `docker compose
build`, `docker compose run` in all three modes, plus deliberately stopping PetClinic to test the
failure path and restarting it to confirm recovery) rather than by inspecting the code and
assuming it would work.

---

## 6. Three races, each caught by re-running rather than by one green run

*(`PROMPTS-TASK-1-2-3.md`, Prompt 36)*

**What the model got wrong, caught by actually running each test against the live app:**

1. **A login race.** `InvoiceLifecycleTests` timed out waiting for `invoice-create-button` — the
   test navigated to `/invoices` immediately after clicking "Sign in," racing the login POST and
   any resulting redirect. Fixed at the root: `LoginPage.LoginAsync` now waits for network idle
   before returning (a neutral wait, not an assertion — whether login *succeeded* is still left for
   the caller to check), and the success-path tests also explicitly assert `SignOutButton` is
   visible before proceeding, which doubles as a clear precondition check.
2. **A stale-DOM race in invoice creation.** After fixing (1), S1 failed differently: it created a
   draft, then landed on an already-ISSUED invoice instead — reading the topmost list row
   immediately after the modal closed sometimes returned the *previous* top invoice, before the
   list had refetched. Fixed by capturing the top invoice id before creating, then polling
   (up to 3s) until it actually changes, rather than trusting a single read right after the modal
   closes.
3. **The pagination test initially failed for the wrong reason twice.** First attempt: clicking
   `Next` 14 times in a tight loop with no wait between clicks left the test stuck on page 1 (the
   app's own page-state update lagging the click handler) — caught by the assertion "should have
   landed exactly on the last page" failing with `current: 1` instead of the expected total. Fixed
   `ClickNextAsync` to wait for the page indicator's number to actually change after each click.
   Second attempt: this fix alone passed in isolation but flaked once under the full 9-test suite's
   sustained load (3s retry budget too tight) — caught by re-running the full suite three times in a
   row rather than treating one green run as sufficient, doubled the retry budget to 6s, then
   re-verified stable across three more consecutive full-suite runs, plus once more inside Docker.

Final state: 9 UI tests, 6 pass, 3 fail by design (the three defect-reproduction tests, which assert
correct behavior and will pass once each underlying bug is fixed) — stable across five consecutive
full-suite runs (three local, two Docker) before being reported as done. Updated `README.md`: a new
"UI test coverage" section justifying what's covered and why (mirroring the "why did you test what
you tested" framing the assignment cares about), an explicit "expected to fail right now" callout so
the three red tests don't read as a broken pipeline, and the two race-condition lessons captured
under Known issues.

---

## 7. Performance figures the reports themselves contradicted

*(`task4-performance/PROMPTS-TASK-4.md`, Prompt 5)*

**What the review found that the reports had wrong:**

1. **Defect #2 was documented as deterministic when it is intermittent.** Re-running Test 2 four
   times produced PASS, PASS, FAIL, PASS — with the original run, 2 failures in 5. Both `SUMMARY.md`
   and `DEFECTS.md` presented it as a settled reproduction, so a reviewer running it once had a ~60%
   chance of seeing PASS and doubting the whole report.

2. **The sequential control was asserted but never run.** `DEFECTS.md` claimed the defect doesn't
   reproduce "under a single sequential request" — but the control that actually matters for this
   defect is ten *sequential* payments, not one. If ten sequential $10 payments also failed to reach
   PAID, this would be an ordinary logic bug, not a concurrency defect, and the entire link to Task
   1's Defect #3 would collapse. Ran it directly against the API: payments 1–9 return
   `PARTIALLY_PAID`, payment 10 transitions to `PAID`, ending at balance 0.00 / status PAID. The
   concurrency framing holds — and is now backed by evidence rather than assertion. Added to
   `DEFECTS.md` as its own section.

3. **Think time is measured inside the scenario step**, so Tests 1/3/4's percentiles (~1014–1072ms)
   are ~1000ms of `Task.Delay` plus ~15–70ms of actual server time. The brief explicitly asks for
   p90/p95 latency, and those numbers don't describe the application. Verified `Step.Run` exists in
   NBomber 6.6.0 via reflection over the shipped assembly, so the fix is available; recorded as the
   top follow-up rather than implemented in this pass, since it changes what every report's latency
   column means and warranted its own decision.

4. **Test 4's achieved read/write split was 58/42, not the stated 50/50** — it's a per-iteration coin
   flip, not an enforced ratio.

5. **The top-level README still said Task 4 was "not attempted"** in three places, and the root
   `PROMPTS.md` never referenced this file — meaning the AI-disclosure trail had a gap for anyone
   reading only the root.

Findings 1, 2, 4 and 5 were reported back with the recommendation to fix them; 3 was raised as a
decision to make rather than something to change unprompted.

---

## 8. A result written up confidently, then found to be a measurement artifact

*(`task4-performance/PROMPTS-TASK-4.md`, Prompt 6)*

**A side effect noted at the time — and later found to be a misreading.** Test 2's reported latency
went from 30–44ms to 156–486ms after the barrier was added, and this was written up as the barrier
exposing real queuing contention that the staggered version had hidden. That was wrong. The barrier
wait and warm-up GET were inside the measured scenario function, so the extra time was virtual users
sitting at the barrier, not the server working. Prompt 7's step refactor isolated the payment call
and put real latency back at ~30–45ms — almost exactly the original figure. Corrected in `SUMMARY.md`
rather than left standing.

---

## 9. The same failure again, found in a final audit

Late in the work, a cross-document review checked every numeric claim in Task 4's documentation
against the committed NBomber reports. It found the pattern from §7 had recurred: figures stated
with more confidence than the underlying runs supported.

*(`PROMPTS-TASK-1-2-3.md`, Prompt 57 — abridged to the two findings; the full entry lists six)*

- **`SUMMARY.md` stated failure-rate ranges its own reports contradict.** Test 1 was documented as
  "10.3% to 13.1% across runs", but `test1-20260825-153517` is 4 failures of 148 = **2.7%**, well
  below the stated floor. `DEFECTS.md` gave Test 4 as 5.6–8.3% while `SUMMARY.md`'s own headline
  table showed 9.2%. Both corrected to the true observed ranges (2.7–13.1% and 5.6–9.2%).
- **The pool-saturation conclusion rested on one run of three, presented as reproducible.** Test 5 at
  200 req/s: `hikaricp.connections.pending` reached 21 in run `153154` (p95 161ms) but stayed at
  **0** in runs `153636` and `155531` (p95 62ms and 47ms). The document already disclosed run-to-run
  variance for Tests 1 and 4 but not here, where it was largest. This was the most substantive
  finding — the "connection pool saturates first" conclusion was over-claimed on a single
  observation.

The raw runs behind every figure in Task 4 are now committed under
[`task4-performance/reports-cited/`](task4-performance/reports-cited/), so these claims can be
checked rather than taken on trust.

---

## What this pattern says

Three of the nine entries above are the same failure: the model stating something with more
confidence than its evidence supported, and being believed until someone re-ran the thing. It was
not wrong about the application often — the nine defects it helped find are all real and all
reproduce. It was wrong about **how firmly things were known**: a boundary it had not tested, a
control it had not looked at, a methodology it had not performed, a latency it had not isolated, a
range it had not re-measured.

The check that caught every one of them was the same: go back to the running application and do it
again. Reading the model's output more carefully would not have caught a single one.
