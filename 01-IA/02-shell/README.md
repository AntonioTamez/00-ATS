# ArchShell – architecture-driven AI development shell (design)

> Status: **design draft**. Working name: `archshell`. No code yet.
> Product requirements: [`_bmad-output/initiative-archshell/prd-archshell/prd-archshell.md`](../../_bmad-output/initiative-archshell/prd-archshell/prd-archshell.md).
> Where this README and the PRD disagree, the PRD wins.

ArchShell is a harness that orchestrates agents, skills, and plugins so that AI-generated
code follows an explicit, enforced architecture. The LLM writes code; **templates, contracts,
the compiler, and architecture tests decide whether that code is accepted**.

## 1. Principles

1. **Deterministic process, not deterministic output.** Two runs may produce different code, but
   both pass through the same steps and the same blocking gates.
2. **Architecture is code.** Every rule that matters lives in `architecture.yaml` and becomes a
   failing test when violated. Prose alone is never an enforcement mechanism.
3. **Ask, don't guess.** When the request does not identify the pattern clearly, the shell asks
   discriminating questions, one at a time.
4. **Decisions are written down.** The chosen architecture is recorded once (ADR + charter) and
   every later feature reads it before generating anything.
5. **The pattern is immutable; the charter only grows.** The chosen pattern and its rules never change
   for the life of the project. The charter can only grow within the pattern (new modules or
   contracts), after a human approves it; each growth is recorded in a new, additive ADR, and the
   yaml and the tests are then regenerated.

## 2. Shell architecture (hexagonal)

The shell itself is hexagonal, so phase 2 adds a new driving adapter instead of a rewrite.

```
        ┌──────────────────────── driving adapters ────────────────────────┐
        │ Host adapters (Phase 1)                         │ CLI (Phase 2)  │
        │ claude-code (launch) · codex · gemini · opencode │                │
        └────────────────────────────┬─────────────────────────────────────┘
                                     ▼  engine JSON interface (stdin/stdout)
 ┌──────────────────── core engine (Python, language-agnostic) ────────────────────┐
 │ PatternCatalog  PatternSelector  Elicitor  CharterWriter                        │
 │ Scaffolder      ArchTestGenerator  GateRunner  FeatureWorkflow                  │
 └───────┬──────────────────────┬─────────────────────────────┬────────────────────┘
         ▼                      ▼                             ▼
   LlmProvider port      Filesystem/Git port           LanguagePack port
   anthropic (launch)    (charter, ADRs)                      │
   openai · google ·                              ┌───────────┼─────────────┐
   ollama (future)
                                                  ▼           ▼             ▼
                                            dotnet-csharp   (java)   (typescript) ...
                                            (launch pack)   future packs
```

The core never contains language-specific code. Everything that depends on a language lives in a
**language pack**, so packs can be added or changed without touching the core.

### Language pack contract

A language pack implements the `LanguagePack` port:

| Capability | .NET/C# pack (launch) |
|---|---|
| `scaffold(pattern, charter)` | `dotnet new` templates per pattern (solution, projects, folders) |
| `wire_dependencies(charter)` | `.csproj` `ProjectReference`s that match `may_depend_on` |
| `generate_arch_tests(charter)` | NetArchTest / ArchUnitNET test project |
| `generate_contract_tests(charter)` | OpenAPI conformance tests |
| `gates()` | `dotnet build -warnaserror`, `dotnet test` (architecture, contract, unit) |
| `naming(rule)` | C# naming conventions (PascalCase types, `I` prefix for interfaces) |
| `supported_patterns()` | Patterns with a template in this pack |

Packs are discovered as Python entry points (`archshell.language_packs`). Each pack declares the
contract version it implements, and the core refuses packs with an incompatible version.
`architecture.yaml` stays language-neutral except for its `language:` field.

### Host and model contracts

The core knows neither the agent runtime it runs inside nor the model vendor. Two separate extension
points keep it that way:

| Extension point | What varies | Launch | Future |
|---|---|---|---|
| **Host adapter** | Where the shell runs: plugin format, skill/agent files, hook events, question UI | `claude-code` | `codex`, `gemini-cli`, `opencode` |
| **`LlmProvider`** | Which model API the CLI calls directly | `anthropic` | `openai`, `google`, `ollama` |

