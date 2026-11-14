# ArchShell PRD – Addendum

The architecture spine (`../architecture-archshell/architecture-archshell.md`) holds the binding technical decisions. This addendum holds the technical rationale behind the PRD for downstream architecture work: where the design lives, which mechanisms were chosen or rejected for FR-43–FR-60, and a market digest. When this addendum and the PRD conflict, the PRD wins.

## Technical Design Reference

The technical "how" lives in the design README (`01-IA/02-shell/README.md`). Downstream architecture work should read:

- §2 Shell architecture: hexagonal core, component table (PatternCatalog, PatternSelector, Elicitor, CharterWriter, Scaffolder, ArchTestGenerator, GateRunner, FeatureWorkflow).
- §2 Language pack contract: `LanguagePack` capabilities; entry point `archshell.language_packs`.
- §2 Host and model contracts: engine JSON interface (`archshell-engine <command>`, stdin/stdout), `LlmProvider` Protocol, entry points `archshell.hosts` / `archshell.providers`.
- §3 Pattern card schema; §3.1 driver schema and D3 policy values.
- §4 `architecture.yaml` sketch.
- §5 Per-feature workflow: feature plan and its location, contracts, backlog and its location, gate timing, charter growth, misfit.
- §6 Phase 1 folder layout (`archshell-core`, `archshell-claude-code`, `archshell-dotnet`), `hooks.json`.
- §7 Decisions D1–D13.

## Feature Plan File

Covers FR-43–FR-60. The plan location is set in README §5. Its top-level contents: spec items, intents, verb/noun tokens, policies (FR-60), building blocks, paths, names, a create-or-modify flag per block, with the planned surface change for modified blocks (FR-45, FR-56), consumer-adaptation blocks (FR-51), extensions (FR-54), the inflection-table version (FR-58), and `contracts`.

Chosen: the pack's mapping table derives building blocks from intents (FR-45). Illustrative mapping table in the .NET pack (Clean + Screaming):

```yaml
state_change: [command, command_handler, endpoint, handler_test]
side_effect:  [port, adapter, port_test]
new_concept:  [entity, entity_test]
```

The mapping table also derives the surface change of a modified block: `state_change` on an existing entity adds a `{Verb}()` method, so `cancel` gives `Appointment.Cancel()`. Policies (FR-60) are applied in the .NET pack by MediatR behaviors or middleware.

Illustrative contract data in the plan:

```yaml
contracts:
  - port: AppointmentCancellation
    kind: public            # public | internal
    operation: cancel
    input:  { appointmentId: id<Appointment>, reason: string? }
    output: { feeCharged: money? }
```

Rejected alternative for feature structure: always scaffolding a pattern's full set of building blocks and deleting the unused ones. This approach only moves the nondeterminism to the deletion step, and it leaves dead code in every feature.

## Contract Mechanism

Covers FR-47–FR-51. Chosen: the model proposes contracts as data, the pack generates the code, and the developer confirms a plain-language summary of each new or changed public contract (FR-49, FR-59).

Breaking-change detection (FR-51) compares contract data, not compiled code; the earlier candidate tooling (`PublicApiAnalyzers`, an OpenAPI diff) is superseded. See the architecture spine, AD-13.

Rejected alternatives: the developer writes contracts by hand, which defeats *Delegate* for the target user with low-to-medium experience. The model writes contract code that the developer confirms, which produces nondeterministic signatures, and a junior developer cannot meaningfully review C# contracts.

Charter growth versus feature-level contract changes: see the PRD Glossary, *Charter growth*.

## Feature Backlog

Covers FR-52. The location is set in README §5. The backlog holds one checklist item per capability, grouped by module, so it stays hand-editable. Deferring capabilities to the backlog keeps bootstrap free of business code, which keeps the atomic charter-and-scaffold operation (FR-15a) small and safe to roll back.

## Competitive Landscape

Research digest, 2026-10-30. Based on three web searches; positioning lines are summaries, not verified product claims.

**Gap (inference):** no product closes the full loop of pattern selection → machine-readable record → generated tests → blocking gate on agent output. .NET is underserved; positioning as an architecture layer *for* Spec Kit, BMad, or Kiro is viable.

**Competitor risk (inference):** low now but rising. Sources of risk: Spec Kit and Kiro constitutions, cheap marketplace skills, and Sonar, Nx, and Bitloops governance features. Defensibility lies in enforcement depth and .NET template quality, not pattern-selection questions.

Evidence:

- **Architecture-enforcement libraries** (ArchUnit, ArchUnitNET, NetArchTest, dependency-cruiser, Nx module boundaries, import-linter, Sonar, eslint-plugin-boundaries, go-arch-lint, ArchUnitTS, tsarch, Sheriff): deterministic, not AI-aware, rules hand-written by humans.
- **Spec-driven AI dev** (GitHub Spec Kit, Kiro, BMad Method, Tessl, OpenSpec, Task Master, cc-sdd): define *what* to build; no architecture-test enforcement found.
- **Agent guardrails** (CLAUDE.md, AGENTS.md, Cursor rules): advisory; practitioners pair them with executable checks. Closest existing loop: agent rules + Sheriff + Claude Code Stop hooks (TypeScript only). Source: [Angular Architects article on agent rules and Sheriff](https://www.angulararchitects.io/en/?p=34037)
- **ADRs as executable specs**: guidance only (the advice is to map each ADR to a fitness function); marketplace pattern skills advise and write ADRs but do not enforce them.
