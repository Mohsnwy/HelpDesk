# Implementation progress

## Stage 1 — Audit and stabilize (baseline)

- Confirmed target framework .NET 10 and retained ASP.NET Core controllers, Swagger, and OpenAPI.
- Removed the unused WeatherForecast template endpoint and model.
- Added this progress log and a root `.gitignore`.
- The repository is maintained in Git; the current feature branch is `feature/stage-6`.

## Stage 2 — Ticket CRUD (baseline)

- Implemented list, get, create, update, and delete routes with appropriate `200`, `201`, `204`, `400`, `404`, and `409` responses.
- Added request and response DTOs, enum validation, generated database IDs, and server-managed UTC timestamps.
- Added the status/priority enums and status transition checks.

## Stage 3 — PostgreSQL and EF Core (baseline)

- Added EF Core 10 and Npgsql 10, the domain entities, explicit relationships, length constraints, indexes, enum string conversions, and lookup seed data.
- Added the PostgreSQL DbContext, design-time factory, initial migration, and local EF CLI manifest.
- The design-time factory loads the same environment-specific appsettings and environment variables as the API.
- Connection configuration comes from `ConnectionStrings:HelpDesk` / `ConnectionStrings__HelpDesk`; no real credentials are stored in the repository.
- The supplied local database already had both migrations recorded. `dotnet ef database update` confirmed it was current and applied no changes.

## Stage 4 — Testing foundation (baseline)

- Added xUnit unit tests for allowed and rejected workflow transitions.
- Added a `WebApplicationFactory` CRUD integration test using PostgreSQL Testcontainers, including validation, not-found behavior, location header, and invalid transition coverage.
- The PostgreSQL integration tests use Testcontainers and report a framework-level skipped result when Docker is unavailable.

## Stage 5 — Architecture and error handling (baseline; test catch-up committed before Stage 6)

- Added `ITicketService` / `TicketService`, asynchronous EF operations, centralized status workflow, and generic RFC 7807 exception handling.
- Added catch-up coverage for TicketService persistence/mapping, generic exception responses without leaked details, and request validation.

## Pre-Stage-6 verification

- `dotnet build HelpDesk.sln --no-restore -m:1`: succeeded with 0 warnings and 0 errors.
- `dotnet test HelpDesk.sln --no-restore -m:1`: 13 passed, 0 failed, 0 skipped. PostgreSQL Testcontainers ran successfully against `postgres:17`.
- `dotnet list HelpDesk.sln package --vulnerable --include-transitive`: no vulnerable packages found in the configured NuGet source.
- The existing local PostgreSQL container is reachable at `localhost:5432`; `GET /api/tickets` returned `200` with an empty list.
- `dotnet ef database update` connected successfully and reported the local database was already up to date.

## Stage 6 — Comments and API usability

- Pending: comments, assignment, filtering, sorting, pagination, OpenAPI descriptions, and tests.

## Remaining roadmap

- Stage 7: category/department endpoints and user profile support.
- Stage 8: authentication and role/ownership authorization.
- Stage 9: Docker Compose, CI, and portfolio documentation refinements.
- The current unauthenticated API is for local development only.
