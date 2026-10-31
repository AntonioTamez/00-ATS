# Modular Monolith – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-MODULAR-MONOLITH.md` | Step-by-step guide (Spanish) to build a monolith split into modules with enforced boundaries |
| `docs/MODULAR-MONOLITH-VS-OTRAS.md` | Modular monolith vs Layered / Clean / Vertical Slice / Microservices; when to use each and how to combine (Spanish) |
| `sample/` | Reference .NET 10 solution (Catalog, Ordering, Notifications modules + host); 47 tests, including executable module-boundary rules |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Run the sample: `cd sample && dotnet test` or `dotnet run --project src/Shop.Host`.
