# Phase 2 persistence design

## Scope

This phase defines storage and migrations only. No authentication configuration, JWT/refresh tokens, user registration, business endpoints, booking creation/status workflows, slot generation, overlap constraint, admin interface, or frontend feature pages are implemented.

## User storage decision

`ApplicationUser` derives from `IdentityUser<Guid>` and `ApplicationDbContext` derives from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`. This adds the standard Identity storage tables to the first migration and preserves the same user table and Guid foreign keys for Phase 3. Only the storage package is registered through the context; there are no authentication handlers, UserManager/SignInManager registrations, role seeds, or login endpoints.

`Users` is inherited from IdentityDbContext rather than introducing a second user DbSet. Custom fields are FirstName/LastName (required, 100 characters), IsActive, and CreatedAtUtc. Identity's existing user fields remain available but are not used for authentication yet. Identity's standard dependent claim/login/token tables retain their standard delete behavior; every ServiceHub domain relationship uses Restrict. Identity's token storage table is not a RefreshToken entity or a refresh-session implementation.

## Relationships

- User owns zero or one business; Business.OwnerId is required and unique.
- Category has many businesses. Names are unique using PostgreSQL's ordinary case-sensitive comparison. Business names are not globally unique.
- Business has many services, working-hour intervals, blocked periods, and bookings.
- User has many customer bookings.
- Service has many bookings.
- Booking references both a business and a service. A composite foreign key `(BusinessId, ServiceId)` references the service alternate key `(BusinessId, Id)`, ensuring a booking cannot select another business's service.

All ServiceHub foreign keys use restricted deletes. Deactivate businesses/services through IsActive instead of deleting referenced records. There are no automatic active-record query filters yet. A service cannot be moved between businesses because its business ID participates in an alternate key; create a new service for a different business instead.

## Entity fields and validation

All domain IDs are application-generated Guids. Required strings use C# `required` plus EF non-null mappings. Database checks reject empty/space-only user names, business/category/service names, and booking snapshot names. SQL NOT NULL alone would still permit empty text.

| Entity | Storage details |
| --- | --- |
| Business | Required OwnerId, BusinessCategoryId, Name (200), Description (2000), Address (500), ContactEmail (254), TimeZoneId (100); optional ContactPhone (30); IsActive; CreatedAtUtc/UpdatedAtUtc |
| BusinessCategory | Id; required unique Name (100) |
| Service | BusinessId; Name (200); Description (2000); positive Price numeric(12,2); Currency (3 uppercase letters); positive DurationMinutes; IsActive; CreatedAtUtc/UpdatedAtUtc |
| BusinessWorkingHours | BusinessId; DayOfWeek integer 0–6 (Sunday–Saturday); local StartTime/EndTime; EndTime greater than StartTime |
| BusinessBlockedPeriod | BusinessId; StartUtc/EndUtc; EndUtc greater than StartUtc; optional Reason (500) |
| Booking | CustomerId, BusinessId, ServiceId; StartUtc/EndUtc; positive interval; Status; CreatedAtUtc; nullable CancelledAtUtc; ServiceName (200), positive ServicePrice numeric(12,2), ServiceCurrency (3 uppercase letters), positive ServiceDurationMinutes |

Currency checks verify shape, not membership in the ISO currency registry. Free services are not allowed by the positive-price requirement. Numeric(12,2) supports ten digits before the decimal and two after; PostgreSQL rounds extra fractional digits. Future request validation should reject or deliberately normalize excessive precision rather than silently relying on rounding.

Booking status is stored as readable text: Confirmed, Cancelled, Completed. A database check restricts other values. No status transition or cancellation eligibility rules are implemented yet.

## Time conventions

UTC fields are DateTime values mapped explicitly to `timestamp with time zone`. Use DateTimeKind.Utc when saving; Npgsql rejects non-UTC DateTime values for this type. PostgreSQL stores instants rather than an original time-zone identifier. Business.TimeZoneId separately stores an IANA identifier such as Australia/Sydney; validating that identifier and daylight-saving behavior are deferred.

Working-hour values use TimeOnly and `time without time zone`, interpreted in the business's time zone. A positive within-day interval excludes overnight hours and zero-length intervals. Multiple intervals on the same weekday are supported; interval overlap validation is deferred.

Appointments will use half-open intervals `[start, end)`: 09:30–10:00 and 10:00–10:30 do not overlap. Only positive interval length is constrained now. No overlap detection, service-duration alignment, or availability calculation is implemented in this phase.

Creation timestamps and initial update timestamps are initialized in the model to UTC now. Future application services must deliberately update UpdatedAtUtc when editing records. There is no timestamp interceptor or automatic update trigger.

## Historical booking snapshots

Service name, price, currency, and duration are stored on Booking itself. Editing Service does not update these columns. Future booking creation must populate them server-side; Phase 2 defines storage, not copying/creation logic. Cancellation is intended to preserve the booking row.

## Migration and demo data

`20261006142457_InitialServiceHub` is the first active migration. Archived GoVilla migrations are not discovered by this context and must not be applied to ServiceHub. Use a new database. Existing GoVilla data migration is outside scope.

The explicit `--seed-development` command adds five categories idempotently using stable IDs and skips existing matching IDs/names. Seeding is Development-only and is not part of a production migration. No users, roles, businesses, or bookings are seeded.

## Verification limits

The .NET build, frontend build, six HTTP tests, and six Npgsql model/migration checks pass. No PostgreSQL server, client, Docker command, connection-string environment variable, or default-port listener was available during this phase. The migration was generated but not applied to a live database. Eleven opt-in PostgreSQL test methods report skips without SERVICEHUB_TEST_DATABASE; their parameterized cases run when it is configured.

Run the real PostgreSQL suite before relying on schema behavior locally. It verifies graph relationships, owner/category uniqueness, decimal rounding, multiple working intervals, UTC blocked periods, snapshot independence, cross-business service rejection, restrictive deletion, positive-value/interval checks, and idempotent category seeding. No SQLite provider is used.
