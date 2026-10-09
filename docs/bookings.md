# Phase 6: booking creation and management

## Flow and architecture

React booking controls → controllers → concrete BookingService → Identity user checks / shared AvailabilityService → EF Core → PostgreSQL. No repositories, CQRS, new authentication infrastructure or background jobs are introduced.

A Customer submits only `serviceId` and an ISO-8601 UTC `startUtc` ending in Z or +00:00. The authenticated claims supply the CustomerId. The service supplies the BusinessId; the backend checks active service, business, owner and customer. It derives the local business date and calls the existing availability calculator, including working hours, closures, Confirmed bookings, 15-minute boundaries, current time and the existing DST policy. It compares both requested start and derived end with an available slot, then copies the current service name/price/currency/duration and creates a Confirmed booking. Creation time is supplied by TimeProvider. Client IDs, end time, price, duration, status and timestamps are never used to set server-controlled fields.

A preview or selected slot does not reserve anything. HTTP 201 returns the booking DTO and Location for its detail endpoint. Unavailable/inactive resources return 404. Invalid input or an otherwise invalid slot returns 400. An occupied interval or a database overlap race returns safe ProblemDetails with 409, without SQLSTATE, constraint names or stack traces.

## Endpoints

| Method and route | Access / behavior |
| --- | --- |
| POST /api/bookings | Customer; create, 201 |
| GET /api/me/bookings | Customer; own appointments only |
| GET /api/bookings/{id} | Booking customer or owning BusinessOwner |
| POST /api/bookings/{id}/cancel | Customer; own future Confirmed appointment |
| GET /api/owner/business/bookings | BusinessOwner; own business only |
| GET /api/owner/business/bookings/{id} | BusinessOwner; own business detail |
| POST /api/owner/business/bookings/{id}/cancel | BusinessOwner; own Confirmed appointment |
| POST /api/owner/business/bookings/{id}/complete | BusinessOwner; own ended Confirmed appointment |

State changes return 200 with the updated DTO. Owner list accepts optional `status=Confirmed|Cancelled|Completed`, `fromUtc` and `toUtc` timestamp filters. Date filters apply to appointment starts, inclusive from / exclusive to. Offset-bearing date filters are normalized to UTC; malformed filters or reversed ranges return 400. Lists sort future Confirmed appointments first chronologically, then remaining history newest appointment first, with ID as a tie-breaker. Lists are intentionally unpaged for this MVP.

DTOs include business ID/name/address/time zone, service ID and stored service snapshot, UTC appointment timestamps, status, creation/cancellation timestamps. Owner responses additionally contain customer name/email; customer responses omit those details (Customer is null). No password hash or unnecessary Identity field is returned. Service edits do not rewrite snapshots. Business profile/name/time-zone information is read from the current profile.

## Ownership and transitions

Owner IDs and customer IDs come from JWT claims. Owner queries resolve the business on the server. Unrelated customer/owner reads or actions return 404. Customer endpoints prohibit BusinessOwner/Admin roles; owner endpoints prohibit Customer/Admin roles. Inactive accounts cannot read or manage bookings.

Allowed transitions:

```text
Confirmed → Cancelled
Confirmed → Completed
```

Customers may cancel only before StartUtc (strictly future). Owners may cancel a Confirmed appointment even after it starts. Owners may complete only when EndUtc <= now. Cancelled and Completed are terminal. Invalid/repeated actions return 400. Cancellation sets CancelledAtUtc, preserves the row and releases its slot. Completion leaves CancelledAtUtc null.

One conditional SQL UPDATE requires the current state to remain Confirmed and includes the relevant time predicate. If concurrent cancel/complete requests race, only one update can succeed. This avoids a load-modify-save race without adding a new version column. TimeProvider makes boundaries deterministic in tests.

## PostgreSQL concurrency safeguard

