> test info



test suite: `nbomber_default_test_suite_name`

test name: `nbomber_default_test_name`

session id: `2026-08-25_10-34-28_6e8bdc7b`

> scenario stats



scenario: `create_invoice`

  - duration: `00:00:20`

load simulations:

  - `iterations_for_inject`, rate: `50`, interval: `00:00:01`, iterations: `1000`

|scenario and steps|ok stats|
|---|---|
|scenario name|`create_invoice`|
|requests|total = `1000`, ok = `184`, fail = `816`|
|RPS (req/sec)|total = `50`/s, ok = `9.2`/s, fail = `40.8`/s|
|latency (ms)|min = `758.84`, mean = `8942.92`, max = `19218.96`, StdDev = `4043.1`|
|latency percentile (ms)|p50 = `8855.55`, p75 = `12156.93`, p95 = `14884.86`, p99 = `17268.74`|


|scenario and steps|failures stats|
|---|---|
|scenario name|`create_invoice`|
|requests|total = `1000`, ok = `184`, fail = `816`|
|RPS (req/sec)|total = `50`/s, ok = `9.2`/s, fail = `40.8`/s|
|latency (ms)|min = `451.8`, mean = `8878.76`, max = `23481.18`, StdDev = `4140.11`|
|latency percentile (ms)|p50 = `8937.47`, p75 = `12410.88`, p95 = `15204.35`, p99 = `17629.18`|


> status codes for scenario: `create_invoice`



|status code|count|message|
|---|---|---|
|201|184||
|500|816|POST /api/invoices returned 500|


