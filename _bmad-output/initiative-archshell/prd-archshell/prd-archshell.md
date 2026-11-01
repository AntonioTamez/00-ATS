---
title: ArchShell PRD
status: final
created: 2026-10-30
updated: 2026-10-31
---

# ArchShell PRD

This PRD defines what ArchShell v1 (public launch, Phase 1) must do for developers and why. Technical design (engine interface, scoring internals, folder layout, mechanism choices) lives in the source design README and `addendum.md`.

## 1. Vision

**ArchShell lets developers hand full features to an AI agent without reviewing the structure.** The architecture is chosen once, written down as rules, and enforced by blocking gates on every change. Developers trust that the code stays maintainable and extensible, and they spend their review time on business logic instead of layer violations.

### 1.1 Product Principles

1. **The LLM writes code; templates, contracts, the compiler, and architecture tests decide whether it is accepted.**
2. **Deterministic process, not deterministic output.** Every run passes the same steps and the same blocking gates.
3. **Architecture is code.** Every rule that matters lives in `architecture.yaml` and becomes a failing test when violated. Prose is never an enforcement mechanism.
4. **Ask, don't guess.** When the request does not identify the pattern clearly, ArchShell asks, one question at a time. Model output is never repaired by guessing.
5. **Decisions are written down.** The architecture is recorded once and every feature reads it first.
6. **Never relax a rule.** The chosen pattern and its rules are immutable; the charter can only grow within them.

## 2. Target User

**Early adopters:** developers with low-to-medium knowledge of architecture patterns, working alone or in teams of 1–5, who start **new** .NET/C# projects with an AI coding agent (Claude Code in v1).

Because they cannot reliably choose or enforce a pattern on their own, ArchShell must ask plain-language questions, explain its proposal, and carry the enforcement for them.

### 2.1 Non-Users (v1)

- Teams adopting ArchShell into an **existing codebase** (brownfield). v1 works only on new projects.
- Stacks other than .NET/C#, and agent runtimes other than Claude Code (planned as later packs and host adapters).

### 2.2 Jobs To Be Done

1. **Choose:** "Help me pick the right architecture for my new project, even though I'm not an expert."
2. **Start:** "Give me a project already structured in that architecture."
3. **Delegate:** "Let the agent build full features without me reviewing the structure."
4. **Trust:** "Block any change that breaks the rules, so the code stays maintainable and extensible."

### 2.3 Key User Journeys

#### UJ-1: First architecture selection
**Lucía**, a junior developer (1 year of experience) working alone, needs to start an **appointments API for a clinic** in .NET.

1. **Entry:** in Claude Code she writes: *"I need an API to manage appointments, patients, and billing for a clinic."* (in Spanish)
2. **Elicitation:** ArchShell finds three business modules but read/write asymmetry is `unknown`. It asks, in Spanish and plain language: *"Do the screens that show data, like reports or schedules, need the information shaped differently from how it is stored?"* She answers: *"No, it's shown almost the same."*
3. **Proposal:** ArchShell proposes **Clean + Screaming**, explains why (modules by business capability, domain separated from infrastructure), and why CQRS was rejected (no read/write asymmetry).
4. **Override check:** she asks *"What if I use Hexagonal?"* ArchShell shows what she would gain and lose. She confirms the original proposal.
5. **Resolution:** ArchShell writes the ADR, `ARCHITECTURE.md`, `architecture.yaml`, and scaffolds the solution with `Appointments`, `Patients`, and `Billing` already referenced, plus green architecture tests. Everything compiles; she has a foundation she cannot break without noticing.

Realized by: FR-1–FR-17, FR-15a, FR-39, FR-42.

#### UJ-2: First gate rejection
Two weeks later, Lucía asks: *"Add appointment cancellation. If it is cancelled less than 24 h ahead, charge a fee and notify the patient."*

