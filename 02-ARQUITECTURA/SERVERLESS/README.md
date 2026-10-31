# Serverless / FaaS – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-SERVERLESS.md` | Step-by-step guide (Spanish): stateless, idempotent, short functions; triggers; what a FaaS platform does (retries, dead letters, timeouts, concurrency, instances); honest limits (not a cloud, the outbox problem) |
| `docs/SERVERLESS-VS-OTRAS.md` | Comparison with Microservices, Event-Driven, Hexagonal, Modular Monolith and Pipes & Filters; when to use and combine (Spanish) |
| `sample/` | Reference .NET 10 solution: a local FaaS platform (`Faas.Runtime`), three order functions (`Orders.Functions`: HTTP, queue, timer), a composition root and demo CLI (`Orders.Local`), 76 tests incl. architecture rules |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

This kit teaches the programming model and design rules of functions against a **local simulation** of a FaaS platform. It does not deploy to any cloud and does not measure cold-start latency or scaling.

Run: `cd sample && dotnet build && dotnet run --project src/Orders.Local -- demo` (add `--recycle` for a new function instance per invocation).
Test: `cd sample && dotnet test`.
