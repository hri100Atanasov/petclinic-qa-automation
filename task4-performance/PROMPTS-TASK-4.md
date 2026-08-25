# Prompts — Task 4 (Performance testing)

This file logs the AI-assisted portion of Task 4 specifically, kept separate from the top-level
[`PROMPTS-TASK-1-2-3.md`](../PROMPTS-TASK-1-2-3.md) because Task 4 was run as its own self-contained
unit rather than folded into the same log as Tasks 1-3. Same rules as the top-level file: prompts pasted verbatim, in order, with what the
model produced, what was kept vs. corrected, and what the model got wrong caught against the running
application.

**Ordering:** the prompts below are numbered 1-10 within Task 4, and chronologically they follow
Prompt 56 in the top-level [`PROMPTS-TASK-1-2-3.md`](../PROMPTS-TASK-1-2-3.md). The two files
together cover the whole submission. Both files were named `PROMPTS.md` for most of the work and
renamed at the end; entries below referring to "`PROMPTS.md`" mean whichever file that was at the
time, left as written rather than retroactively edited.

**A naming note.** Task 4's two defects were called "Performance Defect #1/#2" while this work was
in progress, and the entries below still say that — it's a chronological log, so retroactively
editing them would falsify the record. They were renumbered **#8** and **#9** in the final review
pass (`PROMPTS-TASK-1-2-3.md`, Prompt 57) to put every defect in the submission on one sequence.
Read "#1" as "#8" and "#2" as "#9" below.

## Tools / models used

- **Tool:** Claude Code (CLI agent)
- **Model:** Claude Sonnet 5 (`claude-sonnet-5`)

---

## Prompt 1 — Proposal and opinion request

```
I want to attempt Task 4. Consider yourself as a Performance tester. For now don't update any related README files.
Below is what I propose, feel free to suggest or challenge anything.

After a research it appears that NBomber would be e good fit for our tech stack, since it is a .net library. It should provide html report out of the box and no need for external sinks storing timeseries data.

Start with a single simple test - create a thousand invoices, just as a POC.

For this to be meaningful, I believe we have to deploy the AUT with specific CPU and RAM values. I believe best approach for introducing resource cap for the AUT is through a Docker Compose override file shipped with our repo. As to the resources values for the separate containers lets start with something minimal of your choosing.

Eventually the solution should cover the criteria's of Task 4 in the assignment:
Deliver your test definition in a runnable form, plus a short written summary. We expect to see:

* A stated load model — user count, ramp-up, duration, think time, and the read/write mix — with your reasoning.
* Parameterised requests rather than a single hardcoded value replayed thousands of times.
* Response assertions, not just status-code counting.
* p90/p95 latency and error rate, not averages alone.
* Any conclusion you can defend about where the application slows down first.

Think about what your local setup means for the validity of your numbers, and say so in the summary.

Provide your opinion before any implementation.
```

