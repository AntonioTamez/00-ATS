---
title: 'Refactor sweep'
type: 'refactor'
ticket: '12'
created: '2026-11-15'
status: 'built'
baseline_revision: '79f01911574cba5c6494bf0588e5de63213fdee1'
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

**Problem:** Stories 1.1–1.11 left duplicated rules in production code, stale comments, and test helpers copied across four or more files. Each copy is a place where a later epic can change one side and not the other.

**Approach:** Remove the duplicates behind one shared helper each, fix the stale comments and one README line, and move the repeated test helpers into a shared `conftest.py`. Every output byte, error code, message, flag, and persisted format stays the same.

## Boundaries & Constraints

**Always:**
- No behavior change. The same input gives the same stdout, stderr, exit code, and written bytes before and after, including state JSON, prompt files, records, config, and the golden set.
- Existing tests stay as they are, except where a helper moves into `conftest.py`. No test is deleted or weakened.
- Every new helper sits in the layer that already owns the rule. The import-linter contracts stay unchanged and pass.

**Never:**
- Do not route state or prompt writes through `files.write_text` or `normalize.to_bytes`, because that adds NFC and a final newline.
- Do not change the cursor state format. Moving it to YAML stays deferred.
- No prompt-file cleanup and no change to the Application Control workaround.
- Do not remove these: the `except YamlReadError: raise` clause (`yaml_read.py:90`), the `except FileExistsError: raise` clause (`records.py:240`), the `path is None` branch (`hooks.py:160`), or the `unknown_feature` check in `on_next`.
- Do not merge the error mapping of `load_config` with that of `records._load`. Do not merge `Ask`/`Confirm` into a base class. Do not move `is_valid_slug`.
- Do not hoist `Path.cwd()` out of the `try` in `run`.
- Decision (user, 2026-11-15): split only the engine paths `run_hook`, `cmd_submit`, and `on_submit`. Leave `run`, `CursorState.from_dict`, `append_record`, and `extensions.load` as they are.

</frozen-after-approval>

## Code Map

