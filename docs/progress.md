# Implementation progress

Stages 1–8 were merged to `main` before work on `feature/stage-9`.

## Stages 1–8 — API foundation and functionality

- Stabilized the .NET 10 ASP.NET Core API; implemented ticket CRUD, PostgreSQL persistence, EF Core migrations, validation, centralized ticket workflow, RFC 7807 errors, comments, assignment, filtering, sorting, and pagination.
- Added category, department, and user endpoints; JWT login; password hashing; role-based access; Employee ownership enforcement and internal-comment privacy.
- Added xUnit unit tests and PostgreSQL 17 Testcontainers integration coverage, including authentication and authorization behavior.
- The API does not apply migrations at startup. Database migration is a deliberate operator action.

## Stage 9 — Portfolio readiness

- Added a multi-stage .NET 10 Dockerfile, PostgreSQL 17 Compose stack, persistent database volume, health check, and an opt-in migration profile. The API container never applies migrations implicitly.
- Added `.env.example` with placeholders and ignored local `.env` files; expanded `.gitignore` and added `.dockerignore` to keep local configuration, build output, and test artifacts out of images and Git.
- Added GitHub Actions CI to restore, build, and test on .NET 10. GitHub-hosted Ubuntu runners provide Docker for Testcontainers; the workflow uses read-only repository permissions and no credentials.
- Reworked the README with setup, configuration, first-admin bootstrap, migrations, API usage, design decisions, and limitations. Confirmed the Swagger OpenAPI definition uses HTTP bearer authentication; users paste only the JWT and Swagger constructs the `Bearer` header.
- `dotnet build HelpDesk.sln --configuration Release -m:1`: succeeded with 0 warnings and 0 errors.
- `dotnet test HelpDesk.sln --configuration Release --no-build -m:1`: 21 passed, 0 failed, 0 skipped. PostgreSQL Testcontainers ran successfully using Docker.
- `docker compose --env-file .env.example config --quiet`: succeeded.
- `docker build --target runtime -t helpdesk-api:stage-9 .`: succeeded.
- `docker build --target migration -t helpdesk-api-migrations:stage-9 .`: succeeded; the EF CLI tool restored successfully.

## Remaining limitations

- The API has no refresh tokens, password recovery or lockout flows, audit history, automated key rotation, or production admin provisioning.
- Production HTTPS termination, monitoring, secret storage, database backup, and migration approval remain deployment responsibilities.
