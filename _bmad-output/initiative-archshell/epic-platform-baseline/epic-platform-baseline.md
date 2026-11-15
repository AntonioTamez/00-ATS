---
tracker_id: ""
key: ""
type: epic
title: "Platform baseline: engine protocol, schemas, records, and CI on three OSes"
parent: initiative-archshell
covers: [CAP-14, CAP-12]
after: []
assignee: ""
risk: high
---

# Platform baseline: engine protocol, schemas, records, and CI on three OSes

## Description

The `arch-shell` repository holds the core, the Claude Code plugin, the .NET pack, and the analyzer as separate packages. The engine speaks the closed action protocol and hook sub-protocol, validates every model output against schemas, writes normalized output and append-only confirmation records, and converses in English or Spanish. Every later epic builds on this substrate without changing it.

## Outcome

Every later epic can add a command, hook, or generator on a stable engine contract; the signal is a no-op feature flow that runs end to end in Claude Code on all three OSes.

## Requirements

- CAP-14: conversation in English or Spanish following the developer; every artifact in English (FR-12, FR-39).
- E1 (CAP-12): the confirmation-record writer and schema, `records/<feature-slug>/<seq>-<flow>.yaml`, append-only (FR-53 writing part; enforcement is epic-gates-verify-change-control).
- E2: closed action protocol `archshell/action/v1` with action_id, revision, and nonce; hook sub-protocol guard-write, check-edit, on-stop (spine AD-1, AD-2, AD-17, AD-18, AD-20).
- E3: schema validation of every model output with at most 2 retries (NFR-7); normalizer for LF, UTF-8 without BOM, NFC (AD-5).
- E4: `.archshell/` layout and import-linter boundaries between core, adapter, and pack (AD-10, AD-1).
- E5: CI on three OSes, signed tags, and pre-release publishing; conformance-suite skeleton the pack and plugin pass from day one (AD-22, AD-14).

## Done when

1. A scripted no-op flow runs `archshell-engine` through every action type from the Claude Code plugin and returns `done`, on Windows, Linux, and macOS.
2. A stale revision or reused nonce is refused by the engine; an invalid model output is retried at most twice, then reported.
3. A Spanish request gets Spanish questions and reports from the message catalogs; every written file is English, LF, UTF-8 without BOM, NFC, byte-identical across the three OSes.
4. A confirmation record written twice for the same flow appends, never rewrites; its schema validates.
5. import-linter fails the build when the core imports an adapter or pack; the conformance-suite skeleton passes for the plugin and the pack.
6. A signed tag publishes pre-release packages to TestPyPI and GitHub Packages.

## Boundaries

Platform baseline, opening epic. No pattern, charter, or template logic; no LLM provider other than the host's own agent (FR-35 out of scope). Spec non-goals apply.

## References

- parent — _bmad-output/initiative-archshell/initiative-archshell.md
- spec — _bmad-output/initiative-archshell/spec-archshell/spec-archshell.md, section Capabilities
- architecture — _bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md, AD-1–AD-23
- prd — _bmad-output/initiative-archshell/prd-archshell/prd-archshell.md, FR consequences

## Notes

- Decision: the confirmation-record writer lives here because epics 2 and 3 already write bootstrap records; blocking and verification of records belongs to epic-gates-verify-change-control (2026-11-13).
- Decision: the conformance-suite skeleton starts here; epic-extensibility-launch completes it (2026-11-13).