1. **Spec:** ArchShell writes S1 (cancel), S2 (fee under 24 h), S3 (notify the patient), and a RED test first.
2. **Charter growth:** notifying needs a `Notifications` module that the charter does not declare. ArchShell stops and proposes it: *"I'll add the `Notifications` module; it respects Clean."* Lucía confirms.
3. **Rejection:** the agent writes code in `Appointments.Application` that uses an internal `Billing` class. The per-edit check blocks it immediately: *"Rule `no-cross-module-internals`: Appointments may only use `Billing.Contracts`."*
4. **Self-correction:** the agent switches to the public `Billing` contract and retries.
5. **Full gates:** at task completion, build, architecture, contract, and unit tests all pass.
6. **Resolution:** Lucía reviews **only the fee logic**. She does not look at the structure, because a structural mistake could not have passed.

Realized by: FR-13, FR-18–FR-30, FR-41.

## 3. Glossary

- **Pattern card:** catalog entry for one architecture pattern: signals, anti-signals, combinations, exclusions, discriminating questions.
- **Driver:** a project characteristic extracted from the request (for example domain complexity, read/write asymmetry), or `unknown`.
- **Charter:** the ADR, `ARCHITECTURE.md`, and `architecture.yaml` that record the chosen architecture.
- **Gate:** a blocking check (build, architecture, contract, unit tests); any failure rejects the change.
- **Charter growth:** adding modules or contracts within the chosen pattern, confirmed by the developer.
- **Language pack / host adapter / provider:** extension plugins for a programming language, an agent runtime, and a model vendor.

## 4. Features

FR IDs are stable identifiers; some appear out of numeric order within their feature.

### 4.1 Pattern Selection

**Description:** The developer describes the new project in natural language. ArchShell extracts the project's drivers, scores every pattern card, and either proposes a pattern (or a compatible combination) or asks plain-language questions until it can. The developer always confirms the proposal and may override it after hearing the tradeoffs. Serves JTBD 1 (Choose).

**Functional Requirements:**

#### FR-1: Driver extraction
ArchShell extracts drivers (domain complexity, scale, audit needs, read/write asymmetry, deployment model, team size, integrations) from the request into a closed schema.
**Consequences (testable):**
- Every driver the request does not establish is recorded as `unknown`; none is guessed.
- Business modules are extracted from the request and shown in the proposal (FR-7) for the developer to confirm or edit. Modules confirmed here seed `modules` in `architecture.yaml`.
- Model output that does not match the schema is retried a bounded number of times, then reported as an error.

#### FR-2: Deterministic scoring
Patterns are scored from the extracted drivers by deterministic rules, never by the model.
**Consequences (testable):**
- The same drivers always produce the same scores and ranking.

#### FR-3: Discriminating questions
When the proposal criteria (FR-4) are not met, ArchShell asks the question that best separates the top candidates.
**Consequences (testable):**
- One question at a time; ArchShell waits for the answer before continuing.
- Questions use plain language that a developer with low architecture knowledge can answer, in the developer's language (EN/ES).
- Questions about drivers that do not separate the top candidates are never asked.
- At most 5 questions per selection.

#### FR-4: Automatic proposal
ArchShell proposes a pattern only when all criteria hold: top score ≥ 0.75, margin over runner-up ≥ 0.15, no critical driver `unknown`, and no `excludes` conflict in the combination.
**Consequences (testable):**
- Threshold values are configuration, initially as above, calibrated against the golden set (see Success Metrics).
- A critical driver still `unknown` blocks the proposal only if resolving it could change the top candidate; otherwise it is recorded as `unknown` in the ADR and does not block.

#### FR-5: Fallback to human choice
If the question budget runs out without meeting FR-4, ArchShell does not choose. It presents the top two candidates with their tradeoffs, and the developer decides.
**Consequences (testable):**
- The ADR records the outcome as a human decision.

#### FR-6: Compatible combinations
ArchShell can propose combinations (for example Modular Monolith + Vertical Slice, or Clean + Screaming + CQRS) resolved through each card's `combines_with` and `excludes`.

#### FR-7: Confirmation before writing
Nothing is written to the project until the developer confirms the proposal.

#### FR-42: New-project check
Before selection, ArchShell checks that the target is a new project and stops with an explanation otherwise. "New" means no existing solution, project files, or source code in the target folder.

