# Screaming Architecture – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-SCREAMING-ARCHITECTURE.md` | Step-by-step guide (Spanish): make the folder tree say what the system DOES, keep the framework at the edge |
| `docs/SCREAMING-VS-OTRAS.md` | Screaming vs Layered / Clean / Vertical Slice / Modular Monolith / DDD, plus an honest look at what this catalog's own samples "scream" (Spanish) |
| `sample/` | Reference .NET 10 solution (clinic agenda): `Clinic` (the good example: Patients / Appointments / Billing) and `Clinic.Technical` (the BAD example, organized by technical role, kept for comparison); 42 tests, including structure rules proven against the bad example |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Run the sample: `cd sample && dotnet test` or `dotnet run --project src/Clinic`.
`Clinic.Technical` is intentionally framework-shaped. Do not copy it.
