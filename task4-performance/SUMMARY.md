# Task 4 — Performance test report

**Target:** PetClinic Pro's Billing module (`/api/invoices/**`) — same scope rationale as Task 1:
the highest financial and compliance risk in the application. Tests run against the API directly;
Task 2 already covers UI behavior.

**Tool:** [NBomber](https://nbomber.com) 6.6.0 (.NET) — chosen to keep the whole submission in one
language and toolchain (Tasks 2/3 are already C#/NUnit, so the same skills maintain all of it), and
because it emits HTML/CSV/Markdown reports directly with no external time-series backend to stand up.
Its one limitation for this brief is a fixed percentile set (p50/p75/p95/p99, no p90 and no
custom-percentile API) — accepted deliberately, since p95 is what the brief primarily asks for.

**Outcome:** two concurrency-correctness defects that need essentially no load at all — the
invoice-number race fires on **two simultaneous requests**, every time. Under sustained write load the
application never slows down; it discards work instead, losing 33% of invoice creations at 40/s while
p95 latency stays flat at 14ms. Separately, read traffic drives the database connection pool to its
configured maximum while CPU sits below 50%. The correctness defects are the more urgent finding: they are logic races that
hold on any hardware, whereas the capacity constraint is environment-specific and configurable. Full
write-ups in [`DEFECTS.md`](DEFECTS.md).

## Running the tests

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) on the host, and PetClinic Pro
running (`qa-test-automation-task`, `docker compose up`). Deliberately not containerized, so the load
generator doesn't compete with the application for the same Docker resource pool.

```bash
cd task4-performance/PetClinic.PerformanceTests
dotnet run -c Release -- {test1|test2|test3|test4|test5|test6|all}

# Test 6 takes a rate, so the same test builds a scalability curve:
WRITE_RATE_RPS=40 dotnet run -c Release -- test6
```

The run fails fast with instructions if the application isn't reachable. Each test writes an
HTML/CSV/Markdown report plus a metrics CSV to `PetClinic.PerformanceTests/reports/`, timestamped per
run. That directory is a working directory and is gitignored — the specific runs cited in this
document are committed under [`reports-cited/`](reports-cited/) so every number below can be checked
against its source.

**Reproducing the capped-resource runs.** The "API capped to 1 CPU / 1 GiB" figures cited later come
from layering [`docker-compose.resource-limits.yml`](docker-compose.resource-limits.yml) over the
AUT's own compose file — it caps only the `api` container, deliberately, so one variable moves at a
time. Nothing in the default run path uses it:

```bash
cd task4-performance
docker compose -f ../../qa-test-automation-task/docker-compose.yml -f docker-compose.resource-limits.yml up -d
```

## Load model

Tests 1–4 use a **closed model** — a fixed number of concurrent virtual users, not an open
arrival rate. The population being modelled is "N front-desk staff using the system at once," which
is naturally capped, not an unbounded stream of visitors. 10 concurrent users is a generous ceiling
for a small veterinary practice: the application's own seed data ships 2 receptionist accounts.

| | Test 1 (create) | Test 2 (concurrent payments) | Test 3 (read) | Test 4 (mixed) | Test 5 (read ramp) | Test 6 (write scalability) |
|---|---|---|---|---|---|---|
| Model | closed | closed | closed | closed | **open** | **open** |
| Users | 10 | 10 | 10 | 10 | n/a — rate-driven | n/a — rate-driven |
| Ramp-up | 1/sec to 10 over 10s | none — all 10 released together | 1/sec to 10 over 10s | 1/sec to 10 over 10s | 0 → 200 req/s over 60s | none — fixed rate |
| Duration | 20s (10s ramp + 10s hold) | ~1s (10 iterations total) | 20s (10s ramp + 10s hold) | 20s (10s ramp + 10s hold) | 60s | 20s per rate |
| Think time | 1s | none | 1s | 1s | none | none |
| Read/write mix | 100% write | 100% write | 100% read | 50/50 | 100% read | 100% write |

Tests 1, 3 and 4 model realistic usage: staff arrive gradually, pause between actions, and the run
holds steady state for 10s after ramping rather than capturing only a burst.

**Test 4's 50/50 mix is a deliberate simplification, not a traffic model.** Real front-desk usage is
read-heavy — staff look at the worklist far more often than they create an invoice. An even split was
chosen instead so the mixed test stays directly comparable to the pure-write (Test 1) and pure-read
(Test 3) tests at the same concurrency, isolating whether mixing the two changes either one's
behaviour. A realistic ratio would tell you more about expected production throughput; this one tells
you more about interaction effects, which is the question the other tests can't answer on their own.

**Tests 5 and 6 are open-model, and they exist because the other four structurally cannot find a
limit.** A closed model with think time self-throttles: 10 users each pausing a second between
requests cannot exceed ~10 req/s regardless of how fast the server is. Finding a limit requires
imposing an arrival rate rather than gating it on user pacing.

They target opposite paths on purpose, and the contrast is the point. **Test 5 (reads)** finds the
infrastructure ceiling — connection-pool saturation — on the one endpoint with no pre-existing defect
to muddy the signal. **Test 6 (writes)** finds the architectural ceiling, on the path where this
application is actually weak. Test 6 is the more informative of the two: its failures are HTTP 500s
from the application itself rather than client-side transport errors, so nothing about them is
ambiguous, and because the write path collides at a concurrency of 2, its whole curve is measurable
at rates far too low to stress the load generator's own host.

**Test 2 is a targeted concurrency probe, not a usage model.** It has no ramp and no think time
because its purpose is to make ten writes land on one row simultaneously. All ten virtual users warm
their connections, then block on a barrier and are released together; without that, NBomber's own
start-up jitter spreads the requests far enough apart that the defect hides in roughly 3 runs out of
5. See `Test2ConcurrentPayments.cs` for the measured reproduction rates. The barrier wait and warm-up
sit outside the measured step, so they don't inflate the reported payment latency.

Every virtual user in every test is pinned for its whole run to one of 10 seeded RECEPTIONIST
accounts (`ClientPool<T>` + `ScenarioInfo.InstanceNumber`) — a distinct session per user, not a
randomly chosen account per request.

## Parameterization

No test replays one hardcoded request body.

- **Owners:** a 10-owner pool created once per run and referenced by id, rather than one owner per
  request.
- **Discount:** randomized per write from `{0, 10, 20, 50}`.
- **Read filters (Tests 3, 4):** page randomized `0–4`; status filter randomized across
  `{none, DRAFT, ISSUED, PAID}`.
- **Identity:** 10 real seeded RECEPTIONIST accounts, one per virtual user.

## Response assertions

Every request is validated on content, not just status code.

- **Invoice creation:** `id > 0`, `status == "DRAFT"`, `invoiceNo` matches the `INV-` convention,
  `totals.subtotal == 0.00` — deterministically true for any item-less invoice regardless of which
  owner or discount the iteration drew, so one check covers every parameterized variant.
- **Invoice list:** response parses, pagination fields are non-negative, and a page within the
  reported `totalPages` range returns a non-empty `content` array. Pages beyond that range are
  legitimately empty and excluded, rather than producing false failures.
- **Payments (Test 2):** each response must report `PARTIALLY_PAID` or `PAID`, but the decisive check
  is a post-run `GET` of the invoice (untimed, excluded from the percentile stats): 10 payment
  records, `amountPaid == 100.00`, `balance == 0.00`, `status == "PAID"`. No per-request assertion
  could catch Defect #9 — only this whole-entity invariant does.

## Results

Figures for Tests 1–4 are from one coherent `dotnet run -- all` session (reports
`test{1..4}-20260825-1433*` / `-1434*`), rather than assembled from separate runs. **Tests 5 and 6
come from separate targeted runs** — Test 5 from `test5-20260825-153155`, Test 6 from the 40/s point
of the rate sweep (`test6-20260825-155121`) — since neither is meaningful at the `all` run's default
parameters alone.

Each HTTP call is wrapped in a named NBomber step, so **the latency percentiles are server response
time** — the 1s think time paces the run and shows up in throughput (RPS), but is excluded from the
percentiles rather than swamping them.

| Test | Requests | Error rate | p50 | p75 | p95 | p99 |
|---|---|---|---|---|---|---|
| Test 1 — create, ramp-up | 162 | **10.5%** (17 failed) | 9ms | 11ms | 19ms | 26ms |
| Test 2 — concurrent payments | 10 | 0% per-request; **fails the post-run integrity check in ~9 runs out of 10** | 30ms | 40ms | 45ms | 45ms |
| Test 3 — read-heavy list | 144 | **0%** | 19ms | 24ms | 34ms | 38ms |
| Test 4 — mixed, write step | 76 | **9.2%** (7 failed) | 9ms | 10ms | 21ms | 26ms |
| Test 4 — mixed, read step | 76 | **0%** | 18ms | 22ms | 34ms | 37ms |
| Test 5 — read ramp to 200 req/s | 5,900 | **0%** | 38ms | 61ms | 161ms | 271ms |
| Test 6 — write, 40/s fixed | 800 | **32.6%** (261 failed) | 8ms | 10ms | 14ms | 29ms |

Test 6's full rate-by-rate curve is in the conclusions below — a single row understates it, since the
finding is how the numbers move across rates rather than any one measurement.

NBomber reports p50/p75/p95/p99 and offers no custom-percentile API, so **p95 and p99 stand in for the
brief's p90/p95** — p95 is present as asked, and p99 is the closer of the two available neighbours.

**Error rates vary between runs**, because they are driven by a race rather than a fixed capacity
limit. Across five repeated runs, Test 1's write failure rate ranged from **2.7% to 13.1%** (4 of
148 at the low end, 19 of 145 at the high), and Test 4's write step from **5.6% to 9.2%**. Test 3's
reads have been 0% in every run. The variation is in the rate, never in whether the defect appears at
all — even the 2.7% run still lost four writes to it.

Two things worth drawing out of this table:

- **Reads are consistently slower than writes** — Test 4's read step runs at roughly twice the
  latency of its write step (p50 18ms vs 9ms) on the same run, and Test 3 matches Test 4's read
  figures closely. That is the expected shape: listing invoices pages, filters and counts over a
  growing table, while creating a draft invoice is a single insert. Neither is anywhere near slow
  enough to matter at this load.
- **Test 4's 50/50 split is per-iteration randomization, not an enforced ratio.** This run happened to
  land exactly 76/76; an earlier run drifted to 84/60 (58/42). At ~150 iterations that variance is
  expected, and the per-step error rates are unaffected since each is computed against its own total.

Every Test 1 failure and every Test 4 write failure is the same defect: `duplicate key value violates
unique constraint "invoices_invoice_no_key"`. Test 3's reads have never failed. Test 2's individual
payments all succeed while the shared invoice never reaches `PAID`.

## Where the application breaks first

There are two separate answers, at two different load levels.

### It breaks on correctness at two concurrent requests — before "load" is even a factor

The invoice-number race needs no load at all. Fired as a barrier-released pair, **two simultaneous
invoice creations failed one of the two in 12 out of 12 trials** (details in `DEFECTS.md`). That is
the minimum concurrency the system can experience: two people clicking at the same moment.

The load tests then confirm it persists under realistic usage. During Test 1's 20-second run, peak CPU
on the API container was **5.0%**, HikariCP's pending-connection count never left **0**, and
server-side latency stayed under 30ms at p99 — yet 10.5% of writes failed. Test 4 shows the same
pattern: peak CPU **2.4%**, no pool contention, p99 under 30ms, writes still failing at 9.2%.

Nothing is under strain at this level. The application is comfortably fast and comfortably
under-utilised, and still rejects roughly one write in ten. No amount of provisioning changes that,
because the trigger is concurrency, not throughput.

### The write path doesn't slow down — it sheds work

Test 6 injects invoice creations at a fixed rate for 20s, run at five rates to produce a scalability
curve. Every failure is an HTTP 500 carrying `duplicate key value violates unique constraint
"invoices_invoice_no_key"` — the application rejecting the request, not a transport or harness issue.

| Offered | Succeeded | Failed | Error rate | **Actual successful throughput** | p95 |
|---|---|---|---|---|---|
| 2/s | 40 of 40 | 0 | 0% | **2.0/s** | 12ms |
| 5/s | 98 of 100 | 2 | 2% | **4.9/s** | 13ms |
| 10/s | 190 of 200 | 10 | 5% | **9.5/s** | 13ms |
| 20/s | 332 of 400 | 68 | 17% | **16.6/s** | 13ms |
| 40/s | 539 of 800 | 261 | **33%** | **27.0/s** | 14ms |

