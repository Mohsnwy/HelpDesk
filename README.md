# HelpDesk.Api

An ASP.NET Core REST API for internal employee support tickets, backed by PostgreSQL and EF Core. It includes ticket workflows, filtering, comments, assignment, JWT authentication, and role based access.

## Requirements and setup

- .NET 10 SDK
- PostgreSQL 16 or newer
- Docker for PostgreSQL integration tests

Start a local database:

```sh
docker run --name helpdesk-postgres -e POSTGRES_DB=helpdesk -e POSTGRES_USER=helpdesk -e POSTGRES_PASSWORD=local-development-only -p 5432:5432 -d postgres:17-alpine
```

Configure the database connection and signing key. The JWT key must be at least 32 UTF-8 bytes; it is required at startup in every environment except automated tests.

```sh
export ConnectionStrings__HelpDesk='Host=localhost;Port=5432;Database=helpdesk;Username=helpdesk;Password=local-development-only'
export Jwt__SigningKey="$(openssl rand -base64 48)"
```

Or use .NET user-secrets for local development:

```sh
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
```

The API does not automatically alter the database at startup. Apply migrations with:

```sh
dotnet tool restore
dotnet ef database update
```

Run and verify:

```sh
dotnet restore HelpDesk.sln
dotnet build HelpDesk.sln
dotnet test HelpDesk.sln
dotnet run --project HelpDesk.Api.csproj
```

Swagger is available in Development at `/swagger`; the OpenAPI JSON is at `/openapi/v1.json`. The PostgreSQL integration tests use Testcontainers and report a framework skipped result when Docker is unavailable.

## Create the first admin and log in

The initial admin bootstrap runs only in Development. After applying migrations, configure these values through user-secrets (or the corresponding `BootstrapAdmin__...` environment variables):

```sh
dotnet user-secrets set "BootstrapAdmin:Email" "admin@company.example"
dotnet user-secrets set "BootstrapAdmin:FullName" "Help Desk Administrator"
dotnet user-secrets set "BootstrapAdmin:Password" "use-a-unique-password-at-least-12-chars"
dotnet run
```

It creates the configured account only if that email is not already present. Remove the bootstrap password from local configuration after first startup. Additional users are created by an Admin using `POST /api/users`; public self-registration is not available. Passwords are stored as `PasswordHasher<User>` hashes.

In Swagger, submit `POST /api/auth/login` with the admin email and password. Copy `accessToken` from the response, click **Authorize**, paste the token without the `Bearer ` prefix, and authorize. Swagger supplies the bearer scheme for protected requests. Tokens expire after 60 minutes.

## Roles and routes

Employees can create, view, update, and comment on their own tickets. They only see their own tickets; another employee's ticket returns `404`. They cannot assign tickets or create internal notes. Agents can view all tickets, assign tickets, and create/read internal notes. Admins also create users, categories, and departments. All ticket and lookup routes require authentication; `POST /api/auth/login` is anonymous.

| Method | Route | Access / behavior |
|---|---|---|
| POST | `/api/auth/login` | Public login; returns a signed JWT |
| GET | `/api/users/me` | Authenticated profile |
| POST | `/api/users` | Admin creates a user and assigns a role |
| GET | `/api/tickets` | Authenticated; Employee sees only own tickets; supports filters and pagination |
| GET | `/api/tickets/{id}` | Authenticated; `404` for missing or unowned ticket |
| POST | `/api/tickets` | Authenticated; requester comes from token claims |
| PUT | `/api/tickets/{id}` | Employee may update own ticket; Agent/Admin may update any |
| DELETE | `/api/tickets/{id}` | Agent/Admin only |
| GET/POST | `/api/tickets/{id}/comments` | Authenticated; author comes from claims; Employee never sees internal notes |
| PUT | `/api/tickets/{id}/assignment` | Agent/Admin only |
| GET | `/api/categories`, `/api/departments` | Any authenticated user |
| POST | `/api/categories`, `/api/departments` | Admin only |

`GET /api/tickets` accepts `status`, `priority`, `categoryId`, `requesterId`, `assigneeId`, `createdFrom`, `createdTo`, and `search`. Sort fields are `id|title|status|priority|createdAt|updatedAt`; pagination defaults to `page=1&pageSize=20`, with page size capped at 100. The response includes items and page metadata.

Example ticket creation:

```json
{
  "title": "VPN connection issue",
  "description": "Unable to connect to the company VPN.",
  "priority": "High",
  "categoryId": 2
}
```

Example comment request (the server determines the author):

```json
{
  "body": "I am checking the VPN gateway logs.",
  "isInternal": true
}
```

Only Agent/Admin can set `isInternal` to true. The API ignores extra `requesterId` and `authorId` body fields; they are not part of the request DTOs.

## Design decisions and limitations

- Categories and departments are relational lookup tables referenced by tickets and users.
- Enums are stored as strings in PostgreSQL for readable persisted values. `TicketWorkflow` centralizes allowed status transitions.
- Ticket filtering applies ownership in the database query; `404` avoids disclosing another employee's ticket existence. Internal comments are filtered before the Employee response is created.
- `PasswordHasher<User>` provides password hashing without adding Identity's broader account tables and flows. User creation is Admin-only; Development bootstrap is configured using secrets or environment variables.
- JWT validation checks issuer, audience, signature, and expiration. Keep the signing key private and rotate it operationally if exposed.
- The project is not presented as production ready. Docker Compose, CI, key rotation operations, and production admin provisioning remain future work.
