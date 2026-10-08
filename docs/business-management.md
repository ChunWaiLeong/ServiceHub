# Phase 4 business profiles and service management

This document describes the Phase 4 business/service scope. Phase 5 availability is now implemented; see [availability](availability.md).

## Scope and flow

BusinessOwner → create one profile → edit profile → create/edit services → deactivate/reactivate services. Public visitors/Customers → search/filter businesses → open a public profile → view active services. Availability is covered separately in Phase 5; appointment booking, reviews, payments and admin management remain deferred.

React forms call api/businesses.ts and api/services.ts through the existing shared API client. AuthContext supplies bearer tokens for owner calls. Controllers validate requests and derive the authenticated owner ID from sub. BusinessService and ServiceManagementService enforce references, ownership and persistence rules through ApplicationDbContext, with asynchronous EF operations and request cancellation tokens. No repositories or service interfaces were added.

## Endpoints

| Method and route | Access and response |
| --- | --- |
| GET /api/categories | Public, seeded category IDs/names |
| GET /api/time-zones | Public, supported IANA IDs/display labels |
| GET /api/businesses | Public, paged active business summaries |
| GET /api/businesses/{id} | Public, active business details |
| POST /api/businesses | BusinessOwner, 201 with business DTO/Location |
| PUT /api/businesses/{id} | Owning BusinessOwner, 200 updated DTO |
| GET /api/owner/business | BusinessOwner's profile; 404 if none |
| GET /api/owner/business/services | Owner's services, including inactive ones |
| GET /api/businesses/{businessId}/services | Public active services |
| GET /api/businesses/{businessId}/services/{serviceId} | Public active service |
| POST /api/businesses/{businessId}/services | Owning BusinessOwner, 201 DTO/Location |
| PUT /api/businesses/{businessId}/services/{serviceId} | Owning BusinessOwner, 200 DTO |
| PATCH /api/businesses/{businessId}/services/{serviceId}/status | Owning BusinessOwner; body {"isActive":true/false}; 200 DTO |

Public business details and services are separate reads to keep responses focused. Management reads have a distinct owner route so public service responses always exclude inactive rows, even when called by an owner. Responses never serialize EF navigation graphs or expose owner IDs.

## Ownership and server-controlled fields

POST business accepts name, description, businessCategoryId, address, contactPhone (optional), contactEmail and timeZoneId. OwnerId is always taken from verified claims. ID, active creation status and UTC timestamps are server-controlled. PUT uses the same editable fields and preserves ownership, active status and creation date.

Role authorization happens before application services: unauthenticated owner calls return 401; Customer/Admin callers return 403. Owners whose current account is inactive cannot manage data. Every service write verifies the owner of the route business and matches the service to that same business. Unavailable or differently owned resources return 404. The public API hides inactive businesses, their services and businesses whose owner is inactive.

An owner can have only one business, including an inactive business. A precheck returns 409 for a duplicate; the existing PostgreSQL unique owner index remains authoritative if two requests race. The known unique violation is translated to the same 409. No additional locking or booking-concurrency infrastructure is introduced.

Business active-state management is not exposed in this phase. Services use status changes instead of deletion; no DELETE endpoint exists. An inactive business remains accessible to its owner, but not publicly.

## Validation and discovery

Names, descriptions and address are required and trimmed; maximum lengths match existing mappings (names 200, descriptions 2000, address 500, email 254, phone 30). Email syntax is validated. Category IDs must exist.

Services accept name, description, price, currency and durationMinutes only. Price range is 0.01–9,999,999,999.99 AUD with at most two decimal places. Rejecting extra precision avoids relying on PostgreSQL rounding. Duration is an integer from 1 to 480 minutes. Currency must be exactly AUD. Updates preserve status unless the explicit status endpoint is used. Persisted timestamps are reloaded after service writes to match PostgreSQL's microsecond precision.

Discovery supports search (maximum 100 characters), categoryId, page (1–100000), pageSize (1–50; default 12). Search is case-insensitive over business name, description and address, with PostgreSQL wildcard characters escaped so search input is treated literally. Services themselves are not searched. Results sort by name then ID and return total count, current page, page size, category, a short description and active service count. Simple offset paging is adequate for this MVP; distance search and search indexing are deferred.

Supported time zones: Australia/Sydney (Sydney/Melbourne/Canberra), Australia/Brisbane, Australia/Adelaide, Australia/Perth, Australia/Darwin, Australia/Hobart. React loads options from /api/time-zones; the backend validates against the same list. The ID is stored for later scheduling; no slot generation, conversions or DST calculations exist here.

## Local demo

Follow README configuration for ConnectionStrings__Database and Jwt__Key. Keep real values in environment variables or Development user secrets. The root .env.local is not automatically read by ASP.NET Core; it was loaded into verification process memory only.

```powershell
dotnet tool restore
dotnet ef database update --project src/ServiceHub.Api
dotnet run --project src/ServiceHub.Api -- --seed-roles
dotnet run --project src/ServiceHub.Api -- --seed-development
dotnet run --project src/ServiceHub.Api
```

In another terminal:

```powershell
cd src/servicehub-web
pnpm install
pnpm dev
```

1. Open http://localhost:5173 and register as Business Owner, then log in.
2. Open Business Dashboard; create a profile using an API-provided category/time zone.
3. Edit business details, then add services with AUD prices and duration.
4. Edit a service and deactivate it. It remains in the dashboard.
5. Log out; Browse Services, search/filter, and open the public business. Only active services appear.
6. Log in as a Customer: no owner dashboard link is present and owner API calls return 403.

No credentials for demo accounts are committed. Browser verification used existing Phase 3 demo accounts and created Willow & Oak Studio with Signature haircut (active) and Finishing style (inactive). This is verification data, not an automatic business-data seeder. Existing development records/schema were retained.

## Database and verification

No migration or model change was required. InitialServiceHub remains the only active migration. No new packages or architectural frameworks were added. All previous tests remain.

Complete PostgreSQL-backed suite: 75 passed, 0 failed, 0 skipped. It covers business creation/claims, forbidden Customer calls, duplicate/concurrent creation, updates, cross-owner concealment, categories/time zones, public filtering/paging, inactive businesses, literal search characters, service lifecycle, cross-business service IDs, forged fields and input limits. Browser verification covers the live owner workflow and unauthenticated public browsing; API integration tests verify cross-owner rejection. Desktop and 390-pixel mobile layouts were inspected, including forms and navigation, with no horizontal overflow.

Known limits: AUD and the listed Australian zones only; no images/reviews/distance search; no business deactivation UI; memory-only authentication is unchanged, so browser reload requires login. Scheduling and booking remain future phases.
