# ats:code-review

Revisión de código **determinista** para un PR (GitHub / Azure DevOps), commit, rango, diff local o
`.patch`. Un motor Python (solo librería estándar) hace todo lo que se puede hacer sin IA; la IA se
usa en lentes acotadas y **cada hallazgo se verifica contra el diff** antes de aceptarlo.

## Qué es determinista y qué no

| Etapa | Determinista | Cómo |
|---|---|---|
| Resolver la fuente (PR, commit, rango, staged, working, pending, patch) | Sí | git/`gh`; sin fuente -> infiere o **pregunta** |
| Parseo del diff, detección de stack, ignorados | Sí | parser propio, tablas en `engine/detector.py`, `ignore_paths` |
| Reglas (85) | Sí | JSON en `rules/`; cada regla trae ejemplos que `validate` ejecuta como tests |
| Analizadores estructurales | Sí | tests faltantes, lockfile, tamaño, binarios, PR sin descripción |
| Linters externos | Sí (si existen) | ruff, eslint, shellcheck, hadolint, tflint; solo sobre líneas añadidas |
| Alcance, riesgo y *chunking* de IA | Sí | ranking por churn/ruta/tecnología, orden por ruta |
| Validación de salidas de IA | Sí | schema + archivo/línea/evidencia verificables; el resto se descarta |
| Dedupe, orden, IDs, **gate**, reporte | Sí | misma entrada + mismas respuestas de IA => mismo `fingerprint` |
| Juicio de las lentes de IA | **No** | acotado: prompts fijos, JSON con schema, severidad máx. `high`, confianza mínima |

Con `--no-ai` el resultado es reproducible al 100 %: dos ejecuciones dan el mismo `fingerprint`.

## Flujo

```
review.py run ──► fuente ─► diff ─► reglas+analizadores+linters ─► plan de IA ─┐
   │  (needs_input: el skill pregunta con AskUserQuestion y reejecuta)         │
   │                                                                           ▼
   │                                              status=awaiting_agents (requests JSON)
   │                                                  Claude lanza 1 Agent por request
   ▼                                                                           │
review.py resume ◄───────── agentes escriben work/ai/results/*.json ◄──────────┘
   └─► valida (schema + evidencia) ─► reintenta inválidos (máx. 2) ─► gate ─► report.md/json
```

Cuándo pregunta (siempre vía `needs_input`, nunca supone): fuente ambigua (cambios locales **y**
commits sin publicar), stack sin reglas, diff grande (> 40 archivos o 2500 líneas) y fallback de
Azure DevOps. Antes de publicar en un PR el skill hace dry-run y pide confirmación.

## Salida

Raíz fija: `<repo>/.code-review/reviews/` (con su propio `.gitignore`). Cada revisión va en su
carpeta `<tipo>-<id>-<AAAAMMDD>`; si ya existe se crea `-2`, `-3`... (la revisión previa nunca se toca).
Ejemplos: `pr-123-20261004`, `commit-1d2e75f1-20261004-2`, `working-main-20261004`.

```
report.md            reporte para humanos (es/en)
report.json          mismo contenido, estructurado (CI): verdict, counts, findings, fingerprint...
state.json           estado del flujo (reanudable)
work/                diff.patch, config.json, deterministic.json, ai/requests|results|accepted
```

Veredicto (umbrales en `config/default.json -> gate`, evaluados de mayor a menor severidad):
`BLOCKED` (>=1 blocker) · `CHANGES_REQUESTED` (>=1 high ó >=6 medium) · `PASS_WITH_WARNINGS`
(>=1 medium ó low) · `PASS`. `--exit-code`: 0 / 1 / 2.
Cuando algo no pudo revisarse (agente fallido, archivos fuera de alcance, linter roto) el reporte
dice **REVISIÓN INCOMPLETA** y por qué.

## Configuración y extensión

Capas (la última gana): `config/default.json` < `<repo>/.code-review/config.json` < `--config`.
Claves: `language`, `ignore_paths`, `disable_rules`, `severity_overrides`, `gate`, `analyzers`,
`linters`, `ai` (lentes, límites, `min_confidence`), `rule_dirs`.

**Suprimir un falso positivo** en el propio código: `// code-review: ignore NET-SEC-004 motivo`
(en la línea o en la anterior; `all` para todas). Queda listado en el reporte.

**Reglas nuevas** (equipo o stack) = un JSON, sin tocar el motor:
`rules/<pack>.json` o `<repo>/.code-review/rules/*.json`

```json
{"pack": "team", "ai_hints": "qué debe vigilar la IA en este stack",
 "rules": [{
   "id": "TEAM-SEC-001", "severity": "medium", "category": "security", "confidence": "high",
   "stacks": ["python"], "pattern": "forbidden_call\\(",
   "exclude_pattern": "...", "paths": ["**/*.py"], "exclude_paths": ["**/tests/**"],
   "title": {"es": "...", "en": "..."}, "message": {"es": "...", "en": "..."},
   "examples": {"match": ["forbidden_call(1)"], "nomatch": ["allowed_call(1)"]}}]}
```
`kind: "path"` + `path_patterns` para reglas sobre nombres de archivo. Una regla sin
`examples.match` **y** `examples.nomatch` se rechaza. Comprueba con `python review.py validate`.
Stacks: dotnet, python, typescript(js), terraform, docker, kubernetes, shell, powershell,
github-actions, sql (+ reglas comunes de secretos/conflictos). Los demás pasan por las comunes (+IA).

## Instalación

```
powershell -File scripts\install.ps1      # enlaza como ~\.claude\skills\ats\skills\code-review
powershell -File scripts\install.ps1 -Remove
```
Se invoca como `/ats:code-review`. Pruebas: `python -m unittest discover -s tests` (50+ tests).

## Límites conocidos

- **Azure DevOps**: sin API; requiere clon local y usa `git fetch <remote> refs/pull/N/merge`. Si la
  ref no existe (PR con conflictos, permisos) pregunta por una alternativa. Probado con un remoto
  simulado, no contra un servidor Azure real. No se puede publicar comentarios allí.
- **GitHub**: `gh pr diff` rechaza PRs enormes (> 20k líneas); usa `--base/--head` en un clon.
- Linters se ejecutan solo si están instalados **y** el código revisado es el que está en disco
  (si no, se reporta "omitido"). `dotnet format`/gitleaks no están integrados (las reglas cubren
  secretos comunes).
- La IA ve el diff con contexto (`diff.context_lines`, 5), no el repositorio completo; por eso se
  le exige evidencia verbatim y se descartan hallazgos que dependen de código no visible.
- Reglas por regex sobre líneas añadidas: no analizan semántica multi-línea (para eso está la IA).
