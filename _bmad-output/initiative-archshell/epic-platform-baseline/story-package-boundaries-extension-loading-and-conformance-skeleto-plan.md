---
title: 'Package boundaries, extension loading, and conformance skeleton'
type: 'feature'
ticket: '10'
created: '2026-11-14'
status: 'built'
baseline_revision: '590033c0eb1750e88f705bfc6e3d12fae6c908b2'
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

**Problem:** Nothing stops the core from importing a pack or an adapter, or the domain from doing IO or using third-party code (AD-1, AD-4). Extensions have no load path that checks the contract version they implement (AD-14, FR-36). There is also no conformance suite that an extension must pass (FR-37) (epic E4, E5).

**Approach:**
- **Boundaries:** import-linter contracts that fail the build, plus a domain allow-list check.
- **Extension loading:** a core loader for entry-point extensions that refuses an incompatible `extension_contract` or a language claimed twice, with typed errors.
- **Conformance:** a skeleton suite that contributors run locally and CI runs, which the Claude Code plugin and the .NET pack pass from day one.

## Boundaries & Constraints

**Always:**
- **Contracts:** import-linter (latest version, pinned exactly in the lockfile) is configured in the root `pyproject.toml`. It enforces:
  - the core `archshell` never imports `archshell_dotnet`;
  - the layers: `engine`/`cli` above `adapters`, `adapters` above `render`, `render` above `domain`;
  - `domain` and `render` never import `adapters`, `engine` or `cli`;
  - `domain` imports only the standard library and `archshell.domain`.
- **CI:** CI runs the contracts as their own step on all three OSes. A test proves that a core import of the pack makes the contracts fail.
- **Entry-point groups:** `archshell.language_packs`, `archshell.providers` and `archshell.ci`, with no `archshell.hosts`. Each loaded object must expose `extension_contract` equal to the version the core supports for that kind (`archshell/language-pack-ext/v1`, `archshell/provider-ext/v1`, `archshell/ci-ext/v1`).
- **Load refusals:** a mismatch or a missing contract is refused with `incompatible_extension`. Two packs claiming one `language` are refused with `duplicate_language`. Both are typed errors that name the entry point and the distribution.
- **Pack:** `archshell-dotnet` registers its pack object in `archshell.language_packs`, with `extension_contract` and `language = "csharp"`.
- **Plugin:** the host plugin is checked as files, since it has no Python.
- **Conformance suite:** a pytest suite at `archshell-core/conformance/`. Contributors run it with one documented command, and CI runs it.
  - Pack checks: it loads through its entry point, its contract version, and its declared language.
  - Plugin checks: the manifest, `run.md` covering all six action types, hook wiring, and the engine override. These are moved out of `test_plugin_files.py`, not duplicated.
  - Adding a check later means adding a test file.
- Decision (2026-11-14): the existing domain -> render import is removed by moving `fingerprint` and the fingerprint-matches-content check from `domain/records.py` to `adapters/records.py`; the domain keeps shape and slug checks; record behavior and errors are unchanged.

**Never:**
- No `LanguagePack` port methods and no pack behavior (scaffolding, gates, mapping table). The golden and seeded-violation parts of FR-37 belong to epic-extensibility-launch.
- No new engine or `archshell` CLI command calls the loader yet.
- No change to the action protocol, hooks, or written files.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Clean tree | repo as merged | contracts pass | No error expected |
| Core imports pack | a module under `archshell/` imports `archshell_dotnet` | contracts fail, naming the import | build fails |
| Domain imports IO / third-party | `archshell/domain/x.py` imports `yaml` or `archshell.adapters` | contracts fail | build fails |
| Pack loads | installed workspace | `archshell-dotnet`'s pack returned for `csharp` | No error expected |
| Wrong contract | an entry point whose object has `extension_contract = archshell/language-pack-ext/v2` or none | — | `incompatible_extension` |
| Duplicate language | two entry points with `language = "csharp"` | — | `duplicate_language` |
| Conformance | `uv run python -m pytest archshell-core/conformance` | plugin and pack checks pass | — |

</frozen-after-approval>

## Code Map

