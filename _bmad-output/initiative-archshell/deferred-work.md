- source_plan: `_bmad-output/initiative-archshell/epic-platform-baseline/story-tracer-repository-skeleton-with-engine-plugin-and-ci-on-thre-plan.md`
  summary: On the developer's Windows machine, Application Control blocks the unsigned uv launcher `archshell-engine.exe`, so `/archshell:ping` cannot run locally.
  evidence: Implementer could not run uv launchers or the analyzer build output; ping.md invokes `archshell-engine` from PATH. Settled by an allow rule, signed launchers, or a plugin fallback such as `python -m archshell.engine`.
- source_plan: `_bmad-output/initiative-archshell/epic-platform-baseline/story-tracer-repository-skeleton-with-engine-plugin-and-ci-on-thre-plan.md`
  summary: `arch-shell` has no `.gitattributes`, so Windows checkouts with `core.autocrlf` convert files to CRLF, against the spine's LF rule.
  evidence: git warns "LF will be replaced by CRLF" on every file in the tracer diff; the cross-OS byte comparison in story 1.6 needs `* text=auto eol=lf`.
- source_plan: `_bmad-output/initiative-archshell/epic-platform-baseline/story-claude-code-adapter-loop-for-every-action-type-plan.md`
  summary: Engine prompt files under `.archshell/state/prompts/` are never deleted.
  evidence: Each rerun of a flow creates a new uuid file; real workflows need a cleanup rule (e.g. remove on accepted submit or on terminal action).
- source_plan: `_bmad-output/initiative-archshell/epic-platform-baseline/story-schema-validation-of-model-outputs-with-bounded-retries-plan.md`
  summary: The `archshell-core` wheel installs a top-level `schemas` package into site-packages, which can collide with any other distribution of that name.
  evidence: `pyproject.toml` sets `module-name = ["archshell", "schemas"]`, `namespace = true` to keep AD-11's `archshell-core/schemas/` path; moving them to `archshell/schemas/` (package data) removes the collision but changes the documented layout.
