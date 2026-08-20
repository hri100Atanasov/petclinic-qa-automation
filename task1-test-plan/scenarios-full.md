# Fully-Specified Scenarios — Billing Module

Seven scenarios selected from the shortlist in `test-plan.md` §9 (S1, S2, S3, S6, S8, S9, S15):
three positive scenarios covering the core calculation/lifecycle paths, plus four negative/
data-integrity cases, each directly targeting one of the confirmed defects in `test-plan.md` §8.

Each scenario is written as it would be executed today: through the UI, with the underlying
network requests/responses inspected to confirm what actually happened — consistent with the
methodology in `test-plan.md` §5. For the four defect-targeting scenarios, **Expected Result** is
the correct/intended behavior (what the test asserts), and **Current Actual Result** records what
the application does today, so each scenario doubles as a regression check once its defect is
fixed.

All scenarios assume the seed data has not been mutated beyond what the scenario itself creates,
per the entry criteria in `test-plan.md` §6. Test invoices are created fresh where possible so
scenarios don't depend on a specific pre-existing invoice ID.

---

## S1 — Create, issue, and pay an invoice in full

**Type:** Positive · **Priority:** High · **Related defect:** None (baseline)

**Preconditions**
- Application is up (`docker compose up`), UI reachable at `localhost:8081`.
- Logged in as `reception` / `desk123` (RECEPTIONIST — able to create/issue invoices and record
  payments).
- An owner exists in the system (e.g. "Diego Alvarez", present in seed data).

**Test data**

| Field | Value |
|---|---|
| Owner | Diego Alvarez |
| Tax rate | 10% |
| Discount | 0% |
| Line item | Description: "Annual checkup", Type: Service, Quantity: 1, Unit price: 100.00 |
| Payment method | Cash |
| Payment amount | 110.00 (the full balance) |

**Steps**

1. Navigate to Billing → New Invoice.
2. Select owner "Diego Alvarez", set tax rate to 10% and discount to 0%. (There is no due-date
   field on this form — the due date is set automatically when the invoice is issued, see step 7.)
3. Save the invoice as a draft.
4. Add the line item from the test data table.
5. Confirm the draft totals shown: subtotal 100.00, discount 0.00, taxable amount 100.00, tax
   10.00, total 110.00.
6. Issue the invoice.
7. Confirm the invoice status is now "Issued", the balance shown is 110.00, and a due date has
   been set.
8. Record a payment of 110.00 via Cash.
9. Confirm the resulting invoice status and balance.

**Expected result**

- After step 5: totals are subtotal 100.00 / discount 0.00 / taxable 100.00 / tax 10.00 / total
  110.00.
- After step 6: status transitions from Draft to Issued; balance is 110.00; due date is
  automatically set to 30 days after the issued date (verified: issuing on 8/20/2026 set the due
  date to 9/19/2026).
- After step 8: status transitions to Paid; amount paid is 110.00; balance is 0.00.
- The invoice appears correctly in the invoice list filtered by owner "Diego Alvarez" and by
  status "Paid".

---

## S2 — Multi-item invoice totals

**Type:** Positive · **Priority:** Medium · **Related defect:** None (baseline)

**Preconditions**
- Logged in as `reception` / `desk123`.
- An owner exists (e.g. "Jean Coleman").

**Test data**

| Field | Value |
|---|---|
| Owner | Jean Coleman |
| Tax rate | 10% |
| Discount | 0% |
| Line item 1 | "Consultation", Service, qty 1, unit price 60.00 |
| Line item 2 | "Amoxicillin", Medication, qty 3, unit price 15.00 |
| Line item 3 | "Nail Trim", Procedure, qty 2, unit price 12.50 |

**Steps**

1. Create a draft invoice for the owner with tax rate 10%, discount 0%.
2. Add all three line items from the test data table.
3. Confirm each line item's line total shown in the table (quantity × unit price).
4. Confirm the invoice's subtotal, taxable amount, tax amount, and total.
5. Remove line item 2 ("Amoxicillin") and confirm the totals recalculate.

**Expected result**

- Line totals: item 1 = 60.00, item 2 = 45.00 (3 × 15.00), item 3 = 25.00 (2 × 12.50).
- Subtotal = 130.00 (sum of all three line totals); taxable amount = 130.00 (0% discount); tax
  amount = 13.00 (10% of 130.00); total = 143.00.
