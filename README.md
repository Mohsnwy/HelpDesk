# HelpDesk.Api

An ASP.NET Core REST API for managing internal employee support tickets. The project currently provides ticket CRUD, PostgreSQL persistence, validation, and a centralized ticket status workflow.

## Requirements

- .NET 10 SDK
- PostgreSQL 16 or newer
- Docker (for the PostgreSQL integration test)

Start a local database with:

```sh
docker run --name helpdesk-postgres -e POSTGRES_DB=helpdesk -e POSTGRES_USER=helpdesk -e POSTGRES_PASSWORD=local-development-only -p 5432:5432 -d postgres:17-alpine
```

Set the connection string before starting the API. Do not commit real credentials.

```sh
export ConnectionStrings__HelpDesk='Host=localhost;Port=5432;Database=helpdesk;Username=helpdesk;Password=local-development-only'
```

## Database setup

The repository includes a local `dotnet-ef` tool manifest. After restoring tools and packages, create/update the database using EF migrations:

```sh
dotnet tool restore
dotnet ef database update
```

To change the schema, create a migration and commit its generated files:

```sh
dotnet ef migrations add DescribeYourChange
dotnet ef database update
```

The API does not automatically alter a database at startup. Development example configuration is in `appsettings.Development.example.json`; set local configuration with environment variables or .NET user secrets.

## Run and test

```sh
dotnet restore HelpDesk.sln
dotnet build HelpDesk.sln
dotnet test HelpDesk.sln
dotnet run --project HelpDesk.Api.csproj
```

Swagger UI and the generated OpenAPI document are available in Development at `/swagger` and `/openapi/v1.json`. If Docker is unavailable, the PostgreSQL integration test reports a skip reason in test output; the workflow unit tests still run.

## Ticket routes

All routes are currently unauthenticated. Ticket IDs and timestamps are server generated. Enum values are represented as strings in JSON.

| Method | Route | Behavior |
|---|---|---|
| GET | `/api/tickets` | List tickets ordered by ID |
| GET | `/api/tickets/{id}` | Get a ticket; `404` if missing |
| POST | `/api/tickets` | Create a ticket; `201` with a `Location` header |
| PUT | `/api/tickets/{id}` | Update fields and transition status; `404` if missing, `409` for an invalid transition |
| DELETE | `/api/tickets/{id}` | Delete a ticket; `204` or `404` |

Example create request:

```json
{
  "title": "VPN connection issue",
  "description": "Unable to connect to the company VPN.",
  "priority": "High",
  "categoryId": 2
}
```

Priorities are `Low`, `Medium`, `High`, and `Critical`. Status transitions are `Open` → `InProgress` → `Resolved` → `Closed`; `Resolved` and `Closed` can transition to `Reopened`, and `Reopened` can transition to `InProgress`. Invalid changes return `409 Conflict`.

## Design decisions and current limitations

- EF Core maps enums to strings in PostgreSQL for readable persisted values. Category and department are lookup entities, seeded by the initial migration.
- `TicketWorkflow` is the single place that defines allowed status changes; the service updates resolved and updated timestamps.
- Controllers return DTOs so generated and persistence fields cannot be supplied by clients.
- Unexpected exceptions return RFC 7807 `ProblemDetails`; exception details are logged server-side and not returned to clients.
- The API currently has no authentication/authorization. Requester and assignee are nullable placeholders, so ownership rules are not enforced yet. Do not expose this API to untrusted users or treat it as production-ready.
- Comments, assignment and filtering/pagination, department/category routes, JWT authentication, authorization policies, Docker Compose, and CI are not implemented yet. See `docs/progress.md` for the roadmap state.
