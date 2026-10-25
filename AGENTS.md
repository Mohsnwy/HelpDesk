# AGENTS.md — HelpDesk.Api

## Mission

Develop this repository into a polished, maintainable, portfolio-quality **internal Help Desk / Ticket Management REST API** for a company's employees, suitable for demonstrating junior .NET backend engineering skills.

Work independently and professionally. Inspect the codebase, make sound engineering decisions, implement the roadmap in coherent increments, run available checks, and keep documentation accurate. Do not pause to ask the user to approve routine changes. The user will review the code and git history independently.

Ask the user only when a decision genuinely requires their preference, credentials or external permissions, or involves significant cost, irreversible impact, data loss, or consequential security implications. Do not ask for confirmation before ordinary local code changes.

## Domain

This is an **internal company help desk**. Employees raise tickets (IT issues, HR requests, facilities problems, access requests, etc.) and support staff resolve them.

### Roles

| Role | Capabilities |
|------|--------------|
| `Employee` | Create tickets, view and update their own tickets, comment on their own tickets, close their own tickets. Cannot see other employees' tickets. |
| `Agent` | View all tickets, assign tickets, change status and priority, comment (including internal notes visible only to agents). |
| `Admin` | Everything an Agent can do, plus manage users, roles, departments, and categories. |

### Core entities (keep scope to these unless there is a strong reason)

- **User** (employee): id, full name, email, department, role, active flag, created timestamp (UTC).
- **Ticket**: id, title, description, status, priority, category, requester (the employee who raised it), assignee (an agent, nullable), created/updated/resolved timestamps (UTC).
- **TicketComment**: id, ticket, author, body, `IsInternal` flag (agent-only notes), created timestamp (UTC).
- **Category** and **Department**: simple lookup entities (or enums/strings if that is clearly simpler — decide and document).

### Status workflow

`Open` → `InProgress` → `Resolved` → `Closed`, with `Reopened` allowed from `Resolved`/`Closed`. Enforce valid transitions in one place (service layer or domain logic), not scattered across controllers. Return `400`/`409` with a clear message for invalid transitions.

### Priority values

`Low`, `Medium`, `High`, `Critical`.

## Working principles

1. Inspect the repository, target framework, project structure, and Git status before editing.
2. Preserve unrelated user changes. Never discard or overwrite existing work without understanding it.
3. Implement working functionality, not just plans or pseudocode.
4. Use idiomatic, readable, maintainable C# and established ASP.NET Core practices appropriate to the target framework.
5. Make cohesive changes in logical milestones; do not stop after every small change to request approval.
6. Avoid unnecessary abstractions, overengineering, and speculative features.
7. Add dependencies only when justified. Verify compatibility with the target framework.
8. Run `dotnet build` and `dotnet test` after changes. Fix issues caused by your changes and report any checks that could not be run.
9. Never claim that tests, builds, migrations, deployments, or manual API checks succeeded unless actually verified.
10. Never commit credentials, tokens, passwords, private keys, or production connection strings. Use environment variables or secure configuration.
11. Do not deploy, incur costs, create external accounts, alter external systems, or perform destructive database/file operations without explicit authorization.
12. Keep the project runnable and avoid leaving it in a broken intermediate state.
13. Update documentation to reflect the actual implementation, setup, routes, configuration, migrations, tests, and known limitations.
14. **Scope constraints:** backend API only (no frontend unless the user explicitly changes scope); stay on ASP.NET Core, C#, and PostgreSQL unless there is a compelling compatibility reason.

## Version control workflow

- Work on a dedicated branch (e.g. `feature/roadmap`) rather than directly on `main`, unless the repo's existing convention says otherwise.
- Make **one commit per stage** (more if a stage is large), each with a clear message such as `Stage 3: add EF Core and PostgreSQL persistence`.
- Only commit when the build passes and tests pass. Never commit secrets, `bin/`, `obj/`, or local config.
- Do not push, open pull requests, or alter remotes unless the user explicitly asks.

