> test info



test suite: `nbomber_default_test_suite_name`

test name: `nbomber_default_test_name`

session id: `2026-08-25_10-31-55_a851ed8e`

> scenario stats



scenario: `create_invoice`

  - duration: `00:00:20`

load simulations:

  - `iterations_for_inject`, rate: `50`, interval: `00:00:01`, iterations: `1000`

|scenario and steps|ok stats|
|---|---|
|scenario name|`create_invoice`|
|requests|total = `1000`, ok = `502`, fail = `498`|
|RPS (req/sec)|total = `50`/s, ok = `25.1`/s, fail = `24.9`/s|
|latency (ms)|min = `7.93`, mean = `421.8`, max = `4880.66`, StdDev = `941.34`|
|latency percentile (ms)|p50 = `24.42`, p75 = `232.58`, p95 = `3158.02`, p99 = `4171.77`|


|scenario and steps|failures stats|
|---|---|
|scenario name|`create_invoice`|
|requests|total = `1000`, ok = `502`, fail = `498`|
|RPS (req/sec)|total = `50`/s, ok = `25.1`/s, fail = `24.9`/s|
|latency (ms)|min = `10.88`, mean = `1497.32`, max = `7136.47`, StdDev = `1618.38`|
|latency percentile (ms)|p50 = `1035.78`, p75 = `2648.06`, p95 = `4284.42`, p99 = `6877.18`|


> status codes for scenario: `create_invoice`



|status code|count|message|
|---|---|---|
|201|502||
|500|498|POST /api/invoices returned 500|


