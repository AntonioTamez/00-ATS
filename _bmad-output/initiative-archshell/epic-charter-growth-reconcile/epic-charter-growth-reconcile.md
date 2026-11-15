---
tracker_id: ""
key: ""
type: epic
title: "Charter growth and merge reconciliation"
parent: initiative-archshell
covers: [CAP-11, CAP-12]
after: []
assignee: ""
risk: high
---

# Charter growth and merge reconciliation

## Description

When a feature needs a new module or contract location, ArchShell proposes growth within the immutable pattern and applies it after confirmation; merge conflicts in derived files resolve by regeneration.

## Outcome

A project grows without losing its architecture; the signal is that confirmed growth regenerates charter, tests, and references consistently.

## Requirements

- CAP-11: FR-29–FR-32.
- CAP-12 (FR-61): `archshell reconcile`.

## Done when

1. Growth that would violate the pattern's rules is never proposed.
2. Confirmed growth regenerates the charter, tests, and references consistently and adds a `growth-<slug>.md` ADR.
3. `reconcile` regenerates from both sides; an incompatible pair resolves to the decision present at the merge base on the default branch.
4. Merged to `main` with CI green on Windows, Linux, and macOS, and published as a pre-release (engine to TestPyPI, analyzer to GitHub Packages NuGet).

## Boundaries

Capability boundary: growth and reconcile on the first template. Spec non-goals apply.

## References

- parent — _bmad-output/initiative-archshell/initiative-archshell.md
- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Waits on epic 7 because: needs the full feature workflow.
