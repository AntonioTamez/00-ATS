---
title: ArchShell PRD
status: final
created: 2026-10-30
updated: 2026-11-08
---

# ArchShell PRD

This PRD defines what ArchShell v1 (public launch, Phase 1) must do for developers and why. Technical design (engine interface, scoring internals, folder layout, mechanism choices) lives in the source design README and `addendum.md`.

## 1. Vision

**ArchShell lets developers hand full features to an AI agent without reviewing the structure; they only confirm new business decisions.** The architecture is chosen once, written down as rules, and enforced by blocking gates on every change. Developers trust that the code stays maintainable and extensible, and they spend their review time on business logic instead of layer violations.

### 1.1 Product principles

1. **The LLM writes code; templates, contracts, the compiler, and architecture tests decide whether it is accepted.**
2. **Deterministic process, not deterministic output.** Every run passes the same steps and the same blocking gates.
3. **Architecture is code.** Every rule that matters lives in `architecture.yaml` and becomes a failing test when violated. Prose is never an enforcement mechanism.
4. **Ask, don't guess.** When the request does not identify the pattern clearly, ArchShell asks, one question at a time. Model output is never repaired by guessing.
5. **Decisions are written down.** The architecture is recorded once and every feature reads it first.
6. **Never relax a rule.** The chosen pattern and its rules are immutable; the charter can only grow within them.

## 2. Target user

**Early adopters:** developers with low-to-medium knowledge of architecture patterns, working alone or in teams of 1–5, who start **new** .NET/C# projects with an AI coding agent (Claude Code in v1).

Because they cannot reliably choose or enforce a pattern on their own, ArchShell must ask plain-language questions, explain its proposal, and carry the enforcement for them.

### 2.1 Non-users (v1)

- Teams adopting ArchShell into an **existing codebase** (brownfield). v1 works only on new projects.
- Stacks other than .NET/C#, and agent runtimes other than Claude Code (planned as later packs and host adapters).

### 2.2 Jobs to be done

1. **Choose:** "Help me pick the right architecture for my new project, even though I'm not an expert."
2. **Start:** "Give me a project already structured in that architecture."
3. **Delegate:** "Let the agent build full features without me reviewing the structure."
4. **Trust:** "Block any change that breaks the rules, so the code stays maintainable and extensible."

### 2.3 Key user journeys

#### UJ-1: First architecture selection
**Lucía**, a junior developer (1 year of experience) working alone, needs to start an **appointments API for a clinic** in .NET.

1. **Entry:** in Claude Code she writes: *"I need an API to manage appointments, patients, and billing for a clinic."* (in Spanish)
2. **Elicitation:** ArchShell finds three business modules but read/write asymmetry is `unknown`. It asks, in Spanish and plain language: *"Do the screens that show data, like reports or schedules, need the information shaped differently from how it is stored?"* She answers: *"No, it's shown almost the same."*
3. **Proposal:** ArchShell proposes **Clean + Screaming**, explains why (modules by business capability, domain separated from infrastructure), and why CQRS was rejected (no read/write asymmetry).
4. **Override check:** she asks *"What if I use Hexagonal?"* ArchShell shows what she would gain and lose. She confirms the original proposal.
5. **Resolution:** ArchShell writes the ADR, `ARCHITECTURE.md`, `architecture.yaml`, and scaffolds the solution with `Appointments`, `Patients`, and `Billing` already referenced, plus green architecture tests, and a feature backlog with the capabilities she asked for (schedule appointments, register patients, invoice visits). Everything compiles; she has a foundation she cannot break without noticing. ArchShell offers to start the first backlog item.

Realized by: FR-1–FR-17, FR-15a, FR-39, FR-42, FR-52, FR-58.

#### UJ-2: First gate rejection
Two weeks later, Lucía asks: *"Add appointment cancellation. If it is cancelled less than 24 h ahead, charge a fee and notify the patient."*

1. **Spec:** ArchShell writes S1 (cancel), S2 (fee under 24 h), and S3 (notify the patient).
2. **One confirmation: growth, plan, and contract.** Notifying needs a `Notifications` module that the charter does not declare, so ArchShell proposes the growth first (*"I'll add the `Notifications` module; it respects Clean."*). It then shows the feature plan derived against the grown charter (`CancelAppointmentHandler`, a notification port in `Notifications`, and their tests; the existing `Appointment` entity is marked as modified), and the contract in plain Spanish: *"Cancel appointment: receives the appointment id and an optional reason; returns the fee charged, if any."* Lucía confirms everything at once; the contract is frozen, and a RED test is written against it before any implementation.
3. **Rejection:** the agent writes code in `Appointments.Application` that uses an internal `Billing` class. The per-edit check blocks it immediately: *"Rule `no-cross-module-internals`: Appointments may only use `Billing.Contracts`."*
4. **Self-correction:** the agent switches to the public `Billing` contract and retries.
5. **Full gates:** At task completion, the build and the architecture, contract, and unit tests all pass.
6. **Resolution:** Lucía reviews **only the fee logic**. She does not look at the structure, because a structural mistake could not have passed.

Realized by: FR-13, FR-18–FR-30, FR-41, FR-43–FR-51, FR-53, FR-55–FR-59.

## 3. Glossary