Responded with an opinion, no code written yet, as asked. Agreed with NBomber (keeps the whole
submission in one C#/.NET stack) but flagged one unverified assumption up front rather than taking
it on faith: NBomber's documented default report percentiles are p50/p75/p95/p99, not p90 — worth
confirming before committing, with k6 named as the fallback if that turned out to be real friction.
Endorsed the POC framing (wiring proof, not signal-gathering) but pushed back on two things:
parameterize even the POC (a small owner pool, not one hardcoded owner) since replaying one fixed
body is exactly the anti-pattern the brief names; and assert on response *content* from the start,
not just HTTP status. On the resource cap: agreed with the Compose-override approach, proposed it
live in this repo (never editing the AUT's own compose file), flagged that `deploy.resources.limits`
is historically Swarm-only and needed live verification under plain `docker compose up` rather than
assumed, and proposed capping the API container only at first (not API+DB together) so a later
"where does it slow down" conclusion isolates one variable at a time. Proposed the load generator run
on the host, not in Docker, so it doesn't compete with the capped AUT for the same Docker
Desktop/WSL2 resource pool.

---

## Prompt 2 — Actuator metrics confirmed

```
Even if NBomber lacks p90, it still remains the tool of choice. Will provide the p95 and may be one more of the ones it provides.

I agree with your proposals.

I checked and indeed the actuator returns metrics actuator/metrics
"names": [ ... ~90 metric names ... ]
```

Reacted to the metric name list: flagged HikariCP's pool metrics (`hikaricp.connections.active` /
`.pending` / `.max`) as the standout, since HikariCP's default pool size (10) is a plausible
bottleneck completely independent of any Docker CPU/RAM cap — reframed the working hypothesis from
"the Docker cap will be the first thing to bite" to "there may be at least two independent
bottleneck candidates, and conflating them would produce an indefensible conclusion." Proposed
polling a focused subset (not all ~90 names) on a 1-2s interval into a CSV during each run, since
`/actuator/metrics/{name}` is a point-in-time snapshot with no built-in history — the cheapest way
to get a real time series without standing up Prometheus/Grafana, matching the original "no external
sinks" constraint. Proposed running the POC once with no Docker cap at all first, specifically to see
whether the connection-pool ceiling alone produces a failure signal before layering the Docker cap on
top as a second, separate experiment.

---

## Prompt 3 — Move to implementation

```
Move to implementation with your suggestions
```

Built the POC end to end and ran it for real rather than just writing code:

- Verified NBomber 6.6.0 targets/builds clean on .NET 10 (no compatibility issue).
- Verified NBomber's percentile report is fixed at p50/p75/p95/p99 (confirmed via the official docs,
  no custom-percentile API exists) — matches the candidate's Prompt 2 decision to accept p95 (+p99).
- Verified live, via `docker inspect`/`docker stats`, that `deploy.resources.limits` in a plain
  override file **is** honored under `docker compose up` (Compose v5.4.0 / Engine 29.7.2,
  non-Swarm) — resolved the flagged unknown rather than assuming it.
- Verified live that an owner needs no pet to be usable for invoice creation, and that actuator
  metrics endpoints require the admin bearer token (401 without it) — both confirmed by curl before
  writing the poller.
- Ran the POC twice: once uncapped, once with a 1 CPU/1GiB cap on the `api` container only. Both
  runs failed heavily — not from the cap, but from a genuine, previously-unknown concurrency defect
  (`duplicate key value violates unique constraint "invoices_invoice_no_key"`), reproducible even
  uncapped at 50 req/s. The cap didn't cause it, but made both the failure rate (49.8% → 81.6%) and
  p95 latency (3.16s → 14.88s) dramatically worse, with CPU pinned at ~100% for nearly the whole
  capped run. Reverted the API container back to uncapped afterward so the shared environment was
  left as found.

**What the model got wrong, caught by actually running it:** `ReportFormat` initially failed to
compile — it lives in `NBomber.Contracts.Stats`, not the `NBomber.CSharp` namespace the rest of the
fluent API is in; a one-line `using` fix, caught immediately by the compiler. Nothing else needed
correcting in this pass; the two things flagged as unverified in Prompts 1/2 (p90 availability,
whether the Compose override actually constrains anything) both got resolved by direct verification
before being relied on, not by assumption.

Reported the finding back and asked two open questions before going further: whether to document the
race condition as a new numbered defect in `test-plan.md`, and whether to commit/push. Neither
answered yet at this point — deferred to Prompt 4.

---

## Prompt 4 — Isolate Task 4, seed receptionist users, define four tests

```
Let us do this task in isolation from the rest.
Create a Prompt file where the prompts will be recorded from now on and start from the initial one attempting this task.
Add a Defect report where potential defects discovered with performance testing will be reported and add the one already found, provide short explanations on the defects/failures.

Prior to executing the tests seed 10 users with receptionist role and use them for the tests.

Add a summary Task 4 expects.

I propose the following 4 tests.

1. The initial used as a POC. Can you rework it by Introducing ramp-up, add a user every second until all 10 users are added. Make the duration of the run 20 seconds. Provide 1 second think time.
2. Concurrent payments against the same invoice. No ramp-up or think time here. Execute 10 partial payments against the same invoice. Make a 100$ invoice and execute 10 payments of the amount of 10$.
3. A read-heavy load on GET /api/invoices. Introduce ramp-up, add a user every second until all 10 users are added. Make the duration of the run 20 seconds. Provide 1 second think time
4. A mixed read/write test. Follow the same scenario parameters as the previous test (#3). Make a scenario with creation of invoices, obtaining invoices list in 50/50.

Add any suggestions, concerns.
```

