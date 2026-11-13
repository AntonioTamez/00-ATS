---
tracker_id: ""
key: ""
type: epic
title: "Feature scaffolding and the test-first implementation loop"
parent: initiative-archshell
covers: [CAP-9, CAP-10]
after: []
assignee: ""
risk: high
---

# Feature scaffolding and the test-first implementation loop

## Description

ArchShell scaffolds each feature's structure, composition, and policies from templates, blocks anything outside the confirmed plan, and the agent implements each spec item test-first within bounded retries. The initiative's tracer path ends here.

## Outcome

A developer hands over a feature with no new business decisions and gets a green pull request; the signal is the spec's success signal on the first template.

## Requirements

- CAP-9 on the first template: FR-24, FR-46, FR-56, FR-57, FR-60.
- CAP-10 on the first template: FR-25–FR-28.

## Done when

1. The tracer: one `state_change` feature runs spec, plan, contracts, scaffold, RED, and full gates green, and lands as a green pull request with no structural confirmation.
2. Scaffolding the same plan twice is byte-identical; out-of-plan files, types, or public members fail the gates.
3. Authorization, audit, and transaction rules never appear in method bodies.
4. A reference feature per intent (9) runs end to end on the Clean + Screaming template.
5. A task stops after 5 attempts with an exhaustion report; a misfit ends in a report, never a relaxed rule.
6. Merged to `main` with CI green on Windows, Linux, and macOS, and published as a pre-release (engine to TestPyPI, analyzer to GitHub Packages NuGet).

## Boundaries

One project template; the tracer feature needs no charter growth. Spec non-goals apply.

## References

- parent — _bmad-output/initiative-archshell/initiative-archshell.md
- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Waits on epic 6 because: needs frozen contracts.
