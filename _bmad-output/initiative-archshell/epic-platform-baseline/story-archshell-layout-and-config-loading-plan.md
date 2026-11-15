---
title: '`.archshell/` layout and config loading'
type: 'feature'
ticket: '7'
created: '2026-11-13'
status: 'built'
baseline_revision: '70f6fd42ad0ee9c31b0d3dfda49be7503196fc37'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No code owns the AD-10 `.archshell/` layout: each path is built ad hoc, nothing creates the layout or its gitignore entries, and `config.yaml` is never read, so `max_retries` from story 1.5 is fixed at 2 (epic E4).

**Approach:** One core layout module names every AD-10 path; a create function builds the layout in a project (directories, a default `config.yaml`, and the gitignore entries for `state/`, `locks/`, and `txn.json`); a config loader reads and schema-validates `config.yaml` and feeds `max_retries` into `submit`.

## Boundaries & Constraints

**Always:**
- Every `.archshell/` path in code comes from the layout module (state, locks, prompts, protected dir, error details).
- Creation is explicit and idempotent: it never overwrites an existing `config.yaml`, and rerunning it changes no bytes. Commands that do not create the layout (`next`, `submit`, hooks, any refusal) keep today's lazy behavior; a refusal never creates `.archshell/`.
- Gitignore entries are written to `.archshell/.gitignore` (generated, engine-owned, inside the protected dir), never to the project's root `.gitignore`.
- `config.yaml` declares `schema: archshell/config/v1`, is written with the canonical emitter and `write_text`, read with `yaml_read`, and validated with `output_schemas.validate` against `archshell-core/schemas/config-v1.json`.
- Missing `config.yaml` means defaults (`max_retries: 2`); an invalid one is refused with a new typed error `invalid_config` whose `details` list the schema errors.
- jsonschema stays out of `next` and the hook commands.

**Never:**
- No `init` CLI command or bootstrap flow (epic-bootstrap-scaffolding); no `trust.yaml`, charter, vocabulary, plan, record, derived, or policy content.
- No change to the protected set, hook verdicts or messages, the lock, or the compare-and-swap rules.
- Decision (2026-11-14): the cursor state file stays sorted-key JSON; moving it onto the canonical YAML emitter is deferred.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh project | empty dir, create layout | exactly the AD-10 directories, default `config.yaml`, `.archshell/.gitignore` with `state/`, `locks/`, `txn.json` | No error expected |
| Rerun | layout already created | no file bytes change | No error expected |
| Edited config kept | `config.yaml` with `max_retries: 4`, create layout | file untouched | No error expected |
| No config | `submit` with no `config.yaml` | retries use the default 2 | No error expected |
| Config override | `max_retries: 0` | first invalid `llm_task` result is `invalid_output` | — |
| Invalid config | `max_retries: -1`, unknown key, or bad YAML | — | `invalid_config`, state unchanged |
| Refusal | any refused command in an empty dir | `.archshell/` still absent | existing codes |

</frozen-after-approval>

## Code Map

Baseline `70f6fd4` on `main`; work on `feat/1-7-archshell-layout` in `C:\git\arch-shell`.

- `archshell-core/archshell/adapters/state_store.py:43-46,69-70` -- `state_path`, `prompt_path`, `lock_path` build `.archshell/...` by hand; switch to the layout module, keep lazy `mkdir`, lock, and CAS unchanged
- `archshell-core/archshell/domain/workflow.py:44` `PROMPTS_DIR`, `:49` `DEFAULT_MAX_RETRIES`; `on_submit(..., max_retries=)` at `:361-367`
- `archshell-core/archshell/domain/hooks.py:25` `PROTECTED_DIR`; `is_protected` `:152-154` -- source the constant from the layout module, behavior unchanged
- `archshell-core/archshell/engine/cli.py:126-133` `cmd_submit` (lazy jsonschema import) -- load config here and pass `max_retries`; `:322,329` hard-coded paths in error details; error codes mapped in `run()`
- `archshell-core/archshell/adapters/output_schemas.py` -- `validate(schema_id, instance)`, `UnknownSchema`; reuse for config
- `archshell-core/archshell/render/yaml_emit.py` `emit`, `Ordered`; `archshell/render/normalize.py` `to_bytes`; `archshell/adapters/files.py` `write_text`; `archshell/adapters/yaml_read.py` `read`, `YamlReadError` -- reuse for writing and reading `config.yaml`
- `C:\git\arch-shell\.gitignore:2` ignores all of `.archshell/` for repo smoke runs; fixture tests must use `tmp_path`
- `archshell-core/tests/test_protocol.py:22-56` `Project` fixture; `:252,257,347,355` assert refusals leave no `.archshell/`; `tests/test_hooks.py:101-103` asserts `.archshell/config.yaml` is protected
- `README.md:65` state location, `:90-113` hooks -- add a `.archshell/` layout section

