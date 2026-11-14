---
title: ArchShell PRD - Edge-Case Review
reviewed: prd-archshell.md, addendum.md
lens: edge-case-hunter (bmad-review)
date: 2026-10-30
---

# ArchShell PRD - Edge-Case Review

**Lens:** Edge-Case Hunter, from `bmad-review`. It traces every branch the requirements define and reports only the paths with no defined behavior.
**Content class:** docs that define behavior (PRD + addendum).
**Deviation from the lens:** the shipped lens assigns no severity. The caller asked for a severity ranking, so each finding carries one. The severity measures **how much the gap affects whether downstream architecture and epics can be built safely**:

- **Critical:** the primary promise ("0 features pass the gates while violating `architecture.yaml`") or the immutable-charter model cannot be guaranteed, or the project can end up in a state it cannot recover from.
- **High:** architects or epic writers would have to invent core behavior, and different guesses lead to incompatible designs.
- **Medium:** a bounded behavior gap. A story can close it, but it should be decided before stories are written.
- **Low:** a clarification or polish item.

**Decisions this review respects, and does not flag:** no adoption metrics; greenfield only; the pattern is immutable and the charter only grows within it; Phase 1 scope only (FR-35 providers, the CLI and CI mode are out). The existing open questions (OQ-1 override `excludes` conflict, OQ-2 solo-developer gap report, OQ-3 README sync) are not raised again. They are cited only where a new edge case makes them worse.

## Summary

| Severity | Count |
|---|---|
| Critical | 3 |
| High | 14 |
| Medium | 14 |
| Low | 5 |
| **Total** | **36** |

**Verdict:** the PRD covers the happy paths well but cannot yet be built safely. The three critical gaps all concern ways around enforcement or states the project cannot leave, and these weaken the primary metric directly: (1) tamper protection does not cover project references or build settings; (2) the full gates depend on the agent saying it is done, and the per-edit checks depend on the agent using the edit tool; (3) the charter is written and locked before scaffolding, and no rollback or resume is defined. Close these and the High items on lifecycle and counting (attempt counting, declined growth, interruption and resume, environment failures versus gate failures) before writing the architecture.

---

## Critical

### EC-01 - Tamper protection does not cover project references or build settings
- **Location:** FR-14, FR-16, FR-21, Success Metrics (Primary)
- **Trigger condition:** the agent adds a forbidden `<ProjectReference>`, turns off `TreatWarningsAsErrors`, edits `Directory.Build.props`, removes the arch-test project from the `.sln`, or adds `Skip`/`[Trait]` filters to the gate run.
- **Guard:** add to FR-14's protected set: generated `.csproj` references, solution membership of the gate test projects, build props and targets, and the gate invocation and config. Or state that FR-18 tests independently assert every `may_depend_on` edge, so a forbidden reference fails a test even when it compiles.
- **Potential consequence:** a forbidden dependency compiles and the gates pass, so the "zero deviations" metric is silently broken (UJ-2 step 6's guarantee is false).

### EC-02 - Full gates depend on the agent saying "done", and per-edit checks depend on the edit tool
- **Location:** FR-20, FR-21, FR-26, UJ-2 steps 3 and 5
- **Trigger condition:** the agent edits files through shell commands, scripts, `dotnet` tooling, file moves or deletions (not the edit tool). Or it never formally reports done: it stops mid-task, the session is interrupted or closed, context is compacted, or the developer cancels.
- **Guard:** define the gate trigger as "any end of a turn or session with unverified changes in the working tree", not only "the agent reports done". State that per-edit checks cover every write path, including shell writes, creates, deletes and renames, or that the full gate is the backstop for anything they miss. Define what state the tree is left in when a task ends without passing gates.
- **Potential consequence:** code that violates the rules stays in the repository and never went through a gate, so the "cannot have passed" claim fails.

### EC-03 - The charter is locked before scaffolding, and scaffold failure has no rollback or resume
- **Location:** FR-7, FR-9 to FR-11, FR-15, FR-17, FR-31, section 4.3 description ("After the charter is written")
- **Trigger condition:** scaffolding fails partway (missing .NET SDK, `dotnet new` or NuGet restore failure, disk or permission error, the developer cancels), or FR-17 finds a missing template *after* the charter files exist.
- **Guard:** make charter write plus scaffold one transaction: check templates and the environment before writing anything, roll back fully on failure, or define an idempotent resume. State whether a charter that was never scaffolded is still "immutable" and can be re-selected.
- **Potential consequence:** an immutable pattern is recorded for a project that has no structure, and no defined path changes it. The developer has to delete files by hand, and FR-14 may block that.

---

## High

### EC-04 - Legitimate changes to protected files have no path that is not tampering
- **Location:** FR-14, FR-18 (same tests from an unchanged yaml), FR-36
- **Trigger condition:** an ArchShell or language-pack upgrade changes the generated test output. A git merge, rebase or branch switch changes `architecture.yaml`. A formatter rewrites a generated test. A teammate's growth arrives by pull.
- **Guard:** define the tamper baseline (where it is stored, what it covers, how it is protected itself), and a supported "regenerate after upgrade" and "accept merged charter" flow that is not the developer loosening a rule.
- **Potential consequence:** false tamper blocks with no exit (the pattern is immutable), or a hidden manual bypass that becomes the normal escape hatch.

### EC-05 - Gates that fail for environment reasons are treated as rule violations
- **Location:** FR-20, FR-21, FR-22, FR-26, FR-27
- **Trigger condition:** .NET SDK missing or the wrong version, NuGet offline, a gate times out, a gate crashes, a unit test is flaky, a new NuGet vulnerability warning is promoted to an error by warnings-as-errors with no code change.
- **Guard:** separate "gate could not run" (fail closed, do not consume attempts, report the environment problem) from "gate ran and failed". Set per-edit and full-gate timeout budgets. Define the flaky-test policy. Check for the SDK before selection or charter writing.
- **Potential consequence:** retries are used up on infrastructure problems, the exhaustion report blames rules wrongly, and the "Exhausted retries" counter-metric fills with noise. If gates instead fail open, violations get through.

### EC-06 - The 5-attempt limit has no defined unit or reset rule
- **Location:** FR-26, FR-27, UJ-2 step 4
- **Trigger condition:** a task spans S1..Sn subtasks. Per-edit blocks (FR-20) happen before the full gates. The session restarts mid-retry. Growth happens between attempts. The developer rephrases the spec.
- **Guard:** define "task", whether FR-20 blocks count as attempts, whether the count is per task or per S#, whether it persists across sessions, and what resets it (rephrase, growth, hand fix).
- **Potential consequence:** epics cannot implement FR-26 consistently. Agents loop forever (the counter resets on each restart) or stop too early (each per-edit block counts).

### EC-07 - No behavior defined when the developer declines growth
- **Location:** FR-29, FR-30, UJ-2 step 2
- **Trigger condition:** Lucía answers "no" to the `Notifications` module proposal, or never answers.
- **Guard:** define the outcomes: drop the affected S# (S3) and continue, propose an alternative within existing modules, or stop the feature with a report. State whether a pending growth blocks all other work.
- **Potential consequence:** the agent may implement notification logic inside an existing module, which drifts from the architecture with no rule violated, or the workflow stalls in an undefined state.

### EC-08 - Growth that conflicts with existing modules or contracts is undefined
- **Location:** FR-29, FR-31, FR-32, FR-11
- **Trigger condition:** a proposed module duplicates or overlaps an existing one (`Notifications` vs `Messaging`), collides on name or namespace, or requires renaming, splitting, merging or removing a module or contract. Or a breaking change to an existing `*.Contracts` is needed.
- **Guard:** list the growth operations explicitly (add module, add contract, change a contract version) and the forbidden ones. Define how name and semantic collisions are checked. Define whether modules and contracts can ever be removed or renamed, and whether contract evolution is growth.
- **Potential consequence:** "grow-only" turns into a monotonic charter with dead or duplicate modules, or the agent does renames outside the growth flow and FR-14 blocks them.

### EC-09 - Interrupted growth regeneration leaves the charter inconsistent
- **Location:** FR-32, FR-14
- **Trigger condition:** the session is cancelled or crashes after `architecture.yaml` is updated but before the tests, references or `ARCHITECTURE.md` are regenerated, or a regeneration step fails (for example a project template error).
- **Guard:** apply growth atomically (stage everything, then commit), or record a resumable growth journal. Tamper detection must recognise an interrupted growth.
- **Potential consequence:** a half-grown charter fails tamper detection or the gates on every later run, with no defined recovery.

### EC-10 - "The agent can never approve growth" has no enforcement mechanism
- **Location:** FR-30, FR-14
- **Trigger condition:** in Claude Code, the agent writes the confirmation token or file itself, answers its own question, or runs the growth command with a "confirmed" flag.
- **Guard:** state where the developer's confirmation is captured (the host's native question UI or a hook-validated user turn) and that it cannot be produced by any tool call the agent makes.
- **Potential consequence:** the agent grows the charter on its own to get past a failing gate. The guarantee holds only by convention.

