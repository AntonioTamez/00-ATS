---
tracker_id: ""
key: ""
type: epic
title: "Gates, verify, trust, and change control"
parent: initiative-archshell
covers: [CAP-4, CAP-5, CAP-12]
after: []
assignee: ""
risk: high
---

# Gates, verify, trust, and change control

## Description

Every agent change passes per-edit checks and the full gates; `archshell verify` re-runs every check in CI without a model; the protected set changes only through confirmation flows approved by human pull-request review.

## Outcome

A developer cannot get structurally wrong agent code past the session or CI; the signal is the seeded-violation corpus on the first template.

## Requirements

- CAP-4 on the first template: per-edit checks and full gates (FR-18–FR-22).
- CAP-5: `archshell verify` and trust capability (FR-41, FR-64). `verify` is this epic's interface; later epics add checks to it.
- CAP-12 (FR-14, FR-53 enforcement): protected set, record verification.
- E1: `archshell init`, setup checks, `trust.yaml` detection.
- E2 (CAP-13): the `CiProvider` port and the GitHub provider generating `CODEOWNERS` and the Actions workflow (FR-63); its conformance belongs to epic-extensibility-launch.

## Done when

1. The gates detect 100% of the seeded-violation corpus on the Clean + Screaming template.
2. Per-edit checks finish within 3 seconds (NFR-1); a tree-sitter parse error defers to the gates.
3. `verify` fails on a protected change without a matching record, on a generated file that differs from regeneration, and on any rule violation, with no model and no network beyond package restore.
4. On a platform without enforceable owner review, `verify` reports `trust: unenforced` on every run.
5. Every agent tampering attempt in the corpus is blocked in session or failed by `verify`.
6. Merged to `main` with CI green on Windows, Linux, and macOS, and published as a pre-release (engine to TestPyPI, analyzer to GitHub Packages NuGet).

## Boundaries

One project template. Reconcile is epic-charter-growth-reconcile; the other 20 templates are epic-template-catalog. Spec non-goals apply.

## References

- parent — _bmad-output/initiative-archshell/initiative-archshell.md
- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Decision: `CiProvider` and the GitHub provider are built here, where `CODEOWNERS` and the workflow are generated (2026-11-13).
- Waits on epic 3 because: needs a bootstrapped Clean + Screaming project.
