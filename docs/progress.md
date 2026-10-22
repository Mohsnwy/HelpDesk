# Implementation progress

## Stage 1 — Audit and stabilize

- Confirmed target framework .NET 10 and retained ASP.NET Core controllers, Swagger, and OpenAPI.
- Removed the unused WeatherForecast template endpoint and model.
- Added this progress log and a root `.gitignore`.
- The supplied workspace has no `.git` repository metadata, so a feature branch and stage commits cannot be created here.

## Stage 2 — Ticket CRUD

- Implemented list, get, create, update, and delete routes with appropriate `200`, `201`, `204`, `400`, `404`, and `409` responses.
- Added request and response DTOs, enum validation, generated database IDs, and server-managed UTC timestamps.
- Added the status/priority enums and status transition checks.

## Stage 3 — PostgreSQL and EF Core

- Added EF Core 10 and Npgsql 10, the domain entities, explicit relationships, length constraints, indexes, enum string conversions, and lookup seed data.
- Added the PostgreSQL DbContext, design-time factory, initial migration, and local EF CLI manifest.
- The design-time factory loads the same environment-specific appsettings and environment variables as the API.
- Connection configuration comes from `ConnectionStrings:HelpDesk` / `ConnectionStrings__HelpDesk`; no real credentials are stored in the repository.
- The supplied local database already had both migrations recorded. `dotnet ef database update` confirmed it was current and applied no changes.

## Stage 4 — Testing foundation

- Added xUnit unit tests for allowed and rejected workflow transitions.
- Added a `WebApplicationFactory` CRUD integration test using PostgreSQL Testcontainers, including validation, not-found behavior, location header, and invalid transition coverage.
- The PostgreSQL integration test returns early with a skip reason if the Docker daemon cannot start the container.

## Stage 5 — Architecture and error handling

- Added `ITicketService` / `TicketService`, asynchronous EF operations, centralized status workflow, and generic RFC 7807 exception handling.
- Further service and API behaviors should receive tests as the roadmap proceeds.

## Verification in this workspace

- `dotnet build HelpDesk.sln --no-restore`: succeeded with 0 warnings and 0 errors.
- `dotnet test HelpDesk.sln --no-restore`: all 10 tests passed, including the full PostgreSQL Testcontainers HTTP CRUD test.
- `dotnet list HelpDesk.sln package --vulnerable --include-transitive`: no vulnerable packages found in the configured NuGet source.
- The existing local PostgreSQL container is reachable at `localhost:5432`; `GET /api/tickets` returned `200` with an empty list.
- `dotnet ef database update` connected successfully and reported the local database was already up to date.

## Remaining roadmap

- Stage 6: comments, assignment, filtering, sorting, pagination, richer OpenAPI documentation.
- Stage 7: category/department endpoints and user profile support.
- Stage 8: authentication and role/ownership authorization.
- Stage 9: Docker Compose, CI, and portfolio documentation refinements.
- These features remain unimplemented; the current unauthenticated API is for local development only.
- The repository must be opened from an actual Git checkout before branch and commit requirements can be satisfied.