#### FR-8: Override with explained tradeoffs
When the developer rejects or questions the proposal, ArchShell explains the benefits of its proposal and what the developer's alternative would lose. The developer has the final word.
**Consequences (testable):**
- The accepted override is recorded in the ADR as a human decision, including the tradeoffs that were explained.
- An override that creates an `excludes` conflict is refused: ArchShell explains why the patterns exclude each other and asks the developer to choose one of them.
- Every proposal and alternative shows whether a template ships for it, so the developer knows before confirming whether it will end in a gap report (FR-17).

### 4.2 Architecture Charter

**Description:** Once the developer confirms the pattern, ArchShell records the decision once and every later feature reads it before generating anything. The charter has a human view (ADR, `ARCHITECTURE.md`) and a machine view (`architecture.yaml`) that the gates use as the source of truth. Serves JTBD 1 (Choose) and 4 (Trust).

**Functional Requirements:**

#### FR-9: ADR
ArchShell writes `docs/adr/0001-architecture.md` with the chosen pattern(s), the drivers, the rejected alternatives and why, and whether the decision was automatic or human (FR-5, FR-8).

#### FR-10: Architecture guide
ArchShell writes `ARCHITECTURE.md` with layers, responsibilities, rules in prose, and examples, as guidance for humans and the agent.

#### FR-11: Machine-readable charter
ArchShell writes `architecture.yaml` declaring the language, patterns, modules, layers and allowed dependencies, rules, naming, and contract locations.
**Consequences (testable):**
- `architecture.yaml` is language-neutral except for its `language` field.
- Every rule in it maps to at least one generated test or compile-time constraint (see 4.4).

#### FR-12: English artifacts
All generated code and documentation are written in English, regardless of the conversation language.

#### FR-13: Charter read before generation
Every feature workflow (4.5) reads the charter before generating anything.

#### FR-14: Charter tamper protection
ArchShell detects and blocks changes to the protected set that do not come from the charter growth flow (4.6), whether the developer, a teammate, or the agent makes them.
**Consequences (testable):**
- The protected set includes `architecture.yaml`, the generated architecture tests, project references (`<ProjectReference>`) in `.csproj` files (package references and other settings stay editable), build settings that affect the gates (for example warnings as errors in `Directory.Build.props`), the inclusion of the test projects in the solution, and ArchShell's hook configuration.
- In-session hooks block tampering early; the authoritative check is `archshell verify` in CI (FR-41), which the agent cannot reach.
- An agent cannot pass a failing gate by loosening a rule or editing a generated test.
- The block names the changed file and points to the charter growth flow (4.6).

### 4.3 Scaffolding

**Description:** After the charter is written, ArchShell creates the project structure from the active language pack's templates. The model never decides structure; the templates do. Serves JTBD 2 (Start).

**Functional Requirements:**

#### FR-15: Template-based scaffolding
ArchShell creates the solution, projects, and folders for the confirmed pattern or combination from the language pack's templates.
**Consequences (testable):**
- The launch .NET/C# language pack ships a pre-built, validated template for each of the 18 catalog patterns and for three combinations: Clean + Screaming, Clean + Screaming + CQRS, and Modular Monolith + Vertical Slice (per module).
- The generated structure matches `architecture.yaml` (layers, modules, naming).

#### FR-15a: Atomic charter and scaffold
Writing the charter (FR-9–FR-11) and scaffolding (FR-15, FR-16) form one atomic operation.
**Consequences (testable):**
- If scaffolding fails (for example missing .NET SDK, package restore error, or the developer cancels), ArchShell rolls everything back and the developer can retry from the start.
- The charter becomes immutable (FR-31) only after scaffolding completes and the generated architecture tests pass.

#### FR-16: Dependencies wired to the rules
ArchShell generates project references that match each layer's `may_depend_on`, so a forbidden dependency does not compile.

#### FR-17: Missing-template gap report
When the confirmed pattern combination or a team-added custom card (FR-38) has no template, ArchShell stops before scaffolding and writes a gap report. FR-15 lists the templates that ship at launch.
**Consequences (testable):**
- The report names the chosen pattern or combination, the missing template, and what the template must contain.
- No partial structure is generated.
- The developer can share the report with a tech lead, who extends the language pack with the missing template.
- A solo developer acts as their own tech lead: they add the template themselves (extra catalog or language pack extension, FR-38) or open an issue in the ArchShell repository with the gap report as its content, so each report doubles as a community template request.

