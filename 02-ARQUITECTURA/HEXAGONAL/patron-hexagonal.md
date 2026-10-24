# Hexagonal Architecture — Users API (.NET 10)

## 1. Context

Goal: a .NET 10 Web API with a `UsersController` returning a list of users, built with
Hexagonal Architecture (Ports & Adapters) and a **rich domain model** (no anemic entities).
SDK 10.0.401 is installed. Verified against ASP.NET Core docs: .NET 10 uses `AddOpenApi()` /
`MapOpenApi()` (first-party `Microsoft.AspNetCore.OpenApi`), `AddProblemDetails()`,
`[ApiController]` + attribute routing.

## 2. The pattern, correctly (Cockburn, 2005)

- **Hexagon = application core** (Domain + Application). It knows nothing about HTTP, DB or frameworks.
- **Port** = interface owned by the core.
  - *Driving (primary/inbound)*: how the world calls the core (`IListUsersQuery`).
  - *Driven (secondary/outbound)*: what the core needs from the world (`IUserRepository`).
- **Adapter** = implements/uses a port. Inbound: REST controller. Outbound: persistence.
- **Dependency Rule**: all dependencies point inward. Ports are defined in the core;
  adapters depend on the core, never the reverse (Dependency Inversion).
- **Composition root** (the API host) is the only place that wires adapters to ports.

Common mistakes this plan avoids:
1. Ports declared in Infrastructure (inverts the rule).
2. EF entities used as domain entities (persistence leaks into the core).
3. Controllers calling repositories directly (bypasses the driving port).
4. Domain with public setters + logic in services (anemic model).
5. Returning domain entities from the API (couples contract to domain).

## 3. Solution layout

```
HEXAGONAL/
├─ patron-hexagonal.md
├─ Directory.Build.props          # net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, analyzers
├─ Directory.Packages.props       # Central Package Management
├─ UsersApi.slnx
├─ src/
│  ├─ Users.Domain/               # no package references
│  │  ├─ Users/User.cs            # aggregate root
│  │  ├─ Users/UserId.cs, Email.cs, FullName.cs   # value objects
│  │  ├─ Users/UserStatus.cs
│  │  └─ Common/DomainException.cs
│  ├─ Users.Application/          # refs: Domain
│  │  ├─ Ports/In/IListUsersQuery.cs              # driving port
│  │  ├─ Ports/Out/IUserRepository.cs             # driven port
│  │  ├─ ListUsers/ListUsersHandler.cs            # implements IListUsersQuery
│  │  ├─ ListUsers/UserSummary.cs                 # output model (not the aggregate)
│  │  ├─ ListUsers/PagedResult.cs
│  │  └─ DependencyInjection.cs                   # AddApplication()
│  ├─ Users.Infrastructure/       # refs: Application (+ Domain)
│  │  ├─ Persistence/InMemoryUserRepository.cs    # first adapter (seeded)
│  │  ├─ Persistence/UserRecord.cs                # persistence model, mapped to/from User
│  │  └─ DependencyInjection.cs                   # AddInfrastructure()
│  └─ Users.Api/                  # refs: Application, Infrastructure (composition root only)
│     ├─ Controllers/UsersController.cs           # inbound adapter
│     ├─ Contracts/UserResponse.cs, PagedResponse.cs
│     └─ Program.cs
└─ tests/
   ├─ Users.Domain.Tests/
   ├─ Users.Application.Tests/
   ├─ Users.Api.Tests/            # WebApplicationFactory integration
   └─ Users.Architecture.Tests/   # enforces the dependency rule
```

Reference rules (enforced by tests, see section 7):
Domain -> nothing · Application -> Domain · Infrastructure -> Application · Api -> Application + Infrastructure (wiring only).

## 4. Rich domain model (no anemia)

- `User` aggregate root: **private constructor**, no public setters.
  - `User.Register(FullName, Email, TimeProvider)` — factory enforcing invariants, sets `Status = Active`.
  - `User.Rehydrate(...)` — explicit reconstitution path for repositories (behavior on trusted data documented).
  - Behavior, not data bags: `Deactivate()`, `Activate()`, `ChangeEmail(Email)`, `Rename(FullName)` — each guards state transitions (e.g. deactivating twice throws `DomainException`).
  - Keep behavior real but minimal (YAGNI); seed data is created through `Register`, so listing returns state produced by genuine domain behavior.
