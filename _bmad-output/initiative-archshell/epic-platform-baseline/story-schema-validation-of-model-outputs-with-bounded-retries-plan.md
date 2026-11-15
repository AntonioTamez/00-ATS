---
title: 'Schema validation of model outputs with bounded retries'
type: 'feature'
ticket: '5'
created: '2026-11-13'
status: 'built'
baseline_revision: '8ad99f0be90dba4126735ecb28364b694f40cca8'
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

**Problem:** The engine accepts any JSON object as an `llm_task` result and advances the cursor, so a malformed model answer flows into later steps unchecked (NFR-7, epic E3).

**Approach:** Each `llm_task` step names a core-owned JSON Schema (draft 2020-12, under `archshell-core/schemas/`). On `submit`, the engine validates the result; an invalid result re-issues the same step as a fresh `llm_task` whose prompt states the validation errors, at most `max_retries` times (default 2), and the next invalid result is reported with a typed error.

## Boundaries & Constraints

**Always:**
- Validation runs inside the existing lock, after the `action_id`/revision checks, before the cursor advances.
- A retry is a new action: fresh `action_id`, `revision + 1`, same `step`, new prompt file; the CAS write is unchanged.
- The attempt count lives in the state file and survives across engine invocations.
- `domain/` stays pure: schemas are loaded by an adapter and the validator is passed in.
- Error messages keep exactly `{schema, code, message, details}`; the new code is `invalid_output`, `details` lists the schema errors (path + message), capped.
- `max_retries` is a parameter with default 2; reading it from `.archshell/config.yaml` is story 1.7.
- Decision (2026-11-13): when retries are exhausted, the submit reply is `archshell/error/v1` with `invalid_output` and the state is left unchanged (pending action stays); no protocol or plugin change.

**Never:**
- No validation of `ask`/`confirm`/`instruct_agent` result content beyond today's checks.
- No host-side retry logic; `run.md` keeps "do not retry".
- No change to the stdout one-message protocol, the lock, or the CAS rules.
- No other schema library than `jsonschema` (pinned exact version, as the repo pins `jinja2` and `pyyaml`).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid output | `llm_task` pending, `{"text": "ok"}` | cursor advances to next step, attempts reset | No error expected |
| First invalid | `{"text": ""}` or `{}` | new `llm_task`, same step, revision+1, prompt includes the errors | — |
| Second invalid | after one retry | second retry issued | — |
| Third invalid | after two retries | — | `invalid_output` with schema errors; state unchanged |
| Valid after retry | after one or two retries | cursor advances, attempts reset | No error expected |
| Stale submit | old `action_id` from before the retry | — | `stale_action`, attempt not counted |
| `max_retries = 0` | first invalid | — | `invalid_output` at once |

</frozen-after-approval>

## Code Map

Baseline `8ad99f0` on `main`; work on `feat/1-5-output-validation` in `C:\git\arch-shell`.

- `archshell-core/archshell/engine/cli.py:115-135` -- `cmd_submit`: lock → read → `workflow.on_submit` → `store.ensure_prompt` → CAS `store.write`. Inject the validator here; reuse `ProtocolError`, `refuse()`, `log`. Exceptions map to codes in `run()` (`:300-317`).
- `archshell-core/archshell/domain/workflow.py` -- `Step` (`:59-65`) gains an output schema id for `llm_task`; `FLOWS` (`:70-92`) probe steps get it. `on_submit` (`:290-318`) is where the retry/advance decision goes, returning `Refusal("invalid_output", ...)` on exhaustion. `CursorState` (`:149-157`) and `to_dict`/`from_dict` (`:169-214`, exact-keys check at `:185`) gain `attempts`. `build_action` (`:107-134`) builds the retry prompt.
- `archshell-core/archshell/domain/actions.py` -- leave `LlmTask` fields as they are; the exact-fields check in `action_from_message` (`:113`) must not change.
- `archshell-core/archshell/adapters/state_store.py:103-120` -- CAS write; reuse, do not change. `ensure_prompt` writes the retry's prompt file.
- `archshell-core/pyproject.toml:7` -- add pinned `jsonschema`; make sure `schemas/` ships in the package.
- `archshell-core/tests/test_protocol.py` -- `submission_for` (`:61`) defaults to `{"answer": "yes"}`; existing `llm_task` steps (`:400`, `:415`) must submit `{"text": ...}`. Reuse `Project`, `assert_refused`, `advance_to`.
- `archshell-core/tests/test_workflow.py` -- pure helpers `ids`, `good`, `start`, `drive` (`:31-47`).
- `archshell-claude-code/commands/run.md` -- a retry is just the next `llm_task`; an error reply already stops (`:39`). No change expected.