## Definition of done (applies to every stage)

A stage is complete only when:

1. `dotnet build` succeeds with no new warnings introduced by the change.
2. `dotnet test` passes (once the test project exists), and the stage's new behavior has tests.
3. README and `docs/progress.md` reflect the stage.
4. The stage is committed.

- Tests that cannot run in the current environment (e.g. Docker unavailable) must be marked
  as **Skipped** using the test framework's skip mechanism, never return early and pass.
  The report must state passed, failed and skipped counts separately.
- New services, middleware, and validation logic require tests in the same stage; do not defer.
- If the workspace is not a Git repository, report it as a blocker in the first message
  instead of continuing without version control.
- Work on the current feature branch only. Never commit to or merge into `main`.
## Project context

- Project: `HelpDesk.Api`
- Purpose: internal employee Help Desk / Ticket Management REST API for a software engineering portfolio.
- Environment: macOS, VS Code/Codex, .NET SDK.
- **Core stack:** C#, ASP.NET Core Web API with controllers, Entity Framework Core, **PostgreSQL** (via `Npgsql.EntityFrameworkCore.PostgreSQL`), OpenAPI/Swagger UI.
- Planned quality features: validation, consistent error responses, authentication and authorization, filtering/pagination, automated tests, Docker, CI/CD, and deployment documentation.
- The application has previously run locally and Swagger UI was successfully opened.
- Earlier versions included a `Ticket` model, a `CreateTicketRequest` DTO, and a `TicketsController` with list/create endpoints.
- Ticket data may currently be held in a static in-memory list and therefore disappear when the application restarts.
- This context may be outdated. Inspect the actual repository before relying on it.

## Implementation roadmap

Follow these stages in order, adapting to what is already implemented. Do not duplicate existing functionality. Continue through as many stages as can be completed safely and coherently without requesting routine approval. **Add tests alongside the code from Stage 4 onward** — do not defer them to the end.

### Stage 1 — Audit and stabilize

- Inspect `git status`, the project file, `Program.cs`, controllers, models, configuration, and existing tests.
- Identify the target .NET version and current API behavior.
- Ensure the project builds and existing endpoints remain functional.
- Remove template/demo endpoints such as WeatherForecast only if they are no longer useful and removal will not break existing work.
- Ensure OpenAPI and Swagger UI configuration suits the target framework and development environment.
- Create `docs/progress.md` to log what each stage did, decisions made, and remaining work.

### Stage 2 — Complete ticket CRUD

Implement and verify:

- `GET /api/tickets` — list tickets.
- `GET /api/tickets/{id}` — retrieve one ticket.
- `POST /api/tickets` — create a ticket and return `201 Created` with a location pointing to the created resource.
- `PUT /api/tickets/{id}` — update a ticket, or use `PATCH` if there is a clear reason.
- `DELETE /api/tickets/{id}` — delete a ticket.
- Return appropriate HTTP status codes, including `200`, `201`, `204`, `400`, and `404` where applicable.
- Ensure IDs are generated safely for the storage approach; avoid collision-prone `Count + 1` logic.
- Use request/response DTOs to control client input and output.
- Validate required fields and accepted status/priority values (use enums for status and priority).
- Keep route and response conventions consistent.
- Shape the Ticket model for the employee help desk domain (category, requester, assignee, timestamps). Requester/assignee may be placeholders until Stage 3 and Stage 8 introduce real users.

### Stage 3 — PostgreSQL and Entity Framework Core

