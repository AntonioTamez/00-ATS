---
title: 'English and Spanish message catalogs'
type: 'feature'
ticket: '9'
created: '2026-11-14'
status: 'built'
baseline_revision: '667bb419559931304ca0be4f25de2a1577a85d7a'
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

**Problem:** Every question, option, summary and refusal message the engine shows the developer is hard-coded English, so a developer who writes in Spanish gets English questions and reports (CAP-14, FR-39). Files the engine writes must stay English (FR-12).

**Approach:** The domain names user-facing text by message key plus arguments, never prose. Per-language catalogs (`en`, `es`) map each key to a template, and the engine renders them in the conversation language. The host passes that language on every `next`/`submit` as `--lang en|es`.

## Boundaries & Constraints

**Always:**
- `--lang` is optional on `next` and `submit` and defaults to `en`. Any other value is refused with `invalid_arguments`, rendered in English.
- Catalogs are YAML files with the same key set and the same `{placeholder}` names in every language. A test enforces this parity, so a missing key fails the build, not a run.
- Rendering happens when an action is built. A repeated `next` returns the stored pending action unchanged (same `action_id` and nonce). A language switch takes effect from the next action on.
- Catalog loading lives in an adapter. The domain receives a renderer (or key + args) and stays pure.
- Message codes, `details`, action fields, and the stdout one-message protocol stay unchanged. Only the prose values change language.
- Never localized: the `llm_task` prompt file and its retry text, the state file, records, config, logs, and every other written file.
- `run.md` tells the host to pass `--lang es` when the developer writes in Spanish and `--lang en` otherwise. It still forbids translating engine text.
- Decision (2026-11-14): every `message` the developer sees from `next`/`submit` is localized, flow texts and every refusal/error message alike (`Refusal`, `ProtocolError` become key + args); an error raised before `--lang` is parsed renders in English.

**Never:**
- No languages beyond `en`/`es`, no locale detection inside the engine, no `config.yaml` language field.
- No pack or analyzer catalogs yet (no pack emits messages in this epic).
- No change to hook verdict reasons: hooks get no conversation language from the host.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Spanish ask | `next --feature probe --lang es`, then submit to the `ask` step | `ask` question, header and options in Spanish | No error expected |
| Default | no `--lang` | English text, byte-identical to today's probe actions | No error expected |
| Spanish report | `probe-report` driven with `--lang es` | `report` summary in Spanish | No error expected |
| Files stay English | Spanish run | prompt file, state file and log in English | No error expected |
| Repeat next | pending action rendered `en`, then `next --lang es` | same stored action, unchanged | No error expected |
| Bad lang | `--lang fr` or `--lang` with no value | — | `invalid_arguments`, English |
| Parity | a key missing from `es.yaml`, or a placeholder renamed | — | the catalog test fails |

</frozen-after-approval>

## Code Map

