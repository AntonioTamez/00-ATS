---
tracker_id: ""
key: ""
type: epic
title: "The remaining 20 project templates"
parent: initiative-archshell
covers: [CAP-3, CAP-4, CAP-9, CAP-10, CAP-12]
after: []
assignee: ""
risk: high
---

# The remaining 20 project templates

## Description

The .NET pack ships all 21 project templates, each with the full feature workflow, its seeded-violation corpus, and its reference features.

## Outcome

Every catalog pattern is usable; the signal is the initiative's Done when 1, 2, and 4 on every template.

## Requirements

- The all-templates part of CAP-3, CAP-4, CAP-9, CAP-10, CAP-12: bootstrap, corpus, scaffold, and 9 reference features per template.

## Done when

1. The gates detect 100% of the seeded-violation corpus on all 21 templates.
2. Each template runs its 9 reference features end to end.
3. Bootstrap and feature scaffolding are byte-identical on the three OSes for every template.
4. The spec's success signal is demonstrated on every template.
5. Merged to `main` with CI green on Windows, Linux, and macOS, and published as a pre-release (engine to TestPyPI, analyzer to GitHub Packages NuGet).

## Boundaries

Template content only; no core change. Spec non-goals apply.

## References

- parent — _bmad-output/initiative-archshell/initiative-archshell.md
- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Waits on epic 8 because: needs the full workflow proven on the first template.
