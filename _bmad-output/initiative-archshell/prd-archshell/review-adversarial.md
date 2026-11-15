# ArchShell PRD - Adversarial Review

- **Target:** `prd-archshell.md` (draft, 2026-10-30) and `addendum.md`
- **Lens:** bmad-review `adversarial` only (`references/lens-adversarial.md`)
- **Date:** 2026-10-30
- **Note on format:** the adversarial lens itself emits no severity. Severity (critical / high / medium / low, by impact on the product's core promise) was added at the requester's explicit instruction.
- **Respected, not re-litigated:** no adoption metrics; greenfield only; pattern immutable with charter growth; Phase 1 only; EN/ES conversation with English artifacts. Where a finding touches one of these, it targets a *consequence or gap* of the decision, not the decision itself.

## Verdict

**Not ready for architecture/epics.** The product's entire value proposition is "a structural mistake could not have passed" (UJ-2 step 6), but the PRD never defines a trust anchor the agent cannot reach. The agent that is being gated runs with write access to the same repository, the same hook configuration, and the same conversation channel used for human confirmation. Until FR-14 and FR-30 name a mechanism the agent cannot invoke, edit, or impersonate, and the protected surface covers every file that affects gate outcomes, the primary metric ("Zero deviations") is a promise the product cannot keep. Separately, the flagship journey UJ-1 contradicts FR-15/FR-17 as written.

## Summary by severity

| Severity | Count |
|---|---|
| Critical | 4 |
| High | 8 |
| Medium | 12 |
| Low | 5 |
| **Total** | **29** |

---

## Critical

### C1. Tamper protection has no trust anchor the agent cannot reach
- **Location:** 4.2 FR-14; 4.6 FR-30; Principle 1
- **Trigger condition:** FR-14 says changes "that do not come from the charter growth flow (4.6) are detected and blocked, whether made by the developer or the agent", but the growth flow is itself executed by the agent (skills/hooks in the Claude Code adapter, FR-34). Nothing distinguishes "a change from the growth flow" from "the agent calling the growth flow's writer directly", running the regeneration script, or updating whatever baseline/hash the detector compares against. The detector, its baseline, and `hooks.json` all live where the agent can write.
- **Guard / fix:** Specify the trust anchor as a requirement: e.g. charter and generated-test hashes are signed or recorded only by an action that requires a human-only input (a CLI command the hooks deny to the agent, a git-committed lock the agent's tool permissions cannot touch, or a deny rule in Claude Code permissions for the protected paths). State explicitly which files the agent's tools are denied write access to, and that the detector and its baseline are outside the agent's writable surface. Add a testable consequence: "An agent session with default tools cannot change `architecture.yaml`, generated tests, the tamper baseline, or hook configuration, even by invoking ArchShell's own internal commands."
- **Potential consequence:** Under pressure from FR-26 retries, the agent loosens a rule or regenerates tests through the same code path the growth flow uses, the gates go green, and the primary metric is violated silently while reporting zero deviations.

### C2. "The agent can never approve growth" is not enforceable over a conversational channel
- **Location:** 4.6 FR-30; UJ-2 step 2 ("Lucía confirms."); FR-7
- **Trigger condition:** Confirmation is collected through the host's question UI, which the agent drives (FR-34 lists "question UI" as an adapter capability). The PRD does not say how ArchShell verifies that a confirmation came from the human rather than being asserted by the agent, inferred from an ambiguous reply ("ok, go ahead with everything"), or carried over from an earlier unrelated "yes".
- **Guard / fix:** Require growth confirmation to be bound to a specific, displayed growth diff and captured through a channel the model cannot synthesize (native `AskUserQuestion` response recorded by a hook, or a human-run command), with a testable consequence: "A growth application without a recorded human answer to that exact proposal ID is rejected by the gate." Define what counts as confirmation (explicit yes to a named proposal; no blanket approvals; no carry-over).
- **Potential consequence:** Charter growth becomes the escape hatch for everything the gates block: the agent "proposes" a module, treats any affirmative as approval, and expands the charter until the violating code fits.

### C3. The protected surface omits the files that actually decide gate outcomes
- **Location:** 4.2 FR-14 (protects only "`architecture.yaml` or the generated architecture tests"); 4.3 FR-16; 4.4 FR-21
- **Trigger condition:** FR-16's strongest enforcement ("a forbidden dependency does not compile") lives in `.csproj` project references, which FR-14 does not protect. FR-21's "warnings as errors" lives in `Directory.Build.props`/csproj (`TreatWarningsAsErrors`, `NoWarn`, `<WarningsNotAsErrors>`), also unprotected. The agent can also: add `[Fact(Skip=...)]` or delete unit/contract tests it did not generate, `#pragma warning disable`, add `InternalsVisibleTo`, use reflection or `dynamic` to reach internals, exclude test projects from the solution, or edit the hook configuration so FR-20/FR-21 never run.
- **Guard / fix:** Enumerate the protected set as a requirement: charter, generated tests, project-reference graph, build property files governing warnings/analyzers, solution membership of test projects, hook/permission configuration, and the gate runner itself. Add gate consequences: a project reference not derivable from `may_depend_on` fails the architecture gate even if it compiles; skipped or deleted tests fail the unit gate; `InternalsVisibleTo` across modules is a rule violation.
- **Potential consequence:** The "does not compile" layer, which the PRD itself ranks as strongest, is the easiest to remove: one added `<ProjectReference>` defeats it with no tamper alert.

### C4. The flagship journey UJ-1 fails under the PRD's own FRs
- **Location:** UJ-1 step 3 ("proposes **Clean + Screaming**") and step 5 ("scaffolds the solution ... Everything compiles"); 4.3 FR-15 ("a template for each of the 18 catalog patterns"); FR-17 ("When the confirmed pattern combination ... has no template, ArchShell stops before scaffolding and writes a gap report"); FR-6
- **Trigger condition:** Templates ship per single pattern; combinations are explicitly not guaranteed templates and stop with a gap report. Clean + Screaming is a combination. As written, UJ-1 ends in a gap report, not a scaffolded solution. The same applies to every combination named in FR-6 (Modular Monolith + Vertical Slice, Clean + Screaming + CQRS).
- **Guard / fix:** Decide and state one of: (a) templates compose (a composition rule turns `combines_with` pairs into a structure, with the combinations that must scaffold at launch listed explicitly), or (b) a named list of supported combination templates ships at launch and includes every combination used in UJs and the golden set. Add a launch acceptance criterion: "every combination ArchShell can propose on the golden set scaffolds and passes gates."
- **Potential consequence:** The most common real proposals (combinations) dead-end; the launch demo contradicts the spec; downstream architecture builds 18 single-pattern templates that never get used as-is.

---

## High

### H1. "Without reviewing the structure" overpromises what dependency/naming rules can verify
- **Location:** Vision ("hand full features to an AI agent without reviewing the structure"); UJ-2 step 6 ("a structural mistake could not have passed"); Non-Goals ("Gates guarantee structure and contracts (maintainability, extensibility)")
- **Trigger condition:** FR-18 covers "layer dependencies, naming, port location, and cross-module rules". Many structural mistakes pass all of these: business logic in controllers or infrastructure adapters, anemic entities with logic in handlers, god classes, duplicated domain logic across modules, logic in the wrong layer that only uses allowed dependencies, misuse of DI lifetimes, synchronous coupling via shared database tables. Maintainability and extensibility are not guaranteed by dependency direction alone.
- **Guard / fix:** Restate the promise as what is actually enforced: "Violations of the rules declared in `architecture.yaml` cannot pass." Add an explicit non-goal listing structural qualities that remain the developer's review responsibility, and remove "could not have passed" from UJ-2 or scope it to declared rules.
- **Potential consequence:** The target user (low-to-medium architecture knowledge, by definition unable to spot these) stops reviewing structure exactly as the product tells them to, and accumulates the structural debt the product claims to prevent; trust collapses on first discovery.

### H2. Gates only exist inside Claude Code hooks; every other edit path bypasses them
- **Location:** FR-20, FR-21 (hooks on agent edits); FR-27 assumption ("hand fixes must pass the same full gates"); Out of Scope ("CI mode (`archshell verify` without an LLM)"); Primary metric
- **Trigger condition:** Developer edits in an IDE, `git commit`, a merge from a teammate, or a plain Claude Code request that does not enter the feature workflow do not trigger FR-20/FR-21. With CI mode out of scope, there is no gate outside the agent session. The FR-27 assumption has no mechanism to make it true. Teams of 1-5 are in the target, so teammates' edits are a normal path.
- **Guard / fix:** Either bring a minimal non-LLM verification into v1 (generated tests already run under `dotnet test`; a pre-commit hook or a documented `dotnet test` CI step is enough), or scope the promise and primary metric to "changes made by the agent through ArchShell". Make the FR-27 assumption a requirement with a mechanism.
- **Potential consequence:** "Zero deviations" holds only for one edit path; violations enter through the others and the charter becomes fiction while the agent's own runs stay green.

### H3. The agent can bypass the feature workflow entirely
- **Location:** 4.5 Description ("Every feature follows the same sequence"); FR-13, FR-23, FR-24, FR-25; Principle 2
- **Trigger condition:** The sequence (spec, gap questions, scaffold, ports, RED test) runs only if the agent invokes the workflow. Nothing requires it: a user prompt like "quickly add a field" or an agent that decides a change is "small" can edit code directly. Per-edit checks (FR-20) still fire, but spec, RED test, and structure-before-implementation are skipped, and FR-21 runs only "when the agent reports a task as done", which is agent-declared.
- **Guard / fix:** Define what triggers the full gates independent of agent declaration (e.g. Stop hook on every turn that modified source files), and what happens to source edits made outside a workflow (allowed with full gates, or blocked). Add a testable consequence: "A source change made without an active feature workflow still runs the full gates before the turn ends."
- **Potential consequence:** "Deterministic process" degrades to "deterministic when the agent chooses to follow it"; the RED-first and spec guarantees become advisory, contradicting Principle 3 ("Prose is never an enforcement mechanism").

### H4. RED-first and unit-test gates are gameable by the agent that writes both test and code
- **Location:** FR-25 ("A failing test for the feature exists and is observed failing"); FR-21 (unit tests gate); UJ-2 step 1
- **Trigger condition:** The agent authors the RED test and the implementation. A test that fails because it does not compile, asserts `true == false`, or checks an irrelevant detail satisfies FR-25; a later weakened assertion satisfies FR-21. The S1..Sn spec (FR-23) is not linked to tests, so there is no check that each S# has a test.
- **Guard / fix:** Require: RED must fail on an assertion, not compilation; each S# maps to at least one test (traceability checked by a gate); tests created in the RED step are protected from weakening without a recorded reason (or shown to the developer as a diff). Acknowledge in Non-Goals that test adequacy is not guaranteed.
- **Potential consequence:** Gates are green while the business rule (e.g. the 24 h fee in UJ-2) is untested; the developer's only remaining review (fee logic) is anchored on tests that prove nothing.

### H5. 18 templates plus combinations is not a credible v1 scope
- **Location:** FR-15 ("The launch .NET/C# pack ships a template for each of the 18 catalog patterns"); MVP Scope; FR-37 (same thresholds for every pack)
- **Trigger condition:** Each template needs scaffolding, project-reference wiring (FR-16), generated architecture tests (FR-18), contract tests (FR-19), per-edit checks (FR-20), naming rules, and growth/regeneration support (FR-32), and must pass the conformance suite. That is 18 enforcement stacks plus the combinations (C4), from one team, for a first release. Some catalog patterns may not map to a single-solution .NET template at all (the PRD does not list the 18, so this cannot be checked).
- **Guard / fix:** List the 18 patterns in the PRD. Tier them: a small launch set (e.g. the patterns and combinations needed to cover the golden set and UJs) with full gates, and the rest marked "selectable, scaffolding via gap report" until they reach conformance. FR-17 already provides the mechanism for that.
- **Potential consequence:** Either the launch slips indefinitely, or 18 shallow templates ship with thin rules, which is worse for the trust promise than 5 deep ones.

### H6. The primary metric is unmeasurable as defined
- **Location:** Success Metrics, Primary ("0 features pass the gates while violating `architecture.yaml`")
- **Trigger condition:** If the gates are the only detector of violations, a feature that passes the gates while violating the charter is by construction undetected. The metric needs an independent oracle (adversarial seeded violations, an independent checker, or post-hoc audit) and a denominator. It also says nothing about violations that `architecture.yaml` cannot express (H1).
- **Guard / fix:** Define how it is measured: e.g. a maintained corpus of seeded violations (one per rule type, plus the tamper attacks in C1-C3) that must all be rejected, run per release; report "escaped violations found by audit" separately.
- **Potential consequence:** The metric reads 0 forever, providing false assurance and no signal for regressions in the gates themselves.

### H7. FR-3 and FR-4 can deadlock selection on a non-separating critical unknown
- **Location:** FR-3 ("Questions about drivers that do not separate the top candidates are never asked"); FR-4 ("no critical driver `unknown`")
- **Trigger condition:** If a critical driver is `unknown` but does not separate the top two candidates, FR-3 forbids asking about it and FR-4 forbids proposing. The only exit is exhausting 5 questions (FR-5) or reaching it immediately with no question to ask, forcing a "human decision" the elicitation could have resolved with one question. "Critical driver" is never defined.
- **Guard / fix:** Define "critical driver" (per card or global list). Allow questions about critical unknowns regardless of separation, or relax FR-4 to "no critical unknown that affects the top candidates' scores". Add a test for this case.
- **Potential consequence:** Low-knowledge users are pushed into FR-5 "you decide" more often than necessary, which is exactly the job (JTBD 1) the product exists to take off them, and those decisions are then permanent (FR-31).

### H8. Launch acceptance criteria can be passed by a product that always asks
- **Location:** Launch Acceptance Criteria ("selects the expected pattern(s) or asks a discriminating question"); Secondary metric ("Correct selections" with no target)
- **Trigger condition:** "Or asks a discriminating question" is satisfied by asking on every request; three samples is too few; "correct selections" has no threshold; one "deliberately layer-violating feature" tests one rule type out of many and none of the tamper paths.
- **Guard / fix:** Set a numeric target on the golden set (e.g. >= X% correct after at most N questions, 0 wrong confident proposals); require each rule type in FR-18 and each tamper vector in C1-C3 to be rejected; require UJ-1 and UJ-2 to run end to end.
- **Potential consequence:** Launch gate passes with selection and enforcement both effectively unverified.

---

## Medium

### M1. "Blocks the edit" is undefined for a post-write hook, and per-edit blocking breaks legitimate multi-file refactors
- **Location:** FR-20 ("After each file the agent edits ... blocks the edit on a violation")
- **Trigger condition:** After the edit, the file is already written. Does "block" mean revert, reject the next tool call, or report? Also, intermediate states of a legitimate multi-file change (moving a type, introducing a contract before switching the caller) violate rules transiently, and per-file checks of dependency rules may need a semantic model of the whole solution to be correct. "Fast" has no budget.
- **Guard / fix:** Define block semantics (revert vs. report-and-require-fix), allow a per-task scope where per-edit findings are reported and only FR-21 is final, and set a latency budget.
- **Potential consequence:** Either the agent is trapped by transient states and burns retries (counter-metric "Exhausted retries" explodes), or the per-edit check is weakened ad hoc in implementation.

### M2. "Attempt" is undefined for the 5-attempt limit
- **Location:** FR-26, FR-27; counter-metric "Exhausted retries"
- **Trigger condition:** Does a per-edit block (FR-20) count as an attempt, or only a failed FR-21 run? Can the agent reset the counter by starting a new task or rephrasing? Is the counter stored where the agent can edit it?
- **Guard / fix:** Define an attempt as one FR-21 run after the agent declares done; store the counter outside the agent's writable surface; tie it to the spec ID, not the conversation.
- **Potential consequence:** The limit is either trivially evaded (new task, same feature) or triggered by noise; the exhaustion report and the misfit signal (FR-28) become meaningless.

### M3. The growth taxonomy is too thin for real features
- **Location:** FR-31 assumption ("modules and contracts can grow; patterns, layers, `may_depend_on`, rules, and naming are locked"); FR-29
- **Trigger condition:** Real features need things that are neither a module nor a contract: a new infrastructure adapter project (e.g. the email/SMS sender behind `Notifications` in UJ-2), a background worker host, a new test project, a new external integration, new NuGet dependencies in a layer. The PRD does not say whether these are growth, structure from templates, or forbidden.
- **Guard / fix:** Enumerate the growable element kinds (module, contract, adapter, host, test project, package dependency?) and for each, whether it is growth requiring confirmation or template-driven structure.
- **Potential consequence:** Ordinary features land in the exhaustion report (FR-31 second bullet), or implementations improvise growth kinds, eroding immutability.

### M4. What happens after growth is refused is unspecified
- **Location:** FR-29 ("growth that would violate them is never proposed"); FR-30
- **Trigger condition:** If the needed growth would violate the pattern, nothing is proposed, but the PRD does not say what the developer sees. Also unspecified: what happens when the developer declines a valid growth proposal.
- **Guard / fix:** Specify both paths: invalid growth produces a misfit explanation (FR-28) immediately, without burning 5 retries; declined growth stops the feature with a stated reason.
- **Potential consequence:** The agent, unable to grow, tries to implement anyway and burns attempts, or silently works around the charter.

### M5. "Contract" means two different things
- **Location:** UJ-2 step 3 ("Appointments may only use `Billing.Contracts`"); FR-19 ("published contracts (OpenAPI / JSON Schema)"); FR-11 ("contract locations")
- **Trigger condition:** Cross-module contracts in UJ-2 are C# assemblies/interfaces; FR-19 contract tests target OpenAPI/JSON Schema. In-process module contracts have no conformance test defined, and the glossary does not define "contract".
- **Guard / fix:** Add a glossary entry distinguishing module contracts (public API of a module) from external contracts (OpenAPI/JSON Schema), and state which gate covers each.
- **Potential consequence:** Downstream architecture builds the wrong contract-testing mechanism, or module contract drift goes unchecked.

### M6. Blocking the developer is unenforceable and contradicts developer ownership
- **Location:** FR-14 ("whether made by the developer or the agent")
- **Trigger condition:** The developer owns the repository and can uninstall the plugin, edit files in an IDE, or disable hooks. The PRD promises a block it cannot impose on the developer. It also leaves no sanctioned path for a developer who needs to correct a charter error (e.g. a typo in a module name, a wrong naming rule generated at FR-11).
- **Guard / fix:** For the developer, change "blocked" to "detected and reported on the next gate run" (requires H2's non-agent verification). Define a sanctioned, recorded correction path for charter errors that does not count as relaxing a rule.
- **Potential consequence:** A promise users can trivially disprove, and pressure to hand-edit the charter whenever generation got something wrong.

### M7. Open Question 1 becomes permanent damage under immutability
- **Location:** FR-8 note; Open Questions 1; FR-31
- **Trigger condition:** Because the pattern is immutable for the project's life, an override that creates an `excludes` conflict, if only warned, locks the project into an internally contradictory charter forever. This is not a re-litigation of immutability; it is a consequence that makes the open question blocking.
- **Guard / fix:** Resolve before architecture: refuse `excludes` conflicts (the user can still override among compatible options), or define how conflicting rules are generated.
- **Potential consequence:** Contradictory generated rules (some test always fails), guaranteed exhaustion on features, and no allowed exit.

### M8. Regeneration after growth can overwrite developer and agent work
- **Location:** FR-32 ("`architecture.yaml`, the generated architecture tests, project references, and `ARCHITECTURE.md` are regenerated")
- **Trigger condition:** Regenerating project references rewrites `.csproj` files that also hold package references and build settings; regenerating `ARCHITECTURE.md` discards any human notes. Merge behavior is undefined.
- **Guard / fix:** Specify regeneration as targeted (only ArchShell-owned regions/files), and state that non-owned content is preserved.
- **Potential consequence:** Each growth silently drops package references or documentation, breaking builds and pushing developers to avoid growth.

### M9. Conformance gate thresholds are undefined and its enforcement point is unclear
- **Location:** FR-37 ("same thresholds as the launch pack"); FR-36
- **Trigger condition:** No thresholds are defined anywhere for packs/adapters (the golden set measures selection, which is language-neutral). "Enabled only after it passes" has no enforcer in an open-source plugin model: who enables, and can a user load an unconformant pack locally?
- **Guard / fix:** Define conformance metrics per extension type and state whether the core refuses unconformant packs at runtime (e.g. by checking a conformance record) or whether this is a contribution/release policy only.
- **Potential consequence:** Community packs ship with weaker enforcement under the same brand promise.

### M10. Threshold calibration on the same 15-20 samples invites overfitting
- **Location:** FR-4 ("calibrated against the golden set"); Secondary metric (golden set "derived from `02-ARQUITECTURA/*/sample`")
- **Trigger condition:** The same small set is used to tune thresholds and to measure correctness, and it is derived from the authoring source of the catalog itself. 15-20 requests cannot cover 18 patterns plus combinations.
- **Guard / fix:** Split calibration and held-out evaluation sets; require at least one request per launch pattern/combination; include requests not written by the catalog author.
- **Potential consequence:** Reported selection accuracy is inflated; real users get confident wrong proposals, which the PRD itself calls "the costly error".

### M11. "None is guessed" is untestable for LLM driver extraction, and determinism is narrower than claimed
- **Location:** FR-1 ("Every driver the request does not establish is recorded as `unknown`; none is guessed"); FR-2; Principle 4
- **Trigger condition:** "Establish" is a model judgment; there is no test oracle for it beyond a labeled set. FR-2's determinism holds only from drivers onward; the same request can yield different drivers and thus a different pattern.
- **Guard / fix:** Make FR-1 testable via the golden set (expected driver values, including expected `unknown`s) with an accuracy target; state plainly that selection is deterministic given drivers, not given the request.
- **Potential consequence:** Users see different proposals for the same request and lose trust in "deterministic process".

### M12. Misfit detection and "best guess" are untestable
- **Location:** FR-27 ("its best guess at why"); FR-28 ("When the exhausted failures suggest the feature does not fit")
- **Trigger condition:** "Suggest" and "best guess" have no criterion; any output satisfies them.
- **Guard / fix:** Define a deterministic misfit signal (e.g. the same rule failed in all 5 attempts, or the only passing structure requires a locked element from FR-31) and test against seeded misfit features.
- **Potential consequence:** Misfit reports are vague, and the developer, who per the target-user definition cannot diagnose architecture, has no actionable path.

---

## Low

### L1. FR numbering is out of order
- **Location:** 4.7 (FR-38, FR-40) and 4.8 (FR-39)
- **Trigger condition:** FR-40 precedes FR-39; traceability tools and readers expect monotonic IDs.
- **Guard / fix:** Renumber or move FR-39 before FR-40.
- **Potential consequence:** Minor confusion in epics/traceability.

### L2. Translation of Spanish domain terms into English names is unspecified
- **Location:** FR-12; UJ-1 (Spanish request yields `Appointments`, `Patients`, `Billing`)
- **Trigger condition:** The model translates business terms into module and type names; nothing ensures consistency across later features and growth (e.g. "Citas" becoming `Appointments` once and `Bookings` later). Naming rules (FR-11) cannot catch synonyms.
- **Guard / fix:** Record a glossary (ubiquitous-language map ES to EN) in the charter and require later features to reuse it.
- **Potential consequence:** Duplicate or inconsistent modules that pass naming rules.

### L3. Downstream readers are pointed at a design README known to be stale
- **Location:** Addendum "Technical design reference" and "Superseded by the PRD"; Open Questions 3
- **Trigger condition:** The addendum directs architecture work to README sections that still contain superseded behavior (superseding ADR, full gate per edit).
- **Guard / fix:** Make updating the README a precondition for architecture work, or mark the superseded sections inline in the addendum list.
- **Potential consequence:** Architecture reintroduces the superseded flows.

### L4. Counter-metrics have no guardrail values
- **Location:** Success Metrics, Counter-metrics
- **Trigger condition:** "Do not optimize" without a ceiling gives no signal for when bias toward asking or blocking has gone too far.
- **Guard / fix:** Set alert thresholds (e.g. median questions per selection, exhausted-retry rate on a reference feature set).
- **Potential consequence:** Calibration drifts toward asking/blocking everything with no trigger to correct it.

### L5. FR-5 "top two candidates" is ambiguous when candidates are combinations
- **Location:** FR-5; FR-6
- **Trigger condition:** Unclear whether candidates are single patterns or combinations, and how combinations are scored and ranked by FR-2.
- **Guard / fix:** State that candidates are scored configurations (single or combination) and how combination scores derive from card scores.
- **Potential consequence:** Inconsistent fallback presentations and untestable ranking for combinations.
