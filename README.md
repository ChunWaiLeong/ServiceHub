# ServiceHub

ServiceHub is a portfolio appointment and service-booking platform built incrementally with React, ASP.NET Core and PostgreSQL.

## Current status — Phase 3 authentication

Implemented: .NET 10 controller API, health/Swagger/ProblemDetails, EF Core PostgreSQL persistence, Identity registration/login, JWT authentication, role authorization, React authentication forms, account page and a protected owner dashboard placeholder.

Business/service management, availability, booking workflows, admin management, refresh tokens, password reset, email verification, MFA, CI/CD and deployment are deferred. Domain tables exist; their workflows do not.

## Architecture

```text
React + TypeScript + Vite + Bootstrap
             | REST / JSON / Bearer token
ASP.NET Core API controllers
             | AuthService / JwtTokenService
ASP.NET Core Identity / EF Core ApplicationDbContext
             | PostgreSQL
```

One backend project uses explicit services and EF Core directly. No MediatR, CQRS handlers, generic repositories, UnitOfWork, Dapper, Keycloak, domain events, outbox or Quartz.

```text
src/ServiceHub.Api/
  Authentication/ Configuration/ Controllers/ Contracts/Auth/
  Services/ Data/Configurations/ Data/Migrations/ Data/Seeding/
  Models/ ErrorHandling/ Properties/ Program.cs
src/servicehub-web/src/
  api/ auth/ components/ pages/ styles/
tests/ServiceHub.IntegrationTests/
docs/authentication.md
docs/persistence.md
docs/legacy/GoVilla.Migrations/
```

## Development tools

- .NET 10 SDK; global.json accepts feature bands starting at 10.0.100.
- Node.js 22.12 or newer supported LTS; Node.js 24 used for verification.
- pnpm 11.19.0 (`npm install --global pnpm@11.19.0` if needed).
- PostgreSQL 16 or later recommended, running separately; Git.

## Configuration and local startup

Run commands from the repository root. ASP.NET Core reads appsettings, Development user secrets and environment variables. The backend does **not** automatically read root `.env.local`. Changed Windows user variables require a new terminal/process.

Configure the development connection string securely. For example, use .NET user secrets (replace placeholders locally):

```powershell
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=servicehub;Username=<local-user>;Password=<local-password>" --project src/ServiceHub.Api

# Generate once; never paste the key into source control or chat.
$serviceHubJwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "Jwt:Key" $serviceHubJwtKey --project src/ServiceHub.Api
Remove-Variable serviceHubJwtKey
```

User secrets live outside the repository; they are a development convenience, not encrypted production storage. Environment variables can supply the same settings:

| Variable | Purpose/default |
| --- | --- |
| ConnectionStrings__Database | Required for database-backed functionality |
| Jwt__Key | Required Base64 random key, at least 32 decoded bytes; no default |
| Jwt__Issuer | ServiceHub.Api |
| Jwt__Audience | ServiceHub.Web |
| Jwt__ExpirationMinutes | 30; accepted range 1–60 |
| Cors__AllowedOrigins__0 | http://localhost:5173 in Development |
| SERVICEHUB_TEST_DATABASE | Dedicated PostgreSQL test database connection string |

A missing/invalid JWT key fails API startup. Health checks liveness, not PostgreSQL. No automatic startup migrations or seeding.

```powershell
dotnet restore ServiceHub.sln
dotnet tool restore
dotnet ef database update --project src/ServiceHub.Api
dotnet run --project src/ServiceHub.Api -- --seed-roles
# Optional development categories:
dotnet run --project src/ServiceHub.Api -- --seed-development
dotnet run --project src/ServiceHub.Api
```

Use the existing ServiceHub database; do not reset it. Initial migration: `20261006142457_InitialServiceHub`. Phase 3 requires no new migration: Phase 2 already included Identity tables. Role seeding is explicit and idempotent, creates Customer/BusinessOwner/Admin, and creates no admin account. Category seeding is Development-only.

Development serves http://localhost:5080. Health: `/api/health`; Swagger: `/swagger`; OpenAPI: `/swagger/v1/swagger.json`. Local HTTP avoids certificate setup. Outside Development, HTTPS redirection/HSTS are enabled and Swagger is disabled. Public hosting is deferred.

In a second terminal:

```powershell
cd src/servicehub-web
pnpm install
pnpm dev
```

Open http://localhost:5173 (the exact hostname allowed by CORS). Vite uses a strict port. Optional frontend `.env.local`: `VITE_API_BASE_URL=http://localhost:5080`. Restart Vite after changing it. VITE_ values are public and must contain no secrets. Environment files, generated output and test results are Git-ignored.

## Authentication

| Endpoint | Access/result |
| --- | --- |
| POST /api/auth/register | Public; Customer/BusinessOwner only; 201, then log in separately |
| POST /api/auth/login | Public; 200 token/expiration/user DTO; generic 401 on invalid credentials |
| GET /api/auth/me | Authenticated; current user from JWT claims |
| GET /api/auth/business-owner | BusinessOwner only; permission demonstration, no business management |

Registration takes firstName, lastName, email, password and role. Login takes email/password. Identity owns hashing, normalization and credential verification. Passwords require at least 12 characters plus uppercase, lowercase, digit and symbol. Five failed attempts lock an account for 15 minutes. Public Admin registration is rejected.

AuthContext holds the token **in memory only**. Login updates navigation; `/account` requires authentication and `/business/dashboard` requires BusinessOwner. Logout/expiry clear state; page refresh requires another login. There is no stateless API logout endpoint: issued tokens remain valid until expiration. No browser storage, refresh cookies or refresh tokens. Memory storage reduces persistent exposure but does not protect against malicious JavaScript/XSS.

See [authentication design](docs/authentication.md) for flow and security trade-offs.

## Database and tests

See [persistence design](docs/persistence.md) for ApplicationUser, Business, BusinessCategory, Service, BusinessWorkingHours, BusinessBlockedPeriod and Booking mappings. Booking snapshots preserve history; ServiceHub foreign keys restrict deletion. Working hours use local recurring times; appointments use UTC and half-open intervals `[start, end)`. Overlap protection is deferred.

```powershell
dotnet build ServiceHub.sln
# Configure SERVICEHUB_TEST_DATABASE securely first:
dotnet test ServiceHub.sln
dotnet ef migrations has-pending-model-changes --project src/ServiceHub.Api
dotnet ef migrations script --project src/ServiceHub.Api
```

Frontend production build:

```powershell
cd src/servicehub-web
pnpm build
```

The dedicated test database role needs create/drop schema permissions. Fixtures apply the actual migration in random schemas and drop only those schemas. Persistence cases roll back transactions; HTTP cases write only to their isolated schema. Interrupted runs can leave schemas for manual cleanup. Never point tests at production.

Without SERVICEHUB_TEST_DATABASE, PostgreSQL tests explicitly skip; invalid configured connections fail. Model/SQL checks do not replace database tests. Phase 3 verification: **47 passed, 0 failed, 0 skipped**, including existing persistence checks and the actual JWT pipeline. Backend/frontend builds pass. Browser checks verified both registration types, login, /me, owner authorization, protected navigation and logout. Two demo accounts were created in the development database during verification.

## Legacy transition and licence

Old GoVilla projects and rental/architecture tests were retired in Phase 1. Archived migrations in docs/legacy/GoVilla.Migrations are historical references only, outside active migration history. Do not apply them to ServiceHub. No old architecture was reintroduced.

Adapted from [kmorpex/booking-service](https://github.com/kmorpex/booking-service), MIT License, copyright (c) 2024 Konstantin Fedorov. Original LICENSE is preserved unchanged.