### EC-11 - Greenfield detection is undefined
- **Location:** Target User / Non-Users, FR-7, FR-9, FR-15
- **Trigger condition:** the target folder already has `.sln`/`.csproj` files, a `docs/adr/` folder, an `ARCHITECTURE.md`, an existing `architecture.yaml` (ArchShell was run twice), or only a README and `.git`.
- **Guard:** define what counts as "new project", what ArchShell does when it is not (refuse with a message, or choose another folder), and the behavior when a charter already exists (resume the feature flow, never run selection again).
- **Potential consequence:** existing files are overwritten (data loss), or brownfield use happens quietly against a deliberate non-goal.

### EC-12 - No requirement defines where modules come from
- **Location:** FR-1 (driver list has no modules), FR-11, FR-15, UJ-1 steps 2 and 5
- **Trigger condition:** the request names zero modules, one module, ambiguous capabilities ("billing and invoices"), or the developer wants to add, remove or rename modules before confirming.
- **Guard:** add an FR for module extraction: its schema, the confirmation step, editing before FR-7, minimum and maximum count, and the naming rules.
- **Potential consequence:** module boundaries, which are the main input to Screaming and Modular layouts, are left to model output nobody specified. That contradicts Principle 1.

### EC-13 - Auto-proposal can be impossible when a critical driver does not separate the candidates
- **Location:** FR-3 (non-separating questions are never asked), FR-4 ("no critical driver `unknown`"), FR-5
- **Trigger condition:** a critical driver is `unknown` but does not separate the top two candidates, so FR-3 forbids asking about it and FR-4 can never pass. "Critical driver" is also never defined.
- **Guard:** define the set of critical drivers, and allow questions about critical `unknown` drivers even when they do not separate the candidates (or exempt them from FR-4).
- **Potential consequence:** a needless fallback to human choice (FR-5), which the target user cannot judge well. Calibration on the golden set will look wrong for this reason.

### EC-14 - Interrupted or resumed sessions have no defined state
- **Location:** FR-3, FR-7, FR-23, FR-26, FR-29, UJ-1, UJ-2 ("Two weeks later")
- **Trigger condition:** the developer closes Claude Code in the middle of selection questions, between proposal and confirmation, mid-spec, mid-retry, or with a growth proposal open.
- **Guard:** state which workflow state persists (answered drivers, question count, spec, attempt count, pending growth), where it lives, and whether each flow resumes or restarts.
- **Potential consequence:** the question budget and attempt counters can be bypassed, and duplicate or contradictory specs or growth proposals appear.

### EC-15 - The working-tree state and hand-fix path after exhaustion are undefined
- **Location:** FR-27, FR-28, Assumption FR-27
- **Trigger condition:** five attempts fail. The code from those attempts stays in the tree, and the developer then edits by hand in an IDE, where Claude Code hooks do not run.
- **Guard:** define whether failed attempts are reverted or kept (and marked as failing), how gates are run on hand fixes made outside the host, and whether a hand fix that passes the gates closes the task.
- **Potential consequence:** code that fails the gates stays in the repo, or hand edits skip every gate. Either way the zero-deviation claim is weakened.

### EC-16 - Concurrent sessions and team branches are undefined
- **Location:** Target User (teams of 1-5), FR-14, FR-29 to FR-32, ADR numbering (FR-32 assumption)
- **Trigger condition:** two developers or two parallel agent sessions grow the charter on different branches and merge. Conflicting `architecture.yaml` hunks, duplicate ADR numbers, or the same module added twice.
- **Guard:** define the merge semantics for charter growth (union of additions, conflict = stop), ADR numbering that tolerates branches, and how tamper detection treats a merged charter.
- **Potential consequence:** the target team user meets tamper blocks or a corrupted charter on the first merge.

### EC-17 - A missing template is detected only after selection, which makes FR-8 overrides a trap
- **Location:** FR-8, FR-17, FR-6, FR-38
- **Trigger condition:** the developer overrides to a combination or custom card that has no template. The proposal is confirmed, and only then does scaffolding find the gap.
- **Guard:** check template availability when proposing and overriding, and show "no template" as a tradeoff before confirmation (FR-8). This connects to OQ-2.
- **Potential consequence:** with EC-03, the developer is locked into a pattern that cannot be scaffolded, and as a solo developer has no one to extend the pack.

