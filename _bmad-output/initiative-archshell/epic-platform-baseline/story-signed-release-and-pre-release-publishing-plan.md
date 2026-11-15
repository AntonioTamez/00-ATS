---
title: 'Signed release and pre-release publishing'
type: 'feature'
ticket: '11'
created: '2026-11-14'
status: 'built'
baseline_revision: 'b79a73cd74492915043c6a65ea7150ca85756b36'
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

**Problem:** Nothing turns a commit into installable packages. There are no signed tags, no compatibility matrix tying the pack to a core range, the analyzer to the pack, and the plugin to a protocol version, and no publishing (AD-22, epic E5, Done-when 6).

**Approach:**
- **Release check:** a stdlib script refuses a tag whose versions or compatibility declarations disagree.
- **Release workflow:** on a pre-release tag, it checks that GitHub verified the tag signature. It then publishes `archshell` and `archshell-dotnet` to TestPyPI and `Archshell.Analyzer` to GitHub Packages (NuGet). A clean job installs both on three OSes.

## Boundaries & Constraints

**Always:**
- **Repository:** code lives in `C:\git\arch-shell`, on branch `feat/1-11-release` from `main`.
- **Tags:** pre-release tags only: `vX.Y.Z-(alpha|beta|rc).N`. The Python version maps to PEP 440 `X.Y.Z(a|b|rc)N`. The analyzer and plugin keep the SemVer form `X.Y.Z-rc.N`.
- **Versions:** the committed files are the source of truth. The release refuses a tag that does not match them. CI never rewrites a version.
- **Compatibility matrix:**
  - The pack declares its core range in its `archshell` dependency, and that range includes the release's core version.
  - The pack pins its analyzer in `archshell_dotnet.ANALYZER_VERSION`, equal to this release's analyzer version.
  - The plugin declares its protocol in `archshell-claude-code/archshell-host.json` (`{"protocol": "archshell/action/v1"}`), equal to the core `ACTION_SCHEMA`.
  - `docs/compatibility.md` holds one row per release, and the check requires the tag's row.
- **Signature:** the workflow refuses a tag unless it is annotated and GitHub reports `verification.verified == true` (GPG or SSH).
- **Credentials:** TestPyPI uses Trusted Publishing (OIDC, `id-token: write`) with no stored token. NuGet uses `GITHUB_TOKEN` with `packages: write` in the publish job only. Every other job keeps `contents: read`.
- **Install job:** the clean install pins `archshell` and `archshell-dotnet` to the TestPyPI index. Every other dependency resolves from PyPI, so a squatted name cannot be substituted.
- **Order:** the existing CI must pass on the tagged commit before anything is published.
- Decision (2026-11-14): the core JSON Schemas move from the top-level `schemas/` to `archshell/schemas/`, so the wheel installs only `archshell`. Schema files, content and validation behavior are unchanged; the spine's `archshell-core/schemas/` mention is updated.

**Never:**
- No publishing to PyPI or nuget.org, and no stable-tag release.
- No change to the action protocol, hooks, engine behavior, or written files.
- No version stamping from the tag.
- No long-lived publishing secret.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Release | signed tag `v0.1.0-rc.1`; files at `0.1.0rc1` / `0.1.0-rc.1`; matrix row present | both pre-releases published; install job green on three OSes | No error expected |
| Unsigned tag | lightweight or unverified tag | nothing published | workflow fails naming the tag |
| Version mismatch | tag `v0.1.0-rc.2`, files at `rc.1` | nothing published | check names each mismatched file and its version |
| Stable tag | `v0.1.0` | nothing published | check refuses: pre-release tags only |
| Bad declaration | pack range excludes the core version, `ANALYZER_VERSION` differs, or the plugin protocol is not `ACTION_SCHEMA` | nothing published | check names the declaration |
| Missing matrix row | no `docs/compatibility.md` row for the tag | nothing published | check names the missing row |

</frozen-after-approval>

## Code Map

