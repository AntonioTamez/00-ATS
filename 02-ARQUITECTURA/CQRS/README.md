# CQRS – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-CQRS.md` | Step-by-step guide (Spanish) to separate commands from queries, with eventual consistency and a rebuildable read model |
| `docs/CQRS-VS-OTRAS.md` | CQRS vs CRUD / Clean / Vertical Slice / DDD / Event Sourcing / Event-Driven; when to use it and how to combine (Spanish) |
| `sample/` | Reference .NET 10 solution (bank accounts): write model, read model, event log, projector; 56 tests, including executable CQRS rules |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Run the sample: `cd sample && dotnet test` or `dotnet run --project src/Bank.Api`.