## Tasks & Acceptance

**Execution:**
- [x] `archshell-core/archshell/domain/layout.py` -- pure module: AD-10 relative paths and directory list, gitignore lines, default config document
- [x] `archshell-core/archshell/adapters/layout.py` -- `create_layout(root)` (idempotent, never overwrites `config.yaml`) and `load_config(root)` returning defaults when missing, raising a typed error with schema errors when invalid
- [x] `archshell-core/schemas/config-v1.json` -- `archshell/config/v1`: `schema` const, optional `max_retries` integer 0–10, no extra properties
- [x] `state_store.py`, `workflow.py`, `hooks.py`, `cli.py` -- use layout paths; `cmd_submit` passes config `max_retries`; map the config error to `invalid_config`
- [x] `archshell-core/tests/` -- a layout test asserting the exact file and directory set and `.gitignore` bytes; one test per matrix row
- [x] `README.md` -- `.archshell/` layout, `config.yaml` fields, `invalid_config`

**Acceptance Criteria:**
- Given an empty fixture project, when `create_layout` runs, then the set of paths under `.archshell/` equals the specified list and `.archshell/.gitignore` holds exactly `state/`, `locks/`, `txn.json`.
- Given `git check-ignore` in a fixture repo, then `.archshell/state/x.yaml`, `.archshell/locks/x.lock`, and `.archshell/txn.json` are ignored and `.archshell/config.yaml` is not.

## Implementation Notes

- Refusal gap fixed: on the baseline, `submit` before any `next` (`no_pending_action`) took the lock and so created `.archshell/locks/` in an empty dir, against "a refusal never creates `.archshell/`". `cmd_submit` now refuses without the lock when no state file exists (safe: `next` writes state before any action reaches a host).
- `load_config` decodes the file and calls `yaml_read.load` (not `yaml_read.read`) so `invalid_config` details carry the project-relative path, never an absolute one. `output_schemas` is imported inside `load_config` and `cmd_submit`, so `next` and the hooks still never load jsonschema (pinned by `test_next_and_hooks_do_not_load_jsonschema`).
- `invalid_config` details: `{"config": ".archshell/config.yaml", "errors": [{path, message}]}`, capped with `workflow.cap_errors`. Config is loaded before the lock, so an invalid config changes no state.
- `DEFAULT_MAX_RETRIES` and `PROMPTS_DIR` now live in `domain/layout.py`; `workflow` re-exports them by import. `hooks.PROTECTED_DIR = layout.ROOT`.
- Verification: `uv run python -m pytest` -> 492 passed, 2 skipped (baseline 466 passed, 2 skipped). New `archshell-core/tests/test_layout.py` (26 tests); 13 of its CLI tests fail against the baseline `cli.py`.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens): high 0, medium 0, low 6, false 0, maybe-false 0.

| # | Finding | Verdict | Route | Evidence / action |
|---|---------|---------|-------|-------------------|
| 1 | `cli.py` `invalid_config` message claims a schema mismatch for read, UTF-8, and YAML failures | low | patch | `load_config` raises `InvalidConfig` from four causes; the message names only one. Patch: cause-neutral message. |
| 2 | `create_layout` exists-then-replace can overwrite a `config.yaml` created in between | low | reject | Real race, but only with concurrent setup or a human edit in the same instant; the fix adds an exclusive-create path beside `write_text`. Rejected as unlikely with a non-direct fix. |
| 3 | Pre-lock `no_pending_action` duplicates the domain refusal | low | patch | Code and message re-typed in `cli.py` beside `workflow.on_submit`; drift risk. Patch: one shared source. |
| 4 | `state_store.py` re-implements `layout.path_of` | low | patch | Three inline `joinpath(*rel.split("/"))`. Patch: reuse one helper. |
| 5 | README says state is always gitignored | low | patch | Only true after `create_layout`; `next`/`submit` never write `.archshell/.gitignore`. Patch: reword. |
| 6 | `load_config` `OSError` branch untested; `strerror` may be `None` | low | patch | No test reaches the branch. Patch: non-None fallback plus one test. |

## Design Notes

Why `.archshell/.gitignore`: AD-16 gives every path one writer. The root `.gitignore` is human-owned; appending to it would make it a mixed file. A nested `.gitignore` inside the engine-only directory is generated by the core and covered by the protected set and the single CODEOWNERS glob.

Layout created: `config.yaml`, `.gitignore`, and directories `charter/`, `vocabulary/`, `features/`, `records/`, `derived/`, `state/`, `locks/`. Empty directories are not tracked by git; that is expected.

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass

