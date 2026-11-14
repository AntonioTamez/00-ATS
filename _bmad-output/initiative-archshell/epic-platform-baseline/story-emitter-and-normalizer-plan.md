---
title: 'Emitter and normalizer'
type: 'feature'
ticket: '6'
created: '2026-11-13'
status: 'built'
baseline_revision: 'b760a8dda474316371a3534015bc4b45cce97608'
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

**Problem:** Every generated file in later epics needs one render path and one byte-level normalization, or output drifts across Windows, macOS, and Linux and NFR-4 fails; the repository itself also has no `.gitattributes`, so Windows checkouts turn files into CRLF.

**Approach:** Add the core's single generation path: Jinja2 with `StrictUndefined` rendering from template text, followed by the core normalizer (LF, UTF-8 without BOM, NFC, exactly one final newline), and a canonical YAML emitter for the restricted subset with a safe reader that rejects anchors, aliases, and tags. Add `.gitattributes` forcing LF. Prove byte identity with a golden fixture set checked on all three OSes in CI.

## Boundaries & Constraints

**Always:** Jinja2 pinned at 3.1.6 (spine Stack); `StrictUndefined`, no autoescape, `keep_trailing_newline`. Rendering and normalizing are pure functions in `archshell/render/` (no IO, clock, randomness, or environment: AD-4); reading template files and writing output are adapter IO. Every written file passes through the normalizer (AD-5). YAML output: block style, keys sorted by codepoint unless the caller fixes the order, only str/int/bool/null/list/mapping, deterministic quoting. The reader uses a safe loader. Tests ship with the code.

**Never:** No `.archshell/` layout or config loading (1.7), no migration of the state store's JSON-in-YAML format (1.7 decides), no templates for real artifacts (epic 2+), no formatter run on generated output, no floats in emitted YAML.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Line endings | text with CRLF, lone CR | LF only | — |
| BOM | text starting with U+FEFF | BOM removed | — |
| Unicode form | decomposed `é` (e + U+0301) | NFC `é` | — |
| Final newline | no newline, or several at the end | exactly one `\n` | — |
| Undefined variable | template uses a name not in the context | refused | typed render error naming the variable |
| YAML emit | nested mapping with unsorted keys, strings needing quotes (`yes`, `1`, `: x`, empty, leading space) | sorted, quoted so they read back as the same strings | — |
| YAML unsupported value | float or arbitrary object | refused | typed emit error |
| YAML read | document with an anchor, alias, or tag | refused | typed read error |
| Round trip | emit then read | equal data | — |
| Cross-OS | golden fixture set rendered in CI | identical sha256 on Windows, Linux, macOS | job fails on any difference |

</frozen-after-approval>

## Code Map

