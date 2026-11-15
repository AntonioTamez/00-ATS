---
title: Rubric review — ArchShell architecture spine
reviewed: architecture-archshell.md (status draft, updated 2026-11-08) + .memlog.md
driving_spec: ../prd-archshell/prd-archshell.md (FR-1..FR-60, NFR-1..NFR-7)
date: 2026-11-08
verdict: NOT READY — revise before the level below builds on it
---

# Rubric review — ArchShell architecture spine

## Verdict

**Not ready.** The spine has a strong paradigm and fixes most of the workflow divergence points: stateless engine, closed action protocol, inputs versus derived artifacts, schema ownership, and two-tier analysis. But three of its own rules contradict each other or the PRD in ways that would make independently built units diverge, and two of those break the product's trust promise:

1. AD-6/AD-14/FR-41 treat scaffolded files as pure derived output. Agent-written method bodies make that false, so `reconcile`/`upgrade` would wipe code and `verify` cannot byte-compare.
2. The trust anchor files (CI workflow, `CODEOWNERS`, tool manifest) are neither protected nor owned.
3. Who runs the gates in session (the engine through `ProcessRunner`, or the host through `run`) is ambiguous, and the hook entry path sits outside the closed protocol.

The operational and environmental envelope is mostly silent: security of the plugin itself, install/setup, ArchShell's own release, macOS, and configuration. Stack versions are partly stale.

Severity counts: critical 3, high 6, medium 13, low 6.

## Rubric scorecard

| Criterion | Result |
| --- | --- |
| Fixes the real divergence points, misses none | Partial: misses scaffold body ownership, hook/per-edit entry path, gate executor, configuration, mixed-ownership `.csproj`, policy file runtime form |
| Every AD Rule enforceable and prevents its divergence | Partial: AD-6, AD-10, AD-14, AD-15 contradict other ADs or the PRD; AD-2, AD-3, AD-4, AD-12 are only partly enforceable as written |
| Nothing under Deferred lets two units diverge | Mostly yes; schema shapes are acceptable only if the core lands them first (L-1) |
| Named tech verified-current | No: tree-sitter 0.26.0 and tree-sitter-c-sharp 0.23.5 are out; Roslyn MSBuild is resolvable today; NetArchTest versus ArchUnitNET is undecided |
| Covers the driving spec FR-1..FR-60, NFR-1..NFR-7 | Partial: FR-9/FR-32 contradicted; FR-36 pack check dropped; FR-42, FR-17, FR-19, FR-38, FR-21 (stop), FR-26 bound, FR-53 identity, NFR-2 (macOS), NFR-6 unaddressed; NFR-5 contradicted |
| Every dimension decided, deferred, or open question | No: security of the plugin, install/setup, ArchShell release/infra, configuration, and concurrency are silent; there is no Open Questions section, and the PRD-delta list lives only in the memlog |

## Findings

### Critical

**C-1 — Scaffolded files are not pure derived artifacts, so regeneration destroys implementation code**
- **Location:** AD-6 (Derived includes "contract code + scaffold"; "take either side and run `archshell reconcile`"), AD-14 (`upgrade` regenerates the project), AD-5, Capability map 4.3/4.5; PRD FR-24, FR-41 bullet 5, FR-56.
- **Problem:** Per FR-24 and FR-56, the agent fills method bodies and adds private members in scaffolded files. Those files are therefore a mix of generated surface and hand-written bodies, not "a pure function of the union of inputs". As written, three things follow. `reconcile` after a merge conflict would regenerate scaffold files and erase bodies. `upgrade` would regenerate the project and erase bodies. `verify` cannot "regenerate the scaffold and fail if the committed generated code differs" by byte comparison. The pack (templates), `verify`, and `reconcile` will each invent their own comparison model, which is exactly the divergence a spine exists to prevent.
- **Fix:** Add an AD that classifies every generated file into one of three ownership classes:
  - (a) **Fully derived** (architecture tests, contract code, composition, policies, `.slnx`): byte-compared and regenerated freely.
  - (b) **Scaffold-once** (building-block files with bodies): generated once at creation. After that, `verify` and the gates check them only by surface conformance (FR-56) against the plan. `reconcile` and `upgrade` never overwrite them; an upgrade that changes their template emits a surface diff for the agent to apply under the gates.
  - (c) **Mixed-ownership** (see H-2).
  
  State that FR-41's "regenerate scaffold" means surface comparison for class (b), and record that as a PRD delta.

