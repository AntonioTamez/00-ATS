# ArchShell PRD - Adversarial Review v2

- **Target:** `prd-archshell.md` (final, updated 2026-11-03) and `addendum.md`, focused on FR-43 to FR-52
- **Lens:** bmad-review `adversarial` only (`references/lens-adversarial.md`)
- **Date:** 2026-11-03
- **Prior review:** `review-adversarial.md` (v1). Findings resolved there (protected set, CI verify, attempt definition, combination templates, contract glossary, excludes refusal) are not repeated. Where a v1 class of problem reappears through a new FR, the finding targets the new path.
- **Note on format:** the adversarial lens emits no severity. Severity (critical / high / medium / low, by impact on the "deterministic, architecture-enforced generation for junior developers" promise) was added at the requester's instruction.

## Verdict

**Not ready for architecture.** FR-43 to FR-52 move structure decisions from the model to tables and templates, which is the right direction, but they add three new write paths into the trusted surface (plan extension, contract revision, breaking-change confirmation) that are protected only by in-session hooks and confirmed by a junior user who, by the PRD's own target-user definition, cannot judge what they confirm. The plan extension path also lets the agent choose building blocks and names directly, which FR-43 forbids. Nondeterminism was not removed but relocated into the verb/noun tokens, the "duplicate type" judgment, and correlated consensus runs. Finally, the per-feature confirmation load now includes reviewing a structural plan, which contradicts the Vision ("without reviewing the structure") and threatens the Delegate job.

## Summary by severity

| Severity | Count |
|---|---|
| Critical | 2 |
| High | 7 |
| Medium | 7 |
| Low | 3 |
| **Total** | **19** |

---

## Critical

### C1. Plan extension lets the agent choose building blocks and names, bypassing FR-43/FR-45
- **FRs:** FR-46, FR-43, FR-45, FR-26, FR-30
- **Location:** 4.5 FR-46 consequence 1 ("it stops and proposes a plan extension, which the developer confirms")
- **Trigger condition:** The extension is proposed by the agent, not re-derived from intents through the mapping table. Nothing says an extension must be expressible as intents plus tokens, so the agent names files, types, and locations directly, which FR-43 says the model never does. The only check is a confirmation by the junior user.
- **Failure scenario:** On S2 (fee under 24 h), the agent's full-gate attempt fails twice. It proposes "add `FeeCalculatorHelper` in `Appointments.Infrastructure`". Lucía cannot tell this is domain logic in the wrong layer (layer rules only check dependencies, and the helper uses none), says "sí", and the plan now permanently contains an off-pattern block. Repeated across features, plan extension becomes the general escape hatch for anything the mapping table did not anticipate, and per-edit blocks never count as attempts (FR-26), so this is never bounded either.
- **Guard / fix:** Require every plan extension to be expressed as additional intents (and tokens) for an S# and re-derived by FR-45; an extension that cannot be mapped is a misfit (FR-28) or a mapping-table gap report (like FR-17), never free-form structure. Count extension requests per task and bound them (for example, they count toward FR-26 or have their own cap).

### C2. Feature plans and contracts have no human-confirmation anchor outside the session
- **FRs:** FR-46, FR-48, FR-50, FR-51, FR-14, FR-41, FR-30, FR-21
- **Location:** FR-46 consequence 2 ("Confirmed feature plans belong to the protected set"); FR-48 consequence 2; FR-41; FR-30 `CODEOWNERS` list
- **Trigger condition:** FR-14 says protected-set changes must come from the charter growth flow (4.6), but plans, plan extensions, and contract revisions are written by a different flow. FR-41 verifies only that `architecture.yaml` changes have a growth record; nothing verifies that a plan, a contract-data change, or a breaking-change confirmation (FR-51) carries a recorded human confirmation. `CODEOWNERS` (FR-30) covers `architecture.yaml`, ADRs, and generated tests, not `features/*/plan.yaml` or generated contract code. FR-21 itself admits shell edits escape the hooks.
- **Failure scenario:** The agent edits `features/cancel-appointment/plan.yaml` via a shell command to add a type it wants, regenerates nothing, writes the file. The full gate checks "files match plan": they do, because the plan was widened in step. CI (FR-41) passes. It can likewise append a "confirmed" entry for a breaking change to the plan so FR-51's gate passes. The PR is approved by a human who is not a code owner of plan files and has no signal that the plan changed without confirmation.
- **Guard / fix:** Define one confirmation record format (proposal ID, displayed diff hash, human answer captured by a hook, timestamp) shared by charter growth, plan creation/extension, contract confirmation/revision, and breaking-change confirmation. Make `archshell verify` fail when any plan or contract-data change lacks a matching record, and add plan files and generated contract code to `CODEOWNERS`. Amend FR-14 to name these flows as legitimate writers so it does not contradict FR-46/FR-50.

