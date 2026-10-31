# Microservices – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-MICROSERVICIOS.md` | Step-by-step guide (Spanish): independent services, own data, resilient calls (timeouts, retries, idempotency, compensation) |
| `docs/MICROSERVICIOS-VS-OTRAS.md` | Microservices vs monolith / modular monolith / Layered / Clean / CQRS / Event-Driven; when to use them and how to combine (Spanish) |
| `sample/` | Reference .NET 10 solution: `Catalog.Service`, `Orders.Service`, `Gateway`; 49 tests, including simulated network failures and executable independence rules |
| `portal/index.html` | Interactive beginner portal with a network-failure simulator (open it in a browser, no build needed) |

Run the tests: `cd sample && dotnet test` (the three services start in memory inside the tests).
Run them for real, as three processes: see section 8 of the guide.
