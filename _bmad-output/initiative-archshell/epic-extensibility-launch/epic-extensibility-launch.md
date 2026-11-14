---
tracker_id: ""
key: ""
type: epic
title: "Extensibility, upgrades, and the open-source launch"
parent: initiative-archshell
covers: [CAP-13]
after: []
assignee: ""
risk: high
---

# Extensibility, upgrades, and the open-source launch

## Description

Language packs, host adapters, and CI providers plug into an unchanged core behind the full conformance suite; projects upgrade only with `archshell upgrade`; ArchShell is released open source.

## Outcome

A third party can extend and run ArchShell; the signal is the initiative's Done when 5.

## Requirements

- CAP-13: FR-33, FR-34, FR-36–FR-38, FR-40, FR-62, FR-63 conformance. FR-35 LLM providers are out of Phase 1.

## Done when

1. A new extension is enabled only after passing the extension conformance suite.
2. A newer ArchShell release never changes an existing project's generated files unless `archshell upgrade` runs.
3. The runtime never reads `02-ARQUITECTURA`.
4. ArchShell is released to PyPI and NuGet, and a third party runs the conformance suite locally.

## Boundaries

Release and extension points; owns the PyPI and NuGet public release touch point. Spec non-goals apply.

## References

- parent — _bmad-output/initiative-archshell/initiative-archshell.md
- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Waits on epic 9 because: needs all 21 templates.
