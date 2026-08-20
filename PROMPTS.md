# PROMPTS.md

This file logs the AI-assisted portion of this submission, as required by the assignment brief.

## Tools / models used

- **Tool:** Claude Code (CLI agent)
- **Model:** Claude Sonnet 5 (`claude-sonnet-5`)

## How to read this log

Prompts are pasted verbatim, in the order they were given, under the task they relate to. Each entry will eventually be annotated (or summarized in the README/task docs) with:

- What the model produced from the prompt
- What I kept as-is, and what I rewrote or corrected myself
- What the model got wrong, and how I caught it against the running application

---

## Task 0 — Setup

**Prompt 1:**

```
Help me with the @qa-candidate-task.md - Lab40 Task .  I have created the @petclinic-qa-automation repository where all the artefacts for completion of the task should go. Start by adding a PROMPTS.md file where we will keep track of the prompts I provide you.

Go through the assignment file and tell me, do you think that the test plan from Task 1 should include as a scope, or in general Tasks 2,3 and 4, or should it be done in isolation?
```

---

## Task 1 — Test plan (Billing module)

**Prompt 2:**

```
The application is up and running and can be accessed as described in the @qa-test-automation-task/README.md.
I have explored the application and targeted the Billing module because of its business impact, financial risk it carries and complexity.

I have carried an exploratory testing of the Billing module and found the following 5 bugs:
Bug 1: Given the same invoice subtotal and the same Tax rate with a 100% discount test and 0% discount test, results in the same tax amount.
AR: Tax is calculated from the subtotal amount
ER: Tax is calculated from the taxable amount

Bug 2: An invoice can be overpaid.
ER: A payment exceeding the outstanding balance should not be rejected, or the excess should be explicitly handled as an account credit while the invoice is marked as fully paid.
AR: The payment is accepted, the invoice remains partially paid, and the user's balance becomes negative.

Bug 3: Was not able to reproduce the below. Not a front end issue, data is returned like this from the API It still counts as a reporting bug.
ER: Invoices INV-2024-0004 and INV-2024-0003 with status PAID have remaining balance 0
AR: Invoices INV-2024-0004 and INV-2024-0003 with status PAID have remaining balance different than 0


Bug 4: Tested with RBAC role of a former employee with status disabled. API response /api/auth/login returns enabled: false, but the user can still manipulate the lifecycle of an invoice.
ER: A deactivated account CAN NOT authenticate and use its token against Billing endpoints
AR: A deactivated account CAN authenticate and use its token against Billing endpoints

Bug 5: It should be a front end issue, since the API response for api/invoices?page=2&size=10 returns "last": true
ER: Next button is not active when there is no next page.
AR: Next button is active when there is no next page.

What comes to my mind regarding the sections of the test plan:
Assumptions and Out of scope - Responsive/mobile UI testing is out of scope since the application seems to be desktop/web oriented.
Assumption - there is no credit/overpay functionality in the application.
Risks - RBAC defect could be a compliance issue, wrong calculation of tax, discount and payment.

Explore the application as well and based on the gathered information scaffold an initial test plan focused on the Billing module.
```

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

**Prompt 3:**

```
I don't think you are right regarding the new bug you have reported. I tested the creation of an invoice with Discount %/discountPct and got the following responses/validation errors:
Negative value - {"field":"discountPct","message":"must be greater than or equal to 0.00","rejectedValue":-10}

Very big integer value - {"field":"discountPct","message":"must be less than or equal to 100.00","rejectedValue":1E+156}

from the validation responses it seems that the discount value should be in the range between 0.00 and 100.00.
Calculation of the discount amount itself seems to be correct.
Double check and remove all input related to the "New finding — discount amount is computed two orders of magnitude too small"
```

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

**Prompt 4:**

```
Regarding this part 
"5. Test approach

* API-first. Financial l..."  It sounds like I have done an API first testing, which I have not. I tested UI-first with API/network inspection of the requests and the responses. Rework test approach based on this information
```