- Add Entity Framework Core and `Npgsql.EntityFrameworkCore.PostgreSQL`, using versions compatible with the target framework.
- Configure a `DbContext` and register it through dependency injection.
- Model the domain entities (User, Ticket, TicketComment, Category, Department) with explicit relationships, required fields, max lengths, indexes (e.g. on status, assignee, requester), and enum-to-string conversion for readability in the database.
- Replace the static in-memory list with persistent database storage.
- Add and document EF Core migrations (`dotnet ef`). Do not apply migrations to any non-local database.
- Use asynchronous database APIs where appropriate.
- Add seed data for lookup tables (categories, departments) via migrations or a safe development-only seeder.
- Configure connection strings through environment variables, user secrets, or other safe configuration. Never hardcode real credentials.
- Provide `appsettings.Development.example.json` (or equivalent) with placeholders.
- Document PostgreSQL setup (including a `docker run` one-liner for a local Postgres) and migration commands in the README.
- If a local database is unavailable, complete all independent work and accurately report what could not be verified.

### Stage 4 — Testing foundation

Establish the test infrastructure before building further features, so every later stage ships with tests.

- Create a test project (e.g. `HelpDesk.Api.Tests`) using xUnit, referenced from the solution.
- **Unit tests:** isolate business logic (status transition rules, validation rules, mapping) and test it without a database or HTTP.
- **Integration tests:** use `WebApplicationFactory<Program>` to exercise the real HTTP pipeline. Prefer a real PostgreSQL via **Testcontainers** (`Testcontainers.PostgreSql`) for fidelity; if Docker is unavailable, skip those tests gracefully with a clear skip reason rather than failing, and report this. Do not use the EF Core InMemory provider as a substitute for relational behavior.
- Each integration test must start from a known database state (fresh container/schema or reset between tests) so tests are deterministic and order-independent.
- Tests must not require personal database credentials or a pre-existing local database.
- Cover for the existing ticket CRUD: create (valid and invalid), get (found and `404`), list, update (valid, invalid, `404`), delete (success and `404`), and `Location` header on create.
- Add test helpers/builders where they reduce duplication; avoid over-engineering the harness.
- Document how to run unit tests, integration tests, and how Docker is used, in the README.
- Add a coverage tool (e.g. `coverlet.collector`) only if it is simple to wire up; do not chase a coverage percentage.

### Stage 5 — Architecture and error handling

- Introduce services (e.g. `ITicketService`) or other abstractions only where they improve separation of concerns and testability.
- Use dependency injection appropriately.
- Centralize the ticket status workflow rules in one place with unit tests.
- Add consistent error handling (global exception handling middleware or `IExceptionHandler`) returning RFC 7807 `ProblemDetails`.
- Use standard validation and error response conventions (`ValidationProblemDetails`, FluentValidation only if justified).
- Add suitable structured logging; never log passwords, tokens, or sensitive personal data.
- Do not return internal exception details or stack traces to API clients.
- Keep controllers focused on HTTP concerns and orchestration.
- Add/adjust tests for every behavior changed or introduced in this stage.

### Stage 6 — Comments and API usability

- Implement ticket comments: `GET/POST /api/tickets/{ticketId}/comments`, with an `IsInternal` flag (enforcement of who may see/create internal notes arrives with auth in Stage 8; model it now).
- Implement assignment: assign/unassign a ticket to an agent.
- Add useful filtering (status, priority, category, requester, assignee, date range, text search), sorting, and pagination.
- Validate page sizes and filter values; cap maximum page size.
- Return pagination metadata in a consistent response shape.
- Add clear endpoint descriptions, request/response schemas, and examples to OpenAPI (XML comments or attributes).
- Document API routes and example requests.
- Add tests for filtering, sorting, pagination edge cases (page 0, oversized page, empty results), comments, and assignment.

### Stage 7 — Users and departments (supporting data)

- Implement endpoints for departments and categories (read for all authenticated users; write reserved for `Admin` once auth is in place).
- Prepare the User entity and profile endpoints needed by authentication (no password handling beyond what Stage 8 introduces).
- Add tests for the new endpoints.

### Stage 8 — Authentication and authorization

