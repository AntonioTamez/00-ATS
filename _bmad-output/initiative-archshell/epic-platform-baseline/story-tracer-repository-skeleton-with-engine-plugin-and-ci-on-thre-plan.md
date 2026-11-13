---
title: 'Tracer: repository skeleton with engine, plugin, and CI on three OSes'
type: 'feature'
ticket: '1'
created: '2026-11-13'
status: 'built'
baseline_revision: '77c442288e988f5ab55293d7402f5cce7cb3008e'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The `arch-shell` repository is empty, so no later story has packages, an engine executable, a host plugin, or CI to build on.

**Approach:** Create the four package skeletons from the spine's Structural Seed as one uv workspace plus a .NET analyzer project, make `archshell-engine next` answer a `done` action over JSON stdin/stdout, have a Claude Code plugin command invoke it, and run everything in GitHub Actions on Windows, Linux, and macOS.

## Boundaries & Constraints

**Always:** Code lives in `C:\git\arch-shell` on branch `feat/1-1-tracer`. Python ≥ 3.12; core distribution name `archshell`, pack `archshell-dotnet`, import packages `archshell`, `archshell_dotnet`. Protocol messages are JSON over stdin/stdout, UTF-8, one message per invocation; stdout is reserved for the protocol, logs go to stderr as JSON lines. The plugin holds no logic: it forwards to the engine. Analyzer targets `net10.0`, SDK pinned by `global.json`. Tests ship with the code.

**Never:** No other action types, revision/nonce handling, hooks, schemas, catalogs, import-linter, or release publishing (stories 1.2–1.11). No `archshell.providers` entry point. No network or secrets in the engine.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | `archshell-engine next` with stdin `{}` | stdout one JSON line `{"protocol":"archshell/action/v1","type":"done"}`, exit 0 | No error expected |
| Bad JSON | stdin `not json` | stdout `{"protocol":"archshell/error/v1","code":"invalid_message","message":...,"details":{}}`, exit 2 | Never a traceback on stdout |
| Unknown command | `archshell-engine frobnicate` | `archshell/error/v1` with `code` `unknown_command`, exit 2 | Same |
| CLI stub | `archshell --version` | prints the core version, exit 0 | — |

</frozen-after-approval>

## Code Map

Greenfield: the repository holds only `README.md` (baseline `77c4422`). Layout follows the spine's Structural Seed and Design Paradigm.

- `pyproject.toml` (root) -- uv workspace; members `archshell-core`, `archshell-dotnet`; dev group pytest
- `archshell-core/pyproject.toml` -- distribution `archshell`; scripts `archshell-engine`, `archshell`
- `archshell-core/archshell/{domain,render,ports,adapters,engine,cli}/` -- seed packages; only `engine` and `cli` get code now
- `archshell-dotnet/pyproject.toml`, `archshell-dotnet/archshell_dotnet/__init__.py` -- pack skeleton, depends on `archshell`
- `archshell-dotnet/analyzer/` -- `net10.0` console project, packable as a .NET tool, prints its version
- `archshell-claude-code/` -- Claude Code plugin: `.claude-plugin/plugin.json`, one command
- `.github/workflows/ci.yml` -- three-OS matrix

## Tasks & Acceptance

**Execution:**
- [x] `pyproject.toml`, `.python-version`, `.gitignore`, `uv.lock` -- uv workspace, Python 3.12 -- one lockfile for all Python packages
- [x] `archshell-core/pyproject.toml`, `archshell-core/archshell/__init__.py` and seed subpackages -- core skeleton with `__version__`
- [x] `archshell-core/archshell/engine/__main__.py` (or `engine/cli.py`) -- `next` reads one JSON message from stdin, writes the `done` action; errors per the matrix -- thinnest protocol path
- [x] `archshell-core/archshell/cli/__init__.py` -- `archshell --version` stub
- [x] `archshell-core/tests/test_engine_tracer.py` -- subprocess tests for every matrix row
- [x] `archshell-dotnet/` -- Python pack skeleton plus `analyzer/Archshell.Analyzer.csproj` and `Program.cs`, `global.json` at repo root pinning SDK 10.0.4xx with `rollForward: latestFeature`
- [x] `archshell-claude-code/.claude-plugin/plugin.json`, `archshell-claude-code/commands/ping.md`, root `.claude-plugin/marketplace.json` -- `/archshell:ping` runs `archshell-engine next` with `{}` and shows its output
- [x] `.github/workflows/ci.yml` -- matrix `ubuntu-latest`, `windows-latest`, `macos-latest`: `uv sync --locked`, `uv run pytest`, `dotnet build` the analyzer, run the analyzer
- [x] `README.md` -- layout, local setup, how to install the plugin from the local marketplace

