---
title: Adversarial review — ArchShell architecture spine
target: ../architecture-archshell.md
lens: adversarial (construct two units that obey every AD yet build incompatibly)
reviewed: 2026-11-08
inputs:
  - ../architecture-archshell.md (status draft, AD-1..AD-15)
  - ../../prd-archshell/prd-archshell.md (context)
  - ../architecture-archshell.memlog.md (NOT FOUND, see R-0)
verdict: revise before build (5 critical, 7 high, 5 medium, 2 low)
---

# Adversarial review — ArchShell architecture spine

## Verdict

**Revise before build.** The spine is strong on determinism (AD-4, AD-5) and protocol closure (AD-2), but it treats "generated file", "input", "state", and "host" as cleaner categories than the PRD allows. Five pairs of units can each obey every AD to the letter and still produce a system that loses agent code, fakes approvals, or cannot be built without putting logic in the host. Each finding below names the pair, shows how both sides comply, shows the clash, and proposes the AD text that closes it.

Severity scale: **Critical**: the hole breaks a launch acceptance criterion or the trust model and will appear in the first build. **High**: incompatible implementations are likely, or a PRD guarantee fails under normal team use. **Medium**: divergence is likely between contributors or packs, and the fix is cheap now. **Low**: wording or tie-breaking.

## R-0 — Missing input (note)

`architecture-archshell.memlog.md` does not exist next to the spine. The review proceeds from the spine and PRD only. If the memlog holds decisions that resolve any finding below, it was not available to this review.

## Summary

| ID | Severity | Pair | Hole |
| --- | --- | --- | --- |
| R-1 | Critical | `verify` / `reconcile` vs agent-filled scaffold | "Every generated artifact is a pure function of inputs" is false for scaffold files whose bodies the agent fills |
| R-2 | Critical | engine vs Claude Code host hooks | Per-edit checks, protected-path blocking, and stop-time gates have no engine command; the host must hold logic or call the pack directly |
| R-3 | Critical | engine vs host (`submit`) | `submit` results (confirm answers, gate outcomes) are unauthenticated; the agent is the host |
| R-4 | Critical | feature A plan vs feature B plan (core fold) | Inputs are an unordered union, but plans modify shared entities and contracts in sequence; no identity key or fold order |
| R-5 | Critical | .NET pack vs AD-4/AD-5 (migrations) | `dotnet ef migrations add` generates clock-stamped files that `verify` cannot regenerate byte-identically |
| R-6 | High | two engine processes on one feature | Stateless engine plus files with no revision or lock: lost updates, stale submits |
| R-7 | High | engine `state/` vs `verify` / second machine | Attempts and extensions live in gitignored "reconstructible" state, which cannot be reconstructed and is resettable |
| R-8 | High | records writer vs records writer | `records/<slug>-<flow>.yaml` collides on repeated flows and on growth without a feature slug |
| R-9 | High | .NET pack vs future Java pack | Plan surface schema is core-owned but surface semantics (access levels, members) are language-specific |
| R-10 | High | `reconcile` vs `VcsReader` | "First merged wins" needs merge order, but AD-9 only allows `merge-base`, `show`, `ls-files` |
| R-11 | High | contributor A vs contributor B (slugs) | Duplicate-slug suffixing only sees the local branch; both branches create the same input path |
| R-12 | High | `CiProvider` vs core vs host (owners and hook config) | CODEOWNERS and host hook config are trust anchors with no owner in the spine and no regeneration check |
| R-13 | Medium | core charter writer vs `reconcile` | `architecture.yaml` is both an input ("base charter") and derived ("body"); ADRs, `ARCHITECTURE.md`, backlog unclassified |
| R-14 | Medium | engine vs pack/analyzer (user text) | "All user-facing text comes from the engine" vs pack- and Roslyn-produced violation messages |
| R-15 | Medium | consensus runs vs action protocol | AD-3 makes three runs separate contexts, AD-2 does not say whether consensus is one action or three |
| R-16 | Medium | branch on core v1.2 vs branch on core v1.3 | Mixed schema versions in one input union after merge; no rule for which emitter wins |
| R-17 | Medium | `run` action vs `ProcessRunner` port | Two paths for toolchain execution and gate runs with no rule for which one a step uses |
| R-18 | Low | ADR writer vs PRD FR-9 | AD-10 slug naming contradicts `docs/adr/0001-architecture.md`; same-date ordering has no tie-breaker |
| R-19 | Low | two installed packs | No rule for resolving which pack serves `language` when two entry points claim it |

---

## Critical

### R-1 — Scaffold files are not pure derived artifacts

- **Pair:** `archshell verify` (regenerates and compares) and `archshell reconcile` (regenerates from the union), against the agent filling method bodies in scaffolded files.
- **Both comply:** AD-6 says every generated artifact is a pure function of inputs and is regenerated, never hand-merged. AD-8 says feature generation rolls forward by deterministic regeneration. FR-24 says the agent only fills bodies and adds private members in scaffolded files.
- **Clash:** A handler scaffolded from the plan is generated, then the agent writes its body. Now:
  - `verify` regenerates the scaffold (FR-41 bullet 5) and the bytes differ, so every implemented feature fails CI.
  - `reconcile` after a merge conflict regenerates the file and deletes the implementation.
  - AD-8 roll-forward on resume overwrites bodies written before the interruption.
  An implementer who reads AD-6 literally builds a destroyer; one who reads FR-24 literally builds a `verify` that cannot pass.
- **Proposed rule (new AD-16, "Generated vs seeded files"):** Every file ArchShell writes is exactly one of two kinds, recorded in the plan per path:
  - **Generated:** fully owned by ArchShell (charter tests, contract code, composition, policies, `.slnx`/`.csproj` references). Regenerated and compared byte for byte; never edited.
  - **Seeded:** written once from a template, then owned by the agent within the planned surface. Never regenerated after first write. `verify` checks seeded files by surface conformance (FR-56) and plan conformance (FR-46), never by bytes. Roll-forward writes a seeded file only if it does not exist. `reconcile` never touches seeded files; their conflicts are ordinary code conflicts.
  Amend AD-6 to say "every *generated* artifact" and list seeded files as outside the derived set.

### R-2 — Hooks have no engine entry point, so the host must hold logic

- **Pair:** `archshell-engine` (commands `next`, `submit`, `verify`, `reconcile`, `upgrade`) and the Claude Code host adapter's hooks (PreToolUse, PostToolUse, Stop).
- **Both comply:** AD-14 says the host holds no logic. AD-10 says hooks block every agent write under `.archshell/`. FR-14 needs hooks to block the whole protected set; FR-20 needs a per-edit check after each edit; FR-21 needs full gates when the session stops.
- **Clash:** None of these are driven by the `next`/`submit` loop. They are triggered by the runtime, asynchronously to the workflow. To implement them the host must:
  - hardcode the protected-path globs (which include `.csproj` `<ProjectReference>` elements and `Directory.Build.props`, so .NET knowledge in a host); or
  - import and call the language pack's tree-sitter checker directly (host-to-pack coupling not in the diagram); or
  - invent an engine call outside the closed protocol.
  A second host adapter (Codex, OpenCode) would make a different choice, which is exactly what AD-1 and AD-2 claim to prevent. "Block agent writes under `.archshell/`" also only stops the agent's Edit/Write tools; the agent runs `archshell-engine submit` itself, and runs shell commands the hook does not see.
- **Proposed rule (extend AD-2 with a "guard" sub-protocol):** The engine exposes hook-facing commands with their own versioned schemas, for example `archshell-engine guard write <path>` (allow / block with an error report), `archshell-engine check-edit <path>` (per-edit checks via the pack, within NFR-1), and `archshell-engine stop` (full gates, mark tasks *not verified*). The protected-path set is computed by the engine from the charter and the pack, never stored in host config. A host's hook is a one-line forward to these commands and maps the result to the runtime's allow/deny. The conformance suite (FR-37) tests that a host forwards every hook event and decides nothing.

