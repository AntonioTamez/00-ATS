# Microkernel / Plugins – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-MICROKERNEL.md` | Step-by-step guide (Spanish): small core + plugins loaded at run time, type identity, per-plugin load contexts, failure isolation, unloading, honest limits |
| `docs/MICROKERNEL-VS-OTRAS.md` | Comparison with Hexagonal, Modular Monolith, Microservices, Strategy and Pipes & Filters; when to use and combine (Spanish) |
| `sample/` | Reference .NET 10 solution: a shipping-quote core (`Shipping.Kernel`) and carriers as plugins (`Carriers.Standard`, `Carriers.Express`), six deliberately bad fixture plugins, 34 tests incl. architecture rules |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Run: `cd sample && dotnet build && dotnet run --project src/Shipping.Host -- artifacts/plugins 2 100`.
Test: `cd sample && dotnet test` (builds and stages the plugins into `sample/artifacts/` first).