Baseline `b760a8d` (`main` after PR #4); work on `feat/1-6-emitter-normalizer`.

- `archshell-core/archshell/render/` -- holds `hosts/` (story 1.4, pure renderers); add the generation modules beside it
- `archshell-core/archshell/adapters/state_store.py` -- writes JSON with LF and no BOM by hand; leave it unchanged (story 1.7 moves it onto the emitter)
- `archshell-core/pyproject.toml` -- `dependencies = []`; add Jinja2 and a YAML library, pinned in `uv.lock`
- `.github/workflows/ci.yml` -- three-OS matrix; add the golden byte check
- `.gitattributes` -- new

## Tasks & Acceptance

**Execution:**
- [x] `.gitattributes` -- `* text=auto eol=lf`, binaries marked; renormalize the tree in the same commit
- [x] `archshell-core/pyproject.toml`, `uv.lock` -- `jinja2==3.1.6`; PyYAML pinned to its current 6.0.x release (verify on PyPI) for the safe reader
- [x] `archshell-core/archshell/render/normalize.py` -- the normalizer from the matrix
- [x] `archshell-core/archshell/render/templates.py` -- render template text with a context; typed error on undefined names; output normalized
- [x] `archshell-core/archshell/render/yaml_emit.py` -- canonical emitter for the restricted subset
- [x] `archshell-core/archshell/adapters/yaml_read.py`, `adapters/files.py` -- safe reader rejecting anchors/aliases/tags; write helper that normalizes and writes bytes
- [x] `archshell-core/tests/golden/` -- a few templates plus contexts and a committed sha256 manifest; a test renders them and compares
- [x] `archshell-core/tests/test_render.py`, `test_yaml.py` -- one test per matrix row
- [x] `.github/workflows/ci.yml` -- the golden test runs in every OS job (it is part of pytest); CI fails on mismatch
- [x] `README.md` -- the generation path and the rule that every written file goes through it

**Acceptance Criteria:**
- Given the golden fixture set, when CI runs on Windows, Linux, and macOS, then every rendered file matches the committed sha256.
- Given a fresh Windows checkout with `core.autocrlf=true`, then tracked text files have LF in the working tree.

## Implementation Notes

- Commits on `feat/1-6-emitter-normalizer`: `4990a9c` (`.gitattributes`; renormalize was a no-op, the index was already all LF), `1172144` (deps, render, adapters, golden set, tests, README), `2743619` (CI golden step).
- PyYAML pinned at 6.0.3 (PyPI current on 2026-11-13); Jinja2 3.1.6 pulls MarkupSafe 3.0.4 into `uv.lock`.
- Undefined names are checked statically (`jinja2.meta.find_undeclared_variables`) before rendering, so a name missing from the context is refused even in a branch not taken; `RenderError.names` carries them. Runtime undefined attributes are `RenderError` without `names`. No loader: `include`/`import`/`extends` are refused.
- `Ordered` (a `dict` subclass) is how a caller fixes key order; plain mappings are sorted by codepoint. The emitter NFC-composes strings and keys before sorting (keys equal after NFC are refused) and refuses keys over 1000 characters (PyYAML simple-key limit).
- Plain-string rule is a conservative allow-list (`[A-Za-z_][A-Za-z0-9_./-]*` with single inner spaces, minus YAML 1.1 bool/null words, case-folded); everything else is double-quoted with JSON-style escapes, also escaping DEL, C1, U+2028/2029, U+FEFF.
- The reader goes beyond anchors/aliases/tags: it also refuses merge keys, duplicate keys, non-string keys, multiple documents, and values outside the subset (floats, timestamps).
- Golden CRLF, CR, and BOM inputs come through YAML escapes in the contexts, since `.gitattributes` would normalize raw CRLF in fixture files; a test guards that the fixtures still exercise the normalizer.

## Plan Change Log

## Review Triage Log

Pass 1 (quick): high 1, medium 2, low 3, false 1, maybe-false 0.

| # | Finding | Verdict | Route | Evidence |
|---|---|---|---|---|
| 1 | Jinja environment exposes `lipsum` and `random` | high | patch | Reviewer reproduced nondeterministic output; breaks AD-4 and NFR-4 |
| 2 | Non-Jinja exceptions escape `render` untyped | low | patch | `{{ 1/0 }}` raised ZeroDivisionError |
| 3 | Malformed timestamp raises a plain ValueError on read | low | patch | `a: 2026-13-45` |
| 4 | Reader accepts plain scalars read differently by YAML 1.1 and 1.2 | medium | patch | `1:30`→90, `010`→8, `0x1f`→31, `yes`→True; undercuts the AD-11 restricted subset for hand-written inputs |
| 5 | `write_text` uses a fixed temp name, leaks it on failure, no fsync | medium | patch | Concurrent writers collide; docs claim atomic |
| 6 | Acceptance criterion 2 (LF working tree on Windows) is never checked | low | patch | Verification command inspects the index only |
| 7 | Cross-OS golden match not yet shown | false | — | Pending the CI run after push |

## Design Notes

The golden manifest is the cross-OS proof: three OSes matching one committed hash list means they match each other, with no artifact passing between jobs. Quoting rule of thumb for the emitter: quote any string that a YAML 1.1 or 1.2 reader would not read back as the same string (booleans, nulls, numbers, leading/trailing space, indicators), always with double quotes and JSON-style escapes.

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass
- `git ls-files --eol | grep -v "i/lf\|i/none\|-text"` -- expected: no output (every text file is LF in the index)

**Manual checks (if no CLI):**
- CI green on the three OSes after push (needs the user's approval).
