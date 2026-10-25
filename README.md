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

Swagger UI and the generated OpenAPI document are available in Development at `/swagger` and `/openapi/v1.json`. PostgreSQL Testcontainers tests use `postgres:17`, matching the documented development database. If Docker is unavailable, xUnit marks the affected tests as **Skipped** with the reason; unit and validation tests still run. Tests cover ticket workflow rules, TicketService create/read behavior, request validation, RFC 7807 exception responses, and the ticket CRUD HTTP pipeline. Stages 1–5 are the committed baseline; this branch adds the pre-Stage-6 test catch-up.

## Ticket routes

All routes are currently unauthenticated. Ticket IDs and timestamps are server generated. Enum values are represented as strings in JSON.

| Method | Route | Behavior |
|---|---|---|
| GET | `/api/tickets` | Filtered, sorted, paginated ticket list |
| GET | `/api/tickets/{id}` | Get a ticket; `404` if missing |
| POST | `/api/tickets` | Create a ticket; `201` with a `Location` header |
| PUT | `/api/tickets/{id}` | Update fields and transition status; `404` if missing, `409` for an invalid transition |
| DELETE | `/api/tickets/{id}` | Delete a ticket; `204` or `404` |
| GET | `/api/tickets/{id}/comments` | List comments in creation order; `404` if the ticket is missing |
| POST | `/api/tickets/{id}/comments` | Add a comment or internal note; `201`, `400`, or `404` |
| PUT | `/api/tickets/{id}/assignment` | Assign to an active agent/admin or send `assigneeId: null` to unassign |

`GET /api/tickets` accepts `status`, `priority`, `categoryId`, `requesterId`, `assigneeId`, `createdFrom`, `createdTo`, and `search` filters. Sort with `sortBy=id|title|status|priority|createdAt|updatedAt` and `sortDirection=asc|desc`. Pagination defaults to `page=1&pageSize=20`; page size is capped at 100. The response includes `items`, `page`, `pageSize`, `totalCount`, and `totalPages`. Invalid filters, sort values, dates, or page bounds return `400` Problem Details.

Example filtered page:

```http
GET /api/tickets?status=Open&priority=High&search=VPN&sortBy=createdAt&sortDirection=desc&page=1&pageSize=20
```

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

Example comment request (until Stage 8, `authorId` and `isInternal` are accepted from the request):

```json
{
  "authorId": 2,
  "body": "I am checking the VPN gateway logs.",
  "isInternal": true
}
```

Assign a ticket with `PUT /api/tickets/{id}/assignment` and `{"assigneeId": 2}`. Send `{"assigneeId": null}` to unassign it. Only active `Agent` or `Admin` users can be assigned. The API is still unauthenticated: internal comment visibility and assignment permissions are not authorization boundaries until Stage 8.

## Design decisions and current limitations

- EF Core maps enums to strings in PostgreSQL for readable persisted values. Category and department are lookup entities, seeded by the initial migration.
- `TicketWorkflow` is the single place that defines allowed status changes; the service updates resolved and updated timestamps.
- Controllers return DTOs so generated and persistence fields cannot be supplied by clients.
- Unexpected exceptions return RFC 7807 `ProblemDetails`; exception details are logged server-side and not returned to clients.
- The API currently has no authentication/authorization. Requester and assignee are nullable placeholders, so ownership rules are not enforced yet. Do not expose this API to untrusted users or treat it as production-ready.
- Department/category routes, JWT authentication, authorization policies, Docker Compose, and CI are not implemented yet. See `docs/progress.md` for the roadmap state.

### Stage 6 design decisions

- Ticket lists use a response envelope so clients receive stable page metadata. Filters are combined with AND; text search matches title or description case-insensitively using PostgreSQL `ILIKE`.
- Sort fields are allow-listed and include an ID tie-breaker for deterministic pages. Page numbers start at 1 and page size is limited to 100.
- Comment author IDs are temporary request fields because authentication does not exist yet. The API stores and returns `IsInternal`, but does not hide internal notes until Stage 8 authorization.
- Assignment accepts only active agents/admins and supports clearing the nullable assignee. Role-based access restrictions remain pending authentication.
- Swagger XML summaries and endpoint response metadata describe the current routes; request examples are in this README and `HelpDesk.Api.http`.