### R-3 — `submit` trusts whatever the host says, and the host is the agent

- **Pair:** engine (`submit <result>` → validate schema → transition) and the Claude Code host, where "the host executes the action" means the agent executes it.
- **Both comply:** AD-2 says hosts never reinterpret an action and the engine validates the result (NFR-7). Schema validation passes.
- **Clash:** The engine cannot tell:
  - a `confirm` answered by the developer in the question UI from one the agent filled in itself (the agent never approves growth, FR-30);
  - a `run` result of "gates passed" from a real gate run;
  - a plan-extension request from the agent vs a reclassification the developer chose.
  Every one is a well-formed `submit`. The attempts counter (FR-26) and the record's "developer who confirmed" field (FR-53) are only as true as the host's word. PR review catches tampered records, but a fabricated "gates passed" leaves no file to review: CI catches it later, after the agent has already reported done.
- **Proposed rule (new AD, "Engine owns verdicts; host owns only human input"):**
  - The engine never accepts a gate verdict from `submit`. Gates, per-edit checks, surface checks, and breaking-change checks run inside the engine through `ProcessRunner` and the pack; a `run` action is only for steps whose output is not a verdict, if any remain.
  - `ask` and `confirm` results carry a source tag (`human_ui`) that only the host's native question tool can produce, and a nonce issued with the action. The conformance suite proves the host routes them only through the question UI.
  - Records state that in-session identity is self-reported and that PR review remains authoritative (already true in FR-53); the spine should say it so no implementer treats `confirmed_by` as verified.

### R-4 — An unordered union of inputs cannot fold ordered modifications

- **Pair:** the core's derivation of current entities and contracts, applied to feature A's plan (creates `Appointment` and contract `CancelAppointment`) and feature B's plan (modifies `Appointment`, revises the contract).
- **Both comply:** AD-6 says derived artifacts are a pure function of the *union* of inputs, stored one file per slug. AD-13 says breaking-change detection diffs contract data in plans. FR-46 says a shared file is checked against the plan that created it plus every later plan that modified it.
- **Clash:** "Later" has no defined meaning in a set. Two implementers of the fold will choose different orders (record date, slug order, merge order), and when two plans modify the same type the derived surface differs. The spine also names no identity key for an entity, data type, or contract, so "the same contract" in AD-7 and AD-13 is undefined: one implementer keys by normalized name, another by module plus name, another by file path. And the contract has no owner: its current shape is spread across N plan files, none of which is authoritative.
- **Proposed rule (new AD, "Identity and lineage of shared entities"):**
  - Every entity, data type, contract operation, and building block has a stable identity key defined by the core: `<module>/<kind>/<normalized-name>`.
  - A plan that modifies an existing item records the identity key and the fingerprint of the version it modifies (`base: sha256:…`). The derived current state is the chain from the creating plan through each modification, ordered by these base pointers, not by dates or slugs.
  - Two plans that name the same base fingerprint for one identity are a semantic conflict under AD-7, detected mechanically.
  - The derived per-identity current state is materialized as a derived artifact (for example `.archshell/derived/contracts.yaml`), so `verify`, the pack, and breaking-change detection read one shape.

### R-5 — EF migrations break byte-identical regeneration

- **Pair:** the .NET pack's `data_migration` building block, against AD-4 and AD-5 as `verify` applies them.
- **Both comply:** AD-5 says the .NET SDK runs `ef migrations add` and "never generates files". FR-24 says migrations are created through the toolchain. AD-4 says no clock in output.
- **Clash:** `dotnet ef migrations add` *does* generate files: `<timestamp>_<Name>.cs`, its designer file, and an updated `ModelSnapshot`, with the timestamp from the wall clock and contents that depend on the EF version and the current model. AD-5's sentence is false on its face, and `verify`'s regenerate-and-compare (FR-41) cannot reproduce them. The pack implementer must either violate AD-5 or skip migrations from `verify`, and two packs will pick differently.
- **Proposed rule (amend AD-5):** Toolchain-generated files are a third kind next to generated and seeded (R-1): **toolchain-owned**. They are created once by the pack through `ProcessRunner`, never regenerated, excluded from byte comparison, listed in the plan by path pattern, and checked by `verify` only for existence and by the build. Any timestamp the toolchain needs comes from the plan's stored clock input (pass an explicit migration ID instead of letting EF stamp it). Remove "never generates files" from AD-5.