**Host adapter.** All workflow logic lives in the Python engine and is exposed through a stable,
versioned JSON interface (`archshell-engine <command>`, JSON in on stdin, JSON out on stdout). A host
adapter is a thin translation layer:
- It renders host-native skills, agents, and hooks from core-owned prompt templates. Prompts are
  owned by the core and written once; adapters only change the packaging.
- It maps host events (for example `PostToolUse`, `Stop`) to engine commands (`verify`, `gate`).
- It maps the engine's `ask_user` result to the host's question UI.

**`LlmProvider` port.** It is used when the engine itself needs a model (the Phase 2 CLI, or a host
without a native agent). It is implemented with three capabilities:

```python
class LlmProvider(Protocol):
    contract_version: str
    def complete_structured(self, prompt: Prompt, schema: JsonSchema) -> dict: ...
    def capabilities(self) -> ProviderCapabilities: ...   # context window, native JSON mode, tools
```

**Rules that keep the core vendor-neutral:**
1. Every LLM output is validated against a JSON Schema owned by the core. Invalid output gets a
   bounded retry, then becomes an error. It is never repaired by guessing.
2. Vendor-specific prompting (JSON mode, tool calling, system prompt placement) lives in the adapter,
   never in the core.
3. **Conformance gate:** a new provider or host is enabled only after it passes the D3 golden set and
   the engine contract tests with the same thresholds as the launch pair. Switching models changes
   extraction quality, so the golden set is what proves equivalence.
4. Adapters and providers are discovered as Python entry points (`archshell.hosts`,
   `archshell.providers`) and declare the contract version they implement.

| Component | Responsibility |
|---|---|
| `PatternCatalog` | Loads one **pattern card** per pattern, built from `02-ARQUITECTURA/*` |
| `PatternSelector` | Extracts drivers from the request and scores the cards; returns candidates plus confidence |
| `Elicitor` | When confidence is low or the top candidates tie, picks the question that best separates them |
| `CharterWriter` | Writes the ADR, `ARCHITECTURE.md`, and `architecture.yaml` |
| `Scaffolder` | Delegates to the active language pack's templates (the LLM never decides structure) |
| `ArchTestGenerator` | Delegates to the language pack to generate architecture tests from `architecture.yaml` |
| `GateRunner` | Runs the language pack's gates (build, architecture, contract, unit); any failure blocks |
| `FeatureWorkflow` | Runs the per-feature flow (section 5) |

## 3. Pattern selection

### Pattern card

```yaml
pattern: cqrs
provenance: 02-ARQUITECTURA/CQRS   # authoring source only; never read at runtime
signals: [reads far exceed writes, distinct read models, heavy reporting]
anti_signals: [simple CRUD, small team, thin domain]
combines_with: [ddd, event-sourcing, clean]
excludes: []
discriminators:
  - "Do reads need a different shape than the data being written?"
```

**`02-ARQUITECTURA` is an authoring source, not a runtime dependency.** When the shell is built,
its guides are distilled into pattern cards (shipped in `archshell-core/catalog/`), and its .NET
samples become the `dotnet-csharp` pack templates and golden-set cases. At runtime the shell reads
only what ships inside its plugins, so it works in any repository. Teams can add their own cards
through an optional, configured extra catalog path.

Launch catalog (authored from `02-ARQUITECTURA`): Layered, MVC-MVVM, Clean, Clean-Screaming, Screaming, Hexagonal,
Onion, DDD, CQRS, Event-Sourcing, Event-Driven, Vertical-Slice, Modular-Monolith, Microkernel,
Pipes-and-Filters, Microservices, Serverless, Frontend.

### Algorithm

1. Extract drivers from the request: domain complexity, scale, audit needs, read/write asymmetry,
   deployment model, team size, and integrations.
2. Score every card (signals add, anti-signals subtract).
3. If the top score is under the confidence threshold, or the gap to the runner-up is small, the
   `Elicitor` asks the discriminator that best splits the top candidates. It asks **one question,
   then stops** and repeats the loop with the answer.
