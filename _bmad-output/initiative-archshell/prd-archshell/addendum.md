# ArchShell PRD – Addendum

## Competitive Landscape (research digest, 2026-10-30)

Based on 3 web searches; positioning lines are summaries, not verified product claims.

- **Architecture-enforcement libraries** (ArchUnit, ArchUnitNET, NetArchTest, dependency-cruiser, Nx module boundaries, import-linter, Sonar, eslint-plugin-boundaries, go-arch-lint, ArchUnitTS, tsarch, Sheriff): deterministic, not AI-aware, rules hand-written by humans.
- **Spec-driven AI dev** (GitHub Spec Kit, Kiro, BMad Method, Tessl, OpenSpec, Task Master, cc-sdd): define *what* to build; no architecture-test enforcement found.
- **Agent guardrails** (CLAUDE.md, AGENTS.md, Cursor rules): advisory; practitioners pair them with executable checks. Closest existing loop: agent rules + Sheriff + Claude Code Stop hooks (TypeScript only). Source: https://www.angulararchitects.io/en/?p=34037
- **ADRs as executable specs**: guidance only (map each ADR to a fitness function); marketplace pattern skills advise and write ADRs but do not enforce.

**Gap (inference):** no product closes the full loop of pattern selection → machine-readable record → generated tests → blocking gate on agent output. .NET is underserved; positioning as an architecture layer *for* Spec Kit / BMad / Kiro is viable.

**Competitor risk (inference):** low now, rising — Spec Kit/Kiro constitutions, cheap marketplace skills, Sonar/Nx/Bitloops governance. Defensibility lies in enforcement depth and .NET template quality, not pattern-selection questions.

## Technical Design Reference

The technical "how" lives in the design README (`01-IA/02-shell/README.md`); this addendum does not duplicate it. Downstream architecture work should read:

- §2 Shell architecture: hexagonal core, component table (PatternCatalog, PatternSelector, Elicitor, CharterWriter, Scaffolder, ArchTestGenerator, GateRunner, FeatureWorkflow).
- §2 Language pack contract: `LanguagePack` capabilities; entry point `archshell.language_packs`.
- §2 Host and model contracts: engine JSON interface (`archshell-engine <command>`, stdin/stdout), `LlmProvider` Protocol, entry points `archshell.hosts` / `archshell.providers`.
- §3 Pattern card schema; §3.1 driver schema and D3 policy values.
- §4 `architecture.yaml` sketch.
- §6 Phase 1 folder layout (`archshell-core`, `archshell-claude-code`, `archshell-dotnet`), `hooks.json`.
- §7 Decisions D1–D5 (Python core, .NET first, D3 thresholds, one plugin per language, host adapters + providers).

**Superseded by the PRD:** principle 5 / superseding-ADR flow (now charter growth, PRD 4.6); hooks running the full gates on every edit (now fast per-edit checks plus full gates at task completion, FR-20/FR-21).