- After removing item 2: subtotal = 85.00, taxable amount = 85.00, tax amount = 8.50, total =
  93.50 — the totals recalculate to reflect only the two remaining items, not a stale or
  partially-updated figure.

---

## S3 — Partial payment then final payment reaches Paid

**Type:** Positive · **Priority:** High · **Related defect:** None (baseline; adjacent to Defect #3)

**Preconditions**
- Logged in as `reception` / `desk123`.
- An owner exists (e.g. "Diego Alvarez").

**Test data**

| Field | Value |
|---|---|
| Invoice | Single line item, unit price 100.00, tax rate 10%, discount 0% → total/balance 110.00 |
| Payment 1 | Amount: 60.00, Method: Cash |
| Payment 2 | Amount: 50.00, Method: Cash |

**Steps**

1. Create, add the line item to, and issue an invoice per the test data (balance: 110.00).
2. Record Payment 1 (60.00, Cash). Confirm the resulting status and balance.
3. Record Payment 2 (50.00, Cash). Confirm the resulting status and balance.
4. Confirm both payments appear in the invoice's Payments table with the correct amount and
   method.

**Expected result**

- After Payment 1: status is "Partially Paid"; amount paid is 60.00; balance is 50.00.
- After Payment 2: status is "Paid"; amount paid is 110.00; balance is **0.00**.
- The Payments table lists two entries (60.00 and 50.00, both Cash).

This scenario exercises the same balance arithmetic that Defect #3 shows failing on two specific
seed invoices — it doesn't reproduce that defect, but a failure here (e.g. balance not reaching
exactly 0.00 after payments summing to the total) would indicate the rounding/status-transition
issue in Defect #3 is broader than those two isolated records.

---

## S6 — Tax is computed on the taxable amount, not the subtotal

**Type:** Negative (data-integrity) · **Priority:** Critical · **Related defect:** Defect #1

**Preconditions**
- Logged in as `reception` / `desk123`.
- An owner exists (e.g. "Jean Coleman").

**Test data**

Two invoices, same owner, same tax rate, same single line item, different discount:

| | Invoice A | Invoice B |
|---|---|---|
| Owner | Jean Coleman | Jean Coleman |
| Tax rate | 10% | 10% |
| Discount | 0% | 100% |
| Line item | "Consultation", Service, qty 1, unit price 100.00 | "Consultation", Service, qty 1, unit price 100.00 |

**Steps**

1. Create draft Invoice A with the data above; add the line item.
2. Note Invoice A's totals (subtotal, taxable amount, tax amount).
3. Create draft Invoice B with the data above (100% discount); add the line item.
4. Note Invoice B's totals (subtotal, taxable amount, tax amount).
5. Compare the two invoices' tax amounts.

**Expected result**

- Invoice A: subtotal 100.00, taxable amount 100.00, tax amount 10.00 (10% of 100.00).
- Invoice B: subtotal 100.00, discount amount 100.00, taxable amount 0.00, tax amount **0.00**
  (10% of the 0.00 taxable amount — tax should track the discounted base, not the subtotal).
- The two invoices' tax amounts should differ (10.00 vs 0.00), reflecting their different taxable
  amounts.

