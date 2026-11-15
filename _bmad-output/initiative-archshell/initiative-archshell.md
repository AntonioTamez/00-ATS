---
type: initiative
title: "ArchShell Phase 1: delegate whole .NET features to an AI agent without reviewing structure"
parent: none
covers: [CAP-1, CAP-2, CAP-3, CAP-4, CAP-5, CAP-6, CAP-7, CAP-8, CAP-9, CAP-10, CAP-11, CAP-12, CAP-13, CAP-14]
after: []
assignee: ""
risk: high
---

# ArchShell Phase 1: delegate whole .NET features to an AI agent without reviewing structure

## Description

ArchShell chooses a project's architecture once, records it as rules, and enforces it with blocking gates on every agent change. The developer delegates whole features and confirms only new business decisions. The spec owns the capabilities, constraints, and non-goals; this initiative delivers Phase 1: the Claude Code host adapter, the .NET/C# pack with all 21 project templates, and `archshell verify`.

## Outcome

A developer with low-to-medium architecture experience bootstraps a .NET project and lands features as green pull requests without reviewing structure. The signal is the spec's success signal.

## Done when

1. The spec's success signal is demonstrated on every shipped project template: a feature with no new business decisions lands as a green pull request with no structural confirmation.
2. The gates detect 100% of the seeded-violation corpus on all 21 project templates.
3. Pattern selection reaches at least 90% correct and 0 wrong automatic proposals on the golden set.
4. Generated output is byte-identical on Windows, macOS, and Linux for every project template.
5. ArchShell is released open source with the extension conformance suite, and a third party can run it locally.

## Boundaries

The initiative follows capability boundaries for a single owner with agent lanes, in one new repository. It excludes everything listed in the spec's non-goals. The tracer path runs one request through charter, scaffold, one `state_change` feature, and green gates on the first project template, Clean + Screaming; it runs through epics 1–7.

- Touch point: GitHub (branch protection, `CODEOWNERS`, Actions) — configured for projects ArchShell generates; owner: epic-gates-verify-change-control
- Touch point: TestPyPI and GitHub Packages (NuGet) — pre-release publishing for every epic; owner: epic-platform-baseline
- Touch point: PyPI and NuGet — public release; owner: epic-extensibility-launch

## References

- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- constraint — same spec, section Constraints (determinism, NFRs, Phase 1 scope)
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Decision: ArchShell code lives in a new repository `arch-shell` (one repository holding the core, the Claude Code plugin, the .NET pack, and the analyzer), separate from `00-ATS` (user's decision, 2026-11-11).
- Decision: shared decisions across epics (action protocol, schemas, file classes) are already settled in the architecture spine; no separate decision story (2026-11-11).
- Decision: epics are cut by capability boundary, in a single dependency chain 1→10; the platform baseline opens (2026-11-13).
- Decision: an epic before the launch is delivered when merged to `main` with CI green on Windows, Linux, and macOS and published as a pre-release to TestPyPI and GitHub Packages (NuGet); release plumbing and signed tags go in epic-platform-baseline (user's decision, 2026-11-13).
- Decision: FR-35 LLM providers (`archshell.providers`) are out of Phase 1, per the spec's non-goals (2026-11-13).
- Decision: `verify` is epic-gates-verify-change-control's interface; epics 6–8 add their checks to it (2026-11-13).
- Decision: the repository is named `arch-shell`; package and command names stay `archshell` (user's decision, 2026-11-13).