**What the model got wrong:** §5 of the test plan claimed the exploratory testing methodology was
"API-first," inferred from the fact that the confirmed-defect writeups all cite raw API responses.
That inference was wrong — those API responses came from the candidate driving the UI and reading
the underlying requests/responses via browser network inspection (UI-first), not from crafting API
calls directly. Reworded §5 to describe UI-first testing with network-tab inspection as the actual
exploratory method, and separated that from the API being the more efficient *execution* surface
for the formal scenario writeups and Task 3 automation — those are two different things and the
first draft conflated them.

**Prompt 5:**

```
Since I did not validate the logic through direct API testing, I suggest remove this bullet in general. API testing will take place once Task 3 is reached
```

**What the model got wrong:** even after Prompt 4's rework, §5 still included a bullet claiming
the API was used as "the primary surface once a defect is identified" to pin down exact boundaries
(e.g. sweeping `discountPct` through 0/50/100) — implying the candidate had done direct,
independent API testing during Task 1 exploration. That conflated the model's own verification
work in this session (curl calls made to confirm the reported bugs before writing the plan) with
the candidate's actual Task 1 methodology, which was UI-only with network inspection. Removed the
bullet and reworded §5/§8 to state plainly that direct API testing is deferred to Task 3, not
something that happened here.

**Prompt 6:**

```
Remove the swagger confirmation as well "Swagger UI (/swagger-ui.html)...".
```

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

**Prompt 7:**

```
Bug and Defect are used interchangeable, do you think it is better to stick to Defect only?
```

**Prompt 8:**

```
Yes
```

Recommended standardizing on "Defect" (the standard QA term for a formally tracked document,
versus "bug" reading as informal shorthand), while keeping the existing 1–5 numbering so
traceability back to the candidate's original Bug 1–5 notes is preserved. On confirmation, swapped
every "Bug N" reference to "Defect #N" across the risk table's Evidence column and §5, and also
replaced the remaining generic uses of the word "bug" in prose (§7's "zero-bug" → "defect-free",
defect #3's "status-transition bug" → "status-transition defect", S7's "the bug is..." → "the
defect is...") for full consistency, since the candidate's ask was to stick to one term throughout,
not just in the numbered labels.

**Prompt 9:**

```
In the scope section, what does this mean, elaborate.

* API-level and UI-level behavior for the above, where both surfaces exist (the UI is a thin client over the same REST API per the app README).
```

Explained the bullet as a scoping decision (both API and UI surfaces are in scope for each listed
billing behavior, not a separate item to test), and the reasoning behind it (the app's React
front end sits on the Spring Boot REST API with no separate business logic of its own). Flagged
that "thin client" was the model's own characterization, not a literal quote from the README (which
only states "a React front end, a Spring Boot REST API and a PostgreSQL database"), and pointed out
Defect #5 as a live counterexample showing the two surfaces aren't fully redundant — the API is
correct there but the UI still gets it wrong. Offered to reword the bullet to make that nuance
explicit.

**Prompt 10:**

```
Ok, leave it as is
```

No changes made — candidate reviewed the explanation and confirmed the existing wording in
`test-plan.md` is fine as written.

**Prompt 11:**

```
Remove this part from point 8 - "Note — discount calculation checked and found correct. While..."
```