**Acceptance Criteria:**
- Given a fresh clone, when `uv sync` and `uv run pytest` run, then all tests pass on the developer's machine.
- Given a push to `feat/1-1-tracer`, when CI runs, then all three OS jobs are green.
- Given the plugin installed from the local marketplace (hitl), when the user runs `/archshell:ping` in Claude Code, then the engine's `done` action is shown.

## Implementation Notes

- Implemented in `C:\git\arch-shell` commit `d0b3ea6` on `feat/1-1-tracer` (not pushed).
- Engine entry is `archshell/engine/cli.py` (`run` = parse, dispatch via `COMMANDS`, emit); `python -m archshell.engine` and `python -m archshell.cli` call the same `main()`.
- The developer machine's Windows Application Control blocks unsigned executables (uv launchers `pytest.exe`, `archshell-engine.exe`, and the analyzer `.dll`). The tests therefore drive the engine through `python -m` and separately assert the console-script entry points; CI also runs the real `archshell-engine` / `archshell` scripts and the analyzer. Local equivalents: `uv run python -m pytest`, `echo {} | uv run python -m archshell.engine next`.
- An empty argv also returns `unknown_command`; a non-object JSON value (e.g. `[]`) is `invalid_message`; an unexpected exception returns `internal_error` (exit 2) with no traceback on stdout.
- `global.json` pins `10.0.400` with `rollForward: latestFeature`; the analyzer builds with `TreatWarningsAsErrors`.

## Plan Change Log

## Review Triage Log

Pass 1 (quick): high 0, medium 1, low 3, false 4, maybe-false 0.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | `/archshell:ping` calls `archshell-engine.exe`, which Application Control blocks on this machine | medium | defer | Machine policy, not caused by the change; the plugin works where unsigned launchers run. Recorded in deferred-work.md |
| 2 | README `echo {} \|` fails in PowerShell | low | patch | `{}` is a PowerShell script block; reviewer reproduced `invalid_message` |
| 3 | CI has never run | false | — | Pending verification that needs the user's push, not a code defect |
| 4 | Deeply nested JSON gives `internal_error` | low | patch | `json.loads` raises RecursionError, which `read_message` does not catch |
| 5 | `NaN`/`Infinity` accepted | low | reject | Unlikely in everyday use, and the fix adds a parse hook; story 1.2 schema validation covers it |
| 6 | `dotnet pack` never exercised | low | patch | `PackAsTool` set, no CI step packs |
| 7 | Happy path not checked against the console script locally | false | — | CI smoke step runs the real `archshell-engine`; local form is the same `main()` |
| 8 | No agent instruction files | false | — | Informational; no rule broken |

## Design Notes

The `done` message is the tracer's only action; story 1.2 replaces it with the full `archshell/action/v1` model and adds `action_id`/`revision`. Keep the engine entry thin: parse, dispatch, emit, so 1.2 extends instead of rewrites.

## Verification

**Commands:**
- `uv sync --locked && uv run pytest` -- expected: all pass
- `echo {} | uv run archshell-engine next` -- expected: one `done` JSON line, exit 0
- `dotnet build archshell-dotnet/analyzer` -- expected: build succeeds with no warnings

**Manual checks (if no CLI):**
- GitHub Actions run on the pushed branch: three green jobs (needs the user's push approval).
- `/archshell:ping` in Claude Code shows the `done` action (user runs it).
