# ServiceHub

ServiceHub is a portfolio project for an appointment and service-booking platform. The intended product will let customers discover businesses and manage appointments, while business owners manage their profiles, services, availability, and bookings.

## Current status — Phase 1 foundation

Implemented:

- A single .NET 10 ASP.NET Core API project with controller-based routing.
- `GET /api/health`, returning `{ "application": "ServiceHub API", "status": "Healthy" }`.
- Development Swagger UI and OpenAPI documentation.
- Central ProblemDetails exception handling, safe error responses, and console logging.
- EF Core with the PostgreSQL provider and an empty `ApplicationDbContext`.
- React, TypeScript, Vite, and Bootstrap application shell with responsive navigation.
- Home page and explicitly labelled Browse Services, Login, and Register placeholders.
- Live API connectivity status, timeout handling, and a retry action.
- Six API integration tests.

Authentication, domain entities, database migrations, booking functionality, admin features, CI/CD, and deployment are not implemented. The health endpoint checks API liveness only; it does not check PostgreSQL.

## Architecture

```text
React + TypeScript
        | REST / JSON
ASP.NET Core API controllers
        | Application services (introduced with domain features)
EF Core ApplicationDbContext
        | PostgreSQL (configured for future database operations)
```

The backend uses one project with folders for responsibilities. Controllers handle HTTP; future application services will enforce business rules and use EF Core directly. No MediatR, CQRS handlers, Dapper, repository wrappers, Keycloak, domain events, outbox, or Quartz is used.

```text
src/
  ServiceHub.Api/
    Controllers/
    Contracts/
    Data/
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
- PostgreSQL will be required when database-backed features are introduced. It is not needed for Phase 1 health or UI verification.

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

Database configuration is optional for Phase 1. Before any future database-backed operation, set a connection string in your terminal or development environment:

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

Browser verification also confirmed API connectivity and placeholder navigation. PostgreSQL connectivity has not been verified in Phase 1 because no database-backed features exist yet.

## Legacy transition

The four GoVilla backend projects and three legacy test projects were retired after the new foundation built successfully. Rental-domain tests and MediatR/DDD architecture tests no longer describe this application's behavior, so they were replaced with foundation integration checks. The old Docker/Keycloak configuration and CQRS illustration were also removed.

The original migration, designer, and model snapshot are preserved in `docs/legacy/GoVilla.Migrations` as historical reference. They are not part of the solution, do not compile, and must not be applied to a ServiceHub database. The next database phase must deliberately choose its migration strategy and account for any existing data. No database has been changed by this phase.

## Acknowledgements and licence

The starting repository was adapted from [kmorpex/booking-service](https://github.com/kmorpex/booking-service). Its original code is distributed under the MIT License, copyright (c) 2024 Konstantin Fedorov. The original `LICENSE` is preserved unchanged. ServiceHub is being redesigned incrementally as an independent portfolio application.
