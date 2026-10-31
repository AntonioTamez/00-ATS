# Hexagonal Architecture – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-ARQUITECTURA-HEXAGONAL.md` | Step-by-step guide (Spanish) to build a hexagonal project from scratch |
| `docs/HEXAGONAL-VS-CLEAN.md` | Differences, when to use each, and when to combine them (Spanish) |
| `sample/` | Reference .NET 10 solution (Library): one core, two driving adapters (REST, CLI), two driven repositories; `dotnet test` runs 28 tests |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Run the sample: `cd sample && dotnet test`, `dotnet run --project src/Library.Adapters.Rest`, or
`dotnet run --project src/Library.Adapters.Cli -- add "Clean Code"` (set `LIBRARY_FILE` to choose where data persists).
