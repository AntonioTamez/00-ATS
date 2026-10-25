# Event-Driven Architecture – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-EVENT-DRIVEN.md` | Step-by-step guide (Spanish): events, broker topology, transactional outbox, idempotent consumers, retries and dead letters, eventual consistency |
| `docs/EVENT-DRIVEN-VS-OTRAS.md` | Event-Driven vs request/response, Microservices, Modular Monolith, CQRS and Event Sourcing; when to use it and how to combine (Spanish) |
| `sample/` | Reference .NET 10 solution: `Eda.Messaging` (in-memory broker, outbox, inbox, pump) + three participants (`Orders`, `Inventory`, `Notifications`) + host; 55 tests, including broker outages, duplicates and poison messages |
| `portal/index.html` | Interactive beginner portal with a step-by-step message simulator (open it in a browser, no build needed) |

Run the tests: `cd sample && dotnet test`. Run the app: `dotnet run --project sample/src/Eda.Host`
(`POST /orders` answers `202`; the background worker runs every 50 ms, so a `GET /orders/{id}` right after usually already shows `Confirmed`. The tests, which control time by hand, are where `Pending` is observable. Verified by running the real host with `curl`).