Removed the retraction note from §8 of `test-plan.md` (the paragraph explaining that the earlier
false "discount amount" finding had been checked and found correct). The correction itself
remains preserved here in `PROMPTS.md` (Prompt 3's entry above) as the AI-usage record required by
the assignment brief — removing it from the test plan just keeps that deliverable focused on
current, standing defects rather than a narrated history of a retracted false positive.

**Prompt 12:**

```
I approve the prioritizing of S1, S6, S8, S9, S15 scenarios, write them in full.
```

Wrote all five scenarios in full (preconditions, test data, steps, expected result) into a new
file, `task1-test-plan/scenarios-full.md`. Before writing, re-checked the OpenAPI schema for
`InvoiceItemRequest`/`PaymentRequest`/`CreateInvoiceRequest` to confirm field constraints
(`itemType`/`method` are free-text up to 20 chars, not fixed enums; `taxRate` is `0–1`,
`discountPct` is `0–100`) so the test data in each scenario is accurate. Each of the four
defect-targeting scenarios (S6, S8, S9, S15) states the correct/intended behavior as "Expected
Result" and the app's current (defective) behavior as a separate "Current Actual Result," so each
scenario doubles as a regression check once its defect is fixed — rather than writing the expected
result as "the bug happens," which would make the test case vacuous once the defect is corrected.
Also flagged, for S15, that reproducing Defect #3's exact balance discrepancy on fresh (non-seed)
data hasn't been done yet — carried forward as a follow-up rather than asserted as solved.

**Prompt 13:**

```
Regarding first scenario, there is no way to enter a due date/range. As is written right now leaves the impression that one should enter it. 

The `former.staff` seed account exists with role RECEPTIONIST and is deactivated (`enabled: false`), per the AUT README's accounts table. - What is AUT?
```

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

**Prompt 14:**

```
I think the section "The 5–8 fully-specified scenarios (pre..." could be removed from the test-plan now
```

Removed the now-stale forward-looking paragraph from §9 of `test-plan.md` (it described which
scenarios *would be* written up in full, but that's now done in `scenarios-full.md`). Replaced it
with a single-line pointer to `scenarios-full.md` so the test plan still tells a reader where to
find the full write-ups, rather than leaving no trace at all.

**Prompt 15:**

```
Summon a subagent to provide overall assessment of the test-plan and the scenarios-full against the qa-candidate-task. It should be an independent reviewer and report any critical gaps, questionable assumptions, provide any suggestions
```

Spawned a general-purpose subagent as an independent reviewer, with no prior context from this
conversation, given only the assignment brief (`qa-candidate-task.md`), `test-plan.md`, and
`scenarios-full.md`, and asked to assess the latter two against Task 1's stated requirements —
report critical gaps, questionable assumptions, and suggestions.

**Review findings (subagent report, condensed):**
- Critical gap: only 1 of 5 full scenarios was positive (S1); S3/S2 were shortlisted but never
  promoted, understating the required "mix of positive and negative."
- Critical gap: S15's test data depended on two specific seed invoices with an unknown repro path,
  conflicting with §3's own rule against depending on specific invoice IDs.
- Critical gap: S7/S10–S13's "already confirmed working" claims cited raw status codes
  (`400`/`422`/`403`) with no documented repro — an echo of the same model-verification-bleeding-
  into-candidate-methodology issue fixed earlier for §8's defects, this time in §9's one-liners.
- Questionable assumption: §3 claimed client/server clocks were "effectively UTC/consistent... per
  the README's TZ=UTC note," but the README only states the *API* is UTC — the UI renders in the
  browser's local timezone. Overstated the source.
- Internal inconsistency: Defect #5 was called "confirmed" in §1 and §4 but hedged as "not yet
  independently re-verified... carried forward as reported" in §8.

**Prompt 16:**

```
Add s3 and s2 to the full scenarios.
Remove s15 from the full scenarios.

s10 was initially tested in the browser by me with dev tools, there is indeed validation and the api returns 400 status code as per the response in dev tools.
s13 - add one liner how to be reproduced with API testing

regarding non-UTC browser, place it as an entry criteria browser to be set on UTC time and remove it from the assumptions.

regarding defect #5 it was initially confirmed by me, remove the "Not yet independently re-verified in-browser by me — carried forward as reported, to be confirmed in Task 2 UI automation setup." part

After the fixes make an agent check again
```

Actioned all six points:
- Added S2 (multi-item invoice totals) and S3 (partial payment → Paid) as full scenarios in
  `scenarios-full.md`, between S1 and S6, giving three positive + three negative scenarios total.
- Removed S15 entirely from `scenarios-full.md` (§8's defect #3 still has a one-line scenario
  reference in §9 of `test-plan.md`, just not a full write-up); updated the intro paragraph and
  `test-plan.md`'s pointer sentence accordingly (S1, S2, S3, S6, S8, S9).
- S10: reworded to state it was confirmed via the UI with the browser's network tab showing a
  `400 VALIDATION_FAILED` response, per the candidate's own testing — no longer an unattributed
  claim.
- S13: added a one-liner describing how to reproduce it directly via the API in Task 3
  (authenticate as `auditor`/`vet.carter`, send a billing write request, expect `403 FORBIDDEN`).
- Moved the UTC-browser requirement out of §3 Assumptions and into §6 Entry criteria as an explicit
  precondition, with the corrected sourcing (API is UTC, UI renders in local browser timezone).
- Removed the hedge from defect #5's write-up in §8, since the candidate confirmed it directly —
  resolves the §1/§4/§8 inconsistency the review flagged.

A second independent review subagent (fresh, no shared context with the first) was then spawned to
verify the fixes. Result: all 5 previously-flagged issues confirmed FIXED, no regressions. One new
finding surfaced: §7 Exit Criteria still says "every confirmed defect has a corresponding scenario
demonstrating it, filed with repro steps," but Defect #3 no longer has one after S15's removal —
S3 explicitly disclaims reproducing it ("it doesn't reproduce that defect"). The reviewer also
noted §10 Open Questions doesn't capture the underlying question of whether Defect #3 is even
reproducible from a fresh invoice, only from the two specific seed records. Not yet acted on —
pending candidate direction.

**Prompt 17:**

```
I tried to reproduce the Defect 3 when found the defect in the first place by mimicking the same data in the invoices with wrong amount, but I was not able to reproduce it.

Can you return the s15 to the full scenario and make it not lean towards the specific invoices, but rather towards data integrity? I don't want to weaken the criteria.
```

New information from the candidate: they had already attempted to reproduce Defect #3 on fresh
data (matching subtotal/tax/discount/payment amounts) when they originally found it, and could
not. That resolves the open question about reproducibility with an actual negative result, rather
than leaving it unknown.

Re-added S15 to `scenarios-full.md`, reframed as a system-wide sweep ("every Paid invoice has a
zero balance") over whatever invoices the "Paid" filter currently returns, rather than a lookup of
the two specific seed invoice numbers — this satisfies both the candidate's requirement (§7's exit
criteria stays exactly as-is, not softened) and the second review's concern (§3's own rule against
depending on specific invoice IDs). Documented the candidate's failed reproduction attempt directly
in the scenario's "Current actual result" section and in defect #3's write-up in §8, and added a
corresponding open question to §10 laying out candidate root causes (legacy/migration data
artifact, an untried payment sequence, or an unisolated rounding edge case) for follow-up once
Task 3's direct API access makes sweeping payment sequences cheaper than doing it through the UI.
Updated `test-plan.md`'s §9 one-liner and full-scenario pointer sentence accordingly (now seven
scenarios: S1, S2, S3, S6, S8, S9, S15).