4. Resolve compatible combinations (for example *Modular-Monolith + Vertical-Slice per module*, or
   *Clean + Screaming + CQRS*) using `combines_with`/`excludes`.
5. The user confirms the proposal before anything is written.

### 3.1 Decision policy (D3)

**Determinism boundary.** The LLM only extracts drivers into a closed schema; it never scores
patterns. Unknown information must be recorded as `unknown`, never guessed. Python scores the
drivers against the pattern cards, so the same drivers always produce the same score.

```yaml
domain_complexity: high        # low | medium | high | unknown
read_write_asymmetry: unknown
audit_trail: required
deployment: single_unit
```

**Auto-proposal** requires **all** of the following:

| Condition | Initial value | Prevents |
|---|---|---|
| Top candidate score | >= 0.75 | Choosing without enough evidence |
| Margin over runner-up | >= 0.15 | Ties disguised as certainty |
| Critical drivers still `unknown` | 0 | Deciding on gaps |
| Combination validity | no `excludes` conflict | Incoherent pattern mixes |

**Question selection.** The next question targets the `unknown` driver with the highest information
gain: the one where the top candidates differ the most. Questions about drivers that do not separate
the candidates are never asked.

**Budget and fallback.**
- At most 5 questions per selection.
- If the budget runs out below threshold, the shell never chooses on its own: it presents the top two
  candidates with their tradeoffs, and the user decides. The ADR records this as a human decision.
- Even above threshold, the user confirms the proposal before the charter is written.

**Calibration.** The values above are initial. They are tuned against a golden set of 15–20
requests with expected patterns (derived from `02-ARQUITECTURA/*/sample`). The evaluation tracks
correct selections, unnecessary questions, and wrong selections. Wrong selections are the costly
error, so tuning is biased toward asking.

## 4. The architecture charter (persisted)

| Artifact | Audience | Content |
|---|---|---|
| `docs/adr/0001-architecture.md` | Humans | Chosen pattern(s), drivers, rejected alternatives and why |
| `ARCHITECTURE.md` | Humans + LLM | Layers, responsibilities, rules in prose, examples |
| `architecture.yaml` | Gates (source of truth) | Layers, allowed dependencies, naming, port and contract locations |
| `tests/Architecture.Tests/` | Compiler / CI | Tests generated from `architecture.yaml`; never hand-edited |

```yaml
# architecture.yaml (sketch)
version: 1
language: dotnet-csharp
adr: docs/adr/0001-architecture.md
patterns: [clean, screaming]
modules: [Appointments, Billing, Patients]          # screaming: top level = business capability
layers:
  - { name: Domain,      project: "{Module}.Domain",      may_depend_on: [] }
  - { name: Application, project: "{Module}.Application", may_depend_on: [Domain] }
  - { name: Adapters,    project: "{Module}.Adapters",    may_depend_on: [Application, Domain] }
rules:
  - id: ports-live-in-application
    interfaces_matching: "I*Repository|I*Gateway"
    must_reside_in: Application
  - id: no-cross-module-internals
    modules_may_reference_only: [SharedKernel, "{Other}.Contracts"]
naming:
  use_case: "{Verb}{Noun}Handler"
contracts:
  http: contracts/openapi.yaml
```

Tamper protection: changes to `architecture.yaml`, the generated tests, `<ProjectReference>`s, gate
build settings (for example `Directory.Build.props`), test project inclusion, or hook configuration
that do not come from an approved charter growth are blocked. The hooks catch them early in the
session; `archshell verify` in CI is the authoritative check, because the agent cannot reach it.
`CODEOWNERS` assigns the charter files to human owners, and the agent works under its own GitHub
identity without admin rights, so a human always approves charter changes.

Enforcement layers, from strongest to weakest:
1. **Project references**: a forbidden dependency does not compile.
2. **Generated architecture tests**: naming, port location, and cross-module rules.
3. **Contract tests**: implementations conform to the published OpenAPI/JSON Schema.
4. **Prose in `ARCHITECTURE.md`**: guidance for the LLM only, never the last line of defense.

## 5. Per-feature workflow