Baseline: `main` at `b79a73c` (after PR #10).

- `archshell-core/pyproject.toml`, `archshell-dotnet/pyproject.toml` -- both at `version = "0.1.0"`. The pack's dependency is an unpinned `archshell`, so it needs a range. The build backend is `uv_build`, and the core's `module-name = ["archshell","schemas"]`.
- `archshell-dotnet/analyzer/Archshell.Analyzer.csproj` -- `<Version>0.1.0</Version>`, `PackAsTool`, `PackageId Archshell.Analyzer`. It has no `RepositoryUrl`, which GitHub Packages needs to link the package to the repo.
- `archshell-claude-code/.claude-plugin/plugin.json` -- `version 0.1.0`. Keep it free of custom keys; the protocol goes in a sibling `archshell-host.json`.
- `archshell-core/archshell/domain/actions.py:12` -- `ACTION_SCHEMA`, the protocol source.
- `archshell_dotnet/__init__.py` -- add `ANALYZER_VERSION`.
- `archshell-core/conformance/test_plugin_conformance.py` -- add the host-protocol check here, not in `tests/`.
- `.github/workflows/ci.yml` -- a three-OS `build` job on `push`. The release workflow either reuses it (`workflow_call`) or waits on it; do not duplicate its steps.
- `uv build --all-packages` works today. Its wheel holds the top-levels `archshell` and `schemas`. `output_schemas.py:21` resolves `SCHEMAS_DIR` as `archshell/../schemas`; grep `tests/` and `conformance/` for other `schemas` path uses before moving.
- Machine note: Application Control blocks uv launcher `.exe` files locally. Tests run the script with `python`, not through a console script.
- Facts confirmed during planning: `archshell` and `archshell-dotnet` are free on both PyPI and TestPyPI, and the repo is public.

## Tasks & Acceptance

**Execution:**
- [x] `archshell-core/schemas/` → `archshell-core/archshell/schemas/`, `archshell-core/pyproject.toml` (`module-name = "archshell"`), `adapters/output_schemas.py:21`, the spine (`architecture-archshell.md` Deferred) -- move per the decision; a test asserts the built wheel's only top-level package is `archshell`.
- [x] `scripts/release_check.py` -- `check(tag, root)` → list of named problems; CLI exits 1 with them -- one stdlib gate for every matrix row except the signature.
- [x] `archshell-core/tests/test_release_check.py` -- one test per matrix row on a scratch copy of the declaration files.
- [x] `archshell-dotnet/pyproject.toml`, `archshell_dotnet/__init__.py`, `archshell-claude-code/archshell-host.json`, `docs/compatibility.md` -- the declarations, and the core range for the pack.
- [x] Version files (both pyprojects, csproj, plugin.json, `uv.lock`) -- bump to `0.1.0rc1` / `0.1.0-rc.1`; add `RepositoryUrl` to the csproj.
- [x] `archshell-core/conformance/test_plugin_conformance.py` -- the plugin declares `ACTION_SCHEMA`.
- [x] `.github/workflows/release.yml` (+ `workflow_call` in `ci.yml`) -- jobs: `verify` (tag shape, signature via `gh api`, `release_check`), `ci`, `publish` (TestPyPI and GitHub Packages), and `install` (three OSes: a uv temp project with TestPyPI as an explicit index for our two packages; `dotnet tool install` from GitHub Packages; `archshell --version`, `archshell-analyzer`).
- [x] `README.md` -- the release procedure: one-time setup and cutting a signed pre-release.

**Acceptance Criteria:**
- Given the one-time setup is done (hitl: the TestPyPI pending publisher, a signing key registered on GitHub), when the user pushes a signed `v0.1.0-rc.1`, then both pre-releases are published and the install job passes on Windows, Linux, and macOS.
- Given `uv run python -m pytest`, then all tests pass, the new conformance check included.

## Implementation Notes

- The core range is `archshell>=0.1.0rc1,<0.2`. The script implements the PEP 440 subset it needs with the stdlib, including the rule that `<V` excludes pre-releases of `V`. A lower bound of `>=0.1.0` would exclude `0.1.0rc1`.
- `release_check.py` also checks the two workspace entries in `uv.lock`, and reports a missing or unreadable declaration file by name.
- The `install` job adds `packages: read`, which is read-only. GitHub Packages NuGet needs a token even to read a public package. Every write permission stays in `publish`. The NuGet config used for the install lists only GitHub Packages (`<clear />`) and reads the token from the environment.
- `publish` runs in the GitHub environment `testpypi`. The TestPyPI pending publisher must name that environment (README one-time setup). `uv publish --check-url` skips files that were already uploaded, so a re-run is safe.
- `ci.yml` gains only `workflow_call`. A tag push still also triggers `ci.yml` through `push`, which is a harmless duplicate.
- The spine had three `archshell-core/schemas/` mentions: Deferred, the Schemas convention, and the Structural Seed. All three now point at `archshell/schemas/`.
- The tests for the unsigned-tag row are structural: they check `release.yml` for the ref and tag API calls, `.verification.verified`, the job order, and the permissions. The real refusal can only be observed on GitHub.
- Not verified locally: the workflow run itself. Acceptance 1 is hitl and needs the one-time setup and a pushed signed tag.

## Plan Change Log

## Review Triage Log

Pass 1 (quick): high 0, medium 2, low 1, false 0, maybe-false 0.

| # | Finding | Verdict | Route | Evidence / action |
|---|---------|---------|-------|-------------------|
| 1 | `_matrix_releases` checks only a row's first cell; the other cells are never compared, and `docs/compatibility.md` claims they are | medium | patch | Confirmed at `release_check.py` `_matrix_releases`/`check`: a row with wrong versions passes, so the published matrix can disagree with the release. Patch: compare every cell of the tag's row with the declared values, naming each mismatched column. |
| 2 | `publish` uploads to TestPyPI before packing the analyzer, so a pack failure leaves a permanent half-release | medium | patch | Confirmed in `release.yml` step order; TestPyPI refuses re-uploads. Patch: build both Python dists and pack the analyzer first, push the analyzer (deletable) next, upload to TestPyPI (permanent) last; README wording to match. |
| 3 | The BOM assertion in `test_plugin_conformance.py` uses a literal invisible U+FEFF | low | patch | Confirmed: the rest of the file uses `"﻿"`; an editor stripping it turns the test into an always-failing one. Patch: use the escape. |

## Design Notes

The signature check uses the GitHub API. It needs no keyring on the runner and covers GPG and SSH alike:

```bash
sha=$(gh api repos/$REPO/git/ref/tags/$TAG --jq .object.sha)
gh api repos/$REPO/git/tags/$sha --jq .verification.verified   # must be "true"
```

A lightweight tag points at a commit, not a tag object, so the second call fails. That refusal is intended.

## Verification

**Commands:**
- `uv run python -m pytest` -- expected: all pass
- `uv run python scripts/release_check.py v0.1.0-rc.1` -- expected: exit 0, no problems
- `uv run python scripts/release_check.py v0.1.0` -- expected: exit 1, pre-release only

**Manual checks (if no CLI):**
- The user pushes the signed tag, and the release run is green across verify, ci, publish, and install on three OSes.
