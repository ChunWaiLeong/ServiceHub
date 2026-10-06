# ServiceHub

ServiceHub is a portfolio project for an appointment and service-booking platform. The intended product will let customers discover businesses and manage appointments, while business owners manage their profiles, services, availability, and bookings.

## Current status — Phase 2 persistence model

Implemented:

- A single .NET 10 ASP.NET Core API project with controller-based routing.
- `GET /api/health`, returning `{ "application": "ServiceHub API", "status": "Healthy" }`.
- Development Swagger UI and OpenAPI documentation.
- Central ProblemDetails exception handling, safe error responses, and console logging.
- EF Core with PostgreSQL, seven ServiceHub entities, explicit Fluent API mappings, and the first active migration.
- Identity-compatible user storage only (authentication is not configured).
- An opt-in development category seeder and PostgreSQL persistence tests.
- React, TypeScript, Vite, and Bootstrap application shell with responsive navigation.
- Home page and explicitly labelled Browse Services, Login, and Register placeholders.
- Live API connectivity status, timeout handling, and a retry action.
- Six API integration tests and six PostgreSQL-provider model/migration checks.
- Opt-in PostgreSQL tests for relationships, constraints, timestamps, snapshots, deletion protection, and seeding.

Authentication, JWT, business APIs, booking workflows, availability calculation, admin features, CI/CD, and deployment are not implemented. PostgreSQL migration application is pending local database setup. The health endpoint checks API liveness only; it does not check PostgreSQL.

## Architecture

```text
React + TypeScript
        | REST / JSON
ASP.NET Core API controllers
        | Application services (introduced with domain features)
EF Core ApplicationDbContext
        | PostgreSQL (schema defined; configure and apply migration locally)
```

The backend uses one project with folders for responsibilities. Controllers handle HTTP; future application services will enforce business rules and use EF Core directly. No MediatR, CQRS handlers, Dapper, repository wrappers, Keycloak, domain events, outbox, or Quartz is used.

```text
src/
  ServiceHub.Api/
    Controllers/
    Contracts/
    Data/
      Configurations/
      Migrations/
      Seeding/
    Models/
    ErrorHandling/
    Properties/
    Program.cs
  servicehub-web/
    src/
      api/
      components/
      pages/
      styles/
tests/
  ServiceHub.IntegrationTests/
docs/
  legacy/GoVilla.Migrations/
```

## Required tools

- .NET 10 SDK (stable). `global.json` accepts installed .NET 10 feature bands starting at 10.0.100.
- Node.js 22.12 or newer supported LTS; Node.js 24 was used for verification.
- pnpm 11.19.0, matching `packageManager` in the frontend package manifest.
- Git.
- PostgreSQL 16 or later is recommended for local development and persistence tests. PostgreSQL must be installed/running separately; this repository does not provision it. API health and the frontend shell still run without it.

If pnpm is not installed, install it using `npm install --global pnpm@11.19.0`.

## Run the backend

From the repository root:

```powershell
dotnet restore ServiceHub.sln
dotnet run --project src/ServiceHub.Api
```

The launch profile uses Development and `http://localhost:5080`.

- Health: http://localhost:5080/api/health
- Swagger UI: http://localhost:5080/swagger
- OpenAPI: http://localhost:5080/swagger/v1/swagger.json

Local development uses HTTP to avoid certificate setup. Outside Development, HTTPS redirection and HSTS are enabled, and Swagger is disabled. Production hosting configuration is deferred.

## Run the frontend

In a second terminal:

```powershell
cd src/servicehub-web
pnpm install
pnpm dev
```

Open http://localhost:5173. With the backend running, the footer displays **API connected**. If the API is stopped or unreachable, it displays **API unavailable** with a retry action.

Vite uses a strict port so it will not silently switch to an origin that the API has not allowed. Stop any other process using port 5173 before starting it.

The frontend makes a direct browser request to the backend; local CORS allows `http://localhost:5173`. Open that exact hostname rather than `127.0.0.1`, unless you also configure that origin.

## Configuration and secrets

ASP.NET Core loads `appsettings.json`, environment-specific appsettings, and environment variables. Nested environment keys use double underscores.

Database configuration is required for migration application, seeding, and persistence tests. API health still works without it. Set a connection string in your terminal or development environment:

```powershell
$env:ConnectionStrings__Database = 'Host=localhost;Port=5432;Database=servicehub;Username=<your-user>;Password=<your-local-password>'
dotnet run --project src/ServiceHub.Api
```

The example contains placeholders only. Keep real credentials out of source control. The API registers the PostgreSQL context lazily and gives a configuration error if database functionality is requested without a connection string. It does not connect, seed data, or apply migrations at startup.

To override the allowed frontend origin:

```powershell
$env:Cors__AllowedOrigins__0 = 'http://localhost:5173'
```

For the frontend, optionally copy `.env.example` to `.env.local` and set:

```dotenv
VITE_API_BASE_URL=http://localhost:5080
```

The default is already `http://localhost:5080`. Restart Vite after changing environment variables. `VITE_` values are public browser configuration and must never contain secrets. Local environment files, certificates, package output, and test results are ignored by Git. No dotenv loader is installed in the backend; use environment variables there.