```
read charter → spec (S1..Sn) → ask if gaps → scaffold from pattern templates
→ define ports/contracts → RED test → LLM implements → gates (build, arch, contract, unit)
→ fail = reject and retry (max 5 attempts per spec item) | pass = done
```

- **Gate timing:** fast checks (dependency and naming rules) run after every edit; the full gates
  run when the agent reports a task done and whenever the session stops.
- **Charter growth:** if a feature needs a module or contract the charter does not declare, the
  shell proposes the growth and applies it only after the developer confirms (and, in CI, after the
  charter owners approve the pull request).
- **Misfit:** if a feature only fits by changing the pattern, the shell stops after the 5th attempt
  with a report (failing rule, what the agent tried, best guess why). It never changes or relaxes a
  rule; the developer reshapes the feature.

## 6. Phases

### Phase 1 – Claude Code plugin

```
archshell-core/                 # Python engine: host-, vendor-, and language-agnostic
  engine/                       # scoring, D3 policy, charter, workflow, ports
  prompts/                      # core-owned prompt templates (select, elicit, feature, grow)
  schemas/                      # JSON Schemas for every LLM output and engine command
  catalog/                      # pattern cards
  golden/                       # D3 golden set + conformance tests

archshell-claude-code/          # host adapter (launch)
  .claude-plugin/plugin.json
  skills/                       # rendered from prompts/: arch-select, arch-feature, arch-grow
  agents/                       # arch-elicitor, arch-verifier
  hooks/hooks.json              # PostToolUse -> fast checks; Stop -> full gates; block on failure

archshell-dotnet/               # language pack plugin: .NET / C#
  .claude-plugin/plugin.json
  pack/                         # Python LanguagePack implementation (entry point)
  templates/                    # dotnet new templates: 18 patterns + 3 combinations
  skills/dotnet-conventions/SKILL.md
```

Phase 1 also ships `archshell verify`: a deterministic CI check (no model) that runs on every push
and pull request. ArchShell generates the CI workflow and `CODEOWNERS` file for the project.

Launch templates cover the 18 catalog patterns plus three combinations: Clean + Screaming,
Clean + Screaming + CQRS, and Modular Monolith + Vertical Slice. Any other combination stops before
scaffolding with a gap report.

Installing a language means installing its pack plugin, and supporting a new agent runtime means
adding a host adapter. In both cases the core does not change.

Exit criteria:
- Given three sample requests, it selects the expected pattern(s) or asks a discriminating question.
- It writes the ADR, `ARCHITECTURE.md`, `architecture.yaml`, and generated architecture tests.
- The gates detect 100% of a seeded-violation corpus (features written on purpose to break each
  rule type) for every shipped template.

### Phase 2 – Standalone CLI

- A new driving adapter (`archshell select | feature | grow`) over the **same core engine**.
  `archshell verify` already ships in Phase 1.
- The engine calls a model directly through an `LlmProvider` plugin (`anthropic` at launch), selected
  in configuration (`archshell --provider openai`).

## 7. Decisions

| ID | Decision | Status |
|---|---|---|
| D1 | Core engine language | **Decided**: Python |
| D2 | Target stacks at launch | **Decided**: .NET/C# only (.NET 10 LTS or later), via the `dotnet-csharp` language pack; other languages are added as new packs |
| D3 | Confidence threshold and question budget | **Decided**: see section 3.1 (initial values, calibrated with a golden set) |
| D4 | Language-specific code location | **Decided**: one plugin per language; the core stays language-agnostic |
| D5 | Agent runtime and model vendor | **Decided**: host adapters and `LlmProvider` plugins; Claude Code + Anthropic at launch, with Codex, Gemini, and others added without core changes |
| D6 | Changing the architecture | **Decided**: the pattern and its rules are immutable; the charter only grows within the pattern, with human approval |
| D7 | Trust anchor | **Decided**: `archshell verify` in CI ships in Phase 1, with `CODEOWNERS` and a separate agent GitHub identity |
| D8 | Project scope | **Decided**: new (greenfield) projects only |
| D9 | Distribution | **Decided**: open source; the community contributes packs, adapters, and providers |
| D10 | Language | **Decided**: the conversation follows the user (English or Spanish); all generated code and docs are in English |
