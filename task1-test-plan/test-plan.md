# Test Plan — Billing Module (PetClinic Pro)

## 1. Module and rationale

**Module:** Billing (invoices, line items, payments, invoice lifecycle) — `/api/invoices/**`.

**Why this module:** it carries direct financial and compliance risk (money changing hands, tax
and discount calculations, audit trail), has a non-trivial state machine (DRAFT → ISSUED →
PARTIALLY_PAID / PAID, VOID as a terminal branch), and sits behind role-based access control that
gates who can issue, amend, void, and take payment. A short exploratory pass surfaced seven
confirmed defects in this area alone (see §8), which is itself a signal that the module's risk is
higher than average.

## 2. Scope

### In scope

- Invoice lifecycle: create draft → add/remove line items → issue → record payment(s) → void,
  including the allowed/blocked transitions between these states.
- Financial calculations on an invoice: subtotal, discount amount, taxable amount, tax amount,
  total, amount paid, balance — individually and combined (discount + tax together).
- Payment recording: valid payments, boundary and invalid amounts, multiple partial payments,
  overpayment.
- Read paths: single invoice retrieval, invoice list with filtering (status, owner) and pagination.
- Access control on billing endpoints across the roles present in the seed data: `ADMIN`,
  `RECEPTIONIST`, `VET`, `READONLY`, and a **disabled** account (`former.staff`).
- API-level and UI-level behavior for the above, where both surfaces exist (the UI is a thin
  client over the same REST API per the app README).

### Out of scope (and why)

- **Responsive/mobile layout testing.** The app presents as a desktop/web-oriented back-office
  tool (invoice tables, forms); no mobile breakpoints or touch interactions were advertised.
  Assumption — revisit if the app turns out to target tablet use at the front desk.
- **Appointment scheduling, medical records, owner/pet management** as standalone modules —
  only touched where they're a precondition for billing (e.g. an invoice needs an owner, optionally
  a visit).
- **Load/performance characteristics of Billing** — covered separately in Task 4, not duplicated
  here.
- **Non-billing RBAC roles' full permission matrix** (e.g. what `VET` can do in medical records) —
  only the billing-relevant slice of RBAC is in scope for this plan.
- **Internationalization / multi-currency** — the app shows no currency selector or locale
  switching; single-currency assumed.
- **Email/notification delivery** for issued invoices or receipts — no such feature was observed
  in the API surface (`/v3/api-docs` has no notification endpoints); assumed not implemented.

## 3. Assumptions

- There is no account-credit / overpayment-handling feature in the application — a payment is
  expected to be capped at (or rejected above) the outstanding balance. This is inferred from
  there being no credit-balance field anywhere in the `InvoiceTotals` / owner schema, not from
  explicit documentation, so it's flagged as an assumption rather than a fact.
- `taxRate` is a fraction in the `[0, 1]` range (e.g. `0.10` = 10%), while `discountPct` is a
  **different** scale — a whole percentage in `[0, 100]` (e.g. `100` = 100%), per the validation
  errors returned for each field. Worth flagging in test data setup since the two fields look
  similar but aren't scaled the same way — an easy place for a test (or a future API consumer) to
  mix up.