**Prompt 18:**

```
Initiate another independent review
```

Spawned a third independent review subagent, fresh/no shared context, to re-check the current
state of `test-plan.md` and `scenarios-full.md` against the brief.

**Round 3 findings:** the S15/§7 fix confirmed FIXED and complete — S15 correctly reframed as a
system-wide sweep, the failed reproduction attempt documented consistently in three places, §7
left unchanged as required, §9 correctly lists all seven scenarios. No new methodology
bleed-through found (specifically re-checked for this, since it had recurred multiple times
before). Three minor items:
1. Flagged S1's claim "verified: issuing on 8/20/2026 set the due date to 9/19/2026" as
   suspicious, since it matches "today's date" and the reviewer couldn't confirm its provenance.
   **Resolved, not a defect**: this was the model's own direct browser verification from earlier
   in this session (creating and issuing a real invoice, INV-2026-0036, and reading the result off
   the page) — already disclosed in this file under Prompt 13. The date match is expected, since
   the test genuinely ran today.
2. Typo in §10: "a intended" → "an intended". Fixed.
3. §4's risk table still shows "Confirmed, Defect #3" without the reproduction-failure caveat
   that S15/§8/§10 now carry. Left unchanged — the defect is still genuinely confirmed on real
   data; the caveat is about root cause, not confirmation status, and the reviewer itself called
   this choice defensible.

This closes the review loop for Task 1's deliverables. No outstanding findings remain unaddressed.

**Prompt 19:**