Baseline `590033c` on `main` (after PR #9); the work is on `feat/1-10-package-boundaries` in `C:\git\arch-shell`.

- `pyproject.toml` (root) -- workspace members `archshell-core` and `archshell-dotnet`; dev group `dev = ["pytest>=8"]`; pytest `testpaths`. Add import-linter to `dev` and add `[tool.importlinter]`.
- `archshell-core/archshell/domain/records.py:19-20` -- **existing violation**: imports `render.normalize.to_bytes` and `render.yaml_emit` for `fingerprint`; it moves to the adapter (decision).
- `archshell-core/archshell/render/hosts/claude_code.py` imports `domain.hooks` (allowed). `render/templates.py:13-15` uses `jinja2` (allowed in render). `adapters` imports `render` and `domain`; `engine/cli.py` imports all three. `cli` imports only `__version__`.
- `archshell-core/archshell/ports/__init__.py` -- an empty seed. Put any typed extension protocol here only if the loader needs it.
- `archshell-core/pyproject.toml` -- `module-name = ["archshell","schemas"]`, scripts only, no entry points.
- `archshell-dotnet/pyproject.toml`, `archshell_dotnet/__init__.py` -- the skeleton. Add a `[project.entry-points."archshell.language_packs"]` table and the pack object. `archshell-dotnet/tests/test_pack_skeleton.py` checks the version and dependency.
- `archshell-core/tests/test_plugin_files.py` -- the generic plugin checks (frontmatter parser `:28-37`, the agent `:135`, hooks `:170-194`, `run.md` coverage `:53-128`, `:203`) move to the conformance suite. Keep the tests specific to this repo where they are.
- `archshell-core/tests/test_engine_tracer.py:13,42` -- the only `entry_points` use today (it reads `console_scripts`).
- `.github/workflows/ci.yml` -- one `build` job on three OSes: sync, golden, pytest, smoke, analyzer. Add `lint imports` and `conformance` steps after `uv sync`.
- Machine note: Application Control blocks uv launcher `.exe`s locally. Tests must call import-linter through Python (e.g. `python -m importlinter` or its API), not the `lint-imports` script.

## Tasks & Acceptance

**Execution:**
- [x] `pyproject.toml`, `uv.lock` -- pin import-linter; add the contracts listed in Always.
- [x] `archshell-core/archshell/domain/records.py`, `archshell-core/archshell/adapters/records.py` -- move `fingerprint` and its content check to `adapters/records.py` (no domain → render import), with the record tests still green.
- [x] `archshell-core/tests/test_boundaries.py` -- the contracts pass on the tree; in a scratch copy, a core import of the pack and a domain import of `yaml` each fail them; the domain allow-list check.
- [x] `archshell-core/archshell/adapters/extensions.py` -- `load(kind)` over the entry-point group, the contract check, the duplicate-language check, and typed errors.
- [x] `archshell-dotnet/pyproject.toml`, `archshell_dotnet/__init__.py` -- register the pack (contract + `language`).
- [x] `archshell-core/conformance/` -- the suite: a pack module, a plugin module (checks moved from `test_plugin_files.py`), and a README with the local command.
- [x] `archshell-core/tests/test_extensions.py` -- one test per loader matrix row, using fake distributions or entry points.
- [x] `.github/workflows/ci.yml` -- `lint imports` and `conformance` steps.
- [x] `README.md` -- the boundaries, the extension contract versions, and how to run conformance.

**Acceptance Criteria:**
- Given a branch that adds `import archshell_dotnet` under `archshell/`, when CI runs, then the import-contract step fails.
- Given `uv run python -m pytest`, then all tests pass on the three CI OSes, and the conformance step passes for the plugin and the pack.

## Implementation Notes

- import-linter pinned at 2.16 (latest on PyPI at build time). Contracts: `core-never-imports-packs`, `core-layers` (`engine : cli` siblings may import each other), `domain-render-stay-inner`, `domain-is-pure` (forbidden list with `include_external_packages`); the stdlib-only allow-list is the AST check in `test_boundaries.py`.
- Besides `fingerprint`, `domain/records.build_record` also imported `render.yaml_emit.Ordered`; it now returns a plain insertion-ordered dict and the adapter wraps it in `Ordered` before emitting. Emitted bytes unchanged (golden and record tests green). Tests now call `archshell.adapters.records.fingerprint`.
- The loader also refuses a language pack whose `language` is missing or blank, as `incompatible_extension` (duplicate detection needs it).
- `test_plugin_files.py` keeps only the repo-specific `/archshell:ping` check (rewritten as a text check, so the frontmatter parser is not duplicated); all 21 other checks moved to `conformance/test_plugin_conformance.py`, plus a manifest check and an override-on-every-command check. `conformance` added to pytest `testpaths`, so plain `pytest` also runs it.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens): high 0, medium 1, low 5, false 0, maybe-false 0.

| # | Finding | Verdict | Route | Evidence / action |
|---|---------|---------|-------|-------------------|
| 1 | `domain-is-pure` contract name/README claim stdlib-only, but it is a fixed forbidden list | low | patch | Unlisted third-party imports keep the contract; only the AST allow-list test catches them. Patch: describe the contract as a forbidden list, README points to the allow-list test. |
| 2 | `extensions.load()` leaves `ep.load()` unguarded | medium | patch | A missing target or import error raises raw ImportError/AttributeError, against the "typed errors naming entry point and distribution" rule. Patch: wrap as `incompatible_extension`; one test. |
| 3 | Duplicate-language check compares raw strings (`"csharp"` vs `"CSharp"`) | low | reject | Needs two installed packs for one language with different spellings; fix adds a normalization/format rule the plan does not define. |
| 4 | CI conformance command differs from the documented one; suite runs twice | low | patch | CI `uv run pytest -v`, docs `uv run python -m pytest`. Patch: one spelling; double run kept (cheap, plain `pytest` covers it). |
| 5 | README Quickstart vs conformance section give different commands; Quickstart uses blocked `lint-imports` | low | patch | Same root as 4 plus missing Python form. Patch: unify, add Python form. |
| 6 | `archshell.ports` in no inner-package contract | low | patch | Ports could import adapters/engine/cli. Patch: add to `domain-render-stay-inner` sources. |

## Design Notes

Loader shape, for illustration:

```python
SUPPORTED = {"language_packs": "archshell/language-pack-ext/v1", ...}
for ep in entry_points(group=f"archshell.{kind}"):
    obj = ep.load()
    if getattr(obj, "extension_contract", None) != SUPPORTED[kind]:
        raise IncompatibleExtension(ep.name, ep.dist.name, found, SUPPORTED[kind])
```

The plugin is a host adapter, which AD-14 says "holds no logic". So its conformance is structural plus the protocol rules `run.md` must state. The engine-interface conformance run (AD-18 `human_ui`) arrives with story 1.13.

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass
- `uv run python -m pytest archshell-core/conformance` -- expected: plugin and pack pass
- the import-linter run through Python -- expected: all contracts kept

