---
tracker_id: ""
key: ""
type: epic
title: "Feature spec, intents, vocabulary, and the derived plan"
parent: initiative-archshell
covers: [CAP-6, CAP-7]
after: []
assignee: ""
risk: high
---

# Feature spec, intents, vocabulary, and the derived plan

## Description

ArchShell turns a feature request into a numbered spec, classifies each item into the closed intent set or a policy with three-run consensus, draws names from the vocabulary, and derives the plan without a model, asking at most one confirmation for new business decisions.

## Outcome

A developer confirms only new business decisions; the signal is a fully derived feature that proceeds with no confirmation.

## Requirements

- CAP-6: spec, intents, policies, vocabulary (FR-23, FR-43, FR-44, FR-58).
- CAP-7: plan derivation and single confirmation (FR-45, FR-54, FR-55, FR-59).

## Done when

1. The intent golden set classifies 100% correctly under consensus; a disagreement becomes a closed-choice question.
2. A new term always triggers an explicit synonym question.
3. The same intents and tokens always produce the same `plan.yaml`, byte for byte.
4. A fully derived feature proceeds with no confirmation; plan extensions stay within their bounds.
5. Merged to `main` with CI green on Windows, Linux, and macOS, and published as a pre-release (engine to TestPyPI, analyzer to GitHub Packages NuGet).

## Boundaries

Capability boundary: up to a confirmed plan. Policies are classified here and generated in epic-generation-implementation-loop; the pre-plan growth hook is wired in epic-charter-growth-reconcile. Spec non-goals apply.

## References

- parent — _bmad-output/initiative-archshell/initiative-archshell.md
- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Decision: handoffs — FR-60 policies classified here, generated in epic 7; FR-54 extensions requested from epic 7; FR-55 pre-plan growth wired in epic 8 (2026-11-13).
- Waits on epic 4 because: needs the gates and the `verify` interface.
