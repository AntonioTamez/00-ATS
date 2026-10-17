# hexagonal-users-api

Objective: .NET 10 Users API (GET /api/users) with Hexagonal Architecture and a rich domain model.
Spec: `02-ARQUITECTURA/HEXAGONAL/patron-hexagonal.md` (authoritative). Branch: `feat/hexagonal-users-api`.
Delivery strategy: ask-on-risk (forecast < 400 lines per work unit; PR/push left to the user).

## Tasks
- [x] T1 Scaffold: slnx, Directory.Build.props, Directory.Packages.props, 4 src + 4 test projects, reference graph. Route: delegated (writer, 2+ files).
- [x] T2 Domain (tests first): value objects, User aggregate, DomainException.
- [x] T3 Application (tests first): ports, ListUsersHandler, ListUsersRequest guard, PagedResult.
- [x] T4 Infrastructure + Api: InMemoryUserRepository, UsersController, Program.cs, ProblemDetails, OpenAPI.
- [x] T5 Architecture tests + API integration tests; full build/test.

## Acceptance
`dotnet build -warnaserror` and `dotnet test` green; GET /api/users returns paged 200; bad paging 400 ProblemDetails; architecture tests enforce the dependency rule.

## Progress / evidence
- T1 fd814f2 scaffold: `dotnet build -warnaserror` 0 warnings/0 errors.
- T2 7d94c22 domain: RED observed (CS errors, types missing), GREEN 29/29 tests.
- T3 a33ebfe application: RED observed (CS errors), GREEN 14/14 tests.
- T4 16934a4 infrastructure + api: build -warnaserror green.
- T5 55c2e63 tests: architecture 10/10, API integration 7/7.
- Full `dotnet build -warnaserror`: 0 warnings, 0 errors. `dotnet test`: 29+14+10+7 passed, 0 failed/skipped.
- Manual run: GET /api/users?pageSize=2 -> 200 paged body (totalCount 25); GET /api/users?page=0 -> 400 application/problem+json; /openapi/v1.json -> 200 (Development).
- Route: delegated writer for all tasks.