## Tasks & Acceptance

**Execution:**
- [x] `archshell-core/pyproject.toml`, `uv.lock` -- add pinned `jsonschema`; include `schemas/` in the build -- schemas are runtime data
- [x] `archshell-core/schemas/llm-text-v1.json` -- `archshell/llm-text/v1`: object, `text` required non-empty string, no extra properties -- the probe output contract
- [x] `archshell-core/archshell/adapters/output_schemas.py` -- load schemas by id from the package, validate with Draft 2020-12, return errors as (path, message) list; unknown schema id is an `internal_error`
- [x] `archshell-core/archshell/domain/workflow.py` -- `Step.output_schema`, `CursorState.attempts`, retry/exhaust logic in `on_submit` with injected validator and `max_retries=2`, retry prompt with the errors appended
- [x] `archshell-core/archshell/engine/cli.py` -- pass the validator into `on_submit`; log `submit_retry` / `submit_invalid_output`
- [x] `archshell-core/tests/` -- unit tests for every matrix row; subprocess test for the verify line; fix existing `llm_task` submissions; state round-trip with `attempts`
- [x] `README.md` -- `invalid_output` code, the `llm_task` result schema, and retry behavior

**Acceptance Criteria:**
- Given `next --feature probe`, when the host submits an invalid `llm_task` result three times in a row, then the first two replies are new `llm_task` actions for the same step and the third is `archshell/error/v1` with `code: invalid_output`.
- Given the full suite, when `uv run python -m pytest` runs, then it passes on Windows, Linux, and macOS CI.

## Implementation Notes

- `jsonschema==4.26.0` pinned (pulls `attrs`, `jsonschema-specifications`, `referencing`, `rpds-py`, `typing-extensions`).
- Packaging: uv_build only ships module directories, so `pyproject.toml` sets `module-name = ["archshell", "schemas"]` with `namespace = true`; the wheel installs `schemas/` as a top-level directory in site-packages beside `archshell/`. The adapter resolves it as `Path(archshell.__file__).parent.parent / "schemas"` (works editable and from a built wheel; verified by installing the wheel into a scratch venv). Tradeoff: a generic top-level `schemas` name in site-packages could collide with another distribution.
- Besides `attempts`, the state also stores `errors` (the capped `{path, message}` list behind the latest retry). Needed so a repeated `next` (which calls `ensure_prompt`) regenerates the retry prompt instead of overwriting it with the plain step text. `from_dict` requires `attempts > 0` iff `errors` non-empty, and only on a step with `output_schema`.
- A state file written before this story (no `attempts`/`errors` keys) is read as `attempts=0`, so in-flight cursors are not turned into `invalid_state`.
- `on_submit` takes `validate` as a required keyword argument (no silent skip) and `max_retries=2`. Error paths are jsonschema `json_path` (`$`, `$.text`), sorted; capped at 10 errors, 200 characters per message. `invalid_output.details` = `{output_schema, error_count, errors}`.
- An unknown or malformed schema id raises `UnknownSchema` in the adapter; `run()` maps it to `internal_error` through the existing catch-all.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens): high 0, medium 1, low 2, false 0, maybe-false 0; plus 1 implementer-reported item.

| # | Finding | Verdict | Route | Evidence / action |
|---|---------|---------|-------|-------------------|
| 1 | `engine/cli.py:21` top-level `output_schemas` import loads jsonschema on every hook call | medium | patch | Import is module-level and `run()` dispatches hooks from the same module; adds ~130-150 ms to `guard-write` (NFR-1 per-edit budget). Patch: lazy import in `cmd_submit`. |
| 2 | `cap_errors` caps messages but not paths | low | patch | Paths flow uncapped into state, retry prompt, and details; fix is a direct slice. Patch: cap path length too. |
| 3 | `invalid_output` message says "after 0 retries" when `max_retries=0` | low | patch | Message uses `state.attempts`; code and details correct. Patch: wording correct for any limit. |
| 4 | Build ships a top-level `schemas` package into site-packages (implementer report) | medium | defer | Real collision risk with any other distribution named `schemas`; the fix moves schemas inside the `archshell` package, which changes the AD-11 layout `archshell-core/schemas/`, so it needs a decision. |

## Design Notes

Retry prompt: the original step text, then a section such as `Your previous answer did not match archshell/llm-text/v1:` with one line per error, so the model can correct itself. Attempts reset to 0 whenever the cursor advances.

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass
- `$env:ARCHSHELL_ENGINE` module form, `next --feature probe` then three invalid submits -- expected: two `llm_task`, then `invalid_output`