This answers the "in isolation" instruction by giving Task 4 its own `PROMPTS.md` (this file),
`DEFECTS.md`, and `SUMMARY.md` under `task4-performance/`, rather than touching `task1-test-plan/`
or the top-level `README.md`/`PROMPTS.md` at all.

Before writing any scenario code, verified two things live rather than assuming: that `POST
/api/users` exists and accepts `{username, password, fullName, email, role}` with `role` one of
`ADMIN|VET|RECEPTIONIST|READONLY` (found via the OpenAPI doc, then confirmed end-to-end by creating
one test account and logging in as it); and that NBomber has a purpose-built mechanism for "pin
virtual user N to a specific pre-authenticated session for its whole run" —
`NBomber.ClientPool<T>` combined with `context.ScenarioInfo` (`GetClient` uses modulo internally, so
it degrades gracefully during ramp-up when fewer than 10 users are active yet). This is a more
literal implementation of "add a user every second" than randomly picking a receptionist account per
request would have been, and was found by reading the installed package's own XML docs rather than
guessing at an API shape.

Interpreted "add a user every second until all 10 users are added" + "make the duration of the run
20 seconds" as two-phase: `Simulation.RampingConstant(copies: 10, during: 10s)` (reaches 10 users at
exactly the 1/sec rate specified) followed immediately by `Simulation.KeepConstant(copies: 10, during:
10s)` (holds at 10 for the remaining 10s), totaling 20s — stated as an explicit interpretation in
`SUMMARY.md` rather than a silent assumption, since the prompt doesn't unambiguously say whether the
20s includes or excludes the ramp.

For Test 2 ("no ramp-up or think time... execute 10 partial payments"), used
`Simulation.IterationsForConstant(copies: 10, iterations: 10)` — a closed-model simulation that
launches all 10 virtual users together (not gradually) and gives each exactly one iteration, which is
what "concurrent" and "no ramp-up" both call for; confirmed the concurrency was real, not just
declared, by checking the AUT's own logs and the metrics poller's CPU/HikariCP readings during the run
rather than assuming NBomber's scheduling behaved as documented.

For Test 4 ("mixed read/write... 50/50"), NBomber has a first-class weighted-multi-scenario feature
(`Scenario.WithWeight`) that would report read and write latency as two separate distributions
instead of one blended one. Deliberately did **not** use it: its exact concurrency semantics when two
`RampingConstant`/`KeepConstant` simulations are registered side by side under one weight split
weren't verified live, and shipping an assumption about a tool's behavior without checking it is
exactly the failure mode this whole project has repeatedly caught and corrected (Prompts 1-3 above
included). Used a single scenario with an internal 50/50 branch instead — fully within what was
already confirmed working for Tests 1 and 3 — and documented the tradeoff (combined, not separated,
read/write percentiles) directly in the scenario's own code comment and in `SUMMARY.md`, rather than
silently picking the simpler option without saying why.

Ran all four tests against the live, uncapped AUT after building them. Test 2's post-run integrity
check (a direct `GET` of the invoice after all 10 payments complete, not part of the timed/percentile
stats) failed: all 10 payments were correctly recorded (10/10 rows, `amountPaid` 100.00, `balance`
0.00) but `status` stayed `PARTIALLY_PAID` instead of auto-transitioning to `PAID`. Checked the AUT's
own logs for the affected invoice and found nothing — no exception, no warning — confirming this is a
silent data-consistency defect, not a loud one, only catchable by checking final state rather than any
individual response or the app's own logging. Test 1 (10.3% failure) and Test 4 (5 of 60 writes
failed, 0 of 84 reads) both reproduced the same invoice-number race condition found during the Prompt
3 POC, now confirmed at a realistic 10-user/1s-think-time load rather than only under the POC's
aggressive 50 req/s burst. Test 3 (pure reads) had zero failures across 142 requests. Full numbers and
analysis are in `SUMMARY.md`; both defects are written up in `DEFECTS.md`.

**What the model got wrong, caught by actually running it:** `NodeStats.AllOkCount`/`AllRequestCount`
(used to set the process exit code after each run) compiled but produced an obsolete-API build
warning — switched to `stats.ScenarioStats.Any(s => s.Fail.Request.Count > 0)` instead, caught by
reading build output rather than ignoring a warning on an otherwise-green build.

---

## Prompt 5 — Review the results and reports

```
Can you go through the results of the tests, check the summary and the reports and advise if something should be improved
```

**Every reported figure was independently verified** against the raw NBomber reports
and metrics CSVs before anything was changed: Test 1's 145/15/10.3% and its percentiles, Test 4's
84 read / 60 write split and 8.3% write failure rate, Test 1's 12.4% peak CPU, Test 4's 7.3%, and
HikariCP pending sitting at 0 throughout both. All of them were accurate as written.

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

## Prompt 6 — Make Defect #2 reproduce reliably, refine the report tone

```
Regarding defect #2 make it reproduce more often.
Current version of the summary is written as some document explaining chronologically the development of the process, can you refine the tone, exclude unnecessary things.
Fix the rest as intended.
```

**Making Defect #2 reproduce reliably.** Rather than guessing at parameters, wrote a standalone probe
(scratch, not committed) that ran 8 trials each of three shapes — 10×$10, 20×$5, 50×$2 — with all
requests held on a semaphore and released together. Results: 8/8, 8/8, 7/8. Two things came out of
that: simultaneity, not volume, is what matters (50×$2 was *worse*, most likely because HikariCP's
10-connection ceiling serializes some requests and lets later ones observe committed state), and the
existing 10×$10 shape was already optimal — so the test's semantics did not need to change.

Ported the barrier into `Test2ConcurrentPayments.cs` (all users block on a `TaskCompletionSource`
until the last arrives, with a 10s timeout so a scheduling change can't hang the run). That alone
took reproduction from 2/5 to 4/5. The residual gap versus the probe's 8/8 was per-user connection
setup — each virtual user has its own `HttpClient`, so the first request after release pays TCP
handshake cost and re-introduces the stagger the barrier removes. Adding a warm-up GET before the
barrier brought it to **9 of 10 measured runs**.

Kept honest about the ceiling: the first draft comment claimed "10 of 10" from the probe's numbers
before the NBomber runs were in; corrected to the measured 9/10 once they were. It remains a race, so
~1 run in 10 still passes — `SUMMARY.md`, `DEFECTS.md`, the code comment and the root README all now
state that a single PASS is not evidence the defect is fixed.

**A side effect noted at the time — and later found to be a misreading.** Test 2's reported latency
went from 30–44ms to 156–486ms after the barrier was added, and this was written up as the barrier
exposing real queuing contention that the staggered version had hidden. That was wrong. The barrier
wait and warm-up GET were inside the measured scenario function, so the extra time was virtual users
sitting at the barrier, not the server working. Prompt 7's step refactor isolated the payment call
and put real latency back at ~30–45ms — almost exactly the original figure. Corrected in `SUMMARY.md`
rather than left standing.

**Report tone.** Rewrote `SUMMARY.md` as a report rather than a chronological account of how the work
unfolded: dropped the "interpretation flagged explicitly" aside, the `PROMPTS.md` cross-references,
the "suggestions/concerns carried into this work" framing, and the "at the candidate's request"
asides in `DEFECTS.md`. Same content and same conclusions, stated directly.

**One more thing the re-runs exposed:** a full `all` run produced Test 1 at 13.1% (not 10.3%) and
Test 4 at 5.6% (not 8.3%) — the error rates move run to run because they're race-driven. The original
figures weren't wrong, but presenting them as single definitive values was. Both documents now cite
one coherent run explicitly and give the observed range across runs, and the metrics figures were
re-derived from that same run's CSVs (peak CPU 16.1% / 5.2%) so the numbers and their source agree.

---

## Prompt 7 — Approve the `Step.Run` refactor

```
yes to your recommendation for the Step.Run refactor
```

Wrapped each scenario's HTTP call in a named `Step.Run(...)`, leaving the think time (and, in Test 2,
the barrier wait and connection warm-up) outside the measured region. Verified against the resulting
reports rather than assumed: NBomber derives scenario-level latency from its steps, so the think time
is now excluded from the percentiles entirely while still pacing the run and showing up in RPS — the
original assumption that scenario stats would *retain* think time was wrong, and the code comment
saying so was corrected once the reports showed otherwise.

The effect on the numbers is large, because the previous figures were mostly `Task.Delay`:

| | Before (cycle time) | After (server time) |
|---|---|---|
| Test 1 p50 / p95 | 1017ms / 1033ms | 9ms / 19ms |
| Test 3 p50 / p95 | 1032ms / 1064ms | 19ms / 34ms |
| Test 4 p50 / p95 | 1018ms / 1037ms | 9ms / 30ms |
| Test 2 p50 / p95 | 156ms / 486ms | 30ms / 45ms |

Test 2's correction is the important one: its inflated figures were barrier wait, not contention (see
Prompt 6). With the wait outside the step, payment latency is ~30–45ms — essentially unchanged from
the pre-barrier measurement, which is the expected result and confirms the barrier only changed *when*
requests fire, not how long the server takes.

Used the same mechanism to resolve the other open limitation: Test 4 now runs two separately named
steps (`write_create_invoice`, `read_invoice_list`) instead of one shared code path, so read and write
latency report as distinct distributions. This avoids `Scenario.WithWeight` entirely — the feature
whose concurrency semantics were deliberately not assumed back in Prompt 4 — while delivering what it
would have been used for. Only one step executes per iteration; the other simply records no data point
that iteration, which NBomber handles without complaint.

That separation immediately showed something the blended figure had obscured: **reads run at roughly
twice the latency of writes** (p50 18ms vs 9ms in the same run), consistent with a paginated, filtered,
counted list query costing more than a single-row insert. Test 3's independent read figures line up
closely with Test 4's read step, which is a useful cross-check that both are measuring the same thing.

Re-derived the metrics figures from this run's CSVs too (peak CPU 5.0% for Test 1, 2.4% for Test 4,
HikariCP pending still 0 throughout) so every number in `SUMMARY.md` traces to the same session, and
strengthened the "breaks before it strains" conclusion now that genuine server latency is available:
p99 stays under 30ms while roughly one write in ten fails. Removed the two follow-up items this work
closed, leaving Test 2's residual ~1-in-10 pass rate as the main remaining caveat.

---

## Prompt 8 — Assess Task 4 against the brief, then apply the recommendations

```
Propose how to improve it based on your findings
```
```
Apply your recommendations
```

