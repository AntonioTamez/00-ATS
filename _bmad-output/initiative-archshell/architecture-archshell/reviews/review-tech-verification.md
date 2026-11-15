---
review: tech-verification
target: ../architecture-archshell.md (+ ../.memlog.md)
lens: every committed technology decision is web-researched or reality-checked, not asserted from training data
date: 2026-11-08
mode: read-only (no edits to the spine)
---

# Tech Verification Review — ArchShell Architecture Spine

## Verdict

**Revise before freezing the spine. No blockers, two High findings.**

Every named technology exists, is maintained, and fits .NET 10 / Python 3.12+ as of 2026-11-08. The stack does not need to change. But the spine shows little evidence of research. Of the version and platform claims in the memlog, only one cites a source (`.slnx` default, memlog line 15). The rest are asserted ("latest seen", no URL) or left open ("pinned at implementation"). Checking them turned up two places where the facts go against how AD-3 and AD-12 are worded, plus several stale pins and open choices.

## How this was checked

- **Registries queried live:** PyPI JSON API and the NuGet v3 registration/flat-container APIs, for current versions, publish dates and target frameworks.
- **Probe run:** `uv run --isolated` in a scratch directory outside the repo. It loaded `tree-sitter-c-sharp` into `tree-sitter` 0.25.2 and 0.26.0, then parsed C# 12–14 constructs.
- **Local SDK inspected:** `dotnet --list-sdks` returned 10.0.401. Its bundled `Roslyn/bincore/Microsoft.CodeAnalysis.dll` is 5.9.0-1.26423.113.
- **Docs fetched:** official Microsoft, GitHub, Astral and Claude Code documentation (URLs are with each finding).

## Evidence matrix