---

## High

### R-6 — No concurrency control on engine-owned files

- **Pair:** two `archshell-engine` processes for one feature: the three parallel consensus subagents of AD-3, or a PostToolUse `check-edit` firing while the main loop calls `submit`, or two terminal sessions on one worktree.
- **Both comply:** AD-1 says each command reads state from disk and returns the next action; nothing forbids concurrent commands.
- **Clash:** Read-modify-write on `state/<slug>.yaml` loses updates (an attempt not counted, a pending question overwritten). A `submit` from a stale `next` applies a result to a different pending action, because AD-2 defines no correlation between an action and its result.
- **Proposed rule (amend AD-1 and AD-2):** Every state file carries a monotonically increasing `revision`. Every action carries `action_id` and the `revision` it was issued at; `submit` must echo both and is rejected with a typed `stale_action` error otherwise. Writes are compare-and-swap (write temp, check revision, atomic rename) under a per-feature lock file.

### R-7 — "Reconstructible" state contains facts that cannot be reconstructed

- **Pair:** the engine on the developer's machine (holds attempts, extensions, pending questions in `state/<slug>.yaml`, gitignored) and the engine on a second machine or after a fresh clone, plus `verify`.
- **Both comply:** AD-1 says all workflow state lives in project files; AD-10 says `state/` is gitignored and reconstructible.
- **Clash:** Attempts (FR-26), extension counts (FR-54, max 2), revision counts, and "not verified" markers are history, not functions of inputs. "Reconstructible" can only mean "reset to zero", so deleting the file (a shell command the hooks do not see) resets the 5-attempt bound, and a teammate resuming the feature starts fresh. One implementer reconstructs from records (extensions and revisions do have records), another resets.
- **Proposed rule (amend AD-10):** Split state into **committed counters** and **ephemeral session state**. Counters that bound the workflow (extensions, contract revisions) are derived from committed records, so they survive clones and cannot be deleted without a protected change. Full-gate attempt counts and pending questions are ephemeral and explicitly reset on a new clone, and the spine says so. List every state field with its class.

### R-8 — Record file names collide

- **Pair:** two confirmations of the same flow in one feature (a second plan extension, a second contract revision), or a growth confirmation and a feature confirmation sharing a slug.
- **Both comply:** AD-10 fixes `records/<slug>-<flow>.yaml`; FR-53 requires a record per confirmation.
- **Clash:** The second extension record overwrites the first, losing the record the first change is checked against (FR-41 bullet 4). `<slug>-<flow>` is also unparseable because slugs contain hyphens (`cancel-appointment-plan-extension`). Growth proposed inside a feature and standalone growth have no rule for whose slug they use.
- **Proposed rule (amend AD-10):** `records/<feature-slug>/<seq>-<flow>.yaml`, where `<seq>` is a zero-padded per-feature sequence derived from existing records, plus a fixed flow vocabulary (`plan`, `extension`, `contract`, `contract-revision`, `breaking-change`, `growth`, `vocabulary`). Growth always belongs to the feature that triggered it. Records are append-only; the hook guard (R-2) and `verify` reject edits to an existing record.

### R-9 — The core-owned plan schema must describe language-specific surfaces

