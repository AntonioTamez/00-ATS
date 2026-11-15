# Reconciliation: README (ArchShell design draft) vs PRD + addendum

Source: `01-IA/02-shell/README.md`. Excluded on purpose (known changes): pattern immutability / Charter Growth replacing principle 5; greenfield only; Phase 2 out of MVP; English artifacts with EN/ES conversation.

## Gaps in PRD

1. **Self-contained runtime (README §3, "authoring source").** `02-ARQUITECTURA` is an authoring source only; pattern cards ship inside the plugins, and the shell reads only what it ships, so it works in any repository. The PRD has no requirement saying the shell must not depend on external folders at runtime.
2. **Phase 1 exit criteria (README §6).** Three sample requests select the expected pattern or ask a discriminating question; all charter artifacts plus tests are written; a feature that deliberately breaks layer rules is rejected. The PRD has no launch acceptance criteria. Success Metrics cover only the golden set and zero deviations.
3. **Tie trigger for elicitation (README §3 algorithm step 3, §2 Elicitor).** The README asks questions when "the gap to the runner-up is small" or the candidates tie. FR-3 points only to the FR-4 criteria. That covers the tie case through the margin rule, but "critical driver" is never defined. Which drivers are critical needs a definition or an owner.
4. **Calibration tracking (README §3.1 Calibration).** The evaluation tracks correct selections, unnecessary questions and wrong selections, and tuning is "biased toward asking". The PRD has these as metrics but no requirement that the golden-set evaluation is runnable or reported. FR-37 only implies it.
5. **Generated tests regenerated, never hand-edited, plus a CI angle (README §4 table).** "Compiler / CI" is the audience for `tests/Architecture.Tests/`. Nothing in the PRD says the gates must run in a developer's own CI in v1. CI mode is out of scope, but the expectation that generated tests run in plain `dotnet test` is not stated.
6. **Prompts owned by the core (README §2, Host adapter).** FR-34 mentions it. There is no FR saying prompts are written once and shared across hosts (skills: arch-select, arch-feature, arch-amend; agents: arch-elicitor, arch-verifier).

## Should move to addendum

The addendum holds only the competitive digest. None of the README's technical design was moved into it:

- Hexagonal shell diagram and the core component table: PatternCatalog, PatternSelector, Elicitor, CharterWriter, Scaffolder, ArchTestGenerator, GateRunner, FeatureWorkflow (README §2).
- Engine JSON interface: `archshell-engine <command>`, JSON on stdin and stdout, versioned (README §2).
- Host adapter mechanics: rendering from core prompt templates, mapping `PostToolUse`/`Stop` to `verify`/`gate`, mapping `ask_user` to the question UI (README §2).
- The `LlmProvider` Protocol code (`contract_version`, `complete_structured`, `capabilities`) (README §2).
- Vendor-neutral rule 2: JSON mode, tool calling and system-prompt placement live in the adapter (README §2).
- Entry-point groups: `archshell.language_packs`, `archshell.hosts`, `archshell.providers` (README §2).
- LanguagePack capability table: `scaffold`, `wire_dependencies`, `generate_arch_tests`, `generate_contract_tests`, `gates`, `naming`, `supported_patterns`, with their .NET mappings (`dotnet new`, `ProjectReference`, NetArchTest/ArchUnitNET, OpenAPI, `-warnaserror`, PascalCase / `I` prefix) (README §2).
- Pattern card YAML schema (README §3), the driver YAML example and the D3 threshold table with the "Prevents" column (README §3.1).
- `architecture.yaml` sketch: layers, `may_depend_on`, rule ids, naming, contracts (README §4).
- Phase 1 folder layout: archshell-core, archshell-claude-code, archshell-dotnet, and the `hooks.json` behavior (README §6).
- Decisions D1–D5, for example D1 Python core (README §7).
- Launch catalog list of the 18 patterns (README §3). The PRD says "18" but never names them.

## Contradictions

1. **Per-edit hook vs full gate (README §6 hooks.json vs FR-20/FR-21).** In the README, PostToolUse/Stop run `archshell-engine gate` (full gates) and block on failure. The PRD splits this into fast checks per edit and full gates at task completion. The README layout needs to change, or the split needs to be recorded in the addendum.
2. **Template coverage (README §2 `supported_patterns()` vs FR-15).** The README implies a pack may support only some patterns. FR-15 requires templates for all 18 patterns at launch, yet FR-17 still expects missing templates. These need reconciling: FR-17 then applies only to combinations or community packs.
3. **Conformance baseline (README §2 rule 3 vs FR-37).** The README compares against the "launch pair" (host + provider). The PRD compares against the launch pack, adapter and provider, and adds language packs to the conformance gate. This is an extension, but the golden set measures selection, not pack quality. The PRD needs to say which suite applies to packs.
4. **Phase 2 command name (README §6 `amend` vs PRD `grow`).** This follows from Charter Growth, but the README skill `arch-amend` and the prompt `amend` are now stale.
5. **Misfit handling (README §5 "proposes a superseding ADR").** It conflicts with FR-28. This is already known through Open Question 3; it is listed here only because §5 is a second spot to update, besides principle 5.

## Dropped qualitative ideas

1. **Tagline (README intro):** "The LLM writes code; templates, contracts, the compiler, and architecture tests decide whether that code is accepted." It is the clearest statement of the product's stance, and the PRD Vision does not keep it.
2. **Principle 2, "Architecture is code" (README §1):** "Prose alone is never an enforcement mechanism." FR-11 and the enforcement-strength line touch on it, but it is not stated as a principle.
3. **Principle 3, "Ask, don't guess" (README §1),** as a general stance beyond pattern selection. It also covers feature specs and unknown drivers.
4. **Principle 1, "Deterministic process, not deterministic output" (README §1).** It survives only as a Non-Goal and in the 4.5 description. It deserves a principles section.
5. **"It never relaxes a rule silently" (README §5)** as a tone commitment. Transparency about stops is implied but never named.
6. **Extensibility as the shell's own hexagonal design, "add a driving adapter instead of a rewrite" (README §2).** It is the dogfooding narrative: the shell follows the architecture it enforces.
7. **"Invalid output is never repaired by guessing" (README §2 rule 1).** FR-1 and FR-35 cover the retry but drop the "never repair" wording.