| Claim in spine / memlog | Status as of 2026-11-08 | Verified how | Result |
| --- | --- | --- | --- |
| Python ≥ 3.12 | Fine; tree-sitter 0.26 needs ≥ 3.10, Jinja2 ≥ 3.7 | PyPI metadata | OK |
| Jinja2 3.1.6 | Still the latest (2025-03-05); no 3.2 released | PyPI | OK (asserted in memlog, confirmed now) |
| tree-sitter (py) 0.25.2 | **Stale**: 0.26.0 released 2026-06-30, with breaking API removals | PyPI + GitHub releases | F-4 |
| tree-sitter-c-sharp 0.23.1 | **Stale**: 0.23.5 is the latest (2026-04-14). ABI 15 is compatible with bindings 0.25.2 and 0.26.0 (MIN 13 / LATEST 15) | Probe | OK for ABI; F-2 for syntax coverage |
| .NET SDK 10 (LTS) | 10.0.14 runtime / SDK 10.0.4xx; LTS until 2028-11-14. **.NET 11 goes GA on 2026-11-10** | MS support policy page | F-6 |
| `.slnx` default in .NET 10 | Confirmed (cited in memlog) | MS Learn | OK |
| MSBuildWorkspace on .NET 10 / `.slnx` | `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.9.0 (2026-08-17) targets `net10.0` + `net472`. `.slnx` support since Roslyn 5.0 (dotnet/roslyn#77326, merged 2025-05-23) | NuGet + GitHub API | OK; F-3 on how to pin it |
| NetArchTest / ArchUnitNET | NetArchTest.Rules last release 1.3.2 (**2021-05-23**). TngTech.ArchUnitNET 0.13.5 (2026-10-26) is active | NuGet registration | F-5 |
| .NET local tool in `.config/dotnet-tools.json` | Confirmed; `dotnet tool restore` restores it; .NET 10 adds `dnx`, which honours the manifest version | MS Learn | OK; F-6 (roll-forward) |
| `uv tool` / `pipx` | uv 0.13.7, pipx 1.18.16. `uv tool` exposes only the main package's executables; extra packages go in with `--with` | Astral docs | F-7 |
| Claude Code hooks block writes under `.archshell/` | Only **PreToolUse** can block. PostToolUse runs after the tool, Stop blocks the end of the turn. Plugin hooks run inside subagents too; `allowManagedHooksOnly` can switch them off | Claude Code hooks docs | F-8 |
| Each `llm_task` in "a separate subagent" with no shared history | Named subagents start fresh, **but fork mode (on by default) inherits the parent's context**, and the parent writes the delegation message | Claude Code sub-agents docs | F-1 |
| GitHub CODEOWNERS + branch protection | Protected branches, required reviewers and code owners for **private** repos need Pro/Team or higher; Free covers public repos only. Rulesets: Team/Enterprise for private | GitHub docs | OK; AD-15 already degrades to `trust: unenforced` |
| GitLab Code Owners needs Premium; Azure DevOps uses required reviewers by path | Not re-verified here (deferred provider) | — | Unverified, Low |
| JSON Schema draft 2020-12 | `jsonschema` 4.26.0 supports it; the spine names no library | PyPI | F-9 |

## Findings

### F-1 — High — AD-3 isolation does not hold by default in Claude Code

- **Claim:** AD-3 says each `llm_task`, including the three consensus runs, runs "in a separate context with no shared history (in Claude Code, a separate subagent)".
- **Reality:**
  - Claude Code subagents do start with a fresh context, with one exception: a **fork**, which "inherits the parent conversation". Fork mode is on by default in interactive sessions.
  - For any subagent, "Claude composes a delegation message that summarizes the task". The parent agent writes that message, so it can carry an earlier run's output into the next run. This is the correlated consensus that AD-3 is meant to prevent.
- **Fix:** add the mechanism to AD-3 or to the host adapter rules:
  - Dispatch only to a named plugin agent shipped by `archshell-claude-code`, never to a fork.
  - Pass the engine's prompt by reference, as a file path the engine wrote. The parent must not paraphrase it.
  - Have `submit` check that each run's input hash matches the prompt the engine issued.
- **Also note:** plugin agents ignore the `hooks`, `mcpServers` and `permissionMode` frontmatter, so hooks for these agents must live in `hooks/hooks.json`.
- **Source:** https://code.claude.com/docs/en/sub-agents

### F-2 — High — The tree-sitter C# grammar cannot parse parts of C# 14 (the .NET 10 language)

- **Probe:** `tree-sitter-c-sharp` 0.23.1 and 0.23.5, run on bindings 0.26.0.
  - Parsed cleanly: C# 12 primary constructors, C# 13 `params` span, C# 14 `field` keyword, partial constructors.
  - Produced `ERROR` nodes: **C# 14 `extension(...) { }` blocks** and **null-conditional assignment (`c?.P = 1`)**.
- **Upstream:** the PR that adds C# 14 extension declarations (tree-sitter/tree-sitter-c-sharp#428) was closed without merging on 2026-05-19, and no newer release exists.
- **Impact:** AD-12 lets the syntactic tier "over-block" and says "when unsure, it defers". Without an explicit rule, a parse with `ERROR` nodes could cause either a false block on valid code or missed facts that approve wrong code.
- **Fix:** make the rule explicit in AD-12. Any `ERROR` or `MISSING` node in the edited file means "unsure, defer to the full gates", never pass or fail on the per-edit tier. Add C# 14 samples to the golden corpus, and record the grammar's language-version ceiling in the pack.
- **Sources:**
  - https://pypi.org/project/tree-sitter-c-sharp/
  - https://github.com/tree-sitter/tree-sitter-c-sharp/pull/428
  - https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14

### F-3 — Medium — "Pinned at implementation against SDK 10" is not one pin: Roslyn changes with the SDK feature band

- **Fact:** the local SDK 10.0.401 ships Roslyn 5.9.0-1. NuGet has `Microsoft.CodeAnalysis.Workspaces.MSBuild` at 5.0.0, 5.3.0, 5.6.0 and 5.9.0, which follow the 10.0.1xx–4xx bands. Version 5.9.0 dropped `net8.0`/`net9.0` and now targets `net10.0` and `net472`.
- **Impact:** when MSBuildWorkspace's Roslyn is older than the compiler that built a project's analyzers or source generators, they fail to load (CS9057 class of failure). Generated types are then missing from the semantic model, and Roslyn's "authoritative" verdict (AD-12) and consumer lookup (AD-13) become wrong. NFR-4 determinism also depends on which SDK band CI happens to have.
- **Fix:**
  - Generate a `global.json` that pins the SDK feature band plus `rollForward` policy. Record it in `architecture.yaml` next to the core and pack versions (AD-14).
  - Make the analyzer's `Microsoft.CodeAnalysis.*` version at least the compiler version of that band.
  - Ship `Microsoft.Build.Locator` (latest 1.11.2) and register it before creating the workspace.
  - Name these in Stack rather than deferring them.
- **Sources:**
  - https://www.nuget.org/packages/Microsoft.CodeAnalysis.Workspaces.MSBuild
  - https://github.com/dotnet/roslyn/pull/77326
  - https://www.nuget.org/packages/Microsoft.Build.Locator
  - https://learn.microsoft.com/dotnet/core/tools/global-json

### F-4 — Medium — The tree-sitter Python binding pin is one release behind, with breaking changes in between

- **Fact:** 0.26.0 (2026-06-30) removed `Language.version`, `Language.query()` and `Parser.timeout_micros`, and turned `Point` into a tuple subclass. All of these were deprecated in 0.25.
- **Risk:** code written against 0.25.2 using the deprecated APIs will break on the first upgrade.
- **Fix:** pin `tree-sitter==0.26.0` and `tree-sitter-c-sharp==0.23.5`, and use only `Query(language, src)`, `QueryCursor` and `abi_version`. The probe confirmed that this pair loads (ABI 15) and parses.
- **Sources:**
  - https://pypi.org/project/tree-sitter/
  - https://github.com/tree-sitter/py-tree-sitter/releases

### F-5 — Medium — "NetArchTest / ArchUnitNET" is undecided, and NetArchTest is effectively unmaintained

- **Fact:**
  - `NetArchTest.Rules` 1.3.2, released 2021-05-23, is the last release. The repo's last push was 2024-07, and it targets `netstandard2.0`.
  - The community fork `NetArchTest.eNhancedEdition` is at 1.4.5 (2025-06-04).
  - `TngTech.ArchUnitNET` 0.13.5 (2026-10-26) is actively released and has xUnit/NUnit/MSTest integrations.
- **Impact:** with two libraries, the generated architecture tests (AD-5, AD-6) could come in two shapes. The memlog has no record comparing them.
- **Fix:** pick **ArchUnitNET** as the single library in the Stack, and record the rejected options (NetArchTest because it is stale, eNhancedEdition because it is a fork with a single maintainer).
- **Sources:**
  - https://www.nuget.org/packages/NetArchTest.Rules
  - https://www.nuget.org/packages/NetArchTest.eNhancedEdition
  - https://www.nuget.org/packages/TngTech.ArchUnitNET
  - https://github.com/TNG/ArchUnitNET/releases

### F-6 — Medium — A `net10.0` local tool and ".NET 10 LTS or later" (NFR-3) will collide in two days

- **Fact:** .NET 11 goes GA at .NET Conf on 2026-11-10 (RC3 is go-live today). By default a `net10.0` tool does not roll forward to a new major runtime, so a machine or CI image with only the .NET 11 runtime cannot run the analyzer. .NET 8 and .NET 9 reach end of support on 2026-11-10.
- **Fix:** do one of these:
  - set `<RollForward>Major</RollForward>` in the analyzer tool, or multi-target `net10.0;net11.0`;
  - or narrow NFR-3 to "the SDK band pinned in `global.json`" (ties in with F-3).
- **Sources:**
  - https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
  - https://learn.microsoft.com/dotnet/core/versions/selection#framework-dependent-apps-roll-forward
  - https://learn.microsoft.com/dotnet/core/tools/local-tools-how-to-use

### F-7 — Medium — `uv tool` / `pipx` put each tool in its own isolated environment, so the pack cannot be found through entry points

- **Fact:** `uv tool install archshell` creates an isolated environment. `archshell-dotnet` installed with a second `uv tool install` lands in a different environment, and `archshell.language_packs` entry-point discovery will not find it. Additional packages go in with `--with`, which installs them as dependencies without their executables. pipx needs `pipx inject`.
- **Impact:** AD-14 ("core and pack ... install with `uv tool` or `pipx`") and "`verify` installs exactly those" will not work as written.
- **Fix:** state the install command form, for example `uv tool install archshell==X --with archshell-dotnet==Y`, or `uvx --from archshell==X --with archshell-dotnet==Y archshell verify` in CI, or `pipx install` followed by `pipx inject`. Or ship the pack as an extra (`archshell[dotnet]`).
- **Source:** https://docs.astral.sh/uv/concepts/tools/

### F-8 — Medium — AD-10's "in-session hooks block every agent write under `.archshell/`" must name PreToolUse, and is best effort

- **Facts from the docs:**
  - PostToolUse cannot block: "the tool already ran". Only **PreToolUse**, with exit 2 or `permissionDecision: deny`, prevents a write.
  - The Stop hook can block the end of a turn, which fits the full-gates step.
  - Plugin hooks do fire inside subagents.
  - Plugin hooks can be switched off by `allowManagedHooksOnly`, or by disabling the plugin.
  - Writes made through the Bash tool (`echo > .archshell/...`, `git checkout -- .archshell`) only reach a Bash matcher as a command string, so they cannot be blocked reliably.
- **Fix:**
  - AD-10 should say: PreToolUse on `Write|Edit|MultiEdit|NotebookEdit` plus a best-effort Bash path check; Stop for the full gates; and in-session protection is advisory, with `verify` in CI as the authority (FR-41).
  - The README's mapping of PostToolUse/Stop to gates (PRD reconcile-readme item 1) should move per-edit checks to PostToolUse only for feedback, never for protection.
- **Source:** https://code.claude.com/docs/en/hooks

### F-9 — Low — Libraries the rules depend on are not named

- **Missing from Stack:**
  - The JSON Schema 2020-12 validator (for example `jsonschema` 4.26.0).
  - The YAML safe loader and canonical emitter that AD-11 requires. PyYAML vs. ruamel.yaml matters for byte-identical output (NFR-4), because the emitters differ in quoting and line width.
- **Fix:** name and pin them, or state that the core hand-writes the emitter. The second is consistent with AD-11's "written only by the core's canonical emitter".
- **Source:** https://pypi.org/project/jsonschema/

### F-10 — Low — Research provenance is missing from the memlog

- **Fact:** only one version claim (`.slnx`) carries a source URL. The memlog states "Jinja2 3.1.6 (latest seen)" with no source, gives tree-sitter versions with no source, and leaves Roslyn "unconfirmed". The GitHub Free and GitLab Premium capability claims (memlog lines 25–26) have no citation.
- **Fix:** add a `(version)` line with URL and check date for each Stack row. This review's evidence matrix can serve as that record.

### F-11 — Info — Claims that hold up

These need no change:

- Jinja2 3.1.6 is still the latest release.
- `.slnx` is the default in .NET 10, and MSBuildWorkspace can open `.slnx` from Roslyn 5.0.
- `.config/dotnet-tools.json` is the default manifest location, and `dnx` respects it.
- Claude Code plugins support `hooks/hooks.json`, `agents/` and `bin/` on PATH.
- GitHub Free private repos have no branch protection or code owner enforcement, which matches AD-15's `trust: unenforced` design.
- CODEOWNERS can live in `.github/`, the root or `docs/`. It has a 3 MB limit and supports no `!` negation, so the single `/.archshell/` glob in AD-10 is a valid pattern.
- **Sources:**
  - https://docs.github.com/en/get-started/learning-about-github/githubs-plans
  - https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners
  - https://code.claude.com/docs/en/plugins-reference

## Suggested Stack table after fixes

| Name | Version |
| --- | --- |
| Python | ≥ 3.12 |
| Jinja2 | 3.1.6 |
| tree-sitter (Python bindings) | 0.26.0 |
| tree-sitter-c-sharp | 0.23.5 (C# ≤ 13 plus partial C# 14; `ERROR` nodes defer to the full gates) |
| .NET SDK | 10, band pinned via generated `global.json` |
| Microsoft.CodeAnalysis.Workspaces.MSBuild | ≥ the compiler of the pinned band (5.9.0 for 10.0.4xx) |
| Microsoft.Build.Locator | 1.11.2 |
| Architecture tests | TngTech.ArchUnitNET 0.13.5 |
| JSON Schema validator | jsonschema 4.26.0 |
| Install | `uv tool install archshell==X --with archshell-dotnet==Y` (or `pipx install` + `pipx inject`) |
