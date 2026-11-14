---
title: 'Hook sub-protocol: guard-write, check-edit, on-stop'
type: 'feature'
ticket: '4'
created: '2026-11-13'
status: 'built'
baseline_revision: 'b752120754705a8405beb91da570f78020bfb90e'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Nothing stops the agent from writing where ArchShell owns the files, and the engine has no way to give per-edit or stop-time verdicts, so every later gate would have no blocking point.

**Approach:** Add the AD-17 hook sub-protocol to the engine: `guard-write`, `check-edit`, and `on-stop`, each returning an `allow` or `block` verdict computed by the engine. Wire them into the plugin as one-line forwards in `hooks.json` (PreToolUse, PostToolUse, Stop). The first real rule is AD-10's: every agent write under `.archshell/` is blocked. `check-edit` and `on-stop` return `allow` until the checks and gates of later stories exist.

## Boundaries & Constraints

**Always:** The engine computes every verdict and the protected set; host config holds no paths (AD-17, AD-18). The core protocol is host-neutral: input `archshell/hook/v1`, output `archshell/verdict/v1` (`verdict`, `code`, `reason`). Host payload parsing and output rendering live in pure functions keyed by host id under `archshell/render/` (AD-21), selected with `--host claude-code`. Hook commands are one line, use the `ARCHSHELL_ENGINE` override, and send engine stderr to `${TMPDIR:-/tmp}/archshell-engine.log`. `guard-write` fails closed: an engine error is a `block`. Paths are resolved against the project root, `..` collapsed, and compared case-insensitively. Per-edit hooks stay within NFR-1 (3 s). Tests ship with the code.

**Never:** No tree-sitter checks or gates (epic 4), no protected set beyond `.archshell/` (charter-derived files come with epic 2–4), no records (1.8), no blocking of shell-tool writes (best effort per AD-17; `verify` is the real guard).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Protected write | `guard-write`, path `.archshell/state/probe.yaml` | `block`, code `protected_path` | — |
| Ordinary write | `guard-write`, path `src/app.py` | `allow` | — |
| Escape attempt | path `src/../.archshell/x`, or `.ARCHSHELL/x` | `block` | — |
| Outside project | absolute path outside the root | `allow` | — |
| Bad input | missing path or non-JSON stdin | `block` with `archshell/error/v1` code `invalid_message` | fail closed |
| Engine failure | unexpected exception in `guard-write` | `block`, code `internal_error` | fail closed, no traceback |
| check-edit | any edited path | `allow` | — |
| on-stop | no feature, or any feature | `allow` | — |
| Claude Code PreToolUse | hook JSON with `tool_input.file_path` (Write, Edit, MultiEdit) or `notebook_path` (NotebookEdit), `cwd` | Claude Code deny/allow output with the engine's reason | — |
| Claude Code PostToolUse / Stop | hook JSON | Claude Code output that feeds a `block` reason to the agent, else nothing that blocks | — |
| In Claude Code (hitl) | agent asked to write `.archshell/test.txt` | write refused with the engine's reason | — |

</frozen-after-approval>

## Code Map

Baseline `b752120` (`main` after PR #3); work on `feat/1-4-hooks`.

- `archshell-core/archshell/engine/cli.py` -- add the three commands; reuse `ProtocolError`, `emit`, `log`, the catch-all, and `--feature` slug validation; `--host` is a new option
- `archshell-core/archshell/domain/` -- new pure module for the protected-set rule and path normalization; no IO (AD-4)
- `archshell-core/archshell/render/` -- seed package; add host renderers keyed by host id: parse the host's hook payload into `archshell/hook/v1`, render a verdict into the host's output
- `archshell-claude-code/hooks/hooks.json` -- new; the plugin auto-loads it
- `archshell-claude-code/commands/run.md` -- existing engine-call conventions (override, stderr log) to mirror
- `archshell-core/tests/test_plugin_files.py` -- existing structural checks; extend for `hooks.json`

## Tasks & Acceptance

**Execution:**
- [x] `archshell-core/archshell/domain/hooks.py` -- path normalization and the `.archshell/` rule; verdicts for the three hooks
- [x] `archshell-core/archshell/render/hosts/claude_code.py` -- parse PreToolUse/PostToolUse/Stop payloads; render verdicts in Claude Code's hook output format (check the current Claude Code hooks documentation for field names and exit-code semantics)
- [x] `archshell-core/archshell/engine/cli.py` -- `guard-write`, `check-edit`, `on-stop` with optional `--host`; fail-closed handling for `guard-write`
- [x] `archshell-claude-code/hooks/hooks.json` -- PreToolUse and PostToolUse on `Write|Edit|MultiEdit|NotebookEdit`, Stop; one-line forwards
- [x] `archshell-core/tests/test_hooks.py` -- one subprocess test per matrix row (host-neutral and `--host claude-code`), asserting stdout, exit code, and that nothing is written
- [x] `archshell-core/tests/test_plugin_files.py` -- `hooks.json` parses, each command is one line using the override and the stderr log, and no paths appear in it
- [x] `README.md` -- the hook commands, verdict schema, fail-closed rule, and how to see a block in Claude Code

**Acceptance Criteria:**
- Given the plugin installed (hitl), when the agent is asked to write `.archshell/test.txt`, then Claude Code refuses the write and shows the engine's reason.
- Given an ordinary file edit in Claude Code, then the hooks add no visible noise and the edit proceeds.
- Given `guard-write` on a protected path, then it returns within NFR-1 on the developer's machine through the override.

## Implementation Notes

- Verdict exit codes: a verdict (allow or block) exits 0; any engine error exits 2. `guard-write` errors are a `block` verdict whose `code` is the error code, with the `archshell/error/v1` message in an optional `error` field; `check-edit`/`on-stop` errors are plain `archshell/error/v1`. Allow verdicts use `code: allowed`, `reason: ""`.
- Claude Code rendering: `allow` renders nothing (exit 0), never `permissionDecision: allow`, because that would skip the user's permission prompt. Block renders `permissionDecision: deny` (PreToolUse) or `decision: block` + `reason` (PostToolUse, Stop). With `--host`, check-edit/on-stop errors render nothing so a broken hook cannot trap the agent at Stop.
- The PreToolUse hook command ends in `|| { echo ... >&2; exit 2; }` so an engine that cannot start at all (not on PATH, bad override) still blocks the write: Claude Code treats any non-2 exit as non-blocking.
- Project root is the engine's working directory (existing convention); the payload `cwd` only resolves relative paths. Path normalization is lexical (no symlink resolution) in `domain/hooks.py`.
- Engine-failure matrix row is tested in a subprocess that monkeypatches `hooks.guard_write` before calling `main`.
- Measured: guard-write through the `uv run --project` override took ~0.8 s on the dev machine (NFR-1: 3 s).

## Plan Change Log

## Review Triage Log

Pass 1 (quick): high 1, medium 2, low 5, false 0, maybe-false 0.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | Protected root follows the hook's working directory; after `cd src` a write to `.archshell/` is allowed | high | patch | Root is `Path.cwd()`; Claude Code runs hooks in the session's current directory; fix anchors hooks on `CLAUDE_PROJECT_DIR` |
| 2 | Windows path forms (`\?\`, `C:rel`, trailing dots/spaces) bypass the guard | medium | patch | `_split` keeps `\?\` as anchor `//`; segment compare is exact; Windows drops trailing dots/spaces |
| 3 | Symlinked or unresolved paths compare unequal and are allowed | medium | patch | Lexical compare against a `getcwd` that resolves symlinks; fix resolves both in the adapter |
| 4 | Render-failure branch omits the `error` field | low | patch | Other error path adds it; Implementation Notes promise it |
| 5 | README says `--host` always exits 0 | low | patch | `unknown_host` and render failure exit 2 |
| 6 | README error table misses `unknown_host` and hook rows | low | patch | Only `unknown_command` row updated |
| 7 | Matrix rows not covered under `--host claude-code` subprocess tests | low | patch | Engine failure, PostToolUse/Stop, escape and outside rows only host-neutral or pure |
| 8 | `assert` used as validation in `guard_write` | low | patch | Stripped under `-O`; turns `invalid_message` into `internal_error` |

## Design Notes

Host-neutral core, host-specific edges: the engine speaks `archshell/hook/v1` in and `archshell/verdict/v1` out; `--host claude-code` swaps in the pure parse/render pair, so a second host later adds a renderer and touches nothing else. `on-stop` without `--feature` evaluates every active feature; with none active it allows.

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass
- `echo '{"schema":"archshell/hook/v1","path":".archshell/x"}' | uv run python -m archshell.engine guard-write` -- expected: `block`, code `protected_path`

**Manual checks (if no CLI):**
- In Claude Code with the plugin updated: ask the agent to create `.archshell/test.txt`; it is refused with the engine's reason (user runs it).
- CI green on the three OSes after push (needs the user's approval).
