---
title: 'Action protocol v1 with revision compare-and-swap'
type: 'feature'
ticket: '2'
created: '2026-11-13'
status: 'built'
baseline_revision: 'e5cfa2f9a686a32719fd1de4d72e086ddb2d0a9d'
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

**Problem:** The tracer engine only answers a fixed `done`; there is no closed action set, no state, and nothing stops a host from submitting a stale or forged result.

**Approach:** Implement the closed `archshell/action/v1` set (`llm_task`, `ask`, `confirm`, `instruct_agent`, `report`, `done`) with `action_id`, `revision`, and `nonce`, an engine `next`/`submit` loop over per-feature cursor state written with revision compare-and-swap under a lock, and refusal of stale, mismatched, or reused submissions with `archshell/error/v1`.

## Boundaries & Constraints

**Always:** Engine stays stateless between invocations: every command reads state from disk and returns the next action (AD-1). Messages are JSON over stdin/stdout, one per invocation; `--feature <slug>` is the only argument besides the command (AD-2, AD-20). Every protocol message carries `schema: archshell/<kind>/v1` (AD-11), replacing the tracer's `protocol` key. Cursor state lives at `<project-root>/.archshell/state/<slug>.yaml`, lock at `.archshell/locks/<slug>.lock`; project root is the working directory. `submit` never carries a verdict (AD-18). Tests ship with the code.

**Never:** No model-output schema validation or retries (story 1.5), no hooks (1.4), no host-side loop (1.3), no YAML emitter or full `.archshell/` layout (1.6, 1.7), no records (1.8), no real feature workflow.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First `next` | `next --feature probe`, no state | first probe action with `action_id`, `revision` 1; state written | — |
| Repeat `next` | pending action exists | the same action again, revision unchanged | — |
| Good `submit` | echoes current `action_id`, `revision` (+ `nonce`, `source: human_ui` for ask/confirm) | next action, revision +1 | — |
| Stale revision | `submit` with an older `revision` | refused | `stale_action`, exit 2, state unchanged |
| Wrong action | `submit` with an unknown `action_id` | refused | `stale_action`, state unchanged |
| Nonce mismatch | ask/confirm `submit` with a different or missing `nonce` | refused | `nonce_mismatch`, state unchanged |
| Nonce reuse | resubmitting a consumed ask/confirm with its old nonce | refused | `stale_action`, state unchanged |
| Not from the UI | ask/confirm `submit` without `source: human_ui` | refused | `invalid_source`, state unchanged |
| Nothing pending | `submit` when the flow ended or never started | refused | `no_pending_action` |
| Concurrent write | lock file held | refused | `locked`, state unchanged |
| Bad slug | `--feature Not_A_Slug` or missing | refused | `invalid_feature` |

</frozen-after-approval>

## Code Map

Baseline `e5cfa2f` on `feat/1-1-tracer`; this work is on `feat/1-2-action-protocol`, stacked on it.

- `archshell-core/archshell/engine/cli.py` -- parse/dispatch/emit entry from 1.1; keep `ProtocolError`, `emit`, `log`, exit codes, and the no-traceback catch-all; add `submit`, `--feature`, and the `schema` key
- `archshell-core/tests/test_engine_tracer.py` -- update to the `schema` key; keep its matrix coverage
- `archshell-core/archshell/domain/` -- pure: action model, probe flow, state transitions (no IO, no clock, no randomness: AD-4)
- `archshell-core/archshell/adapters/` -- IO: state file read/write with lock and compare-and-swap, nonce/action-id generation

## Tasks & Acceptance

**Execution:**
- [x] `archshell-core/archshell/domain/actions.py` -- the six action types as immutable data with to-message serialization; common fields `schema`, `type`, `feature`, `action_id`, `revision`; ask/confirm add `nonce`
- [x] `archshell-core/archshell/domain/workflow.py` -- pure transition: (state, submission, fresh ids) → new state + next action or typed refusal; the probe flows below
- [x] `archshell-core/archshell/adapters/state_store.py` -- read state, write with lock file (exclusive create) and revision compare-and-swap; state serialized as sorted-key JSON in the `.yaml` file
- [x] `archshell-core/archshell/engine/cli.py` -- `next` and `submit` commands wired to domain and store; `schema` key on all messages
- [x] `archshell-core/tests/test_protocol.py` -- one subprocess test per matrix row, each asserting stdout message, exit code, and state file bytes (unchanged on refusal)
- [x] `archshell-core/tests/test_workflow.py` -- unit tests of the pure transitions
- [x] `.github/workflows/ci.yml`, `archshell-claude-code/commands/ping.md` -- update the smoke step and `/archshell:ping` to `next --feature probe` and the `schema` key; both break otherwise
- [x] `README.md` -- document `next`/`submit`, message fields, and error codes