### 4.4 Enforcement Gates

**Description:** Every change the agent makes passes through blocking gates generated from the charter. Fast checks run on each edit so violations surface immediately; the full gates run when the agent finishes a task. Any failure blocks. Serves JTBD 3 (Delegate) and 4 (Trust).

**Enforcement strength (strongest to weakest):** project references (does not compile) → generated architecture tests → contract tests → prose in `ARCHITECTURE.md` (guidance only, never the last line of defense).

**Functional Requirements:**

#### FR-18: Generated architecture tests
ArchShell generates architecture tests from `architecture.yaml` (NetArchTest / ArchUnitNET in the .NET language pack) covering layer dependencies, naming, port location, and cross-module rules.
**Consequences (testable):**
- Generated tests are never hand-edited (enforced by FR-14).
- Regenerating from an unchanged `architecture.yaml` produces the same tests.

#### FR-19: Contract tests
ArchShell generates tests that check implementations conform to the published contracts (OpenAPI / JSON Schema) declared in `architecture.yaml`.

#### FR-20: Fast per-edit checks
After each file the agent edits, ArchShell runs fast checks for dependency and naming rules and blocks the edit on a violation.

#### FR-21: Full gates at task completion
When the agent reports a task as done, ArchShell runs the full gates: build with warnings as errors, architecture tests, contract tests, and unit tests.
**Consequences (testable):**
- Any failing gate blocks the task from being reported as done.
- The full gates also run whenever the session stops for any reason (interrupted, cancelled, or ended); a task stopped before passing them is reported as *not verified*.
- Code changed outside the hooks' view (for example through shell commands) is caught by the full gates at stop and by `archshell verify` in CI (FR-41).

#### FR-22: Actionable failure report
Every gate failure names the violated rule, the file, and the violation, in a form the agent can act on.
**Consequences (testable):**
- Environment failures (for example missing .NET SDK, package restore or network errors) are reported separately from rule violations and do not consume retry attempts (FR-26).
- The report is also shown to the developer in their conversation language.

#### FR-41: CI verification
`archshell verify` runs all checks deterministically, without a model, in the project's CI pipeline on every push and pull request.
**Consequences (testable):**
- It regenerates the architecture tests from `architecture.yaml` and fails if the committed tests differ.
- It fails if any project reference violates `may_depend_on`.
- It fails if a change to `architecture.yaml` has no matching growth record (FR-32).
- It runs the full gates (FR-21).
- ArchShell generates the CI workflow file; documentation explains how to make the check required for merging (branch protection).

### 4.5 Feature Workflow

**Description:** Every feature follows the same sequence, so two runs may produce different code but always pass the same steps and the same blocking gates: read charter → spec → ask about gaps → scaffold from templates → define ports and contracts → RED test → agent implements → gates. Serves JTBD 3 (Delegate).

**Functional Requirements:**

#### FR-23: Feature spec
ArchShell turns the feature request into a numbered spec (S1..Sn) and asks about gaps one question at a time before generating code.

#### FR-24: Structure before implementation
ArchShell scaffolds the feature's structure from the pattern templates and defines its ports and contracts before generating any implementation.

#### FR-25: RED test first
A failing test for the feature exists and is observed failing before the agent implements it.

