# Defects found through performance testing (Task 4)

Both defects below were found by running concurrent load against the application. Neither reproduces
under sequential requests, which is why Task 1–3's methodology could not have caught them.

Numbered continuing the sequence in `task1-test-plan/test-plan.md` §8, which ends at #7 — same
application, different method of finding them, one shared numbering so a reference to "Defect #9"
means the same thing everywhere in this submission.

---

## Defect #8 — Invoice creation fails under concurrency (invoice-number race)

**Severity:** High. **Reproduced in:** Test 1, Test 4, and the earlier uncapped and CPU/RAM-capped
load runs.

`POST /api/invoices` returns `500 Internal Server Error` under concurrent write load:

```json
{
  "status": 500,
  "error": "Internal Server Error",
  "code": "INTERNAL_ERROR",
  "message": "could not execute statement [ERROR: duplicate key value violates unique constraint \"invoices_invoice_no_key\"\n  Detail: Key (invoice_no)=(INV-2026-1338) already exists.]"
}
```

**Root cause (inferred — application source was not inspected):** the API appears to generate each
invoice's human-readable number (`INV-YYYY-NNNN`) by reading the current maximum and incrementing it
in application code, rather than using a database sequence or an atomic allocation. Two concurrent
requests can read the same next number before either commits; the second `INSERT` then violates the
table's unique constraint.

### It takes two simultaneous requests — that is the whole threshold

Measured directly, firing N invoice creations released from a barrier, 12 trials each:

| Concurrency | Trials hitting the race | Individual requests failed |
|---|---|---|
| **2** | **12 of 12** | 12 of 24 |
| 3 | 12 of 12 | 24 of 36 |
| 5 | 12 of 12 | 42 of 60 |

At the minimum possible concurrency — two requests at once — **exactly one of the two fails, every
time.** There is no load threshold to cross. Two receptionists pressing "create invoice" in the same
moment is sufficient, on any hardware.

This matters for how the rate figures below should be read. They are not a measure of when the defect
starts; they are a measure of how often requests happen to overlap at a given load, which is why they
vary with both load and environment:

| Run | Load | Failure rate |
|---|---|---|
| Uncapped | 50 req/s, open model | 498/1000 = **49.8%** |
| API capped to 1 CPU / 1 GiB | 50 req/s, open model | 816/1000 = **81.6%** |
| Test 1 | 10 users, 1s think time (~7.25 req/s) | **2.7–13.1%** across 5 runs |
| Test 4 (writes only) | 10 users, 1s think time, mixed | **5.6–9.2%** across 5 runs |

The rate varies between runs and environments; the defect's presence does not. It has appeared in
every run at every concurrency level tested, down to two.

The defect is **not caused by the resource cap** — it reproduces uncapped, and at a realistic
10-user load with think time and no cap at all. The cap widens the race window and makes failures far
more frequent, but the bug exists independently of any infrastructure constraint. In production this
would surface as soon as two staff members create invoices at close to the same moment.

**Why Tasks 1–3 did not catch it:** every write in those suites runs one request at a time by
construction — a single test method, a single virtual user. No sequential test can reproduce this,
however thorough.

---

## Defect #9 — Concurrent payments leave a fully paid invoice stuck at PARTIALLY_PAID

**Severity:** Critical — silent. **Reproduced in:** Test 2, in approximately 9 runs out of 10.

Ten concurrent `POST /api/invoices/{id}/payments` requests of $10 each, against a single freshly
issued $100 invoice, all succeed individually (`200 OK`, no errors). A post-run `GET` of the invoice
shows:

```json
{
  "status": "PARTIALLY_PAID",
  "payments": [ /* exactly 10 entries, $10.00 each — none lost or duplicated */ ],
  "totals": { "amountPaid": 100.00, "balance": 0.00 }
}
```

`amountPaid` and `balance` are both **correct**; the payment total is computed accurately. But
`status` never transitions to `PAID` despite the balance reaching exactly zero. The returned
`allowedTransitions` still lists `PAID` as reachable, so the system does not consider the invoice
settled — it is waiting on an action that no normal workflow step performs.

**No error, warning or exception appears anywhere in the API's logs** for this invoice or time
window. Every request the client sees reports success. This is a silent data-consistency defect,
detectable only by checking the entity's final state against an invariant.

### Sequential control — this is genuinely a concurrency defect

Ten payments of $10 issued **sequentially** against an identical $100 invoice behave correctly:
payments 1–9 each return `PARTIALLY_PAID`, and payment 10 transitions the invoice to `PAID`, ending
at `amountPaid 100.00` / `balance 0.00` / `status PAID`.

This rules out the obvious alternative explanation — that incremental payments simply never flip the
status, which would make this an ordinary logic bug rather than a race. The status-transition logic
is correct; it only fails when the payments overlap.

### Reproduction rate

The defect is a race, so reproduction depends on how tightly the ten requests land together:

| Configuration | Reproduced |
|---|---|
| NBomber default scheduling | 2 of 5 runs |
| Barrier-released (all 10 held, then released together) | 4 of 5 runs |
| Barrier + per-user connection warm-up (current) | 9 of 10 runs |

Test 2 uses the last configuration. **A single PASS does not demonstrate the defect is fixed** —
roughly 1 run in 10 still passes by timing alone.

**Root cause (inferred):** the logic deciding "has this invoice reached `balance == 0`, so flip it to
`PAID`" appears to evaluate that condition per request against a snapshot of the invoice total that
is not guaranteed current while nine other payments are committing. Each request independently
concludes it is not the one reaching zero, so none performs the status flip — even though the
aggregate total, computed correctly elsewhere (likely via a live SQL sum rather than the same
in-memory path), is unambiguously zero once all ten commit.

### Probable relationship to Task 1's Defect #3

This is very likely the same underlying defect as `test-plan.md`'s Defect #3 (`PAID` invoices with a
non-zero balance, confirmed on seed invoices `INV-2024-0003`/`0004`, which Task 1 could never
reproduce through sequential testing — see that document's §8 and §10).

The two are mirror images: there, `status` reads `PAID` while the balance is non-zero; here, the
balance is zero while `status` never reaches `PAID`. Both are what you would expect from one root
cause — the `status` column and the computed payment totals not being updated atomically under
concurrent writes — observed from opposite sides of the race depending on timing.

Task 1's root-cause investigation was blocked precisely because sequential reproduction never
triggered it. This is the first evidence of a mechanism that would explain how Defect #3 can arise at
all. Not proven — the application source was not inspected — but strong enough to justify
investigating the two together rather than as unrelated bugs.

**Why this is worse in practice than Defect #8:** Defect #8 fails loudly and immediately; a
receptionist retrying a failed invoice creation notices at once. This defect leaves a fully paid
invoice permanently misclassified as outstanding, with nothing anywhere to prompt anyone to look —
in a domain where "which invoices are still owed" is exactly the fact that reporting and
reconciliation depend on.