**Acceptance Criteria:**
- Given the `probe` flow, when a test drives `next`/`submit` to the end, then it receives `llm_task`, `ask`, `confirm`, `instruct_agent`, then `done`, with revision rising by one per accepted submit.
- Given the `probe-report` flow, when driven to the end, then it ends with a `report` action and further `submit` returns `no_pending_action`.
- Given any refused submission, then the state file bytes are identical before and after.

## Implementation Notes

- The feature slug selects the built-in flow (`probe`, `probe-report`); any other valid slug is refused with `unknown_feature`.
- Added codes beyond the matrix: `unknown_feature`, `invalid_arguments` (arguments after `--feature <slug>`), `invalid_state` (unreadable state file). A submit whose `feature` differs from `--feature` is `invalid_feature`.
- Every command takes the lock, including a repeat `next`; a lock left by a killed process blocks the feature until deleted by hand (documented in README).
- Check order on submit: no state or terminal → `no_pending_action`; `action_id`/`revision` → `stale_action`; for ask/confirm, `nonce` → `nonce_mismatch`, then `source` → `invalid_source`.
- State file is `archshell/state/v1`: `feature`, `flow`, `step`, `revision`, and the pending action message (including its nonce). A CAS mismatch in the adapter maps to `stale_action`.
- Added `archshell-core/tests/test_state_store.py` (adapter CAS and lock) and `.archshell/` to the repo `.gitignore` (the CI smoke and verification command write state at the repo root).

## Plan Change Log

## Review Triage Log

Pass 1 (quick): high 0, medium 1, low 5, false 1, maybe-false 0.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | `submit` accepts unknown top-level fields, including `verdict` | medium | patch | Reviewer reproduced: `"verdict":"pass"` advanced the flow; violates AD-18 in Always |
| 2 | `submit` for a slug with no flow returns `no_pending_action`, not `unknown_feature` | low | patch | Only `on_next` consults `FLOWS`; inconsistent with `next` |
| 3 | `invalid_message`/`invalid_feature` checked before `no_pending_action` | false | — | The plan's order covers state checks only; malformed input refused first is correct |
| 4 | Lock `unlink()` raises if the lock was deleted by hand mid-run | low | patch | `finally` unlink after a committed write turns success into `internal_error`; `missing_ok=True` |
| 5 | Refused commands create `.archshell/locks/` | low | patch | Lock taken before feature validation; reviewer reproduced |
| 6 | State file whose `flow` differs from `feature` is accepted | low | reject | State is engine-written only; unlikely, and the fix adds a guard |
| 7 | "Wrong action" row tested only on `llm_task` | low | patch | No subprocess test for ask/confirm with a wrong `action_id` |

## Design Notes

`report` is terminal, like `done` (AD-2: the loop runs "until `done` or `report`"). Two built-in diagnostic flows exercise the set until real workflows exist: `probe` = llm_task → ask → confirm → instruct_agent → done; `probe-report` = llm_task → report. Story 1.3 drives them from Claude Code.

Randomness and ids stay out of the domain: the adapter generates `action_id` (uuid4) and `nonce` (`secrets.token_urlsafe`) and passes them in. Consumed nonces need not be stored: once an ask/confirm is accepted its `action_id` is no longer pending, so a replay is `stale_action`.

Example submit for an `ask`:

```json
{"schema":"archshell/submit/v1","feature":"probe","action_id":"…","revision":2,"nonce":"…","source":"human_ui","result":{"answer":"yes"}}
```

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass (module form because of local Application Control)
- `echo '{}' | uv run python -m archshell.engine next --feature probe` -- expected: an `llm_task` action, revision 1, exit 0

**Manual checks (if no CLI):**
- CI on the pushed branch: three green jobs (needs the user's push approval).