- **Pair:** `archshell-dotnet` and a future `archshell-java`, both writing feature plans.
- **Both comply:** AD-11 says the core owns every ArchShell schema and packs never define formats. FR-56 says the plan fixes the public and internal surface of every planned type, with C# access levels.
- **Clash:** The plan schema has to express member surfaces: C# `internal`, properties, constructors, partial classes; Java package-private, `protected`, records, overloads. Either the core schema hardcodes C# terms (the Java pack cannot comply) or each pack adds its own fields (AD-11 forbids it). Two pack authors will pick opposite sides. The same applies to the analyzer payload: AD-12 says the payload is pack-defined, so the core cannot interpret it, but surface comparison needs the planned surface from a core schema.
- **Proposed rule (amend AD-11):** The core schema defines a language-neutral surface model (members by kind `operation | field | constructor`, visibility `public | module | private`, typed by the closed type system of FR-47). Each pack declares, in its conformance manifest, the mapping from its language's constructs to that model, and owns comparison of actual code against it. Pack-defined payloads (AD-12) are allowed only inside the pack boundary and never persisted in ArchShell files.

### R-10 — "First merged wins" requires history the VCS port cannot read

- **Pair:** `reconcile` (must decide which of two decisions merged first) and the `VcsReader` port as AD-9 limits it.
- **Both comply:** AD-7 says the decision merged first into the default branch wins; AD-9 lists `merge-base`, `show <rev>:<path>`, `ls-files` as the read-only queries.
- **Clash:** Merge order needs first-parent log of the default branch (or at least "is this commit an ancestor of the default branch"). An implementer either adds `git log` (expanding the port informally) or uses record dates (wrong under clock skew and rebases). Separately, AD-7 assumes the conflict is caught before the second merge; if both land (CI gap, `trust: unenforced`), the "later feature" is already on the default branch and `reconcile` has no branch to stop.
- **Proposed rule (amend AD-7 and AD-9):** Define the winner as "the decision present at `merge-base(HEAD, default)`"; the branch whose decision is not in the merge base is the later one. This needs only `merge-base` and `show`, which AD-9 already allows. Add `rev-parse` and `merge-base --is-ancestor` to the allowed query list explicitly. For conflicts found on the default branch itself, `verify` fails with a typed `post_merge_semantic_conflict` and names both features; no automatic winner.

### R-11 — Slug suffixing cannot see the other branch

- **Pair:** contributor A on branch `a` and contributor B on branch `b`, both starting "cancel appointment".
- **Both comply:** AD-10: slug IDs, a duplicate gets a suffix. Each engine sees no duplicate on its own branch.
- **Clash:** Both create `.archshell/features/cancel-appointment/plan.yaml` with different content. Git reports a conflict on an *input* file, and AD-6's "take either side and reconcile" applies only to generated files. AD-10's stated purpose ("prevents branch collisions on IDs") is not achieved.
- **Proposed rule (amend AD-10 and AD-7):** A slug collision on merge is a semantic conflict under AD-7 with a fixed resolution: the feature at the merge base keeps the slug, and `reconcile` renames the later feature's slug (directory and records) to the next suffix, then re-derives. Alternatively, make the ID `<slug>-<short-hash of first confirmation>`; the spine should pick one.

### R-12 — Trust anchors with no owner: CODEOWNERS and host hook config

- **Pair:** `CiProvider` (generates the owners file, AD-15), the core (owns layout, AD-10), and the Claude Code host (ships hooks).
- **Both comply:** AD-15 puts the owners file behind `CiProvider`. FR-14 puts "ArchShell's hook configuration" in the protected set. AD-14 says the host holds no logic.
- **Clash:**
  - The owners file is the authoritative approval mechanism (FR-53), but it is not in FR-14's protected set or AD-6's derived set. An agent removes a line from `CODEOWNERS`, and `verify` has no rule to notice.
  - Hook configuration lives in the host's runtime format (`.claude/settings.json`), so the core cannot generate or verify it without host knowledge, and the host cannot generate it without being a writer of protected files.
