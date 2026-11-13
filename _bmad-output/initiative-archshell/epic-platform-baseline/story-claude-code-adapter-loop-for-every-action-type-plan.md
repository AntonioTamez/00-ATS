---
title: 'Claude Code adapter loop for every action type'
type: 'feature'
ticket: '3'
created: '2026-11-13'
status: 'built'
baseline_revision: '8d655e7a2c19e3a58e5b5541cb650e507258a3b4'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/initiative-archshell/architecture-archshell/architecture-archshell.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The engine can issue all six actions, but nothing in Claude Code executes them, so the protocol loop has never run inside the host.

**Approach:** Give the plugin a `/archshell:run <feature>` command that drives `next` → execute → `submit` until `done` or `report`, executing each action the one way AD-2, AD-3, and AD-18 allow: `ask`/`confirm` through Claude Code's native question tool with the issued nonce and `source: human_ui`, `llm_task` in a named plugin agent that reads the engine's prompt from a file, `instruct_agent` by the session agent, and `report`/`done` shown verbatim. The engine writes each `llm_task` prompt to a file so the host never carries or paraphrases prompt text.

## Boundaries & Constraints

**Always:** The plugin holds no workflow logic: it never derives, skips, or reorders actions, and all user-facing text comes from the engine (AD-1, AD-2). An unknown action type stops the loop with a visible error. The engine command is `archshell-engine`, overridable by the `ARCHSHELL_ENGINE` environment variable for machines where the launcher cannot run. Each `llm_task` runs in a fresh named agent with no shared history (AD-3). Submit messages are passed on stdin, never as arguments (AD-20). Tests ship with the code.

**Never:** No hooks (1.4), no result schema validation or retries (1.5), no message catalogs (1.9), no consensus batches, no host renderer in the core (plugin files stay static in this story).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| `llm_task` | engine issues it | action carries `prompt_file` (a path under `.archshell/state/prompts/`), not prompt text; the file holds the prompt exactly | — |
| Prompt file lifetime | the `llm_task` is accepted, or a new `next` repeats it | file still present while pending; repeat `next` returns the same `prompt_file` | — |
| Full probe in Claude Code | `/archshell:run probe` (hitl) | user sees an agent task run, a question, a confirmation, an instruction carried out, then the `done` summary | — |
| Report flow | `/archshell:run probe-report` (hitl) | loop stops at the `report` summary | — |
| Engine refusal | any `archshell/error/v1` reply | loop stops and shows the error code and message | no retry |
| Override | `ARCHSHELL_ENGINE` set | every engine call uses it | — |
| Question text | `ask` or `confirm` issued | action carries engine-written `header` (≤ 12 chars) and `options` (2–4 labels); the host shows exactly these, plus free text for `ask` | an action without them is refused as invalid by `action_from_message` |

**Decision (user, 2026-11-13):** `ask` and `confirm` carry engine-written `options` and `header`; the host never invents question text or status lines.

</frozen-after-approval>

## Code Map