---

## Medium

### EC-18 - A request that matches no pattern well is handled like a close call
- **Location:** FR-4, FR-5
- **Trigger condition:** all scores are low (for example the top score is 0.3) after 5 questions, or every candidate is excluded.
- **Guard:** add a minimum score below which FR-5 does not present the top two as viable, and say so, or apply a documented default.
- **Potential consequence:** the developer is asked to choose between two poor fits as if they were reasonable.

### EC-19 - The developer can reject both fallback candidates
- **Location:** FR-5, FR-8
- **Trigger condition:** in FR-5 the developer picks neither candidate, names a third pattern, or asks to answer more questions.
- **Guard:** state whether a third pattern goes through the FR-8 override flow, and whether the question budget can be extended.
- **Potential consequence:** the flow has no defined exit, and the ADR outcome type is unclear.

### EC-20 - Unhelpful or contradictory answers to discriminating questions
- **Location:** FR-1, FR-3
- **Trigger condition:** the developer answers "I don't know", gives an off-topic or ambiguous answer, or contradicts a driver extracted earlier or a previous answer.
- **Guard:** define how each answer maps to a driver value (including keeping it `unknown`), whether such answers count against the limit of 5, and that a later answer overrides an earlier value with a record of the change.
- **Potential consequence:** the deterministic-scoring guarantee (FR-2) becomes unclear, because nobody defined which value scoring sees.

### EC-21 - A question cannot be told apart from an override
- **Location:** FR-7, FR-8, UJ-1 step 4
- **Trigger condition:** "What if I use Hexagonal?" might be a question or a rejection. "ok, maybe" might or might not be a confirmation.
- **Guard:** require an explicit confirmation action (a native question UI with closed choices) for FR-7 and for accepting an override.
- **Potential consequence:** a pattern is written, and becomes immutable, on an ambiguous answer.

### EC-22 - The conversation language changes mid-session, or is neither English nor Spanish
- **Location:** FR-3, FR-22 assumption, FR-39
- **Trigger condition:** the developer switches from Spanish to English mid-selection, mixes languages, sends a one-word answer ("ok") that gives no language, or writes in Portuguese or French.
- **Guard:** define the rule for detecting the language (per message or fixed per session), what happens for unsupported languages (fall back to English), and which language is used for reports produced asynchronously (gate failures).
- **Potential consequence:** inconsistent output, and golden-set behavior that differs by language.

### EC-23 - Spanish business names must be translated into English identifiers
- **Location:** FR-12, FR-11, FR-15
- **Trigger condition:** "citas / facturación" must become `Appointments` / `Billing`. Accented or space-containing names, names that are C# reserved words or clash with framework names (`System`, `Contracts`, `Task`), or the same concept translated differently in a later session.
- **Guard:** define the naming rules (validation, a recorded glossary of business terms to identifiers in the charter, and confirmation of names before writing).
- **Potential consequence:** the build fails or namespaces collide, and later growth creates duplicate modules under different English names.

### EC-24 - Generated specs are in English while the developer works in Spanish
- **Location:** FR-12, FR-23, FR-39, UJ-2 step 1
- **Trigger condition:** the S1..Sn spec is a document (English, FR-12), but the developer must review it and answer gaps in Spanish (FR-39).
- **Guard:** state whether the spec is shown to the developer translated, stored in English, or both.
- **Potential consequence:** the developer approves a spec they did not fully read. FR-12 and FR-39 conflict here.

### EC-25 - Requests that are not features have no workflow
- **Location:** FR-13, FR-23 to FR-25
- **Trigger condition:** a bug fix, refactor, rename, dependency upgrade, removing a feature, or a growth-only request ("add a Reporting module").
- **Guard:** define whether every change goes through 4.5, whether a lighter path exists, and how growth can be requested directly.
- **Potential consequence:** a "RED test first" requirement that cannot apply (a refactor), or changes made outside the workflow and its gates.

### EC-26 - The first RED test may not compile
- **Location:** FR-24, FR-25, FR-20, FR-21
- **Trigger condition:** the RED test references types that do not exist yet. A compile failure fails the build gate and may trigger a per-edit block. A test that already passes (the behavior exists) cannot be RED.
- **Guard:** define what counts as valid RED (compiles against the ports and contracts from FR-24 and fails on an assertion), and what happens when the test is green from the start.
- **Potential consequence:** RED failures are counted as gate attempts, or "observed failing" is satisfied by a compile error.

### EC-27 - Gate failures without an identifiable rule
- **Location:** FR-22, FR-27
- **Trigger condition:** a compiler error, a unit-test assertion, a test-host crash or a timeout: no `architecture.yaml` rule applies.
- **Guard:** define the report shape for failures that are not rule violations, and how FR-27 "the rule that kept failing" handles them.
- **Potential consequence:** reports are inconsistent, and misfit detection (FR-28) misfires on ordinary logic bugs.

### EC-28 - Custom pattern cards are invalid, collide, or disappear
- **Location:** FR-38, FR-40, FR-37
- **Trigger condition:** a custom card fails schema validation, has the same name as a catalog card, or the extra catalog path is moved or removed after a project chose that card.
- **Guard:** validate custom cards at load time, define precedence on name clashes, and say whether the charter embeds or pins the card so it does not depend on the path. Say whether conformance (FR-37) applies to custom cards.
- **Potential consequence:** a project whose immutable pattern definition can no longer be found.

### EC-29 - Language-pack or ArchShell version drift for growth on an existing project
- **Location:** FR-32, FR-36, FR-15
- **Trigger condition:** the project was scaffolded with pack v1. Growth runs with v2, whose module templates or test generation differ.
- **Guard:** pin the pack and contract version in `architecture.yaml`, and define upgrade or compatibility behavior for existing projects.
- **Potential consequence:** new modules are shaped differently from existing ones, and regenerated tests change (connects to EC-04).

### EC-30 - Override with an `excludes` conflict leaves no consistent charter to generate
- **Location:** FR-8, FR-6, FR-16, OQ-1
- **Trigger condition:** if OQ-1 is decided as "warn only", the combined `may_depend_on` rules of the two excluded patterns contradict each other.
- **Guard:** whatever OQ-1 decides, define how conflicting layer rules are resolved in `architecture.yaml` and the references, or refuse.
- **Potential consequence:** a charter that cannot be generated, or tests that cannot all pass. Adds an edge to OQ-1; it does not raise OQ-1 again.

### EC-31 - Selection-side retry exhaustion has no next step
- **Location:** FR-1, FR-35
- **Trigger condition:** driver extraction returns invalid schema output after N retries. N is not given.
- **Guard:** state the retry count and the user-facing outcome (ask the developer to rephrase, or fall back to asking every critical driver).
- **Potential consequence:** selection dead-ends with a raw error at the first step of UJ-1.

---

## Low

### EC-32 - Equal scores have no tie-break
- **Location:** FR-2, FR-4, FR-5
- **Trigger condition:** two cards get exactly the same score.
- **Guard:** define a deterministic tie-break (for example the card's catalog order).
- **Potential consequence:** "same ranking" (FR-2) is not truly deterministic.

### EC-33 - `combines_with` may be declared on one side only
- **Location:** FR-6
- **Trigger condition:** card A lists B in `combines_with`, but card B does not list A. Or a three-pattern combination is valid pairwise only in part.
- **Guard:** require symmetric declarations (validated when the catalog loads), and define the rule for combinations of more than two patterns.
- **Potential consequence:** a combination is accepted or rejected depending on the order the patterns are considered.

### EC-34 - Regeneration overwrites manual edits to `ARCHITECTURE.md`
- **Location:** FR-10, FR-32
- **Trigger condition:** the developer adds notes to `ARCHITECTURE.md`, which is not tamper-protected, and growth then regenerates it.
- **Guard:** define the regions of the file that are generated and those that the developer owns.
- **Potential consequence:** developer notes are lost without warning.

### EC-35 - Declared contracts may not have files yet
- **Location:** FR-19, FR-11
- **Trigger condition:** a module declares a contract location but no OpenAPI or JSON Schema file exists, or a module has no contracts at all.
- **Guard:** define whether a missing contract file fails the contract gate or is skipped.
- **Potential consequence:** the contract gate passes for no reason, or fails on a fresh scaffold (UJ-1 promises green tests).

### EC-36 - Misfit detection can contradict a declined growth
- **Location:** FR-28, FR-29
- **Trigger condition:** the feature failed because the developer declined a growth that would have made it fit, but the report says "does not fit the pattern".
- **Guard:** the exhaustion report should mention any declined growth that was relevant.
- **Potential consequence:** a misleading diagnosis pushes the developer to reshape a feature that only needed a module.