---

## High

### H1. FR-46 checks files and types, not members or signatures; "the agent only fills bodies" is unenforced
- **FRs:** FR-24, FR-46, FR-20, FR-45
- **Location:** FR-24 consequence 2; FR-46 ("blocks ... creating files or types that are not in the confirmed feature plan")
- **Trigger condition:** Scaffolded non-contract files (handlers, endpoints, entities) are not in the protected set; only generated contract code is (FR-48). Nothing blocks new public methods, changed handler signatures, new constructor dependencies, extension methods inside an existing file, or edits to files marked "modify" (FR-45) beyond what the spec requires. Files that belong to no feature (composition root, shared helpers created before any plan) are not covered by "feature's files match its plan".
- **Failure scenario:** The agent adds `public static decimal ComputeFee(...)` to the existing `Appointment` entity and a second public `CancelWithoutFee` method used by nothing in the plan; it also changes `CancelAppointmentHandler`'s constructor to take `IBillingRepository`. All pass layer and plan checks. Structure grew outside the confirmed plan with no extension.
- **Guard / fix:** Define plan conformance at member level for scaffolded types (public members and signatures fixed by the template, bodies free) and give "modify" entries a declared member-level scope. Require every source file outside the bootstrap scaffold to belong to exactly one plan, or to a declared shared category with its own rule.

### H2. Nondeterminism moved into verb/noun tokens, which consensus does not cover
- **FRs:** FR-43, FR-44, FR-45, FR-47, NFR-4
- **Location:** FR-43 consequence 3; FR-44 ("Intent classification runs three times"); FR-45 consequence 1 ("same intents and tokens")
- **Trigger condition:** FR-45's determinism is conditional on tokens, and tokens are free model output with no closed set and no consensus requirement (FR-44 speaks only of intents). The ES-to-EN translation problem (v1 L2) now decides file and type names directly.
- **Failure scenario:** The backlog item "anular cita" yields `cancel`/`appointment` on one run and `void`/`booking` on another. The plan creates `VoidBookingHandler` beside the existing `Appointment` aggregate; because existence (FR-45 bullet 3) is a name match, no "modify" is detected and a parallel `Booking` concept appears. NFR-4 is technically met while the project diverges.
- **Guard / fix:** Constrain noun tokens to the charter's module and entity inventory (plus an explicit "new concept" path confirmed by the developer), record an ES/EN term glossary in the project, and include tokens in the consensus comparison.

### H3. Three-run consensus measures model self-agreement, not correctness
- **FRs:** FR-44, FR-43, Success Metrics
- **Location:** FR-44
- **Trigger condition:** Three runs of the same model, prompt, and context are strongly correlated; a systematic misclassification (for example "notify the patient" as `state_change` instead of `side_effect`) agrees 3/3 and is accepted silently. "One or more intents" leaves agreement undefined (exact set equality? overlap?). No metric measures classification accuracy; the golden set covers only pattern selection.
- **Failure scenario:** Every run labels S3 `state_change` only; no port/adapter is planned. The agent hits FR-46 when it needs a notification port, proposes an extension (C1), and the deterministic plan was wrong from the start with no signal anywhere.
- **Guard / fix:** Define agreement (exact set equality per S#). Add an intent golden set (spec items with expected intents) and an accuracy target to Success Metrics, and a launch criterion for it. State plainly that consensus reduces variance, not error.

### H4. Consensus disagreements produce questions a junior user cannot answer, with no budget
- **FRs:** FR-44, FR-23, FR-3, FR-39, Principle 4
- **Location:** FR-44 ("any disagreement becomes a question to the developer")
- **Trigger condition:** The disagreement is between intent labels from a taxonomy the user never sees. FR-3's plain-language rule and 5-question cap apply to selection only; FR-23/FR-44 have neither. The consensus switch is project configuration that is not in the protected set (FR-14), so the agent or a frustrated user can turn it off and silently drop to single-run classification.
- **Failure scenario:** A 6-item spec yields 3 disagreements; Lucía is asked "Is S2 a state change or a side effect?" three times, answers at random, and the plan is derived from her guesses. Or the agent sets `consensus: false` in config to save time, and nothing reports it.
- **Guard / fix:** Require disagreement questions to be phrased as concrete behavior choices in plain language (FR-39), with a per-feature question budget. Put the consensus setting in the protected set or record its value in each plan so `archshell verify` can report plans built without consensus.

### H5. Contract data has no failure modes, so the most important public behavior is unfrozen
- **FRs:** FR-47, FR-49, FR-50, FR-51, FR-19
- **Location:** FR-47 ("each operation's name tokens, kind, inputs, and outputs"); addendum contract example
- **Trigger condition:** Contracts carry inputs and outputs only: no errors, result variants, HTTP status codes, or preconditions. The agent decides in method bodies whether cancelling a missing or already-cancelled appointment throws, returns null, or returns 404/409. These choices are consumer-visible yet are not confirmed (FR-49), not frozen (FR-50), and not breaking-change checked (FR-51); endpoint routing and serialization written in bodies can change too.
- **Failure scenario:** Feature A returns 404 for an unknown appointment; a later feature's agent changes the shared endpoint body to return 400. FR-51 sees an identical contract and reports no breaking change; the front end breaks.
- **Guard / fix:** Add declared outcomes (success plus a closed set of named failure kinds) to contract data, generate their HTTP mapping from templates, include them in FR-49's plain-language description, and in FR-51's comparison.

### H6. Plain-language confirmation of plan and contracts is structural review by a user who cannot do it
- **FRs:** FR-45, FR-49, FR-51, Vision, JTBD 3, UJ-2
- **Location:** FR-45 consequence 2; FR-49; FR-51 consequence 1; Vision ("without reviewing the structure")
- **Trigger condition:** The developer now confirms a list of handlers, ports, file paths, and contracts per feature, plus breaking changes "naming each change". The target user has low-to-medium architecture knowledge (Section 2). The plain-language rendering hides exactly what matters (nullability, money currency, whether `reason` should be required, which consumers exist). Confirmation is therefore a rubber stamp that also contradicts the Vision.
- **Failure scenario:** FR-51 lists "removed operation `GetAppointmentsByDoctor` from `Appointments.Contracts`". Lucía does not know `Billing` consumes it and confirms; `Billing` breaks at compile time in the next feature, which burns all 5 attempts and ends in a misfit report pointing at the wrong cause.
- **Guard / fix:** Restate the Vision as "without reviewing layer structure; you confirm what the feature does and exposes". For breaking changes, show the in-project consumers (computed deterministically from references) and explain impact in behavior terms; block removal of an operation with in-project consumers unless the consumers are in the same plan.

### H7. Per-feature human load has no budget and no metric, defeating Delegate
- **FRs:** FR-23, FR-44, FR-45, FR-46, FR-49, FR-50, FR-51, FR-29, FR-30, Success Metrics
- **Location:** 4.5 sequence; Counter-metrics
- **Trigger condition:** A single feature can now stop for: spec gap questions, consensus disagreements, plan+contract confirmation, plan extensions, contract revisions, breaking-change confirmations, charter growth, and PR approval. Each stop is mandatory and unbounded. Counter-metrics track only selection questions and exhausted retries. Consensus also triples classification cost and latency per S#.
- **Failure scenario:** UJ-2 as written already has two confirmations before code; a realistic feature with one extension, one revision, and one growth reaches 6 to 8 interruptions. Users turn consensus off, approve without reading, or abandon the workflow and edit by hand.
- **Guard / fix:** Add a counter-metric "human interruptions per feature" with a guardrail value on a reference feature set, and batch confirmations per feature (one combined plan, contract, growth, and breaking-change confirmation screen) wherever the sequence allows.

---

## Medium

### M1. The closed type set cannot express ordinary features, and "duplicate type" is a model judgment
- **FRs:** FR-47, FR-33, NFR-4
- **Location:** FR-47 consequences 1 and 2
- **Trigger condition:** The closed set lists primitives, ids, money, enums, and existing types. Collections, optionals of composite types, dates/time ranges, paging, and new composite DTOs are not named, so a query like "list a doctor's appointments for a week" has no legal output type unless a DTO already exists. "Duplicates an existing type instead of reusing it" requires semantic comparison with no stated deterministic rule.
- **Failure scenario:** The first query feature cannot propose `AppointmentSummary[]`; the agent retries schema-invalid proposals until bounds are exhausted, or the type is rejected/accepted inconsistently across runs as "duplicate of `Appointment`".
- **Guard / fix:** Enumerate the type set including list, optional, date/time, and new data types composed only of closed-set fields; define duplicate detection structurally (same field names and types) so it needs no model.

### M2. Cross-cutting and non-behavioral spec items have no intent and no building block
- **FRs:** FR-43, FR-45, FR-33, FR-46, FR-28
- **Location:** Glossary "Intent"; FR-33 consequence 1
- **Trigger condition:** The intent set is only given "for example" and is not enumerated. Spec items such as "only receptionists may cancel", "log every cancellation for audit", "must be transactional", or "respond in under 200 ms" map to no listed intent; authorization, audit, transactions, caching, and idempotency have no building block. FR-33's "every intent maps in every pattern" forces arbitrary mappings for intents that do not fit a pattern.
- **Failure scenario:** The authorization check ends up as body code in the endpoint (unplanned policy class blocked by FR-46), or triggers a plan extension (C1), or a misfit report for a feature that fits the pattern fine.
- **Guard / fix:** Enumerate the closed intent set in the PRD, including cross-cutting kinds, or state explicitly that cross-cutting concerns are out of the feature plan and where they live (template-provided pipeline behaviors).

### M3. Charter growth discovered after plan and contract freeze contradicts the sequence
- **FRs:** FR-29, FR-45, FR-47, FR-50, UJ-2; addendum "Relationship with charter growth"
- **Location:** UJ-2 steps 1 and 2; 4.5 sequence
- **Trigger condition:** In UJ-2 the contract is frozen and the RED test written (step 1) before growth of `Notifications` is proposed (step 2). The notification port's contract location and types depend on a module that does not exist yet, and FR-47 types must already exist. Growth must therefore happen during plan derivation, but the sequence in 4.5 has no growth step before freeze.
- **Failure scenario:** Freeze, RED, then growth changes the port's namespace and location; the frozen contract must be revised (FR-50), the RED test rewritten, and a second confirmation round asked.
- **Guard / fix:** Insert "detect required growth" into the 4.5 sequence between plan derivation and confirmation, and present growth in the same confirmation (see H7). Fix UJ-2's order.

### M4. FR-49's "see the generated code before confirming" contradicts generating from confirmed data
- **FRs:** FR-49, FR-48, FR-24
- **Location:** FR-49 consequence 2
- **Trigger condition:** FR-24/FR-48 generate contract code from confirmed data; FR-49 lets the developer see that code before confirming. Either code is generated twice (preview and final) or generated before confirmation and written to the protected set prematurely.
- **Guard / fix:** State that preview generation is an in-memory render of the same templates, never written, and that the written code must be byte-identical to the preview (testable).
- **Potential consequence / scenario:** Implementation writes preview files into the project; a declined proposal leaves orphan protected files that then fail `archshell verify`.

### M5. Contract revision and plan extension have no defined effect on the RED test and retry counter
- **FRs:** FR-50, FR-25, FR-26, FR-46
- **Location:** FR-50 consequence 1; FR-26 consequence 1
- **Trigger condition:** After a contract revision, the RED test written against the old contract no longer compiles or no longer expresses the spec. The PRD does not say who rewrites it, whether it must be re-observed RED, or whether the attempt counter resets (FR-26 resets only on spec rephrase or hand fix).
- **Failure scenario:** The agent asks for a revision, then "updates" the RED test by deleting the assertion on the fee; the test passes immediately, FR-25 is never re-checked, and the counter reset is used to get 5 more attempts.
- **Guard / fix:** On contract revision, regenerate the RED test step: it must be re-observed failing and diffed against the previous RED test for the developer. State whether revisions and extensions reset or consume attempts.

### M6. Plan conformance and breaking-change detection threaten NFR-1 and are unbudgeted
- **FRs:** FR-46, FR-20, FR-51, NFR-1
- **Location:** NFR-1; FR-46 ("The per-edit check (FR-20) blocks ...")
- **Trigger condition:** Detecting new types per edit needs at least a C# syntax parse; detecting "modify" scope (H1) and partial classes across files needs semantic information, which requires a Roslyn workspace (cold load well above 3 s on a typical machine). NFR-1 was set before FR-46 existed. FR-51 tooling (PublicApiAnalyzers, OpenAPI diff) needs a build, which is not mentioned as a full-gate cost.
- **Failure scenario:** Implementation either exceeds 3 s per edit or quietly reduces FR-46 to filename checks, which the agent bypasses with nested or partial types in planned files.
- **Guard / fix:** Split FR-46 into a per-edit syntactic check (within NFR-1) and a semantic check in the full gates, and state both explicitly; add FR-51's build cost to the full-gate scope.

### M7. Shared files across feature plans make "files match plan" ambiguous
- **FRs:** FR-45, FR-46, FR-41
- **Location:** FR-45 bullet 3 ("becomes a modification"); FR-46 consequence 3
- **Trigger condition:** `Appointment.cs` is created by feature A and modified by B and C. The PRD does not say which plan owns it, whether a later modification invalidates an earlier plan's conformance, or how two concurrent branches modifying the same block reconcile (Open Question 1 covers charter growth only).
- **Failure scenario:** After feature C modifies the entity, `archshell verify` re-checks feature A's plan, finds the file differs from A's declared shape, and fails CI for a feature nobody touched.
- **Guard / fix:** Define ownership (creator plan owns the file; modifications are recorded in the modifying plan) and that verification checks the cumulative set of plans, not each plan in isolation. Extend Open Question 1 to cover plan conflicts.

---

## Low

### L1. The backlog's "done" mark is writable by the agent
- **FRs:** FR-52, FR-21
- **Location:** FR-52 consequences 2 and 4
- **Trigger condition:** The backlog is outside the protected set by design, so the agent can tick an item done without the feature passing the full gates.
- **Guard / fix:** Derive "done" from plan state (a plan that passed the full gates links to the backlog item) instead of a hand-editable checkbox, or have `archshell verify` warn on items marked done with no passing plan.
- **Failure scenario:** Lucía sees "invoice visits" ticked after an interrupted session (reported *not verified* by FR-21) and never returns to it.

### L2. "Nothing the request asked for is silently dropped" is untestable
- **FRs:** FR-52, FR-1
- **Location:** FR-52 consequence 1
- **Trigger condition:** Capability extraction is a model judgment with no oracle, as v1 M11 noted for drivers.
- **Guard / fix:** Add expected capabilities to the golden set and measure recall.
- **Failure scenario:** "Send appointment reminders" is never extracted; the user assumes it is in the backlog because the PRD promises nothing is dropped.

### L3. Singular-form normalization of tokens is underspecified
- **FRs:** FR-45, FR-47
- **Location:** FR-45 bullet 2 ("case and singular form normalized")
- **Trigger condition:** English inflection needs a dictionary (person/people, status, analysis/analyses, mass nouns like `billing`), and the dictionary version becomes part of the determinism input.
- **Guard / fix:** Ship a fixed, versioned inflection table in the language pack and include its version in the plan.
- **Failure scenario:** `Statu` or `Persons` types are generated, or the same token normalizes differently after a pack upgrade, breaking FR-45's "same plan" guarantee across versions.