(Preceded by an off-the-record request to assess the project against Task 4's stated requirements.)

**Assessment.** Checked each requirement against the documents rather than from memory. Parameterisation,
response assertions and the local-setup-validity discussion were comfortably above bar. Four gaps:

1. **"Where does it slow down first" was never actually answered.** The four tests run at ~7 req/s with
   peak CPU at 2–5% and zero connection queuing. The conclusion "it breaks on correctness before it
   strains" is true and well-evidenced, but it is an answer about correctness, not capacity — the
   application was never loaded hard enough to find a limit. Worse, it *couldn't* be: a closed model
   with 1s think time caps throughput at users/think-time (~10 req/s), so no amount of running those
   tests longer would ever reach saturation.
2. **The read/write mix had no stated reasoning**, despite the brief naming it explicitly among the
   things to justify.
3. **p90 was never mentioned.** NBomber has no p90 and no custom-percentile API — a decision taken back
   in Prompt 2 but recorded only in this log, so a reviewer checking the box would see an unexplained
   omission.
4. **NBomber itself was never justified in the summary**, and the summary didn't state its own .NET SDK
   prerequisite despite Task 4 being framed as self-contained.

**Test 5 — read ramp-to-failure.** Added an open-model scenario (`Simulation.RampingInject`, 0 → 400
req/s over 60s, no think time) because only an imposed arrival rate can find a capacity limit. Targeted
reads deliberately: `GET /api/invoices` is the only endpoint here with a 0% error rate, so ramping it
isolates saturation, whereas ramping writes would have produced an error curve dominated by the
invoice-number race rather than by capacity.

**The first version overshot, and its numbers were discarded rather than published.** Ramping to 400
req/s produced 11,800 requests, 24.7% errors and p95 of 4.2s — superficially a dramatic result. Two
things about it didn't hold up. The failures were client-side transport exceptions (`An error
occurred while sending the request`), not HTTP error responses from the application; and afterwards
the app appeared unreachable from the host entirely.

Investigated rather than assuming the load test had broken the application. `docker compose ps`
reported the API healthy, its logs showed no errors, and `wget` **from inside the Docker network**
returned `{"status":"UP"}` normally — while `curl` from the host timed out. Checking the host's TCP
state ruled out the obvious explanation (only 6 connections in TIME_WAIT, ephemeral ports nowhere
near exhausted). The actual cause: the ports were bound on IPv6 via a wedged Docker `wslrelay`
process, so `localhost` (which resolves to `::1` first on Windows) hung while `127.0.0.1` returned
200 instantly. The application had been fine the entire time, with all 474 invoices intact.

So the 400 req/s run was measuring the load generator's own host, not the server. Reduced the ramp to
200 req/s — enough to cross the connection-pool knee with headroom — and re-ran cleanly: **5,900
requests, 0 errors, 98 req/s sustained, p95 161ms**, with `hikaricp.connections.active` reaching the
pool maximum of 10 and `pending` climbing to 21, at a peak CPU of **49.4%**.

That is the publishable result, and it still answers the question: the connection pool saturates
first, with roughly half the CPU idle. It confirms the hypothesis formed back in Prompt 2, before any
load test existed, that HikariCP's default pool of 10 was a plausible bottleneck independent of any
Docker resource cap — and it identifies a config value
(`spring.datasource.hikari.maximum-pool-size`), not a hardware limit, as the thing to change.

Recorded the overshoot itself in `SUMMARY.md` as a methodological note rather than deleting it: it
explains why the ramp target is 200 and not higher, and it is a real limitation of single-host load
generation worth stating. Also lowered `Settings.RampToRps` with a comment explaining that raising it
materially risks measuring the harness instead of the server, and added a troubleshooting line about
the Windows IPv6/`localhost` trap.

**Surfaced evidence that already existed but was buried.** The two POC runs' metrics CSVs showed
`hikaricp_pending` peaking at **81** (uncapped) and **167** (capped) with active pinned at 10/10 in
both — the same pool-exhaustion signature, never referenced in any document. Promoted into the
conclusion, so the finding now rests on three independent runs across two different workloads rather
than on Test 5 alone.

**Reframed the conclusion into two answers at two load levels** rather than one — though the specific
ratio stated here was itself wrong, and was corrected in Prompt 9 below after being challenged:
correctness fails at
~7 req/s, capacity fails at ~150 req/s, so the application breaks on correctness roughly twenty times
sooner than it breaks on capacity. That ordering is the actually useful conclusion, and it only became
statable once both regimes had been measured.

Closed the three documentation gaps as well: the 50/50 mix is now explained as a deliberate
simplification chosen for comparability with the pure-read and pure-write tests rather than as a
traffic model; the p95/p99-in-place-of-p90 substitution is stated with its reason; NBomber's selection
and its percentile limitation are justified up front; and the .NET SDK prerequisite is listed. Updated
the root README's run commands and expectations for Test 5, and replaced the "fifth test worth
building" follow-up (now built) with two new ones — separating the load generator onto its own hardware
to get a clean server-side error rate, and raising the Hikari pool size and re-running Test 5 to turn
the inferred cause into a demonstrated one.

---

## Prompt 9 — Challenge: is the conclusion machine-dependent?

```
is this based on my machine

Conclusion restructured into two answers: correctness fails at ~7 req/s, pool saturation begins ~100 req/s — the app breaks on correctness roughly fourteen times sooner than it runs short of capacity.
```

A fair challenge, and checking it properly showed the framing was wrong in a way that mattered — in
both halves of the comparison.

**The "~7 req/s" number was never a threshold.** It was simply the rate Test 1 happened to run at
(10 users × 1s think time ≈ 7 req/s). The invoice-number race isn't triggered by throughput at all;
it's triggered by overlap. Rather than reason about that, measured it: fired N barrier-released
invoice creations, 12 trials each.

| Concurrency | Trials hitting the race | Requests failed |
|---|---|---|
| **2** | **12 of 12** | 12 of 24 |
| 3 | 12 of 12 | 24 of 36 |
| 5 | 12 of 12 | 42 of 60 |

At two simultaneous requests — the minimum concurrency a system can have — exactly one of the two
fails, every single time. So the correct statement is not "fails at 7 req/s" but "fails whenever two
requests overlap," which is both machine-independent and considerably more damning: two receptionists
clicking at the same moment is enough, on any hardware.

**The "~100 req/s" number genuinely is machine-dependent**, and the summary now says so explicitly.
What saturates a pool of 10 is how long each query holds a connection, which depends on CPU and disk
speed, on the size of the dataset being queried (this database grew from ~16 to ~474 invoices during
this work, and the list endpoint paginates and counts over it), and on the load generator sharing the
same machine. Faster hardware pushes the number up; a larger table pulls it down.

**So the "fourteen times sooner" ratio was meaningless** — an arbitrary numerator from the chosen load
model divided by an environment-specific denominator. Removed it. Replaced with the comparison that
actually holds: the correctness defect needs two concurrent requests, pool saturation needs sustained
traffic orders of magnitude higher, and unlike the pool limit the correctness defect cannot be
configured or provisioned away.

Also strengthened the validity section: Defect #1 has now reproduced at four unrelated concurrency
levels (2 concurrent, ~7 req/s paced, 50 req/s capped and uncapped), and a race that fires reliably on
two concurrent requests is a property of the code, not of the hardware it ran on. That is a much
firmer claim than the one it replaces, and it came from the challenge rather than from the original
analysis.

---

## Prompt 10 — Target writes instead of reads

```
What if we targe the creation of draft invoices/write operations instead of read? Wont this show flaws in the application architecture instead of unknown reason like socket depletion?
```

Correct, and a better experiment than the one built in Prompt 8. The original reasoning for choosing
reads — that ramping writes would conflate saturation with the invoice-number race — treated the
race as noise to be excluded. It is the opposite: on this application it is the signal.

Two advantages, the second of which had been underweighted:

- **Writes fail self-attributingly.** A failed invoice creation returns HTTP 500 carrying
  `duplicate key value violates unique constraint "invoices_invoice_no_key"` — unambiguously the
  application rejecting the request. Test 5's high-rate failures were client-side transport
  exceptions, which is exactly why proving they were host artifacts rather than server behaviour took
  a separate investigation.
- **The whole curve fits under the host's limits.** Because the write path collides at a concurrency
  of 2, it can be characterised at rates far too low to stress the load generator's own machine, so
  the ambiguity that contaminated the 400 req/s read ramp cannot arise.

Built Test 6 as a fixed-rate write injection (`Simulation.Inject`, 20s, rate from `WRITE_RATE_RPS`)
rather than a smooth ramp — a ramp blends every rate into one aggregate, whereas discrete runs give a
clean point per rate. Ran it at 2, 5, 10, 20 and 40 req/s:

| Offered | Succeeded | Error rate | Successful throughput | p95 |
|---|---|---|---|---|
| 2/s | 40 of 40 | 0% | 2.0/s | 12ms |
| 5/s | 98 of 100 | 2% | 4.9/s | 13ms |
| 10/s | 190 of 200 | 5% | 9.5/s | 13ms |
| 20/s | 332 of 400 | 17% | 16.6/s | 13ms |
| 40/s | 539 of 800 | **33%** | 27.0/s | 14ms |

The result is a stronger finding than anything the read path produced. **p95 latency does not move**
— 12 to 14ms across a twentyfold increase in load — so by every conventional performance signal the
application looks perfectly healthy. Yet successful throughput diverges further from offered load at
every step, losing 13 writes per second at 40/s offered. The application does not slow down under
write load; it discards work while staying fast.

That reframes the answer to "where does it slow down first" considerably: on the write path it never
slows down, it just fails, and the ceiling is a non-concurrency-safe invoice-number allocation rather
than any resource. Hardware cannot move that curve.

Kept Test 5 rather than replacing it — the two now answer different questions, and the contrast is
itself informative: reads scale cleanly to ~100 req/s and are limited by a configurable connection
pool, while writes are limited by application design at a fraction of that rate. Updated the load
model table, results, conclusions and both READMEs accordingly, and replaced the "sixth test worth
building" follow-up (now built) with extending Test 6's curve to find where successful write
throughput plateaus outright.