Two simultaneous conflicting inserts can fail either with the named exclusion violation (`23P01`) or with a deadlock (`40P01`): each exclusion scan may see and wait for the other's uncommitted tuple. PostgreSQL aborts one transaction so the other can finish. Booking creation maps both outcomes to the same safe 409 without exposing database details. This handling is limited to the booking INSERT save; unrelated exclusions and other database errors retain normal error handling. No retry is needed to create the already-conflicting appointment. The existing independent-request test preserves its INSERT barrier and verifies one commit, one conflict and the actual PostgreSQL failure code. Deterministic error-mapping tests complement, rather than replace, that real race. See [PostgreSQL's exclusion-check implementation](https://github.com/postgres/postgres/blob/REL_16_STABLE/src/backend/executor/execIndexing.c#L27-L44).

Migration: `20261008083922_ProtectConfirmedBookingIntervals`.

Npgsql's default non-retrying execution strategy treats deadlocks as transient and wraps the `DbUpdateException` in `InvalidOperationException`. The booking INSERT handler recognizes that wrapper and checks the underlying SQLSTATE. It does not convert arbitrary transient failures, serialization failures, unique violations or unrelated constraint violations to 409.

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA public;
ALTER TABLE "Bookings" ADD CONSTRAINT "EX_Bookings_ConfirmedBusinessOverlap"
EXCLUDE USING gist (
    "BusinessId" public.gist_uuid_ops WITH =,
    tstzrange("StartUtc", "EndUtc", '[)') WITH &&
) WHERE ("Status" = 'Confirmed');
```

The extension supplies a GiST equality operator class for UUID BusinessId. The constraint rejects two indexed rows when they have equal business IDs AND overlapping UTC ranges. It covers every service within one business. `[)` makes end boundaries exclusive: 09:30–10:00 and 10:00–10:30 are valid together. Cancelled and Completed rows are outside the partial constraint, so they retain history without reserving time.

The application check provides useful validation, but cannot prevent two independent requests seeing the same free slot. The constraint decides at insertion/commit even when both requests pass that check. BookingService catches only the expected PostgreSQL exclusion violation for this named constraint and translates it to 409. Unexpected database failures still use the normal error handler.

The SQL constraint is migration-managed because it is not represented by the current EF Fluent model. The generated designer remains the ordinary entity model; no pending EF model change is expected. No booking columns or initial migration were changed.

PostgreSQL must provide btree_gist and the migration role must be permitted to install it (or an administrator must preinstall it in public). The public operator-class qualification also works when tests use an isolated schema search path. Test fixtures serialize migration application to avoid simultaneous database-wide extension installation. Migration rollback drops the constraint, deliberately retaining the shared extension because other schemas/applications may depend on it. Upgrade refuses existing overlapping Confirmed records; it does not silently cancel or delete them.

References: [PostgreSQL range/exclusion constraints](https://www.postgresql.org/docs/18/rangetypes.html#RANGETYPES-CONSTRAINT), [btree_gist](https://www.postgresql.org/docs/18/btree-gist.html).

## Frontend

Public profiles allow service/date selection, selectable start times and Customer confirmation. Logged-out visitors get a login link; login returns to the business profile, where they choose the date/slot again. No booking intent or JWT is persisted in browser storage. BusinessOwner accounts receive an explanation instead of a confirmation button. Success links to My Bookings. A 409 displays “That time is no longer available” and automatically requests fresh slots.

Account → My Bookings separates future Confirmed appointments from history. Owners use Business Dashboard → Business bookings, including customer details, status filtering, refresh, cancellation and eligible completion. Controls show loading, success/error and status feedback. UTC timestamps are always formatted using `Intl.DateTimeFormat` with the business TimeZoneId, never the browser's default zone.

## Local commands

With the existing secure database/JWT settings configured:

```powershell
dotnet tool restore
dotnet ef database update --project src/ServiceHub.Api
dotnet build ServiceHub.sln
dotnet test ServiceHub.sln
dotnet ef migrations has-pending-model-changes --project src/ServiceHub.Api
dotnet run --project src/ServiceHub.Api
```

In another terminal:

```powershell
cd src/servicehub-web
pnpm dev
# Production build:
pnpm build
```

Use the existing database; do not reset it. SERVICEHUB_TEST_DATABASE must identify the dedicated test database. Root .env.local remains ignored and is not read automatically by the application. See root README for environment/user-secret setup. No new application settings or packages are needed.

## Verification and demo history

Full suite: 151 passed, 0 failed, 0 skipped (116 prior checks plus 35 new). Actual PostgreSQL cases cover booking rules, snapshot persistence, ownership, state transitions, cancellation/restored slots, Australian-zone responses, shared business calendars and migration rollback/reapply. A command interceptor in tests holds two independent HTTP requests just before their INSERT commands: both have already passed availability, then one returns 201 and one returns 409. The test also verifies exactly one row committed and safe error output. Another test races cancel/complete updates.

Live browser checks verified Customer login/browse/select/create, My Bookings, owner customer details, BusinessOwner booking prohibition, cancellation restoring all slots, stale-slot conflict display/automatic refresh, completion and mobile layouts. Two future reservations on the existing demo business for 7 January 2030 at 09:00 Sydney were cancelled. One controlled historical fixture for 5 October 2026 at 09:00 Sydney was inserted solely to exercise owner completion and was marked Completed (ID 25b7600a-65fe-4575-a019-88a997f15254). All three remain intentional demo history; no future verification reservation remains. No database reset or production account credentials were introduced.

## MVP boundaries

One business per owner, one simultaneous appointment per business, no staff/resource calendars, 15-minute starts, AUD only, no overnight hours, no recurring appointments/closures, no payments, reviews, notifications, analytics, waitlists, cancellation fees, admin booking management or deployment.

Availability is checked immediately before insertion, and the database serializes conflicts between bookings. This constraint does not serialize simultaneous owner edits to hours, closures, service details or business time zone; stricter coordination with configuration edits can be added later if required. Booking lists have no pagination yet. There is no automatic completion job or rescheduling operation; owners complete appointments explicitly.