- **Proposed rule (amend AD-6 and AD-15):** The owners file and host hook config are derived artifacts. `CiProvider` and `HostAdapter` each expose a pure render function (inputs: charter, layout) that the core calls; `verify` regenerates both and compares. Both are added to the protected set. The `trust: unenforced` check also fails if the rendered owners file differs from the committed one.

---

## Medium

### R-13 — Unclear input vs derived classification for charter files

- **Pair:** the bootstrap writer and `reconcile`.
- **Clash:** AD-6's inputs list "base charter", but AD-10's layout has no base charter file; AD-6's diagram derives "architecture.yaml body". `ARCHITECTURE.md`, ADRs, `docs/feature-backlog.md`, and `.gitattributes` appear in neither column. The backlog is human-editable (FR-52) yet the engine marks items done, so two writers share one file with no merge rule.
- **Proposed rule (amend AD-6):** A table assigning every file in the layout to exactly one class (input, generated, seeded, toolchain-owned, human-owned) and one writer. Name the base charter file (for example `.archshell/charter/base.yaml`, input, written once by bootstrap). Make the backlog's "done" marker derived from plans and gate results instead of a written field.

### R-14 — Who renders violation messages

- **Pair:** engine (renders all user-facing text, FR-39) and pack plus Roslyn analyzer (produce violation details, FR-22).
- **Clash:** One pack returns English prose in `message`; another returns codes. Spanish output becomes mixed.
- **Proposed rule (amend Conventions/Errors):** Packs and analyzers return `{code, args}` only; message catalogs per code ship with the pack in core-defined format, and the engine renders them in the conversation language.

### R-15 — Consensus shape in the protocol

- **Pair:** engine (consensus logic) and host (spawns subagents, AD-3).
- **Clash:** One host receives one `llm_task` with `runs: 3` and spawns three subagents; another receives three `llm_task` actions in sequence. With R-6 unresolved, parallel submits corrupt state; with sequential actions, each run's context could include the previous run's result in the host's conversation, which AD-3 forbids.
- **Proposed rule (amend AD-2/AD-3):** Consensus is three separate `llm_task` actions with distinct `action_id`s, issued together as a batch; the host must run them in fresh contexts and may submit in any order. The engine compares only when all three are in.

### R-16 — Mixed schema versions after merge

- **Pair:** branch on core v1.2 (plans `archshell/plan/v1`) and branch upgraded to v1.3 (`archshell/plan/v2`).
- **Clash:** After merge the union holds both versions; AD-14 pins one core version in `architecture.yaml`, which is itself in conflict.
- **Proposed rule (amend AD-14):** `verify` and the engine refuse inputs whose schema version is not the one the pinned core writes, and tell the user to run `archshell upgrade`; migrations must be idempotent so upgrading an already-upgraded file is a no-op.

### R-17 — Two execution paths for the toolchain

- **Pair:** engine-side `ProcessRunner` and host-side `run` action.
- **Clash:** One implementer runs `dotnet build` inside the engine; another emits `run` and lets the host execute. Gate verdicts differ by environment, and R-3 applies.
- **Proposed rule:** State which steps use `run` (ideally none that produce verdicts or files) and require everything else through `ProcessRunner`.

---

## Low

### R-18 — ADR naming

AD-10 names ADRs by slug; PRD FR-9 and FR-32 fix `docs/adr/0001-architecture.md` and `0002-add-notifications-module.md`. Pick one and update the other. Ordering "by the stored record date" needs a tie-breaker (date, then slug) since two growths can share a date.

### R-19 — Pack resolution

`architecture.yaml` has one `language` field; the entry point `archshell.language_packs` can expose two packs for the same language (a fork). Rule: the charter records the pack's distribution name and version (AD-14 already pins the version); resolution is by name, never by language.

## What holds up

- AD-4's ban on clock, randomness, and environment in the domain, with a cross-OS byte comparison job, is concrete and testable.
- AD-2's closed action set and the "unsupported action is a typed error" rule leave little room for host divergence inside the main loop.
- AD-13 basing breaking-change detection on contract data rather than compiled code removes a whole class of build-dependent verdicts.