Baseline `79f0191` on `main` (after PR #11). Work on `feat/1-12-refactor-sweep` in `C:\git\arch-shell`. All paths are under `archshell-core/` unless stated.

- `archshell/engine/cli.py:317-318,331-333` -- fail-closed block built twice; `:127-133` `require_flow` re-builds the `unknown_feature` refusal of `domain/workflow.py:425-430`; `:174` error-list serialization; `:41` `Message` alias duplicates `domain/actions.py:19`; `:33` imports the catalog `Message` as `Text`; `:201-208` two identical returns in `cmd_submit`
- `archshell/domain/workflow.py:259,489` -- error-list serialization (third copy with `cli.py:174`); `:101-105`, `:122-126` the same probe `Step("llm_task", …)` literal; `:47-49`, `:52-54` stale comments left over from moving the constants to `layout.py`
- `archshell/domain/messages.py:43` -- `PLACEHOLDER.sub("", template)` computed twice
- `archshell/adapters/records.py:86-96` -- the fingerprint try/except repeated in both branches; `:84-85`, `:146-147` invalid-slug guard copied; `:231-234`, `:251-254` write/flush/fsync (also in `adapters/files.py:44-47`); `:43` `"; ".join` error format (also `adapters/layout.py:29`)
- `archshell/adapters/state_store.py:66-68,119-121` -- tmp-write + `os.replace` twice, so add a private helper inside `state_store` only; `:3-4` stale docstring ("until the canonical YAML emitter exists")
- `archshell/adapters/files.py` -- `read_text` has no production caller, but README:252 documents it as API, so keep it
- `README.md:236` -- unknown `--host` or a failing renderer produces a `block` verdict only for `guard-write`
- `tests/test_protocol.py:19,22-58,39-45`, `test_hooks.py:21,26,30-60,35-48`, `test_layout.py:16,32,40-48`, `test_engine_tracer.py:19,29-38` -- `ENGINE`, the subprocess runner, single-JSON-line parsing, `Project`, and tree walkers repeated; no `conftest.py` exists. `test_layout`'s runner does not assert a single line, so keep that difference with a flag.
- `test_protocol.py:61-73` and `test_messages.py:73-85` -- `submission_for` copies differ (`None` stripping). Leave them unless they are merged with that difference kept.
- Oversized functions: `cli.py:280-339` `run_hook`, `:152-208` `cmd_submit`, `:352-398` `run`; `workflow.py:434-496` `on_submit`, `:263-311` `CursorState.from_dict`; `adapters/records.py:67-136` `append_record`; `adapters/extensions.py:108-162` `load`

## Tasks & Acceptance

**Execution:**
- [x] `archshell-core` -- run the full suite and import-linter, and record pass counts as the baseline -- proof the sweep changes nothing
- [x] `archshell/engine/cli.py`, `archshell/domain/workflow.py`, `archshell/domain/messages.py` -- add a `_fail_closed` helper, a `workflow.unknown_feature` factory used by `require_flow`, `workflow.error_entries`, and a `_PROBE_TASK` constant; compute the placeholder once; import the `Message` alias from `actions` and rename the `Text` import; single return in `cmd_submit`; trim the stale comments -- one source per rule
- [x] `archshell/adapters/files.py`, `records.py`, `layout.py`, `state_store.py` -- add a `_write_synced` helper in `files` used by `records`, a shared `format_errors`, `_require_slug`, the fingerprint computed once, and a private `_replace` in `state_store`; fix the docstring -- one source per rule, with bytes unchanged
- [x] `README.md` -- line 236 scoped to `guard-write`
- [x] `archshell-core/tests/conftest.py` + the four test files -- shared `ENGINE`, `run_engine`, `single_json_line`, a `Project` base, and `tree` -- one test harness
- [x] `archshell/engine/cli.py`, `archshell/domain/workflow.py` -- split `run_hook` (evaluate-or-error, host render), `cmd_submit` (`_load_config_or_refuse` with the jsonschema import kept lazy, logging tail), and `on_submit` (`_check_human`, the `invalid_output` refusal) -- readable engine paths for later epics

**Acceptance Criteria:**
- Given the baseline, when the sweep is done, then `uv run python -m pytest` passes with a test count no lower than the baseline, and import-linter passes.
- Given the golden manifest, when CI runs on Windows, Linux, and macOS, then every hash matches unchanged.
- Given a scratch copy of the baseline and the change, when the same `next`/`submit`/`guard-write`/`check-edit`/`on-stop` inputs run against each, including refusals, then stdout, exit codes, and written files are byte-identical.

## Implementation Notes

- Baseline (79f0191): 648 passed, 2 skipped; import-linter 4 kept. After the sweep: identical counts.
- `_load_config_or_refuse` keeps the `load_config` import lazy; `output_schemas` stays a lazy import in `cmd_submit`.
- `files.format_errors` is the shared `; `-joined error text (used by `records.InvalidRecord` and `layout.InvalidConfig`); `_write_synced(fd, data)` is used by `files.write_text` and both record writers. State/prompt writes go through the private `state_store._replace`, never `files`.
- `run_hook` split into `_hook_error`, `_evaluate_payload`, `_fail_closed`, `_render_for_host`; `on_submit` into `_check_human` and `_invalid_output`.
- Tests import shared helpers with `from conftest import ...` (`ENGINE`, `engine_process`, `run_engine(single_line=...)`, `single_json_line`, `tree`, `Project`); `test_layout` keeps `single_line=False`. `submission_for` copies left as they are.
- Differential check: 90 scenario steps (full probe and probe-report flows in en/es, retries, every refusal code, invalid config, corrupt state, lock held, all three hooks with and without `--host`, failing renderer and failing hook evaluator, plus in-process `append_record`/`read_records`/`load_config` errors) with deterministic ids against a `git archive 79f0191` copy and the change: stdout, stderr, exit codes and written files byte-identical.

## Plan Change Log

## Review Triage Log

Pass 1 (quick): high 0, medium 0, low 2, false 0, maybe-false 0.

| # | Finding | Verdict | Route | Evidence / action |
|---|---------|---------|-------|-------------------|
| 1 | `_load_config_or_refuse` docstring says the local import avoids loading jsonschema | low | patch | Confirmed: `adapters/layout.py` imports only `yaml_read`, `files`, `domain.layout`, `yaml_emit`; jsonschema is only in `output_schemas.py:17`. The copied comment is false and duplicates `cli.py:192`. Patched: docstring no longer mentions jsonschema. |
| 2 | `_render_for_host` docstring and `noqa` say a broken renderer always blocks | low | patch | Confirmed: with `fail_closed` false (`check-edit`, `on-stop`) it returns `EXIT_OK` with no output, which is the same overstatement README:236 fixes. Patched: both texts are now scoped to `guard-write`. |

After patches: `uv run python -m pytest` 648 passed, 2 skipped; import-linter 4 kept, 0 broken; golden diff empty.

## Verification

**Commands:**
- `uv run python -m pytest` (repo root) -- expected: all pass, count ≥ baseline
- `uv run python -c "import sys; from importlinter.cli import lint_imports; sys.exit(lint_imports(no_cache=True))"` -- expected: all contracts kept
- `git diff --stat 79f0191 -- archshell-core/tests/golden` -- expected: empty
