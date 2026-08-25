# Task 4 — Performance and concurrency testing

Load tests against PetClinic Pro's Billing API, built with [NBomber](https://nbomber.com) 6.6.0
(.NET). This file is how to run them. The results and reasoning live next door:

| | |
|---|---|
| [`SUMMARY.md`](SUMMARY.md) | Load model, parameterisation, response assertions, results, and where the application breaks first — the written summary the brief asks for |
| [`DEFECTS.md`](DEFECTS.md) | The two concurrency defects this task found (#8, #9), with reproduction detail |
| [`reports-cited/`](reports-cited/) | The raw NBomber runs behind every figure quoted in those two documents |
| [`PROMPTS-TASK-4.md`](PROMPTS-TASK-4.md) | Task 4's own AI-usage log |

## Prerequisites

- **[.NET 10 SDK](https://dotnet.microsoft.com/download)** on the host. Unlike Tasks 2/3, this
  project is **deliberately not containerized** — running the load generator inside Docker would
  make it compete with the application for the same resource pool, which is exactly what a load test
  must not do.
- **PetClinic Pro running**, from its own repository (`qa-test-automation-task`, `docker compose
  up`). This project never starts or manages the application's lifecycle; it only checks the app is
  reachable and then generates load against it.

## Running

```bash
cd task4-performance/PetClinic.PerformanceTests
dotnet run -c Release -- test1   # invoice creation under ramped load (10 users, 1s think time)
dotnet run -c Release -- test2   # 10 concurrent payments against one invoice
dotnet run -c Release -- test3   # read-heavy invoice list
dotnet run -c Release -- test4   # mixed read/write
dotnet run -c Release -- test5   # read ramp to 200 req/s — finds the connection-pool ceiling
dotnet run -c Release -- test6   # write scalability at a fixed rate
dotnet run -c Release -- all     # all six, in order
```

Test 6 takes its rate from an environment variable, so the same test builds a scalability curve by
re-running it at several rates:

```bash
WRITE_RATE_RPS=40 dotnet run -c Release -- test6
```

| Variable | Default | Purpose |
|---|---|---|
| `API_BASE_URL` | `http://localhost:8080` | Where the readiness check and all load traffic point |
| `WRITE_RATE_RPS` | `10` | Test 6 only — the fixed write arrival rate |

These are real environment variables. There is no `.env` file at this layer — the one under
`task2-task3-automation/` is read by Docker Compose and has no effect here.

If the application isn't reachable, the run stops immediately with instructions rather than
producing a report full of connection errors.

## What a run looks like

Each test prints an NBomber stats table (request counts, error rate, latency percentiles) and writes
a timestamped HTML/CSV/Markdown report plus a metrics CSV to `PetClinic.PerformanceTests/reports/`.
That directory is a gitignored working directory; the runs quoted in `SUMMARY.md` are committed
under `reports-cited/` instead.

**This is not a pass/fail suite, and failed requests are the point.** Specifically:

- **Tests 1 and 4 are expected to fail a single-digit-to-10% share of writes**, every run. Those are
  HTTP 500s carrying `duplicate key value violates unique constraint "invoices_invoice_no_key"` —
  Defect #8, the invoice-number race.
- **Test 2 is expected to end with `FAIL — invoice #N did not reach a consistent paid state`** in
  roughly 9 runs out of 10. That's Defect #9. It is a race, so ~1 run in 10 passes by timing alone —
  **a single PASS is not evidence the defect is fixed.**
- **Test 6 is expected to fail a growing share of writes as `WRITE_RATE_RPS` rises.** That curve is
  the result it exists to produce, not a malfunction.
- **Test 5 should complete with 0 errors** and visibly higher latency than the other tests (p95
  roughly 45–160ms depending on what else the machine is doing). The spread across runs is itself
  part of the finding — see `SUMMARY.md`.

**The process exits non-zero whenever any request failed**, which for Tests 1, 4 and 6 is every run
by design. Don't wire this into CI as a pass/fail gate without accounting for that.

## Test data

These tests create data and clean up nothing: 10 owners and 10 RECEPTIONIST accounts per run, plus
one invoice per write request — roughly 435 create requests across a full `all` run, ~400 of them
succeeding. Nothing depends on a clean baseline, but this is by far the heaviest suite in the
submission for accumulated data. To reset, run `docker compose down -v` in `qa-test-automation-task`.

## Capping the application's resources

`SUMMARY.md` quotes runs with the API limited to 1 CPU / 1 GiB, used to show that resource limits
amplify the invoice-number race rather than cause it. Those come from layering
[`docker-compose.resource-limits.yml`](docker-compose.resource-limits.yml) over the application's own
compose file — it caps only the `api` container, deliberately, so one variable moves at a time:

```bash
cd task4-performance
docker compose -f ../../qa-test-automation-task/docker-compose.yml -f docker-compose.resource-limits.yml up -d
```

Nothing in the default run path uses it.