#### FR-26: Bounded retries
When a gate fails, ArchShell rejects the change and the agent retries, up to 5 attempts per task.
**Consequences (testable):**
- A task is one spec item (S#); an attempt is one run of the full gates (FR-21); per-edit blocks (FR-20) do not count as attempts; the count resets when the developer rephrases the spec or fixes code by hand.

#### FR-27: Exhaustion report
After the 5th failed attempt, ArchShell stops and shows a report.
**Consequences (testable):**
- The report names the rule that kept failing, what the agent tried in each attempt, and its best guess at why.
- The developer can fix the code by hand or rephrase the spec.
- Hand fixes must pass the same full gates.

#### FR-28: Misfit detection
When the exhausted failures suggest the feature does not fit the chosen pattern, the report says so explicitly. The pattern and its rules are never changed or relaxed; the developer must reshape the feature to fit.

### 4.6 Charter Growth

**Description:** The chosen pattern and its rules are immutable for the life of the project. The charter can only **grow within the pattern**, for example when a feature needs a new module or a new contract. ArchShell proposes the growth, checks it against the pattern's rules, and applies it only after the developer confirms. Serves JTBD 3 (Delegate) and 4 (Trust).

**Functional Requirements:**

#### FR-29: Growth proposal
When a feature needs something the charter does not declare (for example a new module or contract), ArchShell stops and proposes the growth, explaining what would be added and why.
**Consequences (testable):**
- The proposal is checked against the pattern's rules; growth that would violate them is never proposed.
- Growth that duplicates or conflicts with an existing module or contract name is never proposed; ArchShell explains the conflict instead.

#### FR-30: Developer confirmation
Growth is applied only after the developer confirms it. The agent can never approve growth.
**Consequences (testable):**
- If the developer declines, the feature is treated like a misfit (FR-28): it stops with a report, and the developer reshapes the feature.
- The in-session confirmation is a fast first step; the authoritative approval is a human review of the pull request that changes the charter.
- ArchShell generates a `CODEOWNERS` file that assigns the charter files (`architecture.yaml`, ADRs, generated architecture tests) to human owners, so a charter change cannot merge without their approval (with branch protection, FR-41).
- The agent commits and opens pull requests under its own GitHub identity (a bot or machine account), separate from every human owner, so a human always approves, including on solo projects. Setup documentation covers creating this identity and giving it no admin rights.

#### FR-31: Pattern immutability
Growth can never change the chosen pattern(s) or their rules.
**Consequences (testable):**
- Modules and contracts can grow; patterns, layers, `may_depend_on`, rules, and naming are locked.
- A feature that only fits by changing the pattern ends in the exhaustion report (FR-27, FR-28).

#### FR-32: Regeneration after growth
After confirmed growth, ArchShell regenerates `architecture.yaml`, the generated architecture tests, project references, and `ARCHITECTURE.md` consistently.
**Consequences (testable):**
- Each growth is recorded with date and reason as a new ADR (for example `0002-add-notifications-module.md`) that adds to, and never supersedes, the original architecture ADR.

### 4.7 Extensibility

**Description:** ArchShell is open source. The core never contains language-, host-, or vendor-specific code, so the community can add language packs (for example Java, TypeScript), host adapters (for example Codex, Gemini CLI, OpenCode), and LLM providers (for example OpenAI, Google, Ollama) without changing the core.

**Functional Requirements:**

#### FR-33: Language packs
A language pack provides scaffolding, dependency wiring, architecture and contract test generation, gates, naming conventions, and its list of supported patterns.

#### FR-34: Host adapters
A host adapter packages the core's workflow for an agent runtime (skills, agents, hooks, question UI) without changing workflow logic or prompts, which the core owns.

#### FR-35: LLM providers
A provider lets the engine call a model directly when the host has no native agent (Phase 2). Every model output is validated against a core-owned schema; invalid output gets a bounded retry, then becomes an error.

#### FR-36: Versioned contracts
Packs, adapters, and providers declare the contract version they implement; the core refuses incompatible versions with a clear message.

#### FR-37: Conformance gate
A new pack, adapter, or provider is enabled only after it passes the conformance suite (golden set + engine contract tests) with the same thresholds as the launch pack, adapter, and provider.
**Consequences (testable):**
- Contributors can run the conformance suite locally before submitting.

#### FR-38: Custom pattern cards
Teams can add their own pattern cards through an optional, configured extra catalog path.

#### FR-40: Self-contained runtime
At runtime ArchShell reads only what ships inside its plugins (catalog, templates, prompts, schemas) plus the optional extra catalog path (FR-38).
**Consequences (testable):**
- It never reads `02-ARQUITECTURA` (an authoring source only) at runtime, so it works in any repository.

### 4.8 Localization

**Description:** ArchShell talks to the developer in English or Spanish, following the language of the request. Everything it generates stays in English (FR-12).

**Functional Requirements:**

#### FR-39: Conversation language
Questions, proposals, tradeoff explanations, and reports are delivered in the language the developer uses (English or Spanish).

## 5. Non-Functional Requirements

- **NFR-1 Per-edit latency:** fast checks (FR-20) complete in ≤ 3 seconds on a typical developer machine.
- **NFR-2 Platforms:** Windows, macOS, and Linux.
- **NFR-3 Toolchain:** .NET 10 LTS or later; Python 3.12 or later as a prerequisite, installable with `pipx` or `uv`.
- **NFR-4 Determinism:** the same inputs produce the same scores, charter, generated tests, and gate verdicts (FR-2, FR-18).
- **NFR-5 No model in CI:** `archshell verify` (FR-41) needs no model and no network beyond package restore.
- **NFR-6 Privacy:** ArchShell sends project code to no service other than the host's own model.

## 6. Non-Goals (Explicit)

- **Existing codebases (brownfield).** ArchShell works only on new projects.
- **Changing the chosen pattern.** The pattern and its rules are immutable for the life of the project; only charter growth within the pattern is allowed (4.6).
- **Functional correctness.** Gates guarantee structure and contracts (maintainability, extensibility), not that business logic is bug-free. Unit tests catch what they cover; reviewing logic stays with the developer.
- **Test quality.** ArchShell enforces that a RED test exists first and that tests pass; it does not judge whether the agent's unit tests are meaningful.
- **Identical output.** Two runs may produce different code; only the process and the gates are deterministic.

## 7. MVP Scope

### 7.1 In Scope (public launch = Phase 1)

- Claude Code host adapter (plugin: skills, agents, hooks).
- .NET/C# language pack with templates for all 18 catalog patterns plus three combinations (FR-15).
- Features 4.1–4.8, except direct model calls through providers (FR-35).
- English and Spanish conversation; English artifacts.
- Open-source release with versioned extension contracts and the conformance suite.
- `archshell verify` CI mode (FR-41), the only Phase 2 command brought into the launch.

### 7.2 Launch Acceptance Criteria

- Given three sample requests, ArchShell selects the expected pattern(s) or asks a discriminating question.
- It writes the ADR, `ARCHITECTURE.md`, `architecture.yaml`, and the generated architecture tests.
- The gates detect 100% of the seeded-violation corpus for every shipped template.

### 7.3 Out of Scope for MVP

- The rest of the Phase 2 standalone CLI (`archshell select | feature | grow`).
- LLM providers beyond the host's own agent (OpenAI, Google, Ollama).
- Language packs other than .NET/C#; host adapters other than Claude Code.

## 8. Success Metrics

These measure whether ArchShell keeps its promise. Adoption metrics are intentionally not tracked.

**Primary**
- **Zero deviations:** the gates detect 100% of a seeded-violation corpus, a fixed set of features written on purpose to break each rule type (for example a Domain → Adapters dependency, a port declared outside Application, use of another module's internals, a forbidden `<ProjectReference>`), run against every shipped template. The corpus doubles as ArchShell's own regression suite.

**Secondary**
- **Correct selections** on the golden set (15–20 requests with expected patterns, derived from `02-ARQUITECTURA/*/sample`): target ≥ 90% correct and 0 wrong automatic proposals. Wrong selections are the costly error, so calibration is biased toward asking.

**Counter-metrics (do not optimize)**
- **Unnecessary questions:** questions that did not change the selected pattern.
- **Exhausted retries:** tasks that hit the 5-attempt limit (FR-27). Blocking everything keeps the rules but defeats the Delegate job.

## 9. Open Questions

Non-blocking; deferred with owner and revisit condition.

1. **Interrupted sessions and concurrent branches** (owner: architecture; revisit: architecture design). How a resumed session restores its workflow state, and how charter growth on two team branches is reconciled at merge. `archshell verify` (FR-41) remains the backstop.

## 10. Assumptions Index

None open. All assumptions were confirmed by the product owner on 2026-10-31.

