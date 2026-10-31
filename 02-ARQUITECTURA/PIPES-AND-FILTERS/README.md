# Pipes & Filters – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-PIPES-AND-FILTERS.md` | Step-by-step guide (Spanish): independent filters joined by pipes, typed schemas, lazy pull-based flow, rejects vs failures, bounded buffer, honest limits |
| `docs/PIPES-AND-FILTERS-VS-OTRAS.md` | Comparison with Layered, Microkernel, Event-Driven, Microservices and look-alikes (Chain of Responsibility, middleware, LINQ); when to use and combine (Spanish) |
| `sample/` | Reference .NET 10 solution: a generic pipeline core (`Sales.Pipeline`), five sales filters (`Sales.Filters`), a composition root and CLI (`Sales.Host`), 71 tests incl. architecture rules |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Run: `cd sample && dotnet build && dotnet run --project src/Sales.Host -- data/sales.csv` (add `--quick` and/or `--buffer 4`).
Test: `cd sample && dotnet test`.
