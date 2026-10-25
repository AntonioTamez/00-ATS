# Layered / N-Tier – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-LAYERED.md` | Step-by-step guide (Spanish): presentation / business / data layers, closed layers, layers vs tiers, trade-offs |
| `docs/LAYERED-VS-OTRAS.md` | Layered vs Clean / Hexagonal / Onion / Vertical Slice / DDD / Modular Monolith / Microservices; when to use it and how it evolves (Spanish) |
| `sample/` | Reference .NET 10 solution (library lending): `Lending.Data`, `Lending.Business`, `Lending.Web`; 41 tests, including executable layering rules |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Run the sample: `cd sample && dotnet test` or `dotnet run --project src/Lending.Web`.