Two things stand out, and together they are the central architectural finding.

**Latency never moves.** p95 sits between 12 and 14ms across the entire range — a twentyfold increase
in offered load produces no measurable slowdown. By every conventional performance signal the
application is completely healthy at 40 writes/second.

**Throughput diverges from offered load anyway.** Successful writes grow sublinearly and the gap
widens with every step: at 40/s offered, 13 writes per second are simply lost. The system is not
saturating, queuing, or degrading — it is **discarding work while remaining fast**.

That is the answer to "where does it slow down first": on the write path, it doesn't slow down at
all. It fails. The ceiling on invoice creation is not CPU, memory, or connection capacity — it is a
non-concurrency-safe invoice-number allocation that turns additional concurrency into collisions
rather than throughput. Scaling the hardware would not move this curve; only fixing the allocation
would.

### Under read load, the connection pool saturates first — not CPU

Test 5 ramps read traffic to 200 req/s and sustains **98 req/s with zero errors**, but the cost shows
up in latency and, more tellingly, in the connection pool:

| Read load | p95 latency | Connection pool (max 10) |
|---|---|---|
| ~7 req/s (Test 3) | 34ms | active 0–1, never any queuing |
| ~98 req/s (Test 5) | **47–161ms across 3 runs** | **active reaches 9–10/10; queuing in 1 of 3 runs (up to 21 pending)** |

Between a 1.4× and a 5× latency increase for a 14× traffic increase, depending on the run — and the
reason is visible in the metrics: `hikaricp.connections.active` hits the pool maximum of 10, and in
the slowest run `hikaricp.connections.pending` starts climbing, meaning requests are waiting for a
database connection rather than for the database itself.

**Peak CPU across that entire run was 49.4%.** The application runs out of database connections with
roughly half its CPU idle. The first constraint is a configuration value —
`spring.datasource.hikari.maximum-pool-size`, which defaults to 10 — not hardware. Raising it costs
nothing; adding CPU would not help.

**That run is one of three, and the other two did not queue.** Repeating Test 5 at the same 200 req/s
gave p95 62ms and 47ms, with `hikaricp.connections.pending` never leaving 0 and peak CPU of 28.2% and
22.4%. Active connections reached 9–10 of 10 in all three runs; only the first crossed into actual
queuing. So read ~100 req/s as *the knee is somewhere near here on this machine*, not as a
reproducible threshold — the three runs bracket it rather than agree on it, which is what you would
expect when the pool is sitting right at its limit and small differences in dataset size and host
contention decide whether requests actually wait. What holds across all three runs, and is the
portable part of the finding, is that active connections reach the configured maximum of 10 while CPU
stays under half. The single-run latency figures above should be read as one observation of a range,
not as a repeatable measurement.

This confirms the hypothesis formed before any load test was written: that HikariCP's default pool of
10 was a plausible bottleneck independent of any container resource cap.

The same signature, further past the knee, appears in the earlier 50 req/s write POC: **81** pending
connections uncapped (p95 3.16s) and **167** with the API capped to 1 CPU / 1 GiB (p95 14.88s), active
pinned at 10/10 in both. Different workload, same constraint.

**A methodological note worth recording.** An earlier version of Test 5 ramped to 400 req/s and
reported 24.7% errors with p95 of 4.2s. Those numbers were discarded, not published: the failures were
client-side transport exceptions rather than HTTP error responses, and investigation showed the load
had saturated the *host's* Docker port-forwarding path — the application itself answered
`{"status":"UP"}` normally from inside the Docker network throughout, and its data was intact. At that
overshoot the test was measuring the load generator's host, not the server. Ramping to 200 instead
crosses the pool knee with enough headroom that every number above comes from the application rather
than from the harness around it.

