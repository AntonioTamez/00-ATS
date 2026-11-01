# PRD Quality Review — ArchShell PRD

## Overall verdict
The PRD has a clear, specific thesis: selection, then a machine-readable charter, then generated tests, then blocking gates on agent output. Its principles, non-goals and single protagonist all serve that thesis, and the deliberate choices (no adoption metrics, greenfield only, immutable pattern) are stated as decisions rather than hedged. What puts it at risk for a public launch is done-ness and enforcement plumbing. The primary metric cannot be measured as written. Tamper protection (FR-14) has no mechanism, and with CI mode out of scope it has no place to run. Roughly 40% of the FRs have no testable consequence. There is no NFR section at all: no latency, platform, prerequisite or cost bounds. The launch acceptance criteria are much weaker than the scope they gate.

## Decision-readiness — adequate
The big bets are stated plainly and owned. Principle 6 says "Never relax a rule". The Non-Goals list "Changing the chosen pattern" and "Functional correctness". The Success Metrics open with "Adoption metrics are intentionally not tracked." The two `[NOTE FOR PM]` callouts (FR-8 override vs `excludes`, FR-17 solo developer with no tech lead) sit at real tensions, not safe checkpoints. A skeptic pushing on "what if the pattern turns out wrong" will find FR-28 and the Non-Goal. That objection is answered.

Two tensions are not acknowledged. First, immutability (FR-31) plus tamper protection that blocks the developer too (FR-14: "whether made by the developer or the agent") contradicts FR-8's "The developer has the final word". Once the charter is written, the developer has no final word on anything except growth. Second, the cost of immutability is never named. The PRD says what was chosen but not what the user gives up: no exit, eject or uninstall path for a project that outgrows its pattern. Since the user deliberately chose immutability, the point is not to reverse it. The point is to state its price so a decision-maker sees it.

