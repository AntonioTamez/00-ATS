# Vertical Slice Architecture – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-VERTICAL-SLICE.md` | Step-by-step guide (Spanish) to build a Vertical Slice project from scratch |
| `docs/VERTICAL-SLICE-VS-CAPAS.md` | Vertical Slice vs Clean / Hexagonal / Onion: differences, when to use each, when to combine (Spanish) |
| `sample/` | Reference .NET 10 solution (Helpdesk): five slices in one project, EF Core in-memory; `dotnet test` runs 36 tests |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Run the sample: `cd sample && dotnet test` or `dotnet run --project src/Helpdesk.Api`.
