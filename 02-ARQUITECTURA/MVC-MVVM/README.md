# MVC / MVVM – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-MVC-MVVM.md` | Step-by-step guide (Spanish) to the two presentation patterns, on one shared Model |
| `docs/MVC-MVVM-VS-OTRAS.md` | MVC / MVP / MVVM vs application architectures (Layered, Clean, Hexagonal...); where each pattern lives and how to combine them (Spanish) |
| `sample/` | Reference .NET 10 solution (shopping cart): `Shop.Model`, `Shop.Mvc` (controllers + Razor views), `Shop.ViewModels` (MVVM) and `Shop.ConsoleView` (a view bound to the view model) |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

**Verification status:** the first test run passed 62 of 66; the 4 failures were test-harness mistakes that were then fixed, but Windows App Control blocked the rebuilt test assembly, so the fixed suite has **not** been re-run. See section 10 of the guide.

Run the sample: `cd sample && dotnet test`, `dotnet run --project src/Shop.Mvc` or `dotnet run --project src/Shop.ConsoleView`.