**Current actual result (Defect #1 — FAIL)**

Both invoices show a tax amount of 10.00, regardless of discount. Invoice B's tax is computed on
the 100.00 subtotal instead of its 0.00 taxable amount. The two invoices' tax amounts are
identical when they should not be. Verified to also hold at partial discount levels (e.g. 50%
discount still yields tax amount 10.00 rather than 5% × 100.00... i.e. tax computed on subtotal
throughout).

---

## S8 — A payment cannot exceed the outstanding balance

**Type:** Negative (boundary) · **Priority:** Critical · **Related defect:** Defect #2

**Preconditions**
- Logged in as `reception` / `desk123`.
- An issued invoice exists with a known, non-zero balance.

**Test data**

| Field | Value |
|---|---|
| Invoice | Freshly issued invoice, single line item, unit price 100.00, tax rate 10%, discount 0% → total/balance 110.00 |
| Payment attempt | Amount: 500.00 (exceeds the 110.00 balance by 390.00), Method: Cash |

**Steps**

1. Create, add the line item to, and issue an invoice per the test data (balance: 110.00).
2. Attempt to record a payment of 500.00 against it.
3. Observe the system's response (accepted, rejected, or partially applied) and the resulting
   invoice status and balance.

**Expected result**

One of the following, as an explicit product decision (see `test-plan.md` §10 open questions) —
but in either case, the invoice must not end up in an inconsistent state:

- The payment is **rejected** with a validation error (e.g. "payment exceeds outstanding
  balance"), and the invoice remains Issued with its original 110.00 balance, **or**
- The payment is **accepted**, the invoice is marked **Paid** (balance 0.00), and the excess
  390.00 is explicitly recorded elsewhere (e.g. an account credit) rather than left on the
  invoice itself.

What must **not** happen: the payment is silently accepted in full against the invoice with no
excess-handling, leaving the invoice both non-Paid and carrying a negative balance.

**Current actual result (Defect #2 — FAIL)**

The 500.00 payment is accepted in full. The invoice's balance becomes **-390.00**, and its status
remains **Partially Paid** — it isn't even auto-transitioned to Paid despite amount paid (500.00)
exceeding the total (110.00). No rejection, no credit handling.

---

## S9 — A disabled account cannot authenticate against billing endpoints

**Type:** Negative (access control) · **Priority:** Critical · **Related defect:** Defect #4

**Preconditions**
- Logged out / fresh session.
- The `former.staff` seed account exists with role RECEPTIONIST and is deactivated (`enabled:
  false`), per the application's README accounts table.

**Test data**

| Field | Value |
|---|---|
| Username | `former.staff` |
| Password | `old123` |

**Steps**

1. Navigate to the login page.
2. Enter the test data credentials and submit.
3. Observe whether login succeeds or is rejected.
4. If login succeeds: attempt to navigate to the Billing invoice list, and attempt a billing
   write action (e.g. recording a payment on an existing invoice).

**Expected result**

- Login is rejected (e.g. "account disabled" / "account inactive" error); no usable session or
  token is issued.
- The user cannot reach the Billing module or perform any billing action.

**Current actual result (Defect #4 — FAIL)**

Login succeeds — the response carries `enabled: false` but still returns a valid, usable
session/token. The resulting session can view the invoice list and, per prior exploration, is
not blocked from acting on invoices. (For contrast: role-based restrictions otherwise work
correctly for *active* accounts — a READONLY user is blocked from write actions and a VET user is
blocked from voiding an invoice. This defect is narrowly that the disabled flag itself isn't
checked, not a broader RBAC failure.)

---

## S15 — Data integrity: every "Paid" invoice has a zero balance

**Type:** Negative (data-integrity) · **Priority:** High · **Related defect:** Defect #3

**Preconditions**
- Logged in as any role with invoice-read access (e.g. `auditor` / `audit123`, or `reception`).

**Test data**

None fixed — this is a system-wide invariant check over whatever invoices exist in Billing at
execution time, not a lookup of specific invoice numbers. (An earlier version of this scenario
pinned it to two specific seed invoices; reframed below so it stays valid and executable
regardless of what the current invoice set looks like.)

| Field | Value |
|---|---|
| Filter | Invoice list, status = Paid |
| Scope | All invoices currently returned by that filter, whatever they are |

**Steps**

1. Navigate to Billing → Invoices.
2. Filter the list by status "Paid".
3. For every invoice returned, inspect its balance (invoice detail view, or the underlying API
   response visible via the browser's network tab).
4. Flag any invoice where balance is not exactly 0.00.

**Expected result**

Every invoice with status "Paid" has a balance of exactly 0.00 — this should hold as a general
invariant across the whole invoice set, not just for specific records. A pass means the sweep
finds zero violations.

**Current actual result (Defect #3 — FAIL)**

At time of testing, two invoices — `INV-2024-0003` (balance 2.75) and `INV-2024-0004` (balance
3.25) — violate the invariant while every other "Paid" invoice observed satisfies it. Because this
scenario checks the invariant across the full "Paid" filter rather than those two invoice numbers
specifically, it remains meaningful even if seed data changes: if the affected records are no
longer present on a future run, the sweep reporting "zero violations found" is itself informative
(suggests a data artifact rather than a live code path), rather than the scenario simply becoming
unexecutable.

**Reproduction attempt (root cause still open):** creating a new invoice with the same subtotal,
tax rate, discount, and payment amounts as the two affected seed invoices did **not** reproduce a
non-zero balance — the fresh invoice reached Paid with balance 0.00 as expected. The defect is
therefore demonstrated and confirmed on existing data, but not yet reproducible on demand through
the standard create → issue → pay flow with matching inputs. See `test-plan.md` §10 for the
resulting open question.