- Value objects: `UserId` (Guid, non-empty), `Email` (normalized, validated), `FullName` (non-blank, max length). Immutable, equality by value, validation in constructor/`Create`.
- Domain depends on nothing: time via `TimeProvider` passed as a parameter (BCL type, no extra package).
- `sealed` by default; nullable reference types on.

## 5. Application layer

- Driving port: `IListUsersQuery { Task<PagedResult<UserSummary>> ExecuteAsync(ListUsersRequest, CancellationToken) }`.
- `ListUsersHandler` orchestrates only: calls `IUserRepository.ListAsync(page, pageSize, ct)`, maps `User -> UserSummary`. No business rules here; no framework types.
- Driven port: `IUserRepository` returns **domain `User`** (aggregate), with `(IReadOnlyList<User> Items, int Total)` pagination.
  - Tradeoff: for pure read queries a dedicated read port (CQRS-style `IUserReadModel` returning projections) avoids loading aggregates. Chosen: one repository now (simple, consistent); revisit when read/write diverge.
- Paging validation (page >= 1, 1 <= size <= 100) lives in `ListUsersRequest` (application-level guard), not in the controller.

## 6. Adapters

- **Inbound** `UsersController` (`[ApiController]`, `[Route("api/users")]`): `GET /api/users?page=&pageSize=` -> calls `IListUsersQuery`, maps `UserSummary -> UserResponse` (own contract), returns `200` with paged body, `400` ProblemDetails on invalid paging. Depends on the **port**, never the handler class or repository.
- **Outbound** `InMemoryUserRepository`: stores `UserRecord`, maps to `User.Rehydrate`. Swappable later for EF Core without touching the core.
- `Program.cs`: `AddControllers`, `AddApplication`, `AddInfrastructure`, `AddProblemDetails`, `AddOpenApi`; `MapOpenApi` in Development only; global exception handling via `IExceptionHandler` -> ProblemDetails.

## 7. Verification plan (TDD where deterministic)

- Domain tests (RED -> GREEN first): invariants, `Register`, state transitions, value object equality/validation.
- Application tests: handler with fake `IUserRepository` (paging, mapping, empty list, invalid request).
- Architecture tests (NetArchTest.Rules or ArchUnitNET): Domain has no dependency on Application/Infrastructure/Api/ASP.NET; Application doesn't reference Infrastructure/Api; ports are interfaces in Application; controllers depend only on `Ports.In`; `User` has no public setters / public constructors.
- API integration test with `WebApplicationFactory<Program>`: `GET /api/users` returns 200 + expected shape; bad paging -> 400 ProblemDetails.
- Manual: `dotnet build -warnaserror`, `dotnet test`, `dotnet run --project src/Users.Api`, hit `GET /api/users` and `/openapi/v1.json`.

## 8. Validation passes on the proposed pattern

| Pass | Question | Result |
|------|----------|--------|
| 1. Definition | Core isolated, ports owned by core, adapters outside? | Yes — ports in `Users.Application`, adapters in Infrastructure/Api |
| 2. Dependency direction | Any inward-violating reference? | None; csproj graph (section 3) + architecture tests make it executable |
| 3. Anemia | Logic in entities, not services? | Yes — invariants/transitions in `User` and value objects; handler is orchestration only |
| 4. Pragmatism | Over-engineering for a list endpoint? | Accepted deliberately: reference implementation; no MediatR, no CQRS split, no generic repository, no unit-of-work (YAGNI) |
| 5. .NET 10 practices | Modern defaults? | Nullable, `TimeProvider`, `CancellationToken` everywhere, ProblemDetails, built-in OpenAPI, Central Package Management, `.slnx`, warnings as errors |

## 9. Out of scope

Authentication, real database/EF Core, create/update endpoints, MediatR, domain events (add when a real need appears).

## 10. Implementation steps

1. Scaffold solution + `Directory.Build.props` / `Directory.Packages.props` (`dotnet new`, `.slnx`).
2. Domain (tests first) -> Application (tests first) -> Infrastructure -> Api.
3. Architecture + integration tests; run build/test; commit with conventional commits per work unit.