## Build and test

From the root:

```powershell
dotnet build ServiceHub.sln
dotnet test ServiceHub.sln
```

Frontend:

```powershell
cd src/servicehub-web
pnpm build
```

`pnpm build` checks TypeScript and produces `dist/`. Commit `pnpm-lock.yaml` for reproducible dependency resolution. The pnpm build policy permits only esbuild's required install script.

The integration tests run the real ASP.NET Core request pipeline in memory and cover:

- Health response without database configuration.
- Allowed local CORS origin.
- Rejection of an unconfigured CORS origin.
- ProblemDetails for unknown routes.
- OpenAPI health documentation.
- Safe ProblemDetails for unexpected exceptions without leaking exception details.

Phase 2 verification: backend and frontend builds pass; 12 tests pass. Eleven PostgreSQL test methods are explicitly skipped because no PostgreSQL instance or test connection string was available. The parameterized PostgreSQL methods expand into additional cases when configured. Migration generation and provider-specific SQL generation pass, but these do not establish that the migration has been applied to a live database.

## Database model and migrations

See [the persistence design](docs/persistence.md) for fields, constraints, relationships, time rules, and the Identity storage decision.

| Entity | Purpose |
| --- | --- |
| ApplicationUser | Identity-compatible Guid user key, names, active status, creation timestamp |
| BusinessCategory | Unique category name |
| Business | One owner, one category, profile details, time-zone ID, active status, timestamps |
| Service | Business membership, price/currency/duration, active status, timestamps |
| BusinessWorkingHours | Multiple local recurring time intervals per day |
| BusinessBlockedPeriod | UTC closure interval and optional reason |
| Booking | Customer/business/service references, UTC interval/status, historical service snapshots |

Install or use a PostgreSQL server and create a new `servicehub` database with your own local role. That role must be able to create tables and indexes. Do not reuse the archived GoVilla schema. No credentials or database instance are provisioned by the repository.

From the repository root, after setting `ConnectionStrings__Database`:

```powershell
dotnet tool restore
dotnet ef database update --project src/ServiceHub.Api
```

The checked-in `dotnet-tools.json` pins the EF CLI to 10.0.4. It matches the EF Core design package. The design-time factory permits metadata-only commands without credentials; it never supplies a password or connects during migration generation.

Useful commands:

```powershell
# Generate a SQL script for review; does not require PostgreSQL.
dotnet ef migrations script --project src/ServiceHub.Api

# Check that the current model matches the snapshot.
dotnet ef migrations has-pending-model-changes --project src/ServiceHub.Api

# For a future approved model change; do not recreate InitialServiceHub.
dotnet ef migrations add <MigrationName> --project src/ServiceHub.Api --output-dir Data/Migrations
```

After applying the migration, optionally seed five development categories:

```powershell
dotnet run --project src/ServiceHub.Api -- --seed-development
```

This command uses the Development launch profile, inserts missing categories, and exits. It is rejected outside Development. It does not apply migrations, start the API, seed users, create roles, or create businesses/bookings. Categories are not inserted by the migration and normal API startup does not seed anything.

### PostgreSQL persistence tests

Create a separate empty `servicehub_tests` database with a local role that can create/drop schemas and their objects. Supply your actual credentials only in the terminal:

```powershell
$env:SERVICEHUB_TEST_DATABASE = 'Host=localhost;Port=5432;Database=servicehub_tests;Username=<your-test-user>;Password=<your-test-password>'
dotnet test ServiceHub.sln
```

The fixture creates one randomly named schema, applies the actual migration there, runs each test inside a transaction that rolls back, and drops only its own schema at the end. It never drops the database. Use a dedicated test database, not an application/production database. An interrupted test run may leave a `servicehub_test_...` schema for manual cleanup.

Without this variable, PostgreSQL tests report explicit skips. If it is configured but unreachable, the tests fail rather than silently skipping. The six provider model checks run without a server and inspect the Npgsql relational model and generated migration SQL; they are not substitutes for live PostgreSQL tests.

## Legacy transition

The four GoVilla backend projects and three legacy test projects were retired after the new foundation built successfully. Rental-domain tests and MediatR/DDD architecture tests no longer describe this application's behavior, so they were replaced with foundation integration checks. The old Docker/Keycloak configuration and CQRS illustration were also removed.

The original migration, designer, and model snapshot are preserved in `docs/legacy/GoVilla.Migrations` as historical reference. They are not part of the solution, do not compile, and must not be applied to a ServiceHub database. The first active ServiceHub migration is `20261006142457_InitialServiceHub`, under `src/ServiceHub.Api/Data/Migrations`. Apply it to a new dedicated ServiceHub database; migrating old GoVilla data is outside this phase. No existing database has been changed.

## Acknowledgements and licence

The starting repository was adapted from [kmorpex/booking-service](https://github.com/kmorpex/booking-service). Its original code is distributed under the MIT License, copyright (c) 2024 Konstantin Fedorov. The original `LICENSE` is preserved unchanged. ServiceHub is being redesigned incrementally as an independent portfolio application.
