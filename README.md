# HelpDesk.Api

HelpDesk.Api is an internal company help desk REST API built with ASP.NET Core 10, EF Core, and PostgreSQL 17. Employees raise and track IT, HR, facilities, and access requests; support staff triage, assign, comment on, and resolve them.

## Domain and roles

- **Employee:** create and manage their own tickets and comments. Other employees' tickets are concealed with `404`; internal agent notes are never returned.
- **Agent:** view and manage tickets across employees, assign tickets, and create/read internal notes.
- **Admin:** all Agent capabilities plus manage users, categories, and departments.

Tickets have `Open`, `InProgress`, `Resolved`, `Closed`, and `Reopened` statuses and `Low`, `Medium`, `High`, or `Critical` priorities. Ticket status transitions are centralized in `TicketWorkflow`. Users, tickets, comments, categories, and departments are persisted with EF Core and PostgreSQL.

## Architecture

Controllers handle HTTP concerns; `ITicketService`/`TicketService` encapsulate ticket persistence and mapping; `HelpDeskDbContext` configures relational constraints and string enum storage; `TicketWorkflow` owns transition rules. JWT bearer authentication uses `PasswordHasher<User>` for password hashes. RFC 7807 problem responses are produced by the exception handler. EF migrations are explicit operations and are not applied during API startup.

## Prerequisites

- .NET 10 SDK for local development and migrations.
- Docker with Compose support for the container stack and PostgreSQL Testcontainers integration tests.
- PostgreSQL 17 when running the API outside Compose.

## Configuration and local setup

For Compose, copy the placeholder file and replace every placeholder. The ignored `.env` file holds local credentials and is never committed:

```sh
cp .env.example .env
```

Use unique local values for the PostgreSQL password, JWT signing key (at least 32 UTF-8 bytes), and bootstrap admin password (at least 12 characters). The example environment is Development so Swagger and first-admin bootstrap are available locally. Compose defaults to Production if `ASPNETCORE_ENVIRONMENT` is omitted. In a deployed environment, supply secrets through the deployment platform rather than a committed file.

Start PostgreSQL, then explicitly apply migrations:

```sh
docker compose up -d db
docker compose --profile migration run --rm migrate
```

Start the API:

```sh
docker compose up --build api
```

The API listens at `http://localhost:${API_PORT:-8080}`. In Development, Swagger UI is at `/swagger`. The API does not migrate the database at startup. For local development without Compose, set `ConnectionStrings__HelpDesk` and `Jwt__SigningKey` (or use .NET user-secrets), then run `dotnet tool restore` and `dotnet ef database update` before starting the API.

### First admin

In the Compose Development setup, set `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_FULL_NAME`, and `BOOTSTRAP_ADMIN_PASSWORD` in `.env` before the first API start. Bootstrap creates the account only when absent. Remove the bootstrap password from `.env` after startup. For local `dotnet run`, set the corresponding `BootstrapAdmin__...` environment variables or user-secrets. Bootstrap is disabled outside Development. Further accounts are created by an Admin using `POST /api/users`; public registration is disabled.

### Migrations

Migrations are deliberately not run implicitly in production. For a reviewed deployment, back up the database, inspect pending migrations, then apply them as a separate release operation using the migration profile (or `dotnet ef database update` from a trusted SDK environment). The profile uses the same multi-stage Dockerfile and configured database connection:

```sh
docker compose --profile migration run --rm migrate
```

To create a migration locally after changing the model:

```sh
dotnet tool restore
dotnet ef migrations add DescribeChange
dotnet ef database update
```

Do not run migration commands against a production database without the deployment's normal review and backup process.

## Run and test

```sh
dotnet restore HelpDesk.sln
dotnet build HelpDesk.sln
dotnet test HelpDesk.sln
dotnet run --project HelpDesk.Api.csproj
```

The test suite uses unit tests and PostgreSQL 17 Testcontainers integration tests. Tests need a working Docker daemon; when unavailable, integration tests report framework-level skips with a reason. No personal database credentials are required.

## API examples

Login:

```sh
curl -s http://localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@company.example","password":"your-local-admin-password"}'
```

Create a ticket with the returned access token:

```sh
curl -i http://localhost:8080/api/tickets \
  -H 'Authorization: Bearer YOUR_ACCESS_TOKEN' \
  -H 'Content-Type: application/json' \
  -d '{"title":"VPN connection issue","description":"Unable to connect to the company VPN.","priority":"High","categoryId":2}'
```

List tickets with filters and pagination:

```sh
curl 'http://localhost:8080/api/tickets?status=Open&page=1&pageSize=20' \
  -H 'Authorization: Bearer YOUR_ACCESS_TOKEN'
```

In Swagger UI, call `POST /api/auth/login`, copy `accessToken`, click **Authorize**, and paste only the token value (do not prepend `Bearer `). The OpenAPI scheme is HTTP bearer, so Swagger adds the `Bearer` prefix to the request header. Tokens expire after 60 minutes.

### Routes

| Method | Route | Access |
|---|---|---|
| POST | `/api/auth/login` | Anonymous |
| GET | `/api/users/me` | Authenticated |
| POST | `/api/users` | Admin |
| GET | `/api/tickets` | Authenticated; Employees see their own |
| GET | `/api/tickets/{id}` | Authenticated; Employee ownership enforced |
| POST | `/api/tickets` | Authenticated; requester comes from token |
| PUT | `/api/tickets/{id}` | Employee-owned update; Agent/Admin may update any |
| DELETE | `/api/tickets/{id}` | Agent/Admin |
| GET/POST | `/api/tickets/{id}/comments` | Authenticated; internal notes Agent/Admin only |
| PUT | `/api/tickets/{id}/assignment` | Agent/Admin |
| GET | `/api/categories`, `/api/departments` | Authenticated |
| POST | `/api/categories`, `/api/departments` | Admin |

Ticket listing supports status, priority, category, requester, assignee, created date range, text search, allow-listed sorting, and pagination (default 20, maximum 100).

## Design decisions

- Categories and departments are relational lookup tables because tickets and users reference them.
- Enums are stored as readable strings in PostgreSQL; `TicketWorkflow` centralizes valid status changes.
- Ticket ownership is applied in database queries. Returning `404` for another employee's ticket avoids disclosing that it exists.
- `PasswordHasher<User>` supplies password hashing without Identity's larger account model; user creation is restricted to Admin and bootstrap is Development-only.
- JWT validation checks issuer, audience, signature, and expiry. Signing keys are configuration secrets and must be at least 32 bytes.
- Testcontainers exercises actual PostgreSQL behavior; Docker is required for those integration tests.
- Compose uses PostgreSQL 17 and separates the migration job from API startup. The migration profile is opt-in and intended to make schema changes a deliberate release action.

## Known limitations

This portfolio project is not production ready. It does not provide refresh tokens, password reset or lockout flows, audit history, operational key rotation, production admin provisioning, rate limiting, or deployment-specific secret management. The Development bootstrap credentials must be removed after initial setup. HTTPS termination and operational monitoring are deployment responsibilities.