- Seed data resets via `docker compose down -v` (per the application's README); tests that mutate invoices
  (issuing, paying, voiding) must not assume a specific invoice ID/number is available unless the
  test creates it itself.
- Single active session model — no evidence of concurrent-edit conflict handling (e.g. optimistic
  locking / ETags) on invoices; not yet confirmed either way, treated as an open question (§10).

## 4. Risks

Ranked by business impact, based on exploration to date:

| # | Risk | Impact | Evidence |
|---|---|---|---|
| 1 | Tax miscalculation (computed on subtotal, not taxable amount) | Every discounted invoice over-charges tax — direct financial/compliance exposure, on every sale with a discount | Confirmed, Defect #1 |
| 2 | Disabled accounts can still authenticate and act on billing | Compliance/audit risk — a disabled account can still authenticate and obtain a fresh session at any time after being disabled, not merely retain a pre-existing token | Confirmed, Defect #4 |
| 3 | Invoices can be overpaid without rejection or credit handling | Negative balances, incorrect status, downstream reporting/reconciliation errors | Confirmed, Defect #2 |
| 4 | PAID invoices can carry a non-zero balance | Breaks the PAID-means-settled invariant that reporting/reconciliation likely relies on | Confirmed, Defect #3 |
| 5 | Pagination `last` flag not respected by the UI's Next control | Users can page past the end of results; low financial impact but a data-integrity/UX smell that erodes confidence in the rest of the list view | Confirmed, Defect #5 |
| 6 | Owner selection in the invoice-creation form is capped at the first 100 owners, with no further pagination or search | Caps business capacity — once a clinic has more than 100 registered owners, front-desk staff cannot create an invoice for anyone sorting past the 100th via the UI at all, regardless of role | Confirmed, Defect #6 |
| 7 | Invoice due date renders as the wrong calendar day for any viewer in a timezone behind UTC | Every invoice due date can display one day earlier than the actual due date for a majority of real-world timezones (all of the Americas and points west of Greenwich); risks a customer paying "late" against a due date the UI itself understated, or staff misjudging what's actually overdue | Confirmed, Defect #7 |

Two further defects (#8, #9) were found later by Task 4's concurrency testing and are outside this
plan's scope — it covers sequential behavior, and both of those need two requests to overlap. They
are documented in `../task4-performance/DEFECTS.md`, and #9 bears directly on Defect #3's open root
cause (§10).

The concentration of confirmed defects around *calculation* and *state-invariant* logic (rather
than, say, layout) is itself informative: it raises prior for **other arithmetic paths** (e.g.
partial payments combined with discounts, void-after-partial-payment, multi-item invoices with
mixed item types) being similarly under-tested by the original developers, so those get extra
weight in the scenario list.

## 5. Test approach

- **UI-driven exploration, with network inspection.** The exploratory pass that produced the risk
  list and defects in §8 was driven through the web UI (`localhost:8081`), using the browser's
  network tab to inspect the underlying API requests and responses for each action. This is what
  made it possible to tell *which layer* a defect actually lived in — e.g. Defect #5 (pagination)
  was first noticed as UI behavior, then confirmed to be a front-end defect specifically by
  checking that the underlying API response already correctly reported `"last": true`; Defect #3
  (PAID invoices with a non-zero balance) was traced past the UI to the API response itself,
  ruling out a rendering issue.
- **Complemented by direct API testing** (crafting and sending requests outside the UI), used to
  precisely pin down boundary conditions (e.g. sweeping `discountPct` through 0/50/100, replaying
  a payment with varied amounts) and confirm exact status/error codes behind what the UI pass
  surfaced. This was targeted, ad hoc verification of specific findings, not a systematic sweep —
  that's the distinction from Task 3 below.
- This split still maps onto Tasks 2/3: UI automation (Task 2) exercises the same flows driven
  through the browser here; API automation (Task 3) turns the ad hoc direct API checks used in
  this pass into a systematic, automated suite covering boundary conditions comprehensively,
  rather than the targeted, one-off verification done here.
- **Boundary and negative testing weighted heavily**, given the risk profile: zero/negative
  amounts, 0%/100% discount, overpayment, cross-status transitions, disabled/wrong-role actors.
- **State-machine coverage**: every documented transition (`allowedTransitions` in the API
  response) gets at least one "allowed" and, where applicable, one "blocked-from-here" case.
- Manual exploratory testing (already performed) feeds the scenario list below; Task 2/3 will
  automate a subset of these scenarios at the UI and API layer respectively.

## 6. Entry criteria

- Application stack is up (`docker compose up`) and reachable at `localhost:8080` (API) /
  `localhost:8081` (UI); `/actuator/health` reports `UP`.
- Seed data is in its default state, or the specific fixture a scenario needs is created by the
  scenario itself (invoices are cheap to create via `POST /api/invoices`).
- Test accounts from the README are available and match the documented roles/enabled state.
- Tester's browser/OS clock is set to UTC before running any date-sensitive scenario (due date,
  overdue calculations). The API stores and returns timestamps in UTC, but the UI renders dates in
  the browser's local timezone (per the application's README) — a non-UTC browser can make a
  date-based assertion (e.g. S1's expected due date) appear to fail when it hasn't. **Exception:**
  Defect #7 (§8) shows this assumption breaks down specifically for due-date rendering — a viewer
  in any timezone behind UTC sees a genuinely wrong calendar day, not a false failure, which is
  exactly what S18 was designed to surface. This criterion still holds for every other
  date-sensitive check; it's also the reason a UTC-only manual pass didn't catch Defect #7 in the
  first place.

## 7. Exit criteria

- All 5–8 fully-specified scenarios (§9 shortlist) executed at least once, result recorded.
- Every confirmed defect (§8) has a corresponding scenario demonstrating it, filed with repro
  steps, so the defect is regression-testable once fixed.
- No **new** untriaged high-severity defect discovered in the last full pass through the scenario
  list (i.e. the list has stabilized, not that it's defect-free — seven defects are already known and
  accepted as open going into exit).
- Open questions in §10 are either answered or explicitly carried forward as documented
  assumptions.

This module will **not** exit "green" — it exits with a known, documented defect list. That's a
deliberate outcome of exploratory testing finding real issues, not a plan failure.

## 8. Known defects found during exploration

Surfaced through UI-driven exploratory testing with network inspection (§5), with exact values and
boundaries in several cases additionally pinned down via direct API testing performed during this
collaborative pass (also §5). Task 3 is where API testing becomes systematic and automated, rather
than the ad hoc verification used here.

1. **Tax computed on subtotal instead of taxable amount.** A 100%-discount and a 0%-discount
   invoice with the same subtotal/tax rate produce the *same* tax amount. Verified with a genuine
   100% discount (`discountPct: 100`, the correct top-of-range value — see the scale note in §3):
   subtotal 100.00, `taxableAmount: 0.00` after the discount, but `taxAmount: 10.00` — 10% of the
   subtotal, not 10% of the (zero) taxable amount. Expected: `taxAmount: 0.00` when the taxable
   amount is 0.00. Also holds at partial discount levels (e.g. 50%): `taxAmount` stays 10.00
   regardless of `taxableAmount`. Expected: tax computed on `taxableAmount` throughout.
2. **Invoices can be overpaid.** Recording a 500.00 payment against a 110.00 invoice was accepted;
   resulting state was `balance: -390.00`, status `PARTIALLY_PAID` (not even auto-flipped to
   `PAID`). No rejection, no credit handling.
3. **`PAID` invoices with non-zero balance.** Filtering the invoice list by `PAID` status in the
   UI and inspecting the underlying response shows `INV-2024-0004` (total 373.75, paid 370.50,
   balance 3.25) and `INV-2024-0003` (total 316.25, paid 313.50, balance 2.75) both carrying
   `status: PAID`. The data returned by the API is inconsistent, not a front-end rendering issue —
   likely a rounding or status-transition defect in payment processing. Attempting to reproduce it
   on a freshly created invoice, using the same subtotal/tax/discount/payment amounts as the two
   affected invoices, did not reproduce a non-zero balance — root cause is still open (§10).
4. **Disabled account can authenticate and use billing endpoints.** Logging in as `former.staff`
   (whose login response carries `enabled: false`) succeeds, and the resulting session can still
   view/act on invoices. RBAC *scoping* itself works correctly for active accounts — the defect is
   narrowly that the `enabled` flag isn't checked at authentication or at request time.
5. **Pagination: Next control stays active on the last page.** `GET /api/invoices?page=2&size=10`
   returns `"last": true`, but the Next button in the UI remains active/clickable on the last
   page instead of being disabled.
6. **Owner selection in the invoice-creation form is capped at the first 100 owners.** The
   new-invoice form's owner dropdown requests `GET /api/owners?size=100` and never requests a
   further page — confirmed via the browser's network tab. The API itself paginates correctly
   (`page`, `size`, `totalElements`, `totalPages` all present; `page=1&size=100` returns a
   genuinely different set of owners), so this is UI-only, not an API limitation. Owners are
   sorted by `lastName` ascending with no search or "load more" control inside the dropdown, so
   with more than 100 owners on file, anyone sorting past the 100th is entirely unreachable from
   this form. Confirmed directly: with over 300 owners in the database, the dropdown rendered
   exactly 100 selectable options (plus the placeholder), well short of the full list.
7. **Invoice due date renders as the wrong calendar day for a viewer behind UTC.** `dueDate` is
   returned as a bare, timezone-less date (e.g. `2026-09-24`) — the AUT's own README confirms
   dates are stored in UTC but rendered in the browser's local timezone. Confirmed directly with
   Playwright's per-context timezone emulation on the same invoice: viewed from `UTC` or
   `Pacific/Kiritimati` (UTC+14) it renders correctly as `9/24/2026`; viewed from `Atlantic/Cape_Verde`
   (UTC-1) or `Pacific/Honolulu` (UTC-10) it renders as `9/23/2026` — one day early. Consistent
   with the frontend parsing the bare date string as UTC midnight and then formatting it in the
   viewer's local time: since that's exactly 00:00 UTC, *any* negative offset at all — not just
   large ones — rolls it back to the previous calendar day. That covers a majority of real-world
   timezones (all of the Americas and everywhere else west of Greenwich), not an edge case.

Defects #8 and #9 — the invoice-number race under concurrent creates, and concurrent payments
leaving a fully paid invoice stuck at `PARTIALLY_PAID` — were found later, by Task 4's load testing,
and are written up in `../task4-performance/DEFECTS.md`. They are not repeated here: this plan's
methodology is sequential by construction and could not have surfaced either.

## 9. Scenario list (titles + one-line intent)

Positive:

- **S1 — Create, issue, and pay an invoice in full.** Happy path through the whole lifecycle;
  baseline for every other scenario.
- **S2 — Multi-item invoice totals.** Subtotal correctly sums multiple line items with different
  quantities/unit prices.
- **S3 — Partial payment then final payment reaches PAID.** Two payments summing exactly to the
  balance transitions status correctly.
- **S4 — Void a draft invoice.** No payments involved; confirms VOID is reachable pre-issue.
- **S5 — List invoices filtered by status and owner.** Read-path correctness for the primary
  billing worklist view.

Negative / boundary (the higher-value half, given the risk profile):

- **S6 — Tax is computed on the discounted (taxable) amount, not the subtotal.** Directly targets
  defect #1.
- **S7 — Discount percentage produces a proportionally correct discount amount.** Already
  confirmed working (100% and 50% discount both compute correctly on the field's `0–100` scale) —
  regression guard, and useful precisely because it's adjacent to defect #1 (tax-on-wrong-base):
  it isolates that the discount math itself is sound and the defect is specifically in how tax reads
  the taxable amount.
- **S8 — A payment cannot exceed the outstanding balance.** Directly targets defect #2.
- **S9 — A disabled account cannot authenticate against billing endpoints.** Directly targets
  defect #4.
- **S10 — Zero and negative payment amounts are rejected.** Confirmed via the UI: attempting to
  submit a zero or negative payment amount is rejected, with the browser's network tab showing a
  `400 VALIDATION_FAILED` response — kept as a regression guard, not because it's currently broken.
- **S11 — Line items cannot be added/removed once an invoice is issued.** Confirmed directly via
  API: attempting to add a line item to an issued invoice returns `422 INVOICE_NOT_EDITABLE` —
  regression guard.
- **S12 — A voided invoice rejects further payment.** Confirmed directly via API: attempting to
  record a payment against a voided invoice returns `422 PAYMENT_NOT_ALLOWED` — regression guard.
- **S13 — READONLY and VET roles cannot perform billing write actions.** Confirmed directly via
  API: authenticating as `auditor` or `vet.carter` and sending a billing write request (e.g.
  `POST /api/invoices/{id}/payments` or `/void`) with that token returns `403 FORBIDDEN` in both
  cases — regression guard, and scopes how narrow defect #4 actually is.
- **S14 — Next/pagination control respects the API's `last` flag.** Directly targets defect #5;
  UI-level, to be confirmed in Task 2.
- **S15 — PAID status implies zero balance (data-integrity check).** Directly targets defect #3; a
  system-wide sweep over all "Paid" invoices rather than a lookup of specific invoice numbers, so
  it stays valid and executable regardless of what the current invoice set contains.
- **S16 — Billing controls in the UI match each role's permission level.** READONLY/VET see no
  write controls, RECEPTIONIST gets full access minus void, ADMIN gets full access including void;
  UI-level, to be confirmed in Task 2.
- **S17 — Owner selection in the invoice form is capped at the first 100 owners with no
  pagination.** Directly targets defect #6; UI-level, to be confirmed in Task 2.
- **S18 — Invoice due date renders as the correct calendar day regardless of viewer timezone.**
  Directly targets defect #7; UI-level, confirmed in Task 2 with Playwright's per-context timezone
  emulation across four fixed-offset zones.

S1, S2, S3, S6, S8, S9, and S15 are written up in full (preconditions, steps, test data, expected
result) in `scenarios-full.md`.

## 10. Open questions

- Defect #3 (PAID invoices with a non-zero balance) is confirmed on two existing seed invoices,
  but a reproduction attempt with matching subtotal/tax/discount/payment amounts on a freshly
  created invoice did not trigger it. Root cause is unknown — candidates include a legacy/migration
  data artifact (the two affected invoices predate the current seed generation), a specific
  payment-timing or multi-payment sequence not yet tried, or a rounding edge case tied to exact
  cent values not yet isolated. Worth a targeted investigation once Task 3's direct API access
  makes it cheaper to sweep payment sequences than the UI does.
  **Update from Task 4.** Concurrency testing produced a probable mechanism. Defect #9 is this
  defect's mirror image — balance correct while `status` never reaches `PAID`, rather than `PAID`
  with a balance left over — which is what one root cause would look like from either side of a
  race: the `status` column and the computed payment totals not being updated atomically under
  concurrent writes. Sequential reproduction was never going to trigger it, which is exactly why
  this stayed open. Not proven without the source, but strong enough that the two should be
  investigated together. See `../task4-performance/DEFECTS.md`.
- Is there an intended credit-balance / refund feature that's simply not yet implemented, or is
  "reject overpayment outright" the intended behavior? Affects how S8's expected result should be
  worded in the full scenario write-up.
- Should `former.staff`'s existing token be invalidated immediately on deactivation, or is
  "disabled accounts can't obtain new tokens, but existing tokens survive until natural expiry" an
  acceptable interim design? Affects severity/expected-fix framing for defect #4.
- Is invoice numbering (`INV-YYYY-NNNN`) expected to reset or continue across the year boundary,
  and does anything key off it besides display? Not yet explored; low priority unless it turns up
  elsewhere.
- No optimistic locking / conflict response was observed when probing; worth one dedicated check
  (two concurrent payments against the same invoice) before ruling it in or out of scope.
  **Answered by Task 4.** That check was performed — ten concurrent payments against one issued
  invoice. There is no conflict handling: all ten commit correctly (`amountPaid` 100.00, `balance`
  0.00, all ten payment records present) while `status` never leaves `PARTIALLY_PAID`. Filed as
  Defect #9 in `../task4-performance/DEFECTS.md`.