```
I have these in the scenario list

* `VALIDATION_FAILED` response — kept as a regression guard, not because it's currently broken.
* S11 — Line items cannot be added/removed once an invoice is issued. Already confirmed working (`422 INVOICE_NOT_EDITABLE`) — regression guard.
* S12 — A voided invoice rejects further payment. Already confirmed working (`422 PAYMENT_NOT_ALLOWED`) — regression guard.
* S13 — READONLY and VET roles cannot perform billing write actions. Already confirmed working (`403 FORBIDDEN`) — regression guard, and scopes how narrow defect #4 actually is. Reproducible directly via API in Task 3: authenticate as `auditor` or `vet.carter` to obtain a token, then send a billing write request (e.g. `POST /api/invoices/{id}/payments` or `/void`) with that token and confirm a `403 FORBIDDEN` response.

And I have this in my Test approach

* Direct API testing (crafting and sending requests outside the UI) was not part of this exploration — defects were identified through the UI and confirmed by reading the network requests/responses the UI itself made, not by independently driving the API. Isolating exact boundaries via direct API calls (e.g. sweeping `discountPct` through 0/50/100, replaying a payment with varied amounts) is deferred to Task 3.

Is this a contradiction?
```

**What the model got wrong:** confirmed a real, still-unfixed instance of the same misattribution
pattern caught earlier for defects #3/#4 and the original S10 — S11 and S12's exact status/error
codes (`422 INVOICE_NOT_EDITABLE`, `422 PAYMENT_NOT_ALLOWED`) came from the model's own curl
testing during this session, not from the candidate's UI-first methodology stated in §5, and there
was no confirmation the candidate had independently observed the same codes via the UI. Not yet
fixed in the files at this point — the candidate asked for verification tooling first (Prompt 20)
before deciding how to resolve it.

**Prompt 20:**

```
I want to verify s11, s12 and s13. Provide me ready to execute requests/curls with tokens
```

Provided ready-to-run bash scripts (login for `reception`/`auditor`/`vet.carter`, then one
self-contained block per scenario creating its own fresh invoice) so the candidate could
independently verify S11, S12, and S13 themselves rather than relying on the model's earlier
testing. No files were changed by this exchange — verification is the candidate's own action,
pending their results.

**Prompt 21:**

```
I have verified the API calls.
Can you change this line now "Direct API testing (crafting and sending requests outside the UI) was not part of this exploration —"
State that API testing was indeed a part of the exploration (mainly done by you). Check if changing this statement will rise any contradictions anywhere
```

Candidate confirmed they ran the S11/S12/S13 verification scripts themselves and got the expected
status codes. Changed `test-plan.md` §5 to state that direct API testing was in fact part of the
exploration — mainly performed by the AI assistant during this session, with S11/S12/S13
specifically re-verified directly by the candidate — framed as targeted, ad hoc verification of
specific findings, distinct from Task 3's future systematic/automated API coverage.

Grepped both `test-plan.md` and `scenarios-full.md` for every place that previously asserted "no
direct API testing" or "deferred to Task 3" to check for contradictions before finalizing. Found
and fixed two: §8's intro paragraph, which explicitly said defects were confirmed "not by
independently crafting API calls" (directly contradicted the new §5 statement) — reworded to say
exact values were "additionally pinned down via direct API testing performed during this
collaborative pass." And §9's S11/S12/S13 one-liners, which previously asserted their status codes
(`422`, `403`) with no attribution — reworded to "Confirmed directly via API (independently
re-verified by the candidate)" for all three, and removed S13's now-inaccurate "reproducible... in
Task 3" framing since it's already been done, not merely planned. No changes needed in
`scenarios-full.md` — its seven full scenarios (S1, S2, S3, S6, S8, S9, S15) are all genuinely
UI-driven and don't claim otherwise, so its "consistent with §5" framing still holds. §10's mention
of "Task 3's direct API access" was left as-is since it's about future systematic sweeping, not a
claim that Task 1 lacked API access.

**Prompt 22:**

```
I think it is too verbatim to state what was tested by the "candidate"/me and you. Make the whole file sound like one person did it, your assistance is tracked in the prompts file, it is enough.
```

Removed all "mainly performed by the AI assistant" / "independently re-verified by the candidate"
attribution from `test-plan.md` §5 and S11/S12/S13 — the who-did-what split belongs in this file
(PROMPTS.md), not scattered through the deliverable itself. The test plan now just states what was
tested and how (UI-first exploration, direct API testing for boundary conditions), in one voice,
matching how a real single-author test plan reads. Checked `scenarios-full.md` for the same
pattern — already clean, no changes needed there.