Baseline `8d655e7` on `feat/1-2-action-protocol` (PR #2, not yet merged); this work is on `feat/1-3-claude-code-loop`, stacked on it.

- `archshell-core/archshell/domain/actions.py` -- `LlmTask` has `prompt: str`; becomes `prompt_file: str` (POSIX path relative to project root); `action_from_message` keeps its exact-fields check
- `archshell-core/archshell/domain/workflow.py` -- `build_action` makes `LlmTask` from the step text; keep the domain pure: it computes the path from `action_id`, the engine writes the file
- `archshell-core/archshell/engine/cli.py` -- write the prompt file before emitting an `llm_task`, under the same lock; reuse `ProtocolError`, `emit`, `log`
- `archshell-core/archshell/adapters/state_store.py` -- reuse its `.archshell/` root resolution; prompt files live beside state
- `archshell-claude-code/commands/ping.md` -- existing; keep, switch to the `ARCHSHELL_ENGINE` override
- `.github/workflows/ci.yml` -- smoke step reads `type` only; keep it passing

## Tasks & Acceptance

**Execution:**
- [x] `archshell-core/archshell/domain/actions.py`, `domain/workflow.py` -- `ask`/`confirm` gain `header: str` and `options: tuple[str, ...]` (2–4, each non-empty), validated in `action_from_message`; probe steps supply them
- [x] `archshell-core/archshell/domain/actions.py`, `domain/workflow.py`, `engine/cli.py` -- `llm_task` carries `prompt_file`; the engine writes the prompt there (LF, UTF-8 without BOM) before replying
- [x] `archshell-core/tests/` -- update protocol tests to `prompt_file`; add tests for file content, path, and repeat-`next` stability
- [x] `archshell-claude-code/commands/run.md` -- the loop: one section per action type stating exactly what to execute and what result to submit
- [x] `archshell-claude-code/agents/archshell-task.md` -- named agent that reads only the given prompt file and returns its answer; `run.md` passes it the absolute path (project root joined with `prompt_file`)
- [x] `archshell-claude-code/commands/ping.md` -- use the override
- [x] `archshell-core/tests/test_plugin_files.py` -- structural checks: frontmatter parses, `run.md` covers all six action types and the override, the agent exists and is referenced by its plugin-qualified name
- [x] `README.md` -- how to run `/archshell:run probe`, result shapes per action, and the override (with the module form for this machine)

**Acceptance Criteria:**
- Given the plugin installed (hitl), when the user runs `/archshell:run probe`, then all five probe actions execute in order and the `done` summary is shown.
- Given `/archshell:run probe-report` (hitl), then the loop ends at the `report` summary.
- Given an `ask` or `confirm`, then the submitted result echoes the action's `nonce` and carries `source: human_ui`, and it was answered through the native question tool.

## Implementation Notes

- Built on the `git stash` "story-1.3 attempt 1" (applied, not dropped), keeping every KEEP item from Loop 1.
- `Ask`/`Confirm` gain `header` and `options`; `to_message` emits `options` as a JSON list, `action_from_message` rebuilds a tuple and refuses a header outside 1-12 characters or options outside 2-4 non-empty strings. Probe: ask `Probe` / `Yes`, `No`; confirm `Probe` / `Continue`, `Stop`.
- `confirm` mapping is a protocol rule, not host logic: the first option confirms, any other declines; typed text or a dismissed question stops the loop.
- `run.md` passes each option as both `label` and `description` (AskUserQuestion requires a description; reusing the engine label keeps all text engine-written). The only host-written strings left are the error stops (usage, unexpected reply, unsupported schema/type).
- Verified: `uv run python -m pytest` 202 passed; the module-form `next --feature probe` returns an `llm_task` whose `prompt_file` exists and holds the prompt (no BOM, LF). Manual hitl runs of `/archshell:run probe` and `probe-report` are still pending (user).

## Plan Change Log

- Loop 1 (review pass 1, intent_gap): `run.md` invented `Yes`/`No` options and an `ArchShell` header because `ask` carried no options. Amended: frozen Decision plus matrix row; `ask`/`confirm` carry engine `header` and `options`; host status lines removed; agent receives an absolute prompt path. Avoids host-authored user text (AD-2) and an unreadable relative path. KEEP from attempt 1 (in `git stash` "story-1.3 attempt 1"): `prompt_file` design and `ensure_prompt`, tampered-state path refusal, quoted-heredoc submits, `ARCHSHELL_ENGINE` expansion, `ARCHSHELL_PROMPT_UNREADABLE` stop, dismissed-question rule, README sections; fix the README wording ("missing or differs"), use `"﻿"` instead of a literal BOM, and pin nonce/`human_ui` assertions to the ask/confirm sections including `confirmed: false`.

## Review Triage Log

Pass 1 (quick): high 0, medium 2, low 7, false 0, maybe-false 0.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | `run.md` offers host-invented `Yes`/`No` for `ask`; `Ask` carries no options | medium | intent_gap | `AskUserQuestion` needs 2–4 options; the frozen intent requires all user-facing text from the engine; the plan does not say where options come from |
| 2 | Host-authored status line and `ArchShell` question header | low | intent_gap (with 1) | Same root cause: the protocol has no engine field for this text |
| 3 | `llm_task` agent gets a relative prompt path; Read needs an absolute one | medium | patch | `run.md` passes `.archshell/state/prompts/<id>.md` unchanged |
| 4 | README says the prompt is rewritten only when missing | low | patch | `ensure_prompt` also rewrites when bytes differ |
| 5 | Story 1.2 state files with `prompt` become `invalid_state` | low | reject | Unreleased format; only local diagnostic state is affected |
| 6 | Prompt files are never deleted | low | defer | Unbounded only across reruns of diagnostic flows; needs a cleanup rule for real workflows |
| 7 | Prompt file orphaned if `write` raises `RevisionConflict` | low | reject | Cannot happen while the lock is held; the fix adds cleanup branches |
| 8 | Literal U+FEFF in `test_plugin_files.py` | low | patch | Invisible character; `"\ufeff"` is a direct correction |
| 9 | Plugin-file test does not pin nonce/source to the ask/confirm sections, nor `confirmed: false` | low | patch | Assertion searches the whole file |

Pass 2 (quick, after loop 1): high 0, medium 1, low 4, false 1, maybe-false 0. Pass-1 rows 3, 4, 8, 9 resolved in the re-derivation; rows 1–2 resolved by the user's decision.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 10 | Hitl acceptance criteria not yet shown | false | — | Pending the user's run in Claude Code, not a code defect |
| 11 | Probe confirm option "Stop" does not stop the flow | medium | patch | `on_submit` ignores `result`; the label promises an effect the engine does not apply |
| 12 | Duplicate option labels accepted | low | patch | `AskUserQuestion` returns a label, so "first option confirms" becomes ambiguous |
| 13 | Whitespace-only header or option accepted | low | patch | `len`/truthiness checks pass `" "` |
| 14 | README says the prompt file holds the prompt "exactly" | low | patch | `ensure_prompt` normalizes CR/CRLF to LF |
| 15 | Override row has only a structural test | low | reject | The plan makes it a manual check; an automated host run needs Claude Code |

## Design Notes

Result shapes the command submits: `llm_task` → `{"text": <agent answer>}`; `ask` → `{"answer": <choice or text>}`; `confirm` → `{"confirmed": true|false}`; `instruct_agent` → `{"status": "done"}`. `report`/`done` are terminal: show `summary`, submit nothing.

`source: human_ui` is self-reported by the host, as AD-18 accepts; the conformance suite (story 1.10) is what tests it. On this machine set `ARCHSHELL_ENGINE="uv run --project C:\git\arch-shell python -m archshell.engine"`, because Application Control blocks the uv launcher.

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass
- `echo '{}' | uv run python -m archshell.engine next --feature probe` -- expected: `llm_task` with a `prompt_file` that exists and holds the prompt

**Manual checks (if no CLI):**
- `/archshell:run probe` and `/archshell:run probe-report` in Claude Code with the override set (user runs them).
- CI green on the three OSes after push (needs the user's approval).