Neither defect is a capacity problem. Both are concurrency-correctness bugs in application logic —
invoice-number allocation, and the payment status transition — that reproduce at any concurrency
above 1, on hardware of any size. Provisioning a bigger machine does not fix either one.

That distinction matters more than a throughput ceiling would. A resource bottleneck is something you
provision past; a correctness race under concurrency rejects legitimate work or silently corrupts
billing state no matter how much CPU is available.

Resource limits affect **amplification, not causation**. The 50 req/s write POC failed 49.8% of
requests uncapped, and 81.6% with the API capped to 1 CPU / 1 GiB. The cap widens the race window and
makes the defect far more frequent — it does not create it. The ordering is what matters: the
correctness defect needs two concurrent requests, while pool saturation needs sustained traffic two
orders of magnitude higher. Correctness fails first by a wide margin, and unlike the pool limit, it
cannot be configured or provisioned away.

## Validity of these numbers

Both the application (Docker Desktop/WSL2: db, api, web) and the NBomber load generator ran on **one
physical machine**, with no dedicated load-generation infrastructure. The generator runs on the host
rather than in Docker, so it does not compete for the same container resource pool as the capped API,
but everything else on the machine — IDE, browser, tooling — shares the same hardware.

**Absolute latency and throughput figures here are not production capacity numbers.** They describe
one machine at one point in time.

**The ~100 req/s pool-saturation figure specifically is machine-dependent, and should be read as an
observation rather than a property of the application.** What saturates a pool of 10 is how long each
query holds a connection, which varies with CPU and disk speed, with the size of the dataset being
queried (this database grew from ~16 to ~474 invoices over the course of this work, and the list
endpoint paginates and counts over it), and with the load generator competing for the same machine.
Faster hardware would return connections sooner and push the number up; slower hardware or a larger
table would pull it down. What is portable is the *shape* of the finding: the pool is fixed at 10 by
configuration, it saturates while CPU is roughly half idle, and it is therefore the first constraint
to hit — and the first thing to raise — regardless of where this runs.

What does transfer is the correctness conclusions, and they transfer more strongly than any timing
figure. Defect #8 reproduced identically in character — same error, same constraint — at four
unrelated concurrency levels: two simultaneous requests, ~7 req/s with realistic pacing, and 50 req/s
both capped and uncapped. A race that fires reliably on two concurrent requests is not a property of
this machine; it is a property of the code. The failure *rate* is environment-sensitive; the
failure's *existence* is not. The same application logic runs unchanged wherever it is deployed.

## Limitations and follow-ups

- **Test 2 remains probabilistic** (~9 failures in 10 runs). A single PASS is not evidence the defect
  is fixed. Repeating the trial several times per run and asserting on the aggregate would close the
  remaining gap.
- **Defect #9 is the more urgent of the two to escalate**, despite Defect #8's higher failure rate: it
  produces no error in the client response or the application logs, and leaves a fully paid invoice
  permanently misclassified as outstanding. Defect #8 at least fails loudly.
- **Test 6's curve stops at 40/s** because that was enough to establish the trend. Extending it would
  show where successful write throughput plateaus outright, which would put a hard number on the
  application's maximum sustainable invoice-creation rate.
- **Test 5 finds where saturation begins, not where the server finally fails.** Pushing far enough to
  find the true breaking point saturates this machine's own networking before it breaks the
  application (see the methodological note above), so the hard ceiling can't be measured from a
  single host. That needs the load generator on separate hardware.
- **The obvious next experiment is raising `spring.datasource.hikari.maximum-pool-size`** and re-running
  Test 5, to confirm the knee moves with the pool rather than with CPU. That would turn a
  well-evidenced inference into a demonstrated cause.
- **On Windows, `localhost` may resolve to IPv6 and hit a wedged Docker forwarder** while the
  application is perfectly healthy. If the suite reports the app unreachable but `docker compose ps`
  shows it healthy, try `API_BASE_URL=http://127.0.0.1:8080` before assuming anything is wrong with
  the application.
- **These tests do not clean up after themselves** (owners, receptionist accounts, invoices) — same
  convention as Tasks 2/3. Reset with `docker compose down -v` in `qa-test-automation-task` when a
  clean baseline is needed.
