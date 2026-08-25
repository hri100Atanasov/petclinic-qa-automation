# Cited runs

The raw NBomber reports and metrics CSVs behind every number in
[`../SUMMARY.md`](../SUMMARY.md) and [`../DEFECTS.md`](../DEFECTS.md), committed so the
figures can be checked against their source. `PetClinic.PerformanceTests/reports/` is the
working directory every run writes into; it is gitignored and holds many more runs than these.

| File(s) | Backs |
|---|---|
| `test{1,2,3,4}-20260825-1433*`/`-1434*.md` | The Tests 1–4 rows of SUMMARY's results table — one coherent `dotnet run -- all` session |
| `test1-metrics-...-143350.csv` | Test 1's 5.0% peak CPU and its flat 0 pending-connection count |
| `test4-metrics-...-143444.csv` | Test 4's 2.4% peak CPU, same flat pool |
| `test5-20260825-153155.md` | The read-ramp row: 5,900 requests, 0 errors, p95 161ms |
| `test5-20260825-{153636,155531}.md` | The two repeat runs at the same 200 req/s that did *not* queue (p95 62ms / 47ms) |
| `test5-metrics-...-{153154,153636,155531}.csv` | The pool evidence across all three: active 9–10/10 every time, pending 21 / 0 / 0, CPU 49.4% / 28.2% / 22.4% |
| `test6-20260825-1549*`/`-1551*.md` | The five points of the write-scalability curve (2, 5, 10, 20, 40 req/s) |

| `create-invoice-poc-...-103155.md` + `metrics-...-103154.csv` | The uncapped 50 req/s POC: 498/1000 failed (49.8%), p95 3.16s, 81 pending connections at active 10/10 |
| `create-invoice-poc-...-103428.md` + `metrics-...-103428.csv` | The same POC with the API capped to 1 CPU / 1 GiB: 816/1000 failed (81.6%), p95 14.88s, 167 pending |

The earlier 400 req/s Test 5 overshoot is described in SUMMARY but deliberately not committed:
those numbers were discarded as invalid — the failures were client-side transport errors from
saturating the host's own Docker port-forwarding path, not the application — so publishing the
report alongside valid runs would invite them being read as a result.
