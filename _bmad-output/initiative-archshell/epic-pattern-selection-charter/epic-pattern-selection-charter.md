---
tracker_id: ""
key: ""
type: epic
title: "Pattern selection and the recorded charter"
parent: initiative-archshell
covers: [CAP-1, CAP-2]
after: []
assignee: ""
risk: high
---

# Pattern selection and the recorded charter

## Description

The developer describes a new project and ArchShell proposes the fitting pattern or combination from the catalog, or asks discriminating questions, then records the confirmed architecture as an ADR, `ARCHITECTURE.md`, and `architecture.yaml`.

## Outcome

A developer gets a correct architecture choice recorded as machine-readable rules; the signal is CAP-1's golden-set result.

## Requirements

- CAP-1: pattern selection with discriminating questions (FR-1–FR-8, FR-42).
- CAP-2: charter files ADR, `ARCHITECTURE.md`, `architecture.yaml` (FR-9–FR-11, FR-13).
- E1 (CAP-13): first write of the version pins that generated the project (AD-14).

## Done when

1. The golden set reaches at least 90% correct and 0 wrong automatic proposals; a proposal appears only when every FR-4 criterion holds, otherwise the developer chooses between the top two.
2. After confirmation the three charter files exist, are consistent, and are byte-identical across runs and OSes.
3. The confirmation leaves a record under `records/bootstrap/`.
4. A Spanish request runs the whole selection in Spanish and writes English charter files.
5. Merged to `main` with CI green on Windows, Linux, and macOS, and published as a pre-release (engine to TestPyPI, analyzer to GitHub Packages NuGet).

## Boundaries

Capability boundary: selection and charter only; no scaffold (epic-bootstrap-scaffolding). Spec non-goals apply.

## References

- parent — _bmad-output/initiative-archshell/initiative-archshell.md
- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Waits on epic 1 because: needs the action protocol, schemas, record writer, and message catalogs.