**C-2 — Trust-anchor files are outside the protected and owned set**
- **Location:** AD-10 ("one CODEOWNERS glob" for `.archshell/`), AD-15 (CiProvider generates the workflow and owners file), AD-14 (`.config/dotnet-tools.json` pins the analyzer); PRD FR-14, FR-41, FR-53.
- **Problem:** The CI workflow file that runs `verify`, the `CODEOWNERS` file itself, `.config/dotnet-tools.json` (which analyzer `verify` runs), `.gitattributes`, and the hook configuration location are not assigned to `CODEOWNERS` or to the protected set. An agent PR that edits the workflow to skip or soften `verify`, retargets the analyzer pin, or removes `CODEOWNERS` entries defeats the only authoritative check (FR-14: "the authoritative check is `archshell verify` in CI"). The spine's own `CODEOWNERS` statement covers only `.archshell/` and omits the files FR-53 lists (`architecture.yaml`, ADRs, generated architecture tests, generated contract code).
- **Fix:** Extend AD-10 (or a new security AD) with the full owned set: `CODEOWNERS` itself, the CI workflow path, `.config/dotnet-tools.json`, `.gitattributes`, the hook config, `architecture.yaml`, `docs/adr/`, generated architecture tests, generated contract code, and `.archshell/`. Make every CiProvider declare which paths it must own, and make `verify` fail when the provider's owners file does not cover them. Record the protected-set additions as a PRD delta (FR-14/FR-53).

**C-3 — No trust model for the engine–agent boundary in Phase 1; gate execution is ambiguous**
- **Location:** AD-1, AD-2 (`run` action: the host "returns exit code + output"; `confirm`/`ask` results submitted by the host), Ports (`ProcessRunner`), AD-10 hooks; PRD FR-14, FR-21, FR-30, FR-53.
- **Problem:** In Phase 1 the "host" is the agent running skills, so every `submit` is agent-authored. It can report `confirm: accepted`, a fabricated `ask` answer, or `run` exit code 0 for a failed build. The spine does not say which in-session outcomes the engine trusts. It also leaves two possible gate executors: the engine itself through `ProcessRunner`, or the host through `run`. Pack authors, host adapters, and the engine will split these differently, and if the host executes the full gates, in-session gates become self-reported.
- **Fix:** Add an AD "Trust boundary":
  - The engine itself executes every gate and toolchain command through `ProcessRunner`. `run` is limited to commands whose outcome is not a verdict, or is removed.
  - Results of `confirm` and `ask` are treated as unauthenticated in session; their authority comes only from the confirmation record plus PR review (FR-53).
  - `verify` in CI is the only verdict that counts.
  - Name which state is advisory, so contributors do not build in-session "security" that is not one.

### High

**H-1 — Hook / per-edit / session-stop entry path is outside the closed protocol**
- **Location:** AD-2 (closed action set; engine commands `next`, `submit`, `verify`, `reconcile`, `upgrade`), AD-10 ("in-session hooks block"), AD-12 per-edit tier; PRD FR-20, FR-21 (gates on every stop), FR-46, NFR-1.
- **Problem:** Per-edit checks, the `.archshell/` write block, and the "full gates on stop" are driven by host hooks, not by the `next`/`submit` loop. No engine command, input/output schema, or latency path is defined for them. Every host adapter would wire its hooks to pack code differently, and per-edit logic could leak into the host adapter (which AD-14 says "holds no logic").
- **Fix:** Add engine commands to the protocol, for example `archshell-engine check-edit --path`, `guard-write --path`, and `on-stop --feature`, each with a schema'd verdict (`allow` / `block` + `archshell/error/v1`). Hooks become one-line shims. State the NFR-1 budget for `check-edit`, including Python process start-up, and add a latency benchmark to the conformance suite.

**H-2 — Mixed-ownership files (`.csproj`, `Directory.Build.props`) have no rule**
- **Location:** AD-5 ("Jinja2 generates `.csproj`"), AD-6 (".slnx / .csproj refs" derived); PRD FR-14 (only `<ProjectReference>` and gate-affecting build settings are protected; package references stay editable).
- **Problem:** If `.csproj` is regenerated from inputs, developer-added `PackageReference`s and other settings are wiped on growth, reconcile, or upgrade. If it is not regenerated, verify cannot compare it. Packs will choose differently.
- **Fix:** Define element-level ownership for mixed files. ArchShell owns `ProjectReference` items and the gate-affecting properties, emitted in a marked generated region or an imported `*.archshell.props`/`.targets` file. Everything else is the developer's. `verify` compares only the owned elements. Prefer generated imported files (`Directory.Build.props` → `archshell.generated.props`), so ownership is per file, not per region.

**H-3 — EF Core migrations break determinism and regeneration**
- **Location:** AD-5 ("the .NET SDK only executes the toolchain (restore, build, test, `ef migrations add`)"), AD-4; PRD FR-24 (data_migration), NFR-4, FR-41.
- **Problem:** `dotnet ef migrations add` emits timestamp-prefixed migration IDs and a model snapshot. Its output is not byte-reproducible, so it violates AD-4 (no clock), FR-24 ("Scaffolding the same feature plan twice produces byte-identical files"), and verify's regeneration check. No rule says whether migrations are inputs, derived files, or excluded.
- **Fix:** Classify migration files as committed **inputs**, created once and never regenerated or byte-compared (like scaffold-once). Fix the migration name from the plan (slug-based) and record the timestamp in the plan, per AD-4's explicit-input rule. Have `verify` check only that every `data_migration` intent has its migration and that the snapshot builds. Record this as a PRD delta (FR-24).

**H-4 — Attempt state is gitignored and deletable, so the FR-26 bound is not enforced**
- **Location:** AD-1 (state includes attempts and extensions), AD-10 (`state/<slug>.yaml` gitignored, "reconstructible"; Prevents: "agents resetting attempts").
- **Problem:** Attempts, extensions, and pending questions cannot be reconstructed from the inputs, so "reconstructible" is false for exactly the fields AD-10 claims to protect. The in-session hook blocks Write/Edit, but `rm`/`git clean` through Bash resets attempts, and verify cannot see a gitignored file. The AD's stated Prevents is not delivered by its Rule. FR-54's "at most 2 extensions per spec item" leaks the same way unless extensions are counted from the committed plan.
- **Fix:** Split state into (a) committed counters (attempts and extensions per S#, inside the plan or the records, protected), checked by `verify` against the FR-26/FR-54 bounds, and (b) gitignored cursor state (current phase, pending action), which is truly reconstructible from the inputs. Or state explicitly that the attempt bound is a Delegate aid, not a trust control, and drop it from AD-10's Prevents.

**H-5 — `verify` contradicts NFR-5 (no network beyond package restore)**
- **Location:** AD-15 ("verify still runs but reports `trust: unenforced` on every run"), AD-14 ("verify installs exactly those [versions]" from PyPI).
- **Problem:** To report `trust: unenforced` on every run, `verify` must query branch-protection / code-owner settings through the provider API. That needs network access and credentials in CI, which NFR-5 forbids. Installing the pinned core and pack from PyPI is arguably "package restore", but the spine does not say so.
- **Fix:** Make the capability check a setup-time result recorded in a committed, owned file (for example `.archshell/trust.yaml`, written by setup and re-checked by an optional, explicitly networked `archshell doctor`). `verify` reads only that file. State that installing the pinned core and pack counts as package restore, or update NFR-5.

**H-6 — Spine contradicts PRD FR-9/FR-32 ADR naming without recording a PRD delta**
- **Location:** AD-10 ("ADRs are named by slug"; `docs/adr/<slug>.md`); PRD FR-9 (`docs/adr/0001-architecture.md`), FR-32 (`0002-add-notifications-module.md`); memlog line 32.
- **Problem:** The PRD fixes sequential ADR names, and the spine forbids them. The memlog's "PRD updates needed" list omits this one, and the spine does not name the initial ADR. Templates, verify, and the docs will disagree on which name is right.
- **Fix:** Name the initial ADR explicitly (for example `docs/adr/architecture.md`) and add FR-9/FR-32 to the PRD-delta list (see M-13).

### Medium

**M-1 — Silent dimension: security of the plugin itself, beyond the trust anchor**
- **Location:** whole spine; PRD NFR-6.
- **Problem:** The spine says nothing about:
  - **Extension loading trust:** any installed package can register `archshell.*` entry points.
  - **How FR-37's "enabled only after it passes" is recorded.**
  - **Prompt injection:** project files and the backlog flow into `llm_task` prompts.
  - **MSBuild evaluation:** `MSBuildWorkspace` and `dotnet build` in `verify` run project-defined MSBuild targets on fork pull requests in CI.
  - **Credentials:** the provider token handling AD-15 needs.
  - **Supply chain:** hash-pinned installs in CI, PyPI trusted publishing, signed NuGet.
  - **NFR-6:** no AD covers it. "The engine makes no network calls and has no telemetry" is not stated.
- **Fix:** Add a security AD covering:
  - Only allow-listed entry points recorded in `architecture.yaml` (name + version + hash) are loaded.
  - The engine performs no network IO except through an explicit, named port (none in Phase 1), and has no telemetry. This covers NFR-6.
  - The generated CI workflow runs `verify` without secrets on fork PRs.
  - CI installs use hash-pinned lock data.

**M-2 — Silent dimension: install, setup, and environment checks**
- **Location:** AD-14; PRD FR-22, FR-42, NFR-3, FR-53 (bot identity setup).
- **Problem:** The spine omits several setup decisions:
  - How the Claude Code plugin is installed (marketplace / plugin source).
  - How it locates `archshell-engine` (the memlog says PATH; the spine dropped it).
  - The prerequisite check for Python ≥ 3.12 and the .NET 10 SDK, and which unit reports it as an environment error.
  - How CI bootstraps the pinned versions (it must read `architecture.yaml` before installing the version that reads it).
  - Who owns the setup flow for the bot identity, branch protection, and `CODEOWNERS`.
- **Fix:** Add an "Install & setup" AD:
  - Plugin install source.
  - Engine discovery order.
  - A `doctor`/preflight engine command, returning environment errors per FR-22.
  - A CI bootstrap step that reads the pins with a minimal reader.
  - Setup owned by the CiProvider.

**M-3 — Silent dimension: ArchShell's own build, release, and infrastructure**
- **Location:** Structural Seed, AD-14, Stack.
- **Problem:** Not decided:
  - Repository topology (one monorepo with three packages or not).
  - ArchShell's own CI (the cross-OS byte job of AD-4 is mentioned, but not where it runs).
  - The release process and version coupling between core, pack, analyzer (NuGet), and the plugin.
  - Publishing (PyPI trusted publishing, NuGet).
  - Where the golden set and conformance suite run for contributors (FR-37).
- **Fix:** Add a Deployment & release AD or a Deferred entry with an owner and revisit trigger. Include a compatibility matrix rule: pack declares core range, analyzer version pinned by pack release, plugin declares protocol version.

**M-4 — FR-36 pack/provider version check dropped from the spine**
- **Location:** AD-14; memlog line 19 ("pack declares extension contract version, core refuses incompatible").
- **Problem:** The spine keeps only the host's protocol-version check. How packs and providers declare the extension contract version, and where the core refuses them, is gone, so each pack will declare it differently.
- **Fix:** Restore the rule in AD-14: each extension exposes `extension_contract = "archshell/<kind>-ext/v<n>"` on its entry-point object, and the core checks it at load time with a typed error.

**M-5 — Configuration is a silent dimension**
- **Location:** none; PRD FR-4 (thresholds are configuration), FR-38 (extra catalog path), FR-44 (consensus can be off), NFR-7 (retries configurable).
- **Problem:** No file, schema, or protection status is defined for configuration. If the agent can switch consensus off, raise retries, or point the extra catalog elsewhere, it weakens the process undetected. Thresholds also feed determinism (NFR-4).
- **Fix:** Define `.archshell/config.yaml` (schema'd per AD-11, protected per FR-14, changed only through a confirmation flow). Treat it as an input under AD-6. List which keys are developer-tunable.

**M-6 — Protocol transport for `submit` and protocol messages is unspecified**
- **Location:** AD-2 (`archshell-engine submit <result>`), Conventions (stdout reserved); memlog line 9 (stdin/stdout).
- **Problem:** The spine does not say whether results go as an argument, stdin, or a file path. That choice decides Windows command-line limits for large `llm_task` outputs, encoding, and how hosts correlate a result to its action (an action id or nonce, against stale or duplicate submits). Host adapters will diverge.
- **Fix:** Specify:
  - Request and result bodies travel as UTF-8 JSON on stdin.
  - Each action carries an `action_id` and the state revision.
  - `submit` rejects a stale or mismatched id with a typed error.
  - Exit-code semantics.

**M-7 — Consensus isolation (AD-3) is not achievable with the sequential loop as written**
- **Location:** AD-3, AD-1, AD-2.
- **Problem:** The loop returns one action at a time and persists state on each `submit`. Run 2's subagent has file-read tools and could read run 1's persisted result under `.archshell/state/`. "No shared history" is not enforced.
- **Fix:** Have the engine issue the three runs as one batch action (or hold results unpersisted or hashed until all three return), and forbid persisting classification results where subagents can read them before the batch closes. Add a conformance test.

**M-8 — AD-4's lint does not enforce "no IO / no nondeterminism"**
- **Location:** AD-4.
- **Problem:** Banning only `datetime.now`, `random`, and `os.environ` misses `time`, `uuid`, `os.getenv`, `open`, `pathlib`, `subprocess`, `socket`, `secrets`, `hash()`-dependent and set-iteration ordering, and locale-dependent sorting and casing.
- **Fix:** Enforce with an import contract (for example import-linter: `archshell.domain` may import only the stdlib allow-list + `archshell.domain` + `decimal`). Ban iterating sets into output, and require sort keys to be explicit, codepoint-ordered, case-sensitive strings.

**M-9 — NFR-2 macOS and path normalization are not covered**
- **Location:** AD-4 (CI compares bytes on Windows and Linux only), AD-5 normalizer.
- **Problem:** NFR-2 includes macOS, where the filesystem is case-insensitive and may return NFD filenames. The rules also leave open:
  - The path separator in sort keys and in YAML.
  - Unicode normalization of tokens (Spanish input such as "cancelación").
  - Case-collision of generated paths on case-insensitive filesystems.
- **Fix:** Add macOS to the byte-comparison matrix. Specify that the normalizer emits NFC and that paths are POSIX-separated in artifacts and sort keys. Identifiers must be ASCII after normalization (FR-12). Two generated paths differing only by case are a plan error.

**M-10 — Default branch and merge base are undefined, including in no-repo or new-repo states**
- **Location:** AD-13, AD-9; PRD FR-51.
- **Problem:** The spine does not say how the default branch is determined (`origin/HEAD`, config, provider), or what happens with no git repository at bootstrap, no remote, or no merge base (first commit). Pack, engine, and verify will each guess.
- **Fix:** Add a rule:
  - The default branch is read from config (M-5), else from `origin/HEAD`.
  - With no merge base, the baseline is empty: every contract is new and none is breaking.
  - Bootstrap does not require a repository; feature workflows do.

**M-11 — AD-14 "upgrade lands the result in a pull request" contradicts AD-9**
- **Location:** AD-14, AD-9.
- **Problem:** The engine may not commit or check out, yet `upgrade` is said to land a PR. It is unclear who creates the branch and PR.
- **Fix:** Reword: `upgrade` writes the regenerated files to the working tree and emits instructions. The agent or developer commits and opens the PR. It must also respect C-1 (no scaffold-once overwrite).

**M-12 — Command surface naming: `archshell` versus `archshell-engine`**
- **Location:** Paradigm table (engine CLI: `next`, `submit`, `verify`, `reconcile`, `upgrade`), AD-6/AD-14 (`archshell reconcile`, `archshell upgrade`), Conventions (`archshell.hosts` entry point while the Claude Code adapter is a non-Python plugin with "no logic").
- **Problem:** The spine does not say whether there are two executables or one, and docs, CI templates, and hooks will name them differently. An `archshell.hosts` entry point is meaningless for a host that is not a Python package.
- **Fix:** Fix the executable set explicitly: `archshell-engine` (protocol) and `archshell` (human/CI: `verify`, `reconcile`, `upgrade`, `doctor`), or one binary. State what the `archshell.hosts` entry point is for, or drop it for Phase 1.

**M-13 — No Open Questions section; the PRD-delta list exists only in the memlog**
- **Location:** spine structure; memlog line 32.
- **Problem:** The rubric needs every dimension decided, deferred, or open. The spine itself records no open questions. Its PRD deltas live only in the memlog: the `upgrade`/`reconcile` commands, CiProvider as an extension type, `trust: unenforced`, the data-based FR-51 diff, per-term vocabulary files, the glossary, and (missing even from the memlog) the ADR naming and the scaffold/migration ownership. Downstream readers of the spine will not see them.
- **Fix:** Add an `## Open Questions / PRD deltas` section listing each delta with an owner and a revisit trigger.

### Low

**L-1 — Deferred "field-level schema shapes" carries an ordering dependency**
- **Location:** Deferred.
- **Problem:** The plan, record, mapping-table, and inflection-table schemas are consumed by the pack and by verify. If the pack is built in parallel before the core lands them, the units diverge.
- **Fix:** Add to the Deferred entry: "the core lands each schema before any consumer implements against it; mapping tables and inflection tables are core-owned schemas filled by packs."

**L-2 — Stack versions not current**
- **Location:** Stack; memlog line 31.
- **Problem:** As of 2026-11-08:
  - tree-sitter (py) is **0.26.0** (2026-06-30), not 0.25.2.
  - tree-sitter-c-sharp is **0.23.5** (2026-04-14), not 0.23.1.
  - Microsoft.CodeAnalysis.Workspaces.MSBuild is resolvable today (**5.9.0**), so the "unconfirmed" deferral is no longer needed.
  - Jinja2 3.1.6 is still current.
  - Grammar/ABI compatibility between the two tree-sitter packages is not recorded.
- **Fix:** Update the versions, record a compatible pair, and pin Roslyn 5.x. Mention MSBuildLocator for locating SDK 10.

**L-3 — "NetArchTest / ArchUnitNET" is a choice left open**
- **Location:** Stack; PRD FR-18.
- **Problem:** Two libraries give two styles of generated tests across 21 templates. NetArchTest.Rules has had no release since 1.3.2 (2021). ArchUnitNET (TngTech) is maintained.
- **Fix:** Pick one (recommend ArchUnitNET) for all templates, pinned per pack release.

**L-4 — Policy file runtime form undefined**
- **Location:** AD-10 (`.archshell/policies.yaml`, derived); PRD FR-60.
- **Problem:** It is undecided whether the .NET pipeline reads YAML from a hidden folder at runtime or consumes generated C#. Templates will differ.
- **Fix:** State that the pack generates a C# policy registry (class a, protected) from `policies.yaml`, and that runtime never reads `.archshell/`.

**L-5 — FR coverage gaps with no stated home**
- **Location:** Capability map.
- **Problem:** Not mapped:
  - FR-42 (new-project check: which unit, and the rule for an existing `.git`/README).
  - FR-17 (gap report location and schema).
  - FR-19 (contract test tooling).
  - FR-38 (extra catalog path).
  - FR-39 (how the engine learns and stores the conversation language, and where message catalogs live: no `messages/` in the Structural Seed).
  - FR-53 (source of "the developer who confirmed it").
  - FR-21 (stop-time gates; see H-1).
- **Fix:** Add rows or one-line conventions for each.

**L-6 — Smaller rule-precision issues**
- **Location:** AD-12, AD-2, AD-10, AD-8.
- **Problem:**
  - AD-12 "never approves what Roslyn would reject" is untestable as phrased.
  - AD-2 "all user-facing text comes from the engine" cannot be guaranteed when the agent relays `ask`.
  - AD-10 duplicate-slug suffixing cannot prevent the same slug being created on two branches at once.
  - AD-8 does not say whether `bin/` and `obj/` produced during bootstrap build/test are rolled back.
- **Fix:**
  - AD-12: reword to "a per-edit pass is never recorded as a gate pass; only the full gates mark a task done".
  - AD-2: require hosts to render `ask`/`confirm` through the native question UI with engine-provided text verbatim, checked by host conformance.
  - AD-7: add cross-branch slug collisions to the "first merged wins" cases.
  - AD-8: state that bootstrap rollback deletes the whole target tree created since the journal began.

## What is solid

- AD-1/AD-2 (stateless engine, closed versioned action protocol) is the right spine for multi-host extensibility, and it resolves PRD OQ1's resume bullet.
- AD-6/AD-7 cleanly resolve the PRD's concurrent-branch open question for fully derived files.
- AD-11 (core-owned schemas, restricted YAML, a single canonical emitter, a versioning and migration rule) is enforceable and prevents format drift.
- AD-13 (breaking changes from contract data rather than compiled code) is deterministic and well reasoned.
- AD-15's capability-check intent (never claim a trust anchor the platform cannot enforce) is right; only its runtime placement conflicts (H-5).
- .NET 10 LTS and `.slnx` as the default are verified current.