- **Bootstrap:** the atomic charter-and-scaffold operation (FR-15a).
- **Building block:** one file-level unit of a feature in a given pattern (for example command handler, port, adapter, endpoint, entity, test), with its location and naming fixed by a template.
- **Charter:** the ADRs, `ARCHITECTURE.md`, `architecture.yaml`, and the domain vocabulary (FR-58) that record the chosen architecture.
- **Charter growth:** adding modules or contract locations within the chosen pattern, confirmed by the developer. Adding or changing operations inside a declared contract location is feature-level (FR-47), not growth.
- **Contract:** an operation a feature exposes or depends on, with its inputs and outputs. **Internal** contracts are ports owned by the Application layer (for example a notification sender); **public** contracts are consumed by other modules or clients (a module's `Contracts`, the HTTP API).
- **Core:** the language-, host-, and vendor-neutral part of ArchShell that owns the workflow, prompts, schemas, and extension contracts.
- **Critical driver:** a driver that resolving could change the top candidate (FR-4).
- **Domain vocabulary:** the project's closed list of business nouns (entities and concepts) and the verbs allowed on each; the only source of names for generated code (FR-58).
- **Driver:** a project characteristic extracted from the request (for example domain complexity, read/write asymmetry), or `unknown`.
- **Extension contract:** the versioned interface a language pack, host adapter, or provider implements (FR-36); not a feature contract.
- **Feature plan:** the confirmed list of a feature's spec items, intents, building blocks, file paths, names, and contracts; the only structure the agent may create for that feature. A plan is *confirmed* either by the developer's confirmation or, when it carries no new business decisions, by the derived path of FR-59, whose confirmation record is marked *derived, no new decisions*.
- **Gate:** a blocking check (build, architecture, contract, unit tests); any failure rejects the change.
- **Golden set:** the fixed set of sample requests with expected patterns used to calibrate and measure pattern selection (see Success metrics). The *intent golden set* is the equivalent set of sample spec items with expected intents and tokens (7.2).
- **Intent:** the kind of change a spec item asks for, from the closed, versioned intent set defined in FR-43.
- **Language pack / host adapter / provider:** extension plugins for a programming language, an agent runtime, and a model vendor.
- **Pattern card:** catalog entry for one architecture pattern: signals, anti-signals, combinations, exclusions, discriminating questions.
- **Policy:** a cross-cutting rule declared as data for an operation (authorization, audit, transaction) and applied by the template's pipeline, never written in method bodies (FR-60).
- **Seeded-violation corpus:** a fixed set of features written on purpose to break each rule type, run against every shipped project template; its members are listed in Success metrics.
- **Task:** one spec item (S#); full gates and attempts are counted per task (FR-26).

## 4. Features

FR IDs are stable identifiers; some appear out of numeric order within their feature. The FR index (Appendix A) maps every FR ID to its section.

### 4.1 Pattern selection

**Description:** The developer describes the new project in natural language. ArchShell extracts the project's drivers, scores every pattern card, and either proposes a pattern (or a compatible combination) or asks plain-language questions until it can. The developer always confirms the proposal and may override it after hearing the tradeoffs. Serves JTBD 1 (Choose).

**Functional requirements:**

#### FR-42: New-project check
Before selection, ArchShell checks that the target is a new project and stops with an explanation otherwise. "New" means no existing solution, project files, or source code in the target folder.

#### FR-1: Driver extraction
ArchShell extracts drivers (domain complexity, scale, audit needs, read/write asymmetry, deployment model, team size, integrations) from the request into a closed schema.
**Consequences (testable):**
- Every driver the request does not establish is recorded as `unknown`; none is guessed.
- Business modules are extracted from the request and shown in the proposal (FR-7) for the developer to confirm or edit. Modules confirmed here seed `modules` in `architecture.yaml`.
- The business capabilities the request asks for are extracted too, per module, and seed the feature backlog (FR-52).
- Model output that does not match the schema is retried within bounds, then reported as an error (NFR-7).

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
ArchShell proposes a pattern only when all criteria hold: top score ≥ 0.75, margin over runner-up ≥ 0.15, no critical driver (one whose resolution could change the top candidate) still `unknown`, and no `excludes` conflict in the combination.
**Consequences (testable):**
- Threshold values are configuration, initially as above, calibrated against the golden set (see Success metrics).
- An `unknown` driver whose resolution could not change the top candidate is recorded as `unknown` in the ADR and does not block.

#### FR-5: Fallback to human choice
If the question limit (FR-3) runs out without meeting FR-4, ArchShell does not choose. It presents the top two candidates with their tradeoffs, and the developer decides.
**Consequences (testable):**
- The ADR records the outcome as a human decision.

#### FR-6: Compatible combinations
ArchShell can propose combinations (for example Modular Monolith + Vertical Slice, or Clean + Screaming + CQRS) resolved through each card's `combines_with` and `excludes`.

#### FR-7: Confirmation before writing
Nothing is written to the project until the developer confirms the proposal.

#### FR-8: Override with explained tradeoffs
When the developer rejects or questions the proposal, ArchShell explains the benefits of its proposal and what the developer's alternative would lose. The developer has the final word.
**Consequences (testable):**
- The accepted override is recorded in the ADR as a human decision, including the tradeoffs that were explained.
- An override that creates an `excludes` conflict is refused: ArchShell explains why the patterns exclude each other and asks the developer to choose one of them.
- Every proposal and alternative shows whether a project template ships for it, so the developer knows before confirming whether that choice will end in a gap report (FR-17).

### 4.2 Architecture charter

**Description:** Once the developer confirms the pattern, ArchShell records the decision once and every later feature reads it before generating anything. The charter has a human view (ADR, `ARCHITECTURE.md`) and a machine view (`architecture.yaml`) that the gates use as the source of truth. Changes to the charter are controlled as described in 4.9. Serves JTBD 1 (Choose) and 4 (Trust).

**Functional requirements:**

#### FR-9: ADR
ArchShell writes `docs/adr/0001-architecture.md` with the chosen pattern(s), the drivers, the rejected alternatives and why, and whether the decision was automatic or human (FR-5, FR-8).

#### FR-10: Architecture guide
ArchShell writes `ARCHITECTURE.md` with layers, responsibilities, rules in prose, and examples, as guidance for humans and the agent.

#### FR-11: Machine-readable charter
ArchShell writes `architecture.yaml` declaring the language, patterns, modules, layers and allowed dependencies, rules, naming, and contract locations.
**Consequences (testable):**
- `architecture.yaml` is language-neutral except for its `language` field.
- Every rule in it maps to at least one generated test or compile-time constraint (see 4.4).

#### FR-13: Charter read before generation
Every feature workflow (4.5) reads the charter before generating anything.

### 4.3 Scaffolding

**Description:** After the charter is written, ArchShell creates the project structure from the active language pack's templates. The model never decides structure; the templates do. Serves JTBD 2 (Start).

**Functional requirements:**

#### FR-15: Template-based scaffolding
ArchShell creates the solution, projects, and folders for the confirmed pattern or combination from the language pack's templates.
**Consequences (testable):**
- The launch .NET/C# language pack ships a pre-built, validated project template for each of the 18 catalog patterns and for three combinations: Clean + Screaming, Clean + Screaming + CQRS, and Modular Monolith + Vertical Slice (per module).
- The generated structure matches `architecture.yaml` (layers, modules, naming).

#### FR-15a: Atomic charter and scaffold
Writing the charter (FR-9–FR-11) and scaffolding (FR-15, FR-16) form one atomic operation.
**Consequences (testable):**
- If scaffolding fails or is cancelled (for example missing .NET SDK, package restore error, or the developer cancels), ArchShell rolls everything back and the developer can retry from the start.
- The charter becomes immutable (FR-31) only after scaffolding completes and the generated architecture tests pass.
- The feature backlog (FR-52) and the seeded domain vocabulary (FR-58) are written in the same operation and rolled back with it.
- Bootstrap generates no business code; every capability is built later through the feature workflow (4.5).

#### FR-16: Dependencies wired to the rules
ArchShell generates project references that match each layer's `may_depend_on`, so a forbidden dependency does not compile.

#### FR-17: Missing-template gap report
When the confirmed pattern or combination, or a team-added custom card (FR-38), has no project template, ArchShell stops before scaffolding and writes a gap report. FR-15 lists the project templates that ship at launch.
**Consequences (testable):**
- The report names the chosen pattern or combination, the missing project template, and what the project template must contain.
- No partial structure is generated.
- The developer can share the report with a tech lead, who extends the language pack with the missing project template.
- A solo developer acts as their own tech lead: they add the project template themselves (extra catalog or language pack extension, FR-38) or open an issue in the ArchShell repository with the gap report as its content, so each report doubles as a community template request.

#### FR-52: Feature backlog
ArchShell records the business capabilities extracted from the initial request (FR-1) as a feature backlog, grouped by module.
**Consequences (testable):**
- The developer confirms or edits the backlog together with the proposal (FR-7); every extracted capability is shown before the backlog is written.
- For a module with no extracted capability, ArchShell asks for at least one. The developer may leave that module without items. If the whole backlog is empty, ArchShell skips the offer to start an item.
- If a hand edit leaves the backlog file unreadable, ArchShell asks the developer how to proceed instead of silently skipping items.
- The backlog is stored in the project and versioned with it. It is not part of the protected set; the developer can add, edit, reorder, or remove items at any time.
- When bootstrap completes, ArchShell offers to start the first backlog item; the developer can pick any item or decline.
- Starting an item runs the normal feature workflow (4.5) with the item as the request; the item is marked done only when that feature passes the full gates (FR-21). The item links to its feature plan, and `archshell verify` warns about an item marked done whose plan has not passed the full gates.

### 4.4 Enforcement gates

**Description:** Every change the agent makes passes through blocking gates generated from the charter. Fast per-edit checks run on each edit so violations surface immediately; the full gates run when the agent finishes a task. Any failure blocks. Serves JTBD 3 (Delegate) and 4 (Trust).

**Enforcement strength (strongest to weakest):** project references (does not compile) → generated architecture tests → contract tests → prose in `ARCHITECTURE.md` (guidance only, never the last line of defense).

**Functional requirements:**

#### FR-18: Generated architecture tests
ArchShell generates architecture tests from `architecture.yaml` (NetArchTest / ArchUnitNET in the .NET language pack) covering layer dependencies, naming, port location, and cross-module rules.
**Consequences (testable):**
- Generated tests are never hand-edited (enforced by FR-14).
- Regenerating from an unchanged `architecture.yaml` produces the same tests.

#### FR-19: Contract tests
ArchShell generates tests that check that implementations conform to the published contracts (OpenAPI / JSON Schema) declared in `architecture.yaml`.

#### FR-20: Fast per-edit checks
After each file the agent edits, ArchShell runs fast per-edit checks for dependency and naming rules and blocks the edit on a violation.

#### FR-21: Full gates at task completion
When the agent reports a task (one spec item, FR-26) as done, ArchShell runs the full gates: build with warnings as errors, architecture tests, contract tests, and unit tests.
**Consequences (testable):**
- Any failing gate blocks the task from being reported as done.
- The full gates also run whenever the session stops for any reason (interrupted, cancelled, or ended); a task stopped before passing them is reported as *not verified*.
- Code changed outside the view of ArchShell's in-session hooks (for example through shell commands) is caught by the full gates at stop and by `archshell verify` in CI (FR-41).

#### FR-22: Actionable failure report
Every gate failure names the violated rule, the file, and the violation, in a form the agent can act on.
**Consequences (testable):**
- Environment failures (for example missing .NET SDK, package restore or network errors) are reported separately from rule violations and do not consume attempts (FR-26).
- The report is also shown to the developer in their conversation language.

#### FR-41: CI verification
`archshell verify` runs all checks deterministically, without a model, in the project's CI pipeline on every push and pull request.
**Consequences (testable):**
- It regenerates the architecture tests from `architecture.yaml` and fails if the committed tests differ.
- It fails if any project reference violates `may_depend_on`.
- It fails if a change to `architecture.yaml` has no matching charter-growth confirmation record (FR-32, FR-53).
- It fails if any change to the protected set (FR-14) has no matching confirmation record (FR-53), or if a record's confirmed content does not match the committed change.
- It regenerates each feature's scaffold and contract code from its confirmed plan (FR-24, FR-48) and fails if the committed generated code differs.
- It runs the full gates (FR-21).
- It also runs the checks that other FRs assign to it: plan conformance (FR-46), surface conformance (FR-56), composition-file regeneration (FR-57), breaking-change confirmation (FR-51), and the backlog warning (FR-52).
- ArchShell generates the CI workflow file; documentation explains how to make the check required for merging (branch protection).

### 4.5 Feature workflow

**Description:** Every feature follows the same sequence, so two runs may produce different code but always pass the same steps and the same blocking gates:

1. Read the charter (FR-13).
2. Write the spec (FR-23).
3. Ask about gaps (FR-23).
4. Classify intents (FR-43, FR-44, FR-58, FR-60).
5. Derive the feature plan (FR-45).
6. Propose charter growth if the plan needs it, then re-derive (FR-55).
7. Propose contracts as data (FR-47).
8. The developer confirms growth, plan, and contracts at once, in plain language (FR-59, FR-49).
9. Generate structure and contracts from templates (FR-24, FR-48, FR-57).
10. Freeze contracts (FR-50).
11. Write the RED test (FR-25).
12. The agent implements (FR-54, FR-26–FR-28).
13. Run the gates (FR-46, FR-56, FR-51, FR-21).

The model never decides structure or contract code: it writes the spec and gap questions, classifies intents and supplies tokens, proposes contract data, writes the RED test, and fills method bodies; all feature structure and contract code come from tables, naming rules, and templates. Serves JTBD 3 (Delegate).

The requirements below are grouped by step: spec and classification, plan and contracts, confirmation, generation and freeze, conformance gates, and the implementation loop.

**Functional requirements:**

**Spec and classification (steps 2–4)**

#### FR-23: Feature spec
ArchShell turns the feature request into a numbered spec (S1..Sn) and asks about gaps one question at a time before generating code.

#### FR-58: Domain vocabulary
The charter includes a domain vocabulary: business nouns (entities and concepts) and the verbs allowed on each. Bootstrap seeds it from the modules and capabilities extracted from the initial request (FR-1).
**Consequences (testable):**
- Every generated name is built from vocabulary terms through the naming rules (FR-45).
- A proposed new term is shown with the existing terms, and the developer is asked explicitly whether it is a synonym of one of them (for example *"Is 'Booking' the same as 'Appointment'?"*). A synonym reuses the existing term; ArchShell never decides synonymy on its own.
- The vocabulary changes only through a confirmed proposal (protected, FR-14).
- Singular forms come from a fixed, versioned inflection table shipped in the language pack (for example `people` → `person`; `status` unchanged); the table's version is recorded in each feature plan.

#### FR-43: Intent classification
The model labels each spec item (S#) with one or more intents from the closed intent set.

The intent set is normative and versioned by the core: `query`, `state_change`, `removal`, `new_concept`, `side_effect`, `external_integration`, `validation_rule`, `scheduled_job`, `data_migration`. Changing it is a core contract change (FR-36).

**Consequences (testable):**
- A spec item that states a cross-cutting rule instead of a change (who may run an operation, what is audited, what runs in a transaction) is classified as a policy (FR-60), not as an intent.
- When the classification runs agree that a spec item fits no intent and no policy, ArchShell asks the developer first. If it still fits none (for example a large-scale rename), ArchShell stops with a gap report, as in FR-17; the developer implements it by hand under the same gates, or requests a new intent for the core.
- The model never selects building blocks, file paths, or names directly.
- Output outside the closed set is rejected by schema validation and retried within bounds (NFR-7).
- The model also supplies the verb and noun tokens of each item (for example `cancel`, `appointment`), never full type names. Tokens come from the domain vocabulary (FR-58); a new term is a proposal, never a silent choice.

#### FR-60: Cross-cutting policies
Cross-cutting rules are data, not code. Every project template ships a cross-cutting pipeline (for example MediatR behaviors in .NET) that applies the policies declared for each operation.
**Consequences (testable):**
- The policy kinds form a closed, versioned set owned by the core: `authorization` (allowed roles), `audit` (on or off), and `transaction` (on or off).
- A policy spec item adds an entry to the generated policy file for its operation (for example `CancelAppointment: { authorization: [Receptionist], audit: true }`); the agent never writes authorization, audit, or transaction code in method bodies.
- The policy file is generated from the confirmed feature plans and is protected (FR-14); a new role is a new vocabulary term (FR-58).
- A rule that depends on business data (for example "only morning appointments can be cancelled online") is not a policy; it is a `validation_rule` implemented in code.
- Consensus (FR-44) also covers the policy kind and its values.

#### FR-44: Classification consensus
Intent classification runs three times per spec item. Agreement means all three runs return exactly the same intents and the same normalized tokens; any difference in either is a disagreement and becomes a question to the developer, asked one at a time (FR-23).
**Consequences (testable):**
- A disagreement is never resolved by majority vote or by the model alone.
- The question offers closed choices in plain language: each disagreeing reading, plus *"neither, reshape this item"*, which returns the item to the spec step (FR-23). Free-text answers are not mapped by the model.
- Consensus is on by default and can be turned off in configuration; when it is off, ArchShell accepts a single classification run.

**Plan and contracts (steps 5 and 7)**

#### FR-45: Deterministic feature plan
ArchShell derives the feature plan from the classified intents, without the model:
- The language pack's mapping table for the chosen pattern turns each intent into its building blocks.
- Names come from the charter's naming rules applied to the verb and noun tokens (case and singular form normalized).
- A building block that already exists in the project becomes a modification instead of a new file. The mapping table also derives the modification's surface change (for example `cancel` on the existing `Appointment` entity gives `Appointment.Cancel()`).

**Consequences (testable):**
- The same intents and tokens always produce the same feature plan.
- The developer confirms the plan, together with the spec, before anything is scaffolded, under the rules of FR-59.
- The confirmed plan is stored with the feature and committed with it.

#### FR-47: Contracts as data
The model proposes the feature's internal and public contracts as data inside the feature plan, never as code: each operation's name tokens, kind (internal or public), inputs, and outputs.
**Consequences (testable):**
- Types come from a closed type system:
  - Base types: `string`, `int`, `decimal`, `bool`, `money`, `date`, `datetime`, `duration`, `binary`, `id<Entity>`, and enums.
  - Constructors: `T?` (optional) and `list<T>`.
  - Contracts and data types that already exist in the project.
  - New data types declared in the plan as data (named fields whose types come from this system); the language pack generates them (FR-48).
- ArchShell gives the model the inventory of existing contracts and data types; a proposal that duplicates an existing type instead of reusing it is rejected.
- A duplicate is a new type with the same normalized name as an existing one, or with exactly the same set of field names and types. ArchShell checks this without a model.
- Contract names come from the charter's naming rules applied to the tokens (FR-45).
- Proposals with unknown types are rejected by schema validation and retried within bounds (NFR-7).

**Confirmation (steps 6 and 8)**

#### FR-55: Growth before plan confirmation
When the derived plan needs a module or contract location that the charter does not declare, ArchShell proposes the charter growth (FR-29) before showing the plan, and derives the plan against the grown charter.
**Consequences (testable):**
- If growth is needed after confirmation (for example through a plan extension, FR-54), ArchShell re-derives the affected plan and contracts and asks the developer to confirm only the change; the RED test is observed failing again against the new plan.
- Declining the growth declines the whole confirmation, and the feature is treated as a misfit (FR-28).

#### FR-59: Confirm only new business decisions
A confirmation asks only about new business decisions: new vocabulary terms (FR-58), charter growth (4.6), new or changed public contracts (FR-49), and breaking changes (FR-51). The derived structure (building blocks, paths, names) is shown as a collapsed summary and needs no separate approval.
**Consequences (testable):**
- Growth, new vocabulary terms (FR-58), plan, and contracts are confirmed in a single confirmation; each part produces its own confirmation record (FR-53).
- ArchShell asks for at most one confirmation per feature, plus bounded plan extensions (FR-54), contract revisions (FR-50), and post-confirmation growth (FR-55).
- A feature whose plan derives entirely from the existing vocabulary and contracts, with no growth and no new or changed public contracts, proceeds without a confirmation; its confirmation record (FR-53) is marked *derived, no new decisions*, and the pull request review remains the authoritative approval.
- New internal contracts are shown in the summary but are not confirmed unless the developer opens them.

#### FR-49: Plain-language contract confirmation
The developer confirms new or changed public contracts in the feature's single confirmation (FR-59), shown as plain-language descriptions in the conversation language (FR-39), not as code.
**Consequences (testable):**
- Example: *"Cancel appointment: receives the appointment id and an optional reason; returns the fee charged, if any."*
- The developer can ask to see the generated code before confirming. The preview is an in-memory render of the same contract templates and is never written to the project; the code written after confirmation is byte-identical to the preview.

**Generation and freeze (steps 9–10)**

#### FR-24: Structure before implementation
ArchShell scaffolds the feature's structure from the language pack's building-block templates according to the confirmed feature plan (FR-45), and generates its ports and contracts from the confirmed contract data (FR-47, FR-48), before any implementation is written.
**Consequences (testable):**
- Scaffolding, contract generation, composition (FR-57), and freeze (FR-50) form one atomic step. If the session stops during it, ArchShell rolls back to the confirmed plan and regenerates from it on resume; a confirmed plan is never left partially applied.
- Scaffolding the same feature plan twice produces byte-identical files.
- Scaffolded files contain the fixed structure (types, signatures, locations); the agent only fills bodies and adds private members (enforced by FR-56).
- Data migrations (intent `data_migration`) are created by the language pack through its toolchain (for example `dotnet ef migrations add`), not written by hand.

#### FR-48: Contract generation
ArchShell generates the contract code (for example interfaces, data types, and the OpenAPI fragment) from the confirmed contract data, using the language pack's contract templates.
**Consequences (testable):**
- The same contract data always produces byte-identical contract code.
- Generated contract code is protected (FR-14).

#### FR-57: Generated composition files
Wiring files (for example dependency-injection registration and endpoint mapping) are generated by the language pack from the confirmed feature plans; the agent never edits them.
**Consequences (testable):**
- Composition files are regenerated whenever a plan changes and are protected (FR-14); `archshell verify` fails if they differ from their regeneration.

#### FR-50: Contract freeze
A feature's contracts (confirmed, or derived without new decisions under FR-59) are frozen before the RED test (FR-25) is written, and the RED test is written against them.
**Consequences (testable):**
- The agent cannot change a frozen contract; when it needs a change, it stops and ArchShell asks the developer to confirm the revised contract (FR-49).
- A confirmed revision regenerates the contract code (FR-48); the RED test is rewritten against it and observed failing again (FR-25), and the developer sees the diff between the old and new RED test. A revision counts as an attempt (FR-26).

**Conformance gates (step 13)**

#### FR-46: Plan conformance gate
The per-edit check (FR-20) blocks the agent from creating files or types that are not in the confirmed feature plan.
**Consequences (testable):**
- The per-edit check is syntactic and stays within NFR-1; the semantic check (partial classes, nested public types, types across files) runs in the full gates (FR-21), which include the build that public-contract comparison (FR-51) needs.
- Allowed without a plan entry: private nested types inside planned files, and test-only types (fakes, fixtures) inside planned test files.
- A file shared by several features is checked against the cumulative set of confirmed plans: the plan that created it plus every later plan that modified it. A later modification never invalidates an earlier plan.
- When the agent needs structure outside the plan, it stops and requests a plan extension (FR-54); it never names files or types itself.
- The agent cannot widen a plan by editing it (protected, FR-14).
- The full gates (FR-21) and `archshell verify` (FR-41) also fail when the feature's files do not match its plan.

#### FR-56: Surface conformance
The feature plan fixes the public and internal surface of every planned type: its members and constructor signatures. ArchShell compares the actual surface with the planned one, without a model.
**Consequences (testable):**
- The agent may add private members and fill bodies; any other `public` or `internal` member (C# access level), or any signature change, fails the check.
- For a modified building block, only the planned surface change (FR-45) is accepted; every other member of that type must keep its previous signature.
- The check runs in the full gates (FR-21) and in `archshell verify` (FR-41), and in the per-edit check (FR-20) when it fits the per-edit latency budget (NFR-1).

#### FR-51: Breaking-change detection
When a feature changes an existing public contract, ArchShell compares it with the previous version without a model and lists every breaking change.
**Consequences (testable):**
- The previous version is the contract at the merge base with the default branch, both in session and in `archshell verify`.
- Breaking changes include at least: a removed or renamed operation or field, a changed type, a new required input, a narrowed optionality, and a public contract changed to an internal contract (kind change, FR-47). A language pack may detect more (FR-33).
- Before any breaking change is confirmed, ArchShell offers a non-breaking alternative by default: an additive version (a new operation, with the old one marked obsolete) that keeps every consumer compiling. The breaking change proceeds only if the developer declines the non-breaking alternative.
- A breaking change needs explicit developer confirmation that names each change, recorded as a confirmation record (FR-53).
- When the developer declines the alternative, ArchShell finds every consumer without a model (project references and symbol usages) and adds each one to the feature plan as a consumer-adaptation block, shown in the same confirmation (for example *"This breaks `Appointments.CancelAppointmentHandler` and 2 more files."*).
- In consumer-adaptation blocks the agent may only change method bodies at the call sites; their surface stays fixed (FR-56).
- The full gates (FR-21) and `archshell verify` (FR-41) fail on a breaking change to a public contract that has no recorded confirmation.
- Changes to internal contracts are not breaking-change checked, but still follow FR-49 and FR-50.

**Implementation loop (steps 11–12)**

#### FR-25: RED test first
A failing test for the feature exists and is observed failing before the agent implements the feature.

#### FR-54: Plan extension
A plan extension goes through the same deterministic pipeline as the original plan (FR-43–FR-45).
**Consequences (testable):**
- The agent can only request one of two things: reclassify an existing spec item, or add a new spec item, with its intents and tokens. Each request cites the gate failure or reason behind it.
- ArchShell re-derives the building blocks from the mapping table and shows the developer only the change to the plan; a confirmed extension produces a confirmation record (FR-53).
- At most 2 extensions per spec item; each counts as an attempt (FR-26).
- A declined extension is treated as a misfit (FR-28).

#### FR-26: Bounded retries
When a gate fails, ArchShell rejects the change and the agent retries, up to 5 attempts per task.
**Consequences (testable):**
- A task is one spec item (S#).
- An attempt is one run of the full gates (FR-21), one plan extension (FR-54), or one contract revision (FR-50).
- Per-edit check blocks (FR-20) do not count as attempts.
- The count resets when the developer rephrases the spec or fixes code by hand.

#### FR-27: Exhaustion report
After the 5th failed attempt, ArchShell stops and shows a report.
**Consequences (testable):**
- The report names the rule that kept failing, what the agent tried in each attempt, and its best guess at why.
- The developer can fix the code by hand or rephrase the spec.
- Hand fixes must pass the same full gates.

#### FR-28: Misfit detection
When the failures that exhausted the retries suggest the feature does not fit the chosen pattern, the report says so explicitly. The pattern and its rules are never changed or relaxed; the developer must reshape the feature to fit.
**Consequences (testable):**
- A feature treated as a misfit (for example after a declined growth [FR-30, FR-55], a declined plan extension [FR-54], or an intent declared *not applicable* [FR-33]) stops with a report, and the developer reshapes the feature.

### 4.6 Charter growth

**Description:** The chosen pattern and its rules are immutable for the life of the project. The charter can only **grow within the pattern**, for example when a feature needs a new module or a new contract location. ArchShell proposes the growth, checks it against the pattern's rules, and applies it only after the developer confirms. Serves JTBD 3 (Delegate) and 4 (Trust).

**Functional requirements:**

#### FR-29: Growth proposal
When a feature needs something the charter does not declare (for example a new module or contract location), ArchShell stops and proposes the growth, explaining what would be added and why.
**Consequences (testable):**
- The proposal is checked against the pattern's rules; growth that would violate them is never proposed.
- Growth that duplicates or conflicts with an existing module or contract-location name is never proposed; ArchShell explains the conflict instead.

#### FR-30: Developer confirmation
Growth is applied only after the developer confirms it. The agent can never approve growth.
**Consequences (testable):**
- If the developer declines, the feature is treated as a misfit (FR-28).
- The in-session confirmation is a fast first step; the authoritative approval is a human review of the pull request that changes the charter (FR-53).

#### FR-31: Pattern immutability
Growth can never change the chosen pattern(s) or their rules.
**Consequences (testable):**
- Modules and contract locations can grow; patterns, layers, `may_depend_on`, rules, and naming are locked.
- A feature that only fits by changing the pattern ends in the exhaustion report (FR-27, FR-28).

#### FR-32: Regeneration after growth
After confirmed growth, ArchShell regenerates `architecture.yaml`, the generated architecture tests, project references, and `ARCHITECTURE.md` consistently.
**Consequences (testable):**
- Each growth is recorded with date and reason as a new ADR (for example `0002-add-notifications-module.md`) that adds to, and never supersedes, the original architecture ADR.

### 4.7 Extensibility

**Description:** ArchShell is open source. The core never contains language-, host-, or vendor-specific code, so the community can add language packs (for example Java, TypeScript), host adapters (for example Codex, Gemini CLI, OpenCode), and LLM providers (for example OpenAI, Google, Ollama) without changing the core.

**Functional requirements:**

#### FR-33: Language packs
A language pack provides scaffolding, dependency wiring, architecture and contract test generation, gates, naming conventions, and its list of supported patterns. For each supported pattern it also provides building-block templates, the intent-to-building-block mapping table (FR-45), contract templates (FR-48), public-contract comparison (FR-51), surface comparison (FR-56), composition-file generation (FR-57) and migrations (FR-24), and the cross-cutting pipeline with policy-file generation (FR-60).
**Consequences (testable):**
- Every project template, shipped (FR-15) or custom (FR-38), has building-block templates and a complete mapping table: each of the 9 intents (FR-43) either maps to at least one building block or is declared *not applicable* in that pattern with a written reason. The extension conformance gate (FR-37) fails a pack or extension whose table is incomplete.
- When a spec item needs an intent declared *not applicable*, ArchShell stops at classification, before the plan, explains the pack's reason, and treats the feature as a misfit (FR-28).

#### FR-34: Host adapters
A host adapter packages the core's workflow for an agent runtime (skills, agents, hooks, question UI) without changing workflow logic or prompts, which the core owns.

#### FR-35: LLM providers
A provider lets the engine call a model directly when the host has no native agent (Phase 2). Every model output is validated against a core-owned schema; invalid output gets a bounded retry, then becomes an error.

#### FR-36: Versioned extension contracts
Packs, adapters, and providers declare the extension contract version they implement; the core refuses incompatible versions with a clear message.

#### FR-37: Extension conformance gate
A new pack, adapter, provider, or custom project template (FR-38) is enabled only after it passes the extension conformance suite (golden set + engine interface tests) with the same thresholds as the launch pack, adapter, and provider.
**Consequences (testable):**
- Contributors can run the extension conformance suite locally before submitting.
- For a language pack, the suite also checks every supported pattern: a complete mapping table (FR-33), byte-identical scaffolding (FR-24), the reference feature per intent, and detection of the full seeded-violation corpus (see Success metrics).

#### FR-38: Custom pattern cards
Teams can add their own pattern cards through an optional, configured extra catalog path.

#### FR-40: Self-contained runtime
At runtime ArchShell reads only what ships inside its plugins (catalog, templates, prompts, schemas) plus the optional extra catalog path (FR-38).
**Consequences (testable):**
- It never reads `02-ARQUITECTURA` (an authoring source only) at runtime, so it works in any repository.

### 4.8 Localization

**Description:** ArchShell talks to the developer in English or Spanish (FR-39) and generates every artifact in English (FR-12).

**Functional requirements:**

#### FR-39: Conversation language
Questions, proposals, tradeoff explanations, and reports are delivered in the language the developer uses (English or Spanish).

#### FR-12: English artifacts
All generated code and documentation are written in English, regardless of the conversation language.

### 4.9 Change control

**Description:** The protected set changes only through a confirmation flow that leaves a confirmation record, and a human review of the pull request is the authoritative approval. These rules govern the charter (4.2), charter growth (4.6), and every protected artifact of the feature workflow (4.5). Serves JTBD 4 (Trust).

**Functional requirements:**

#### FR-14: Charter tamper protection
ArchShell detects and blocks changes to the protected set that do not come from a confirmation flow with a matching confirmation record (FR-53), whether the developer, a teammate, or the agent makes them.
**Consequences (testable):**
- The protected set includes `architecture.yaml`, the generated architecture tests, project references (`<ProjectReference>`) in `.csproj` files (package references and other settings stay editable), build settings that affect the gates (for example warnings as errors in `Directory.Build.props`), the inclusion of the test projects in the solution, ArchShell's hook configuration, confirmed feature plans (FR-45), generated contract code (FR-48), generated composition files (FR-57), the generated policy file (FR-60), the domain vocabulary (FR-58), and the confirmation records themselves (FR-53).
- The confirmation flows are charter growth (4.6), vocabulary proposals (FR-58), feature plan confirmation and extension (FR-45, FR-46), contract confirmation and revision (FR-49, FR-50), and breaking-change confirmation (FR-51).
- In-session hooks block tampering early; the authoritative check is `archshell verify` in CI (FR-41), which the agent cannot reach.
- An agent cannot pass a failing gate by loosening a rule or editing a generated test.
- The block names the changed file and points to the confirmation flow that can change it.

#### FR-53: Confirmation records
Every human confirmation (charter growth, feature plan and its extensions, contracts and their revisions, breaking changes) produces a structured confirmation record that is committed with the change it approves.
**Consequences (testable):**
- A record names the flow, the feature or growth it belongs to, the confirmed content (or its fingerprint), and the developer who confirmed it.
- The in-session confirmation creates the record but does not approve the change. The authoritative approval is a human review of the pull request that contains it: the records live under paths that `CODEOWNERS` assigns to human owners.
- ArchShell generates a `CODEOWNERS` file that assigns the charter files (`architecture.yaml`, ADRs, generated architecture tests), feature plans, generated contract code, and confirmation records to human owners, so a charter change cannot merge without their approval (with branch protection, FR-41). The rest of the protected set (FR-14) needs no `CODEOWNERS` entry: any change to it must match a confirmation record (FR-41), and a record cannot merge without an owner's approval.
- An agent that creates or edits a record by any means (including shell commands) cannot merge it without a human owner's approval.
- The agent commits and opens pull requests under its own GitHub identity (a bot or machine account), separate from every human owner, so a human always approves, including on solo projects. Setup documentation covers creating this identity and giving it no admin rights.

## 5. Non-functional requirements

- **NFR-1 Per-edit latency:** per-edit checks (FR-20) complete in ≤ 3 seconds on a typical developer machine.
- **NFR-2 Platforms:** Windows, macOS, and Linux.
- **NFR-3 Toolchain:** .NET 10 LTS or later; Python 3.12 or later as a prerequisite, installable with `pipx` or `uv`.
- **NFR-4 Determinism:** the same inputs produce the same scores, charter, generated tests, feature plans, scaffolded files, generated contracts, and gate verdicts (FR-2, FR-18, FR-24, FR-45, FR-48).
- **NFR-5 No model in CI:** `archshell verify` (FR-41) needs no model and no network beyond package restore.
- **NFR-6 Privacy:** ArchShell sends project code only to the host's own model.
- **NFR-7 Model output validation:** every model output, from the host's agent or a provider, is validated against a core-owned schema; invalid output is retried up to 2 times (configurable), then reported as an error.

## 6. Non-goals (explicit)

- **Existing codebases (brownfield).** ArchShell works only on new projects.
- **Changing the chosen pattern.** The pattern and its rules are immutable for the life of the project; only charter growth within the pattern is allowed (4.6).
- **Functional correctness.** Gates guarantee structure and contracts (maintainability, extensibility), not that business logic is bug-free. Unit tests catch what they cover; reviewing logic stays with the developer.
- **Test quality.** ArchShell enforces that a RED test exists first and that tests pass; it does not judge whether the agent's unit tests are meaningful.
- **Identical output.** Two runs may produce different code; only the process and the gates are deterministic.

## 7. MVP scope

### 7.1 In scope (public launch = Phase 1)

- Claude Code host adapter (plugin: skills, agents, hooks).
- .NET/C# language pack with project templates for all 18 catalog patterns plus three combinations (FR-15). Each of the 21 project templates ships the full feature workflow: building-block templates, a complete 9-intent mapping table, contract templates, surface comparison, and composition generation (FR-33). No project template ships skeleton-only.
- Features 4.1–4.9, except direct model calls through providers (FR-35).
- English and Spanish conversation; English artifacts.
- Open-source release with versioned extension contracts and the extension conformance suite (FR-37).
- `archshell verify` CI mode (FR-41), the only Phase 2 command brought into the launch.

### 7.2 Launch acceptance criteria

- Given three sample requests, ArchShell selects the expected pattern(s) or asks a discriminating question.
- It writes the ADR, `ARCHITECTURE.md`, `architecture.yaml`, and the generated architecture tests.
- The gates detect 100% of the seeded-violation corpus (see Success metrics), including its feature-workflow members, for every shipped project template.
- Scaffolding the same confirmed feature plan twice produces byte-identical files, for every shipped project template.
- For every shipped project template, a reference feature per intent (9 per template) runs end to end: spec, plan, contracts, scaffold, RED, full gates green.
- The intent golden set (sample `S#` with expected intents and tokens) classifies 100% correctly under consensus (FR-43, FR-44).

### 7.3 Out of scope for MVP

- The rest of the Phase 2 standalone CLI (`archshell select | feature | grow`).
- LLM providers beyond the host's own agent (OpenAI, Google, Ollama).
- Language packs other than .NET/C#; host adapters other than Claude Code.

## 8. Success metrics

These measure whether ArchShell keeps its promise. Adoption metrics are intentionally not tracked.

**Primary**
- **Zero deviations:** the gates detect 100% of the seeded-violation corpus, run against every shipped project template. The corpus is a fixed set of features written on purpose to break each rule type, for example a Domain → Adapters dependency, a port declared outside Application, use of another module's internals, and a forbidden `<ProjectReference>`. It includes at least these feature-workflow members: a file outside the plan (FR-46), an unplanned public member (FR-56), a protected change without a confirmation record (FR-14, FR-53), a hand-edited composition or policy file (FR-57, FR-60), a changed frozen contract (FR-50), and an unconfirmed breaking change (FR-51). The corpus doubles as ArchShell's own regression suite.

**Secondary**
- **Correct selections** on the golden set (15–20 requests with expected patterns, derived from `02-ARQUITECTURA/*/sample`, the pattern authoring source, FR-40): target ≥ 90% correct and 0 wrong automatic proposals. Wrong selections are the costly error, so calibration is biased toward asking.

**Counter-metrics (do not optimize)**
- **Unnecessary questions:** questions that did not change the selected pattern.
- **Confirmations per feature:** the share of features that needed a confirmation (FR-59). Asking more keeps the rules but defeats the Delegate job.
- **Feature-workflow interruptions:** consensus questions (FR-44), plan extensions (FR-54), and contract revisions (FR-50) per feature. Consensus triples classification calls; the cost is accepted for determinism, but interruptions must stay low.
- **Exhausted retries:** tasks that hit the 5-attempt limit (FR-27). Blocking everything keeps the rules but defeats the Delegate job.

## 9. Open questions

Non-blocking; deferred with owner and revisit condition.

1. **Interrupted sessions and concurrent branches** (owner: architecture; revisit: architecture design). `archshell verify` (FR-41) remains the backstop for all three:
   - How a resumed session restores its workflow state.
   - How charter growth on two team branches is reconciled at merge.
   - How two in-progress features that modify the same building block or the same frozen contract are reconciled.

## 10. Assumptions index

None open. All assumptions were confirmed by the product owner (latest on 2026-11-03: FR-44 consensus enabled by default).

## Appendix A. FR index

| FR | Title | Section |
| --- | --- | --- |
| FR-1 | Driver extraction | 4.1 Pattern selection |
| FR-2 | Deterministic scoring | 4.1 Pattern selection |
| FR-3 | Discriminating questions | 4.1 Pattern selection |
| FR-4 | Automatic proposal | 4.1 Pattern selection |
| FR-5 | Fallback to human choice | 4.1 Pattern selection |
| FR-6 | Compatible combinations | 4.1 Pattern selection |
| FR-7 | Confirmation before writing | 4.1 Pattern selection |
| FR-8 | Override with explained tradeoffs | 4.1 Pattern selection |
| FR-9 | ADR | 4.2 Architecture charter |
| FR-10 | Architecture guide | 4.2 Architecture charter |
| FR-11 | Machine-readable charter | 4.2 Architecture charter |
| FR-12 | English artifacts | 4.8 Localization |
| FR-13 | Charter read before generation | 4.2 Architecture charter |
| FR-14 | Charter tamper protection | 4.9 Change control |
| FR-15 | Template-based scaffolding | 4.3 Scaffolding |
| FR-15a | Atomic charter and scaffold | 4.3 Scaffolding |
| FR-16 | Dependencies wired to the rules | 4.3 Scaffolding |
| FR-17 | Missing-template gap report | 4.3 Scaffolding |
| FR-18 | Generated architecture tests | 4.4 Enforcement gates |
| FR-19 | Contract tests | 4.4 Enforcement gates |
| FR-20 | Fast per-edit checks | 4.4 Enforcement gates |
| FR-21 | Full gates at task completion | 4.4 Enforcement gates |
| FR-22 | Actionable failure report | 4.4 Enforcement gates |
| FR-23 | Feature spec | 4.5 Feature workflow |
| FR-24 | Structure before implementation | 4.5 Feature workflow |
| FR-25 | RED test first | 4.5 Feature workflow |
| FR-26 | Bounded retries | 4.5 Feature workflow |
| FR-27 | Exhaustion report | 4.5 Feature workflow |
| FR-28 | Misfit detection | 4.5 Feature workflow |
| FR-29 | Growth proposal | 4.6 Charter growth |
| FR-30 | Developer confirmation | 4.6 Charter growth |
| FR-31 | Pattern immutability | 4.6 Charter growth |
| FR-32 | Regeneration after growth | 4.6 Charter growth |
| FR-33 | Language packs | 4.7 Extensibility |
| FR-34 | Host adapters | 4.7 Extensibility |
| FR-35 | LLM providers | 4.7 Extensibility |
| FR-36 | Versioned extension contracts | 4.7 Extensibility |
| FR-37 | Extension conformance gate | 4.7 Extensibility |
| FR-38 | Custom pattern cards | 4.7 Extensibility |
| FR-39 | Conversation language | 4.8 Localization |
| FR-40 | Self-contained runtime | 4.7 Extensibility |
| FR-41 | CI verification | 4.4 Enforcement gates |
| FR-42 | New-project check | 4.1 Pattern selection |
| FR-43 | Intent classification | 4.5 Feature workflow |
| FR-44 | Classification consensus | 4.5 Feature workflow |
| FR-45 | Deterministic feature plan | 4.5 Feature workflow |
| FR-46 | Plan conformance gate | 4.5 Feature workflow |
| FR-47 | Contracts as data | 4.5 Feature workflow |
| FR-48 | Contract generation | 4.5 Feature workflow |
| FR-49 | Plain-language contract confirmation | 4.5 Feature workflow |
| FR-50 | Contract freeze | 4.5 Feature workflow |
| FR-51 | Breaking-change detection | 4.5 Feature workflow |
| FR-52 | Feature backlog | 4.3 Scaffolding |
| FR-53 | Confirmation records | 4.9 Change control |
| FR-54 | Plan extension | 4.5 Feature workflow |
| FR-55 | Growth before plan confirmation | 4.5 Feature workflow |
| FR-56 | Surface conformance | 4.5 Feature workflow |
| FR-57 | Generated composition files | 4.5 Feature workflow |
| FR-58 | Domain vocabulary | 4.5 Feature workflow |
| FR-59 | Confirm only new business decisions | 4.5 Feature workflow |
| FR-60 | Cross-cutting policies | 4.5 Feature workflow |
