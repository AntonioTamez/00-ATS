# Clean Architecture + Screaming Architecture – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-CLEAN-SCREAMING.md` | Step-by-step guide (Spanish): business capabilities at the top level, Clean's circles inside each one, ports/adapters between capabilities, all enforced by the compiler |
| `docs/CLEAN-SCREAMING-VS-OTRAS.md` | The combination vs Clean alone, Screaming alone, Modular Monolith and Vertical Slice; cost and when to choose it (Spanish) |
| `sample/` | Reference .NET 10 solution (the clinic from the Screaming kit, rebuilt): `Patients/`, `Appointments/`, `Billing/`, each with `Domain`, `Application`, `Adapters` projects, plus `SharedKernel` and `Host`; 70 tests that check both ideas |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Read first: `../CLEAN` and `../SCREAMING`.
Run the sample: `cd sample && dotnet test` or `dotnet run --project src/Clinic.Host`.
