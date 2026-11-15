---
title: 'Confirmation-record writer and schema'
type: 'feature'
ticket: '8'
created: '2026-11-14'
status: 'built'
baseline_revision: 'bccf33d67189ad59aff3574380d91f5c72f59f47'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every human confirmation must leave a structured, append-only confirmation record committed with the change it approves (FR-53, AD-10), and epics 2 and 3 already need to write them, but the core has no record format or writer (epic E1).

**Approach:** A core schema `archshell/record/v1` and a writer that appends `.archshell/records/<feature-slug>/<seq>-<flow>.yaml` with the next sequence number, never touching an existing file, plus a reader that loads and validates a feature's records in order.

## Boundaries & Constraints

**Always:**
- Path: `.archshell/records/<feature-slug>/<seq>-<flow>.yaml`, `seq` zero-padded to 4 digits per feature starting at `0001`; `flow` from the closed vocabulary `plan`, `extension`, `contract`, `contract-revision`, `breaking-change`, `growth`, `vocabulary`; the slug follows the existing slug rule, and `bootstrap` is a valid slug.
- A record names `schema`, `feature`, `seq`, `flow`, `fingerprint` (`sha256:<64 hex>` of the confirmed content), `confirmed_by` (self-reported, AD-18), `confirmed_at` (UTC, `YYYY-MM-DDTHH:MM:SSZ`), and `derived` (true for FR-59's *derived, no new decisions*); `content` (the confirmed content) is optional.
- Append-only: the writer creates the new file exclusively and fails rather than replace an existing path; two writers racing for one `seq` both succeed with different `seq` values, and no existing record's bytes ever change.
- Records are validated against `archshell-core/schemas/record-v1.json` before writing and when read, written with the canonical emitter and normalized bytes, read with `yaml_read`.
- Paths come from the layout module.

**Never:**
- No blocking or verification of record edits (epic-gates-verify-change-control); no CODEOWNERS; no confirmation flow or engine command that calls the writer.
- No change to the protected set: agents still cannot write under `.archshell/`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First record | no records for `cancel-appointment`, flow `plan` | `records/cancel-appointment/0001-plan.yaml`, validates | No error expected |
| Same flow twice | second `plan` write | `0002-plan.yaml` added; `0001-plan.yaml` bytes unchanged; both validate | No error expected |
| Mixed flows | `plan` then `contract` | `0001-plan.yaml`, `0002-contract.yaml` | No error expected |
| Race | two writers for one feature at once | two files with distinct `seq` | No error expected |
| Bad input | unknown flow, invalid slug, bad fingerprint, or `confirmed_by` empty | nothing written | typed record error with schema errors |
| Read in order | records `0001`..`0003` | returned in `seq` order, each validated | — |
| Corrupt record on read | a record file that fails the schema or names a different seq/flow than its filename | — | typed record error naming the file |

</frozen-after-approval>

## Code Map

Baseline `bccf33d` on `main` (after PR #7); work on `feat/1-8-confirmation-records` in `C:\git\arch-shell`.

- `archshell-core/archshell/domain/layout.py` -- `ROOT`, `DIRECTORIES` (has `records`), `state_file`/`lock_file` helpers; add the records path helper and the flow vocabulary beside them
- `archshell-core/archshell/domain/workflow.py:60` `is_valid_slug`, `SLUG_PATTERN` -- reuse for the record slug; do not duplicate the rule
- `archshell-core/archshell/render/yaml_emit.py` `emit`, `Ordered` (`:30`, keeps caller key order; no floats) -- write records with it
- `archshell-core/archshell/render/normalize.py` `to_bytes`; `archshell/adapters/files.py` `path_of`, `write_text` (`:31-52`, mkstemp + `os.replace`, which overwrites, so do not use it for records)
- `archshell-core/archshell/adapters/yaml_read.py` `load`, `YamlReadError`; `archshell/adapters/output_schemas.py` `validate`, lazy import pattern from `adapters/layout.py` `load_config`
- `archshell-core/tests/test_layout.py` -- `tmp_path` fixture style to follow; the layout test lists every engine path and must still pass

## Tasks & Acceptance

**Execution:**
- [x] `archshell-core/schemas/record-v1.json` -- the record schema per the Always list, no extra properties
- [x] `archshell-core/archshell/domain/layout.py`, `archshell-core/archshell/domain/records.py` -- pure: flow vocabulary, record path and filename parse, seq formatting, fingerprint of content (sha256 of the canonical emit), record document builder
- [x] `archshell-core/archshell/adapters/records.py` -- `append_record(root, ...)` with exclusive create, retry on a taken `seq`, fsync; `read_records(root, feature)` validating each file and its filename match
- [x] `archshell-core/tests/test_records.py` -- one test per matrix row; the race row with real concurrent writers
- [x] `README.md` -- record path, fields, and append-only rule

**Acceptance Criteria:**
- Given a fixture project, when a `plan` record is written twice for the same feature, then two files exist, the first file's bytes are identical to before the second write, and both pass `read_records`.

## Implementation Notes

- Seq uniqueness across flows: the hard link alone only refuses an identical `<seq>-<flow>` name, so two racers could both take `0002` with different flows. The writer first claims the seq by `O_CREAT | O_EXCL` on `.archshell/locks/records/<feature>/<seq>.claim` (gitignored under `locks/`), re-checks that no record file uses that seq, then links; the claim is removed after the write. A writer killed while holding a claim leaves a gap, never a duplicate. Helpers: `layout.RECORDS_DIR`, `layout.record_claims_dir`, `records.claim_path`.
- `confirmed_at` is also checked as a real UTC time (`2026-13-45T…Z` fails) after the schema; `confirmed_by` must contain a non-whitespace character. A given `fingerprint` must equal the fingerprint of `content` when both are passed; `content=None` means absent.
- Verified: `uv run python -m pytest` 528 passed, 2 skipped; a scratch probe of 8 concurrent writers (alternating `plan`/`contract`) showed real claim contention (up to 7 skipped seqs) and yielded `0001`..`0008`.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 2, false 0, maybe-false 0.

| # | Finding | Verdict | Route | Evidence / action |
|---|---------|---------|-------|-------------------|
| 1 | `append_record` accepts a slug with a trailing newline | medium | patch | Schema `pattern` uses `re.search`, `$` matches before `
`; reviewer probe crashed with a raw `OSError` on Windows; POSIX writes into `abc
/`. Breaks the "bad input" row and the reuse-`is_valid_slug` rule. Patch: check with `is_valid_slug` first. |
| 2 | Fingerprint with a trailing newline is written and read back as valid | medium | patch | Same `$` behavior; probe wrote and read `0001-plan.yaml`. Patch: full-match check in `semantic_errors` on write and read. |
| 3 | `confirmed_by` of Unicode whitespace passes | low | patch | Pattern lists four ASCII whitespace characters; probe wrote `" "`. Patch: refuse when `strip()` is empty. |
| 4 | Interrupted writer leaves a temp file in the committed records dir | low | patch | `mkstemp` in `records/<feature>/`, not gitignored; crash before `finally` leaves it for `git add`. Patch: temp in the gitignored claims dir. |

## Design Notes

Exclusive create: write the normalized bytes to a temp file in the feature directory, fsync, then `os.link(tmp, target)` (fails if the target exists, never replaces) and unlink the temp; on `FileExistsError` pick the next `seq` and retry. This keeps a record either fully present or absent. If hard links are unavailable on the filesystem, fall back to `os.open(target, O_CREAT | O_EXCL)` and write in place.

Fingerprint example: `sha256:` + hex digest of `to_bytes(emit(content))`, so the same content gives the same fingerprint on every OS.

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass
