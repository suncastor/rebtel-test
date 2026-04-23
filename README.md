# Library API

A small library-statistics service. It exposes a public HTTP API that answers questions like "which books get borrowed most often?" and "how fast does this user read?". Under the hood, the HTTP API is a thin layer that calls a gRPC backend, which reads from Postgres.

```
  Browser / curl                 ┌──────────────────────┐        ┌──────────────────────┐        ┌────────────┐
 ────────────────>  Library.Api  │   HTTP REST façade   │ ─gRPC> │  Library.Application │ ─EF──> │  Postgres  │
                                 │  (:5279 / :7231)     │        │     (:7232, gRPC)    │        │            │
                                 └──────────────────────┘        └──────────────────────┘        └────────────┘
```

Two processes must be running for the HTTP API to answer: **Library.Application** (gRPC + DB) and **Library.Api** (HTTP).

## Projects at a glance

| Project                      | What it is                                                     |
|------------------------------|----------------------------------------------------------------|
| `Library.Api`                | ASP.NET Core HTTP API (the thing you hit with curl/browser).   |
| `Library.Application`        | gRPC service hosting the business logic + EF Core.             |
| `Library.Contracts`          | Shared `.proto` files (generates both client & server stubs).  |
| `Library.Infrastructure`     | `AppDbContext`, entities, repositories, migrations, seeding.   |
| `Library.Warmups`            | Startup-warmup helpers.                                        |
| `Library.UnitTests`          | Pure unit tests for service classes.                           |
| `Library.IntegrationTests`   | Repository/query tests against a real Postgres (Testcontainers). |
| `Library.FunctionalTests`    | Feature-level tests against a real Postgres (Testcontainers).  |
| `Library.SystemTests`        | End-to-end: HTTP API → in-memory gRPC → real Postgres.         |
| `Library.Warmups.Tests`      | Unit tests for the warmup helpers.                             |

## Prerequisites

Install these first (once):

1. **.NET 9 SDK** — https://dotnet.microsoft.com/download/dotnet/9.0
   - Verify: `dotnet --version` should print `9.x.x`.
2. **PostgreSQL 16** running locally on the default port (`5432`).
   - Easiest: run it in Docker — `docker run --name library-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16-alpine`
   - The app expects a user `postgres` with password `postgres` and will create the `library` database on first run via EF migrations. (If you have a different local password, update `Library.Application/appsettings.json` → `ConnectionStrings:DefaultConnection`.)
3. **Docker Desktop** — required *only* if you want to run the Integration/Functional/System tests. They spin up throwaway Postgres containers via Testcontainers. Not needed for unit tests or for running the app (if you already have Postgres some other way).
4. **HTTPS dev certificate trusted** (one-time, per machine):
   ```bash
   dotnet dev-certs https --trust
   ```
   Without this, the HTTP API's gRPC call to `https://localhost:7232` will fail with a cert error.

## First-time setup

From the repo root:

```bash
dotnet restore
dotnet build
```

EF migrations run automatically on `Library.Application` startup, and the DB is seeded with a few books/users/borrowings (unless `ASPNETCORE_ENVIRONMENT=Testing`). You do **not** need to run `dotnet ef database update` manually.

## Running the services

You need **both** services running, in this order: gRPC backend first, HTTP API second.

### Option A — From the CLI (two terminals)

Terminal 1 — the gRPC backend + DB:
```bash
dotnet run --project Library.Application
```
It listens on `https://localhost:7232` and will apply migrations + seed data on first boot. Wait until you see `Now listening on: https://localhost:7232` before starting the API.

Terminal 2 — the HTTP API:
```bash
dotnet run --project Library.Api
```
It listens on `http://localhost:5279` (and `https://localhost:7231` when launched with the `https` profile). Use the `https` profile with:
```bash
dotnet run --project Library.Api --launch-profile https
```

To stop either service: `Ctrl+C` in its terminal.

### Option B — From VS Code

1. Install the **C# Dev Kit** extension (Microsoft). It picks up `LibraryApi.sln` automatically.
2. Open the repo folder in VS Code.
3. Open the Run and Debug panel (`Ctrl+Shift+D`).
4. You'll see launch profiles generated from each project's `launchSettings.json`. Start them in this order:
   - **Library.Application** (profile: `Library.Application`)
   - **Library.Api** (profile: `http` or `https`)
5. VS Code shows the output in the Debug Console. You can set breakpoints in either project and hit them via a browser/curl request.

Tip: to run both at once, use the Debug panel's compound launch or just start them one after the other — each runs in its own debug session.

### Option C — From Visual Studio / Rider

Set multiple startup projects on `Library.Api` and `Library.Application`, then hit F5.

## Calling the API

Default HTTP base URL: `http://localhost:5279`. Four endpoints are exposed.

### 1. Most-borrowed books

```
GET /books/most-borrowed?top=10
```

Browser:
```
http://localhost:5279/books/most-borrowed?top=5
```

curl:
```bash
curl "http://localhost:5279/books/most-borrowed?top=5"
```

### 2. Co-borrowed books (books frequently borrowed by the same users as a given book)

```
GET /books/{bookId}/co-borrowed?top=10
```

Browser:
```
http://localhost:5279/books/1/co-borrowed?top=5
```

curl:
```bash
curl "http://localhost:5279/books/1/co-borrowed?top=5"
```

### 3. Top borrowers in a date range

```
GET /users/top-borrowers?from=2026-01-01&to=2026-04-01&top=10
```

`from` and `to` are optional ISO-8601 dates. Omit both to scan all borrowings.

Browser:
```
http://localhost:5279/users/top-borrowers?from=2026-01-01&to=2026-04-01&top=5
```

curl:
```bash
curl "http://localhost:5279/users/top-borrowers?from=2026-01-01&to=2026-04-01&top=5"
```

### 4. Reading pace for a user (pages/day)

```
GET /users/{userId}/reading-pace
```

Browser:
```
http://localhost:5279/users/1/reading-pace
```

curl:
```bash
curl "http://localhost:5279/users/1/reading-pace"
```

### OpenAPI

In Development, the API exposes an OpenAPI document at [`http://localhost:5279/openapi/v1.json`](http://localhost:5279/openapi/v1.json). Paste it into any Swagger/Scalar viewer if you want an interactive UI.

## Running the tests

All test projects use xUnit. From the repo root:

```bash
# Everything
dotnet test

# A single project
dotnet test Library.UnitTests
dotnet test Library.IntegrationTests
dotnet test Library.FunctionalTests
dotnet test Library.SystemTests
dotnet test Library.Warmups.Tests

# A single test
dotnet test --filter "FullyQualifiedName~MostBorrowed"
```

**Which tests need what:**

| Suite                    | Needs Docker? | Needs running services? | What it covers                         |
|--------------------------|---------------|-------------------------|-----------------------------------------|
| `Library.UnitTests`      | No            | No                      | Pure service-layer logic with mocks.    |
| `Library.Warmups.Tests`  | No            | No                      | Warmup helpers.                         |
| `Library.IntegrationTests` | Yes         | No                      | Repositories against a throwaway Postgres container. |
| `Library.FunctionalTests`  | Yes         | No                      | Feature slices against a throwaway Postgres container. |
| `Library.SystemTests`    | Yes           | No                      | HTTP API wired to in-memory gRPC host + throwaway Postgres (full stack, in-process). |

For the three Docker-backed suites: make sure Docker Desktop is running before you hit `dotnet test` — Testcontainers spins up `postgres:16-alpine` on demand and tears it down after.

### In VS Code

The C# Dev Kit adds a Test Explorer (the beaker icon). Click the play button next to any test or suite to run/debug it.

## Troubleshooting

- **`Npgsql.NpgsqlException: Connection refused`** — Postgres isn't running, or it's on a non-default port / has a different password. Check `Library.Application/appsettings.json`.
- **`The remote certificate is invalid` when the API calls gRPC** — run `dotnet dev-certs https --trust`.
- **Tests hang on startup** — Docker Desktop isn't running, so Testcontainers can't pull/start Postgres.
- **Port already in use (5279 / 7231 / 7232)** — another process is bound to the port. Stop it, or change the port in each project's `launchSettings.json` (and, for the API→gRPC link, in `Library.Api/appsettings*.json` → `Services:LibraryGrpc`).
- **`dotnet` not found** — the .NET 9 SDK isn't installed or isn't on PATH.
