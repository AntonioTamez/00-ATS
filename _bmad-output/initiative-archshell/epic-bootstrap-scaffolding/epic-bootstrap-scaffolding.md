---
tracker_id: ""
key: ""
type: epic
title: "Atomic bootstrap from the first project template (Clean + Screaming)"
parent: initiative-archshell
covers: [CAP-3]
after: []
assignee: ""
risk: high
---

# Atomic bootstrap from the first project template (Clean + Screaming)

## Description

From the confirmed charter, ArchShell bootstraps the project atomically from the .NET pack's Clean + Screaming template: solution, projects, dependency wiring, feature backlog, and seeded vocabulary, with no business code.

## Outcome

A developer gets a compiling project whose layer rules are enforced by the compiler; the signal is that a forbidden layer dependency does not compile.

## Requirements

- CAP-3 on the first template, Clean + Screaming: FR-15, FR-15a, FR-16, FR-17, FR-52. The other 20 templates are epic-template-catalog.

## Done when

1. A forbidden layer dependency in the bootstrapped project does not compile.
2. An interrupted or failed bootstrap leaves no partial project.
3. A combination without a template ends in a gap report.
4. Bootstrapping the same charter twice is byte-identical on Windows, Linux, and macOS; file classes (generated, seeded, toolchain-owned, human-owned) are recorded.
5. Merged to `main` with CI green on Windows, Linux, and macOS, and published as a pre-release (engine to TestPyPI, analyzer to GitHub Packages NuGet).

## Boundaries

One project template only. Gates and `verify` belong to epic-gates-verify-change-control. Spec non-goals apply.

## References

- parent — _bmad-output/initiative-archshell/initiative-archshell.md
- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Waits on epic 2 because: needs a confirmed charter and its version pins.