### Findings
- **high** Developer authority contradicts tamper protection (§ FR-8 vs FR-14, FR-31) — FR-8 says "The developer has the final word", but FR-14 blocks charter edits "whether made by the developer or the agent", and FR-31 locks the pattern forever. The PRD never reconciles who holds final authority after the charter exists. *Fix:* State it outright, for example: "After the charter is written, the developer's authority is limited to approving growth (FR-30); manual charter edits are blocked by design." Add a `[NOTE FOR PM]` on whether a deliberate developer bypass or eject exists.
- **medium** Cost of immutability not stated (§ Non-Goals, FR-28) — "the developer must reshape the feature to fit" is the only path. There is no statement of what happens to a project whose domain legitimately changes (stay forever, or remove ArchShell?). *Fix:* Add one Non-Goal or trade-off line naming the given-up capability and the exit path, even if that path is "remove ArchShell; the code remains a plain .NET solution."
- **low** Open Question 3 is a task, not a question (§ Open Questions #3) — "The design README needs updating…" has a known answer. *Fix:* Move it to a follow-up or doc-maintenance list.

## Substance over theater — strong
There is one persona, Lucía, and she drives concrete decisions: plain-language questions (FR-3), Spanish conversation (FR-39), and the "review only the fee logic" payoff in UJ-2 step 6. The Vision is specific to this product: "hand full features to an AI agent without reviewing the structure" could not be swapped into another PRD. No differentiation section was written to fill a template. Competitive positioning is kept in the addendum with honest "(inference)" labels. NFR theater is absent, but only because NFRs are absent (see Done-ness).

### Findings
- **low** Internal authoring path leaks into a public PRD (§ FR-40, Success Metrics) — `02-ARQUITECTURA` and `02-ARQUITECTURA/*/sample` mean nothing to an outside contributor. *Fix:* Name the concept ("the authoring catalog repository") and keep the path in the addendum.

## Strategic coherence — adequate
The thesis is coherent, and the primary metric ("Zero deviations") validates it directly rather than measuring activity. Counter-metrics exist and are well chosen: "Exhausted retries… defeats the Delegate job" is exactly the right tension to watch. The MVP has a clear problem-solving scope kind.

There are two weak points. First, the addendum states "Defensibility lies in enforcement depth and .NET template quality, not pattern-selection questions", yet MVP scope commits to breadth: "templates for all 18 catalog patterns" (FR-15, MVP Scope). The selector can also propose combinations (FR-6) that have no template (FR-17). For the stated target user, a junior working alone, that is a dead end (Open Question 2). Breadth-over-depth pulls against the thesis. Second, none of the metrics has a target value.

### Findings
- **critical** Primary metric is unmeasurable as defined (§ Success Metrics, Primary) — "0 features pass the gates while violating `architecture.yaml`" can only be detected by something other than the gates, and the gates are generated from that same `architecture.yaml` (FR-18). No independent audit, corpus or method is named, so the metric will always read zero. *Fix:* Define the measurement: for example, a seeded violation corpus (N known-violating changes per rule type, per pattern) that must be 100% rejected, plus a rule-to-test coverage check that every `architecture.yaml` rule has at least one failing-on-violation test.
- **high** Secondary and counter-metrics have no targets (§ Success Metrics) — "Correct selections" has no accuracy bar, and "Unnecessary questions" and "Exhausted retries" have no thresholds. FR-4 says thresholds are "calibrated against the golden set", but calibrated to what? *Fix:* Set launch targets, for example "≥ 90% correct or asked-a-question on the golden set, 0 confident wrong selections; exhausted-retry rate ≤ X% on the reference feature set."
- **medium** Breadth of 18 templates vs depth thesis (§ MVP Scope, FR-15, addendum Competitor risk) — Shipping 18 patterns multiplies the template-quality surface the addendum calls the moat, while combinations still fall through to gap reports. *Fix:* Either justify 18 at launch, or scope launch to the patterns and combinations the selector can propose and scaffold end to end, with selection restricted to scaffoldable outcomes.

## Done-ness clarity — thin
The strongest FRs (FR-3, FR-4, FR-14, FR-17, FR-27) have crisp, testable consequences. But many FRs have no Consequences block at all: FR-6, FR-7, FR-10, FR-12, FR-13, FR-16, FR-19, FR-20, FR-23, FR-24, FR-25, FR-30, FR-33, FR-34, FR-36, FR-38 and FR-39. Some are self-evidently testable (FR-7, FR-12, FR-30). Others hide real ambiguity, and story creation will hit it immediately.

Several FRs carry vague bounds: FR-1 and FR-35 say "bounded number of times", FR-20 says "fast checks", and FR-3 says "the question that best separates". FR-4 uses "no critical driver `unknown`" but never defines which drivers are critical.

There is no NFR section. For a tool that runs hooks on every agent edit, these unstated bounds decide whether the product is usable:
- latency per edit and per full gate run
- supported OS
- .NET SDK and Claude Code versions
- prerequisites (the addendum says the core is Python, per D1, which is a non-obvious install dependency for .NET developers)
- token/cost overhead

### Findings
- **critical** Tamper protection has no mechanism or boundary (§ FR-14, MVP Out of Scope "CI mode") — FR-14 must detect changes "whether made by the developer or the agent" that "do not come from the charter growth flow". But detection only runs inside Claude Code hooks, and CI mode is out of scope. A developer editing `architecture.yaml` in an IDE and committing bypasses it entirely, and FR-14 does not define how "comes from the growth flow" is recognized (signature? hash? lockfile?). As written, this FR cannot be built or tested. *Fix:* State the detection point (for example, every ArchShell session start and gate run verifies a charter hash written by FR-32) and the honest boundary: "edits made outside ArchShell are detected at the next ArchShell run, not prevented."
- **high** No non-functional requirements (§ missing section) — There are no bounds on per-edit check latency (FR-20), full-gate duration (FR-21), supported platforms, toolchain versions, the Python runtime prerequisite, or agent cost per feature. *Fix:* Add an NFR section with thresholds, for example "per-edit check p95 ≤ 2 s on the reference solution; runs on Windows/macOS/Linux; .NET 8+; documented prerequisites."
- **high** Launch Acceptance Criteria are far weaker than the launch scope (§ Launch Acceptance Criteria) — "three sample requests" vs a 15–20 request golden set; one "deliberately layer-violating feature". There is nothing on the 18 templates compiling with green tests, nothing on tamper protection (FR-14), growth (FR-29–FR-32), the exhaustion report (FR-27), or Spanish conversation (FR-39). *Fix:* Make acceptance mirror the scope: golden-set target met, all 18 templates scaffold, compile and pass their own architecture tests, the seeded violation corpus is 100% rejected, and one growth and one exhaustion scenario run end to end in both EN and ES.
- **medium** "Critical driver" undefined (§ FR-4) — The automatic-proposal rule depends on "no critical driver `unknown`", but FR-1 lists seven drivers and never marks which are critical. *Fix:* List the critical drivers, or state that criticality is per pattern card and add it to the Glossary.
- **medium** Retry accounting ambiguous (§ FR-20, FR-26) — It is unclear whether a per-edit block (FR-20) counts toward the "5 attempts per task", or only full-gate failures (FR-21) do. *Fix:* Define an attempt explicitly.
- **medium** Unbounded "bounded" retries and "fast" checks (§ FR-1, FR-35, FR-20) — "a bounded number of times" and "fast checks" are adjectives, not bounds. *Fix:* Give numbers or name them as configuration with defaults.
- **medium** Question selection determinism unstated (§ FR-3 vs FR-2) — "the question that best separates the top candidates" could be model-chosen. Principle 2 and FR-2 imply it is deterministic, but FR-3 does not say so. *Fix:* Add a consequence: "Question choice is deterministic from scores and card discriminators; the model only phrases it."
- **low** FR-19 and FR-25 lack observable outcomes — It is unclear what makes a contract test pass or fail when no contract is declared, and FR-25 does not say what counts as "observed failing" (the run is recorded? by which gate?). *Fix:* Add one consequence each.

## Scope honesty — adequate
The Non-Goals do real work, especially "Functional correctness" and "Identical output", which pre-empt the most likely misreadings. Out of Scope is explicit, and the five `[ASSUMPTION]` tags are honestly placed. The open-items density (3 OQs, 2 PM notes duplicated in OQs, 5 assumptions) is moderate for a public launch, and none of the items alone blocks a green light.

The problem is placement: one assumption is load-bearing. FR-31's "[ASSUMPTION: modules and contracts can grow; patterns, layers, `may_depend_on`, rules, and naming are locked.]" *is* the definition of the user's deliberate immutability decision. Leaving it as an inference weakens the PRD's central constraint.

### Findings
- **high** Core immutability boundary is only an assumption (§ FR-31) — The line between growable and locked is the substance of 4.6, and downstream architecture will build the charter schema on it. *Fix:* Confirm it with the user and promote it from `[ASSUMPTION]` to a stated consequence; remove it from the Assumptions Index.
- **medium** Hand-fix path interacts with tamper protection but is assumed (§ FR-27 assumption, FR-14) — "hand fixes must pass the same full gates" is assumed, and it is unclear whether hand fixes may touch generated tests (FR-14 says no). *Fix:* Confirm it, and state that hand fixes are subject to FR-14.
- **medium** Gap report is a dead end for the primary persona (§ FR-17, Open Question 2) — The target user works "alone" (Target User; UJ-1: "working alone"), so "share the report with a tech lead" fails for the persona the PRD is built around. *Fix:* Resolve Open Question 2 before launch, for example by excluding untemplated combinations from proposals (see Strategic coherence).

## Downstream usability — adequate
This is a chain-top PRD (the addendum points architecture work to the README), so this dimension matters. The Glossary is present and mostly respected. The UJs carry "Realized by" FR ranges that resolve, and the features carry JTBD back-references. Weaknesses: several load-bearing nouns are not in the Glossary, gate terminology drifts, and the FR order breaks at the end.

### Findings
- **medium** Key domain nouns missing from Glossary (§ Glossary) — "golden set", "conformance suite", "pattern catalog / catalog pattern" (the 18 are never listed), "exhaustion report", "gap report", "misfit", and "critical driver" are all used in FRs or metrics but never defined. *Fix:* Add them. List the 18 catalog patterns or reference where they are enumerated.
- **medium** "Gate" vs per-edit check drift (§ Glossary "Gate", FR-20, FR-21, 4.4 description) — The Glossary defines a gate as "build, architecture, contract, unit tests". FR-20 per-edit checks also block, yet they are called "fast checks", and 4.4 calls both "blocking gates". Story writers will not know whether FR-20 is a gate for FR-26 purposes. *Fix:* Define "per-edit check" and "full gates" as separate Glossary terms.
- **low** No journeys for unhappy paths (§ Key User Journeys) — Fallback to human choice (FR-5), the gap report (FR-17) and exhaustion/misfit (FR-27, FR-28) have no journey, even though counter-metrics depend on them. *Fix:* Add a short UJ-3 covering exhaustion and misfit with Lucía.

## Shape fit — strong
A capability-spec shape with two targeted UJs fits an open-source developer tool with a single operator role. The UJs are not overhead: UJ-2 makes the core value claim concrete. The "Extensibility" FRs (4.7) are written as contracts rather than journeys, which is right for contributor-facing scope. Brownfield is excluded, so there are no existing-code references to check.

## Mechanical notes
- **ID order:** FR-1 to FR-40 are all present and unique, but FR-40 (§ 4.7) appears before FR-39 (§ 4.8). Renumber or move them so the IDs are monotonic.
- **Section numbering:** Features start at "4.1" with no numbered sections 1–3 before them, so cross-references like "(see 4.4)" work but look orphaned. Either number all top-level sections or drop the "4." prefix.
- **Assumptions Index roundtrip:** 5 inline (FR-22, FR-27, FR-31, FR-32, FR-37) and 5 indexed, all matching. The round trip is clean.
- **Duplicated open items:** both `[NOTE FOR PM]` callouts are repeated verbatim as Open Questions 1–2. That is fine, but keep them in sync when resolved.
- **UJ protagonists:** UJ-1 names Lucía with full context. UJ-2 relies on UJ-1 for her context ("Two weeks later, Lucía…"). Add one inline clause (junior, solo, clinic API) so UJ-2 stands alone when extracted.
- **Phase 2 FR inside the Features list:** FR-35 is specced but excluded by MVP Scope. Add an inline `[NON-GOAL for MVP]` tag on FR-35 itself so extractors don't create launch stories for it.
- **Required sections:** an NFR section is missing for public-launch stakes (see Done-ness).