- Implement **JWT bearer authentication** backed by **ASP.NET Core Identity** (or a simple user table using `PasswordHasher<T>` if Identity is clearly too heavy — document the choice). Never store plaintext passwords.
- Implement registration (restricted — e.g. admin-created users or company-email self-registration, decide and document) and login returning a signed token with a reasonable expiry.
- Implement the roles from the Domain section (`Employee`, `Agent`, `Admin`) and enforce them:
  - Employees can only see and modify their own tickets and comments; they never see internal comments.
  - Agents can see all tickets, assign, change status/priority, and write internal notes.
  - Admins manage users, roles, departments, and categories.
- The requester of a ticket is always taken from the authenticated user, never from the request body.
- Use policy-based or role-based `[Authorize]` attributes; return `401` for unauthenticated and `403` for forbidden, and `404` (not `403`) where revealing existence of another employee's ticket would leak information — document the choice.
- JWT signing key and settings come from environment variables or user secrets only; document required configuration without exposing secrets. Fail fast at startup if the key is missing or too short outside of tests.
- Do not add insecure placeholder authentication or claim production readiness without appropriate security controls and tests.
- Add tests: login success/failure, unauthenticated access, each role's allowed and forbidden actions, ownership enforcement, internal-comment visibility, and token validation.

### Stage 9 — Portfolio readiness

- Add a multi-stage `Dockerfile` and a `docker-compose.yml` (API + PostgreSQL) once the application and configuration are stable. Use placeholder/env-based credentials, never real ones.
- Add or improve `.gitignore` and `.dockerignore` as needed.
- Add GitHub Actions CI to restore, build, and test (with a PostgreSQL service or Testcontainers) without exposing secrets.
- Write a professional README with: project overview, domain and roles, architecture, prerequisites, configuration, database/migration instructions, run/test commands, API examples (curl or `.http` file), and known limitations.
- Add a "Design decisions" section to the README explaining why key choices were made (e.g. enums as strings, status workflow location, 404-vs-403 policy, Identity vs custom users, Testcontainers).
- Keep documentation aligned with actual behavior.
- Do not deploy externally or incur charges without explicit authorization.
- Do not describe the project as production-ready unless its implementation and operational controls justify that claim.

## Code quality requirements

- Use the existing target framework; do not upgrade major versions without a clear reason. If the repo has no clear target, prefer the current .NET LTS release.
- Keep naming, namespaces, routes, and response conventions consistent.
- Do not expose persistence entities directly when DTOs are more appropriate.
- Do not allow clients to set server-controlled fields such as generated IDs, creation timestamps, requester, or resolved timestamps.
- Use UTC timestamps (`DateTime.UtcNow`/`DateTimeOffset`) consistently; PostgreSQL `timestamptz` where appropriate.
- Prefer asynchronous operations when appropriate and pass `CancellationToken` through to database calls.
- Handle missing resources explicitly.
- Avoid broad exception handling that hides failures.
- Keep configuration environment-specific and safe.
- Do not leak secrets or sensitive internal details in API responses.
- Treat employee data (names, emails) as personal data: expose only what an endpoint needs.

## Communication and completion report

- Do not provide tutorials or coding exercises in chat. Keep the final report concise and professional.
- The final report should summarize: implemented features per stage, important decisions, build/test results (actual, verified), configuration the user must provide, and outstanding blockers.
- Put design rationale in the README "Design decisions" section and `docs/progress.md`, not in long chat explanations.
- Do not ask the user to approve routine implementation steps. Make sensible decisions and carry the work forward.
- If blocked by credentials, a missing database service, unavailable Docker, unavailable network, or external permissions, continue with safe work that does not depend on the blocker and identify the exact remaining requirement.

## First action

Inspect the repository and current Git changes, create or switch to the working branch, then implement the next coherent milestone immediately. Do not stop after an audit or plan unless a genuine blocker prevents safe progress. Continue through the roadmap as far as practical, preserve a buildable state, run checks, update documentation, and commit per stage.