Baseline `667bb41` on `main` (after PR #8). The work is on `feat/1-9-message-catalogs` in `C:\git\arch-shell`.

- `archshell-core/archshell/domain/workflow.py`
  - `Step` `:77-84` (`text`, `header`, `options`) and `FLOWS` `:91-121`: 11 strings. These become catalog keys.
  - `build_action` `:149-176` is the only place step text enters an action.
  - `retry_prompt` `:136-139` stays English (prompt file).
  - `Refusal(code, message, details)` `:300-303` and about 15 messages at `:306`, `:319-346`, `:358`, `:386-411`.
  - `STATE_FIELDS`, with an exact-keys check at `:238`. No state change is needed: the stored action already holds the rendered text.
- `archshell-core/archshell/engine/cli.py`
  - `parse_feature` `:72-79` accepts exactly `--feature`. Extend it to an optional `--lang` for `next`/`submit` only.
  - `ProtocolError` `:39-52` and `refuse()` `:68`.
  - About 15 cli-built messages: `:61-134` and `run()` `:322-346`.
  - Single emit points: `:309`, `:351`, `:357`.
  - Hook paths `:181-299` are unchanged.
- `archshell-core/archshell/adapters/output_schemas.py:21-46` -- the pattern to copy: a directory resolved from the package, a `@cache`d loader, and a typed unknown-id error.
- `archshell-core/archshell/adapters/yaml_read.py` `read`/`load` -- read catalogs with these.
- `archshell-core/pyproject.toml:19-22` `module-name = ["archshell","schemas"]` -- put catalogs under `archshell/catalogs/` (package data), not a new top-level dir. A top-level dir would add a second collision risk on top of the deferred `schemas` one.
- `archshell-claude-code/commands/run.md:7,16,22` -- the "never translate" rule and the two command templates. `allowed-tools` `next:*`/`submit:*` already matches the extra flag.
- Tests that assert exact English and must still pass with the `en` default:
  - `test_workflow.py:177-179,402,453`
  - `test_protocol.py:327,414`
  - `test_layout.py:210`
  - `test_plugin_files.py:59-72` (the `{ENGINE_EXPANSION} next --feature` substring must survive)
  - `tests/golden/`

## Tasks & Acceptance

**Execution:**
- [x] `archshell-core/archshell/catalogs/en.yaml`, `es.yaml` -- every flow string and every refusal/error message as `key: template`. The `en` text equals today's strings.
- [x] `archshell-core/archshell/domain/messages.py` (new) -- pure: `LANGUAGES = ("en","es")`, `DEFAULT_LANG`, a `Message(key, args)` type, and a `render(catalog, message)` that formats placeholders.
- [x] `archshell-core/archshell/adapters/catalogs.py` (new) -- `load(lang)`: cached, reads the YAML, raises a typed error for an unknown language or a malformed file.
- [x] `workflow.py`, `cli.py` -- steps and refusals carry message keys plus args. `build_action` and `refuse()` render through the catalog for the request's `lang`. `--lang` is parsed and validated.
- [x] `archshell-claude-code/commands/run.md` -- language rule and `--lang <en|es>` in both templates. Update `test_plugin_files.py` if wording checks need it.
- [x] `archshell-core/tests/test_messages.py` (new) -- one test per matrix row, catalog parity (keys + placeholders), and a check that every key used in code exists.
- [x] `README.md` -- `--lang`, the catalogs, and what stays English.

**Acceptance Criteria:**
- Given the module-form engine, when `probe` is driven with `--lang es`, then every `ask`/`confirm`/`instruct_agent`/`done` text is Spanish, and the files under `.archshell/` contain no Spanish catalog text.
- Given `uv run python -m pytest`, then all pass on the three CI OSes.

## Implementation Notes

- State change (deviates from the Code Map's "no state change is needed"): the frozen matrix and the AC require the state file to stay English in a Spanish run, while a repeated `next` must return the action as first issued. So `CursorState.action` is stored rendered in English and the state gains `lang` (the language the action was issued in); `CursorState.issued(render)` re-renders the step's text fields in that language. A state file without `lang` reads as `en`; one with an unknown `lang` is `invalid_state`. An older engine would reject a new state file (exact-keys check), so a downgrade needs `.archshell/state/` deleted.
- The `llm_task` prompt stays inline English in `Step.text` (not a catalog key): it only reaches the prompt file, which is never localized. Every other step's `text`/`header`/`options` are catalog keys.
- `Refusal.message` and `ProtocolError.message` are `Message(key, args)`; rendering happens at emit. Hook errors render in English. If a catalog is broken, `ProtocolError.text()` falls back to the bare key so guard-write still fails closed.
- `--lang` must directly follow `--feature <slug>`; the slug is validated after `--lang` is parsed, so `invalid_feature` for a bad slug is localized; `unknown_command` and a missing `--feature` are English.
- Domain API: `on_next(..., *, render, lang=DEFAULT_LANG)`, `on_submit(..., render, lang=...)`; `render` is `adapters.catalogs.render`. Test helpers in `test_workflow.py`/`test_state_store.py` pass it; `test_workflow.py` header-length and `invalid_output` message checks now render through the `en` catalog (same English asserted).

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens): high 0, medium 0, low 5, false 0, maybe-false 0.

| # | Finding | Verdict | Route | Evidence / action |
|---|---------|---------|-------|-------------------|
| 1 | Repeated `next` re-renders a Spanish pending action from the current catalog, so an engine upgrade mid-action can change its text | low | reject | Real only across an engine upgrade while an action is pending; `action_id`/nonce/revision unchanged. Storing the rendered Spanish text would break "the state file is never localized"; the fix is not a direct correction. |
| 2 | `unexpected arguments` after a valid `--lang es` renders in English | low | patch | `parse_arguments` raises before `lang` reaches `run()`; contradicts the decision (only errors before `--lang` is parsed stay English). Patch: render in the parsed lang; fix test and README. |
| 3 | `error.stdin_not_json` / `error.state_conflict` embed English exception text in Spanish messages | low | reject | Exception text is a diagnostic tail of rare malformed-input/concurrency errors; moving it into `details` changes `details`, which the plan freezes. |
| 4 | `from_dict` legacy check loosened: `lang` without `attempts`/`errors` accepted | low | patch | Shape never written by any engine version; old exact-set check refused it. Patch: decide on the original key set; one refusal test. |
| 5 | Catalog header claims every key is shown, but pre-`--lang` and hook keys never render in Spanish | low | patch | Keys exist for parity (plan Always rule); comment inaccurate. Patch: reword comment. |

## Design Notes

Why the host passes `--lang`: only the host sees the developer's words. The engine is stateless per call, and run.md already forbids the host from translating. So the engine has to receive the language and render the text itself.

Catalog shape (Spanish accents are normalized to NFC on read):

```yaml
schema: archshell/catalog/v1
lang: es
messages:
  flow.probe.ask.question: "¿Continuar con la prueba?"
  error.stale_action: "La acción {action_id} ya no está pendiente."
```

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass
- Module form: `next --feature probe --lang es` -- expected: Spanish `ask` after the `llm_task`
