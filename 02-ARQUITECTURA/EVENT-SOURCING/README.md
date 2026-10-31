# Event Sourcing – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-EVENT-SOURCING.md` | Step-by-step guide (Spanish): state as a sequence of events, optimistic concurrency, snapshots, upcasting, time travel, projections |
| `docs/EVENT-SOURCING-VS-OTRAS.md` | Event Sourcing vs CRUD / audit log / CQRS / Event-Driven / DDD; when to use it and how to combine (Spanish) |
| `sample/` | Reference .NET 10 solution (warehouse stock ledger): `Ledger.Domain`, `Ledger.Infrastructure`, `Ledger.Api`; 91 tests, including concurrency conflicts, snapshots, time travel and event versioning |
| `portal/index.html` | Interactive beginner portal: an append-only ledger you can replay, snapshot, time-travel and conflict (open it in a browser, no build needed) |

Run the sample: `cd sample && dotnet test` or `dotnet run --project src/Ledger.Api`.
