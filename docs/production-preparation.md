# Production preparation (not deployed)

The intended API host is **Windows Azure App Service with standard IIS/ASP.NET Core Module integration**, with a Vercel static frontend and managed PostgreSQL. No cloud resources or CI/CD have been configured by these changes.

## Required configuration

Configure secrets in the API host's secure settings, never in source control or frontend variables.

| API setting | Requirement |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production`; if `DOTNET_ENVIRONMENT` is also set, keep it consistent |
| `ConnectionStrings__Database` | Managed PostgreSQL Npgsql connection string; configure certificate-verified TLS and network access |
| `Jwt__Key` | Cryptographically random Base64 signing key with at least 32 decoded bytes; stable across replicas and restarts |
| `Jwt__Issuer` | Stable identifier; existing default `ServiceHub.Api` is valid |
| `Jwt__Audience` | Stable identifier; existing default `ServiceHub.Web` is valid |
| `Jwt__ExpirationMinutes` | 1–60; default 30 |
| `Cors__AllowedOrigins__0` | Exact HTTPS frontend origin, without a trailing slash, path or wildcard |

Use subsequent CORS indices for additional explicitly trusted origins. Preview deployments are not automatically trusted. Production startup validates the database setting, JWT settings and HTTPS CORS origins before serving requests or executing seed commands. Validation errors identify settings without printing their values. Development retains local CORS and its database-free liveness endpoint; JWT validation remains enabled in every environment.

The frontend requires `VITE_API_BASE_URL=https://<api-host>` **at build time**, with no `/api` path. Builds fail for absent or non-HTTPS configuration. All browser API calls, including health, share this setting. Vite development retains the `http://localhost:5080` fallback. Every `VITE_` value is public; never put a signing key or database credential there. Rebuild after changing the API URL.

## Database initialization

Apply the committed ServiceHub migrations first through a controlled migration job or reviewed script. The Phase 6 migration needs `btree_gist` installed in `public`; a managed provider may require extension allowlisting and an administrator/migration identity. Keep migration permissions separate from normal API permissions. Never apply archived GoVilla migrations or reset the existing database.

Then run these one-off commands from the directory containing the published API, with the API environment settings above already supplied securely:

```text
dotnet ServiceHub.Api.dll --seed-roles
dotnet ServiceHub.Api.dll --seed-reference-data
```

Both commands exit after completion and are idempotent when run again. `--seed-reference-data` inserts only missing BusinessCategory reference records: Hair & Beauty, Fitness, Automotive, Professional Services and Home Services. It does not change existing records, apply migrations, create accounts, assign passwords, create businesses or create bookings. Run initialization as a single controlled task rather than concurrently on every replica.

The existing `--seed-development` command still rejects Production. Its current category definitions are shared with the reference-data seeder so they cannot drift; the production command never invokes the development seeder.

To exercise the reference-data command from source rather than published output, with Production settings already configured:

```text
dotnet run --project src/ServiceHub.Api --configuration Release --no-launch-profile -- --seed-reference-data
```

Use `--no-launch-profile` because the local launch profile selects Development.

## HTTPS and proxy handling

No additional forwarded-header middleware or broad proxy trust is added for the selected Windows/IIS hosting model. In-process hosting uses the IIS server integration; out-of-process IIS hosting enables restricted forwarding through the ASP.NET Core Module before application middleware. See [Microsoft's IIS integration guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0#iis-iis-express-and-asp-net-core-module).

Enable HTTPS-only access on the future App Service and verify the original HTTPS scheme after publishing. The API retains Production HSTS/HTTPS redirection and hides Swagger. Do not set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` or clear trusted proxy lists to accept arbitrary headers. Moving to Linux, containers, or adding another proxy requires a fresh review of the actual trusted proxy topology.

## Vercel frontend

Set the project root to `src/servicehub-web`, use the Vite preset, the pinned pnpm version and a compatible supported Node LTS. Build with `pnpm build` and serve `dist`. The local `vercel.json` provides the minimal SPA fallback to `/index.html`, allowing direct refresh of React Router routes. Vercel serves existing static files before the SPA fallback. Do not use `pnpm dev` or Vite preview as a production server.

Hosting remains deferred. Before an eventual release, configure database backups, runtime availability, secure networking, log collection and basic public authentication abuse protection, then verify deployed HTTPS/CORS and database behavior. `/api/health` remains a liveness check, not a database readiness check.
