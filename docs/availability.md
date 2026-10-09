# Phase 5: business availability

Temporary closures use separate required start-date, start-time, end-date and end-time controls, with an optional reason. Inputs remain explicitly UTC, preserving the existing API contract; saved closures display in the business time zone. No time is defaulted or fixed. Both frontend and backend reject an end at or before the start. The controls stack on narrow screens. Frontend regression checks run with `pnpm test` from `src/servicehub-web`.

This document records the Phase 5 availability design. Phase 6 now adds booking creation and database concurrency protection; see [bookings](bookings.md). The calculation below remains shared by previews and creation. No schema migration or new package was needed: the Phase 2 working-hours, blocked-period and booking tables are reused.

## Data and ownership

Working hours are local recurring `TimeOnly` values associated with a business and weekday. The API returns all seven days, Monday through Sunday; .NET weekday numbers are Sunday=0, Monday=1 through Saturday=6. Closed days have no intervals. Whole-minute intervals must start before they end, cannot overlap within a day and cannot cross midnight. Up to eight intervals per day are accepted. Adjacent intervals are valid, but an appointment must fit entirely inside one interval.

PUT replaces the entire week. Validation happens before changes; one EF Core `SaveChangesAsync` transaction commits deletes and inserts together. It does not provide concurrent-editor conflict/version detection.

Blocked periods are UTC ranges with an optional reason (500 characters). Start must precede end, years must be 2000–2100 and the range may span at most 366 days. UTC timestamps must include `Z` or `+00:00`; timestamps without an offset or with a non-UTC offset are rejected. Closures can overlap, span dates and be deleted/recreated; there is no recurring closure or edit operation. GET returns upcoming and ongoing closures, sorted by start.

Owner routes require BusinessOwner role. The owner's ID comes exclusively from authenticated claims, and the business is resolved server-side. There is no client business/owner ID on schedule writes. Deleting another owner's closure returns 404. Customers receive 403 and unauthenticated requests receive 401. Existing inactive-account checks apply.

## Endpoints

| Method and route | Result |
| --- | --- |
| GET /api/owner/business/working-hours | 200 full weekly configuration |
| PUT /api/owner/business/working-hours | 200 saved full week |
| GET /api/owner/business/blocked-periods | 200 upcoming/ongoing closures |
| POST /api/owner/business/blocked-periods | 201 closure DTO |
| DELETE /api/owner/business/blocked-periods/{id} | 204 |
| GET /api/businesses/{businessId}/availability?serviceId={serviceId}&date=2030-01-07 | Public, 200 slot preview |

Validation errors use ProblemDetails (400); missing/concealed resources use 404. Public availability requires an active business, active owner and active service belonging to that business. Responses are not cached.

Example full-week PUT body (other days closed):

```json
{
  "days": [
    { "dayOfWeek": 1, "intervals": [
      { "startTime": "09:00", "endTime": "12:00" },
      { "startTime": "13:00", "endTime": "17:00" }
    ] },
    { "dayOfWeek": 2, "intervals": [] },
    { "dayOfWeek": 3, "intervals": [] },
    { "dayOfWeek": 4, "intervals": [] },
    { "dayOfWeek": 5, "intervals": [] },
    { "dayOfWeek": 6, "intervals": [] },
    { "dayOfWeek": 0, "intervals": [] }
  ]
}
```

Example closure POST body:

```json
{
  "startUtc": "2030-01-06T22:30:00Z",
  "endUtc": "2030-01-06T23:00:00Z",
  "reason": "Temporary closure"
}
```

In Sydney this corresponds to Monday 7 January 2030, 09:30–10:00. Slot responses contain `businessId`, `serviceId`, `date`, `timeZoneId`, `durationMinutes` and `slots` with ISO-8601 `startUtc`/`endUtc`. Empty lists are normal for closed/past dates or no available appointments. Dates outside years 2000–2100 return 400.

## Calculation

1. Validate the business/service pairing and resolve the stored IANA time-zone ID using .NET `TimeZoneInfo`.
2. Read current UTC time through injected `TimeProvider`. A wholly past local date returns an empty list.
3. Query only the requested weekday's working intervals. Round each opening up to the next quarter-hour clock boundary (09:10 becomes 09:15), then advance by 15 minutes.
4. Add service duration in local minutes. Keep candidates ending at or before closing, entirely within one interval. Lunch breaks cannot be crossed.
5. Reject invalid or ambiguous local endpoints. Convert safe endpoints to UTC and reject candidates whose UTC elapsed duration differs from the service duration. Keep only starts strictly after now.
6. Query only closures and Confirmed bookings intersecting the candidate UTC range. All services share one business calendar. Cancelled and Completed bookings do not block slots.
7. Filter overlaps, deduplicate and sort by UTC start. Slots are calculated on demand and never stored.

Read-only EF queries use `AsNoTracking`; booking/closure queries project only timestamps. No entire booking history is loaded. Async database calls accept cancellation tokens.

## Half-open intervals and DST

Appointments and closures use `[start, end)`. Overlap is exactly:

```text
candidateStart < existingEnd AND candidateEnd > existingStart
```

An appointment ending at 10:00 does not conflict with one beginning at 10:00. An appointment ending at closing is valid.

Working hours and request dates are local to the business, never the browser or server zone. UTC timestamps are used for comparisons and responses; React formats them with the response's business zone. The public date defaults to today in the business zone.

The conservative DST policy excludes ambiguous fall-back endpoints and invalid spring-forward endpoints. It also excludes an appointment crossing an offset change even if both endpoints individually exist, because wall-clock and elapsed duration would differ. This can produce fewer slots near transitions; it avoids choosing one repeated-time offset silently. Deterministic PostgreSQL-backed tests cover Sydney spring-forward (2030-10-06) and fall-back (2030-04-07), plus ordinary Sydney/Brisbane conversion. The server needs functioning IANA time-zone data/ICU; an unavailable zone returns a safe validation response.

## Frontend and local demo

Start PostgreSQL, configure the existing database/JWT settings and run the backend/frontend using the root README commands. No migration is added for this phase.

1. Sign in as a BusinessOwner with a business and active service. Open Business Dashboard → Availability.
2. Mark Monday open, set 09:00–12:00 and add 13:00–17:00. Leave other days closed and save the week.
3. Open the public profile, choose a 45-minute service and Monday 2030-01-07: 24 slots are expected, ending at 11:15 and 16:15 for each interval.
4. Add the example closure via the dashboard. Closure inputs explicitly ask for UTC; saved entries also show business-local times. Refresh the public preview: 20 slots remain, beginning at 10:00.
5. Remove the closure and request the preview again: 24 slots return. Public browsing requires no login.

Forms provide loading/error/success states, interval controls and responsive layouts. Phase 6 makes slots selectable and lets Customers confirm an appointment. Displaying/selecting a slot does not reserve it; creation validates the same rules again.

Verification used the existing development business, leaving the Monday split schedule in place and removing the temporary test closure. No development bookings were inserted. Confirmed/cancelled/completed booking behavior, role isolation, durations and DST were verified in isolated PostgreSQL test schemas. Full suite: 116 passed, 0 failed, 0 skipped (75 existing + 41 new).

## Intentional MVP limits

One business per owner, one appointment at a time per business, 15-minute starts, 1–480 minute services, AUD only, limited Australian time zones, no staff calendars, no overnight intervals, no recurring closures. Booking creation is now covered by Phase 6. The closure form currently asks for UTC rather than handling ambiguous business-local date-time entry. Concurrent schedule-editor conflict detection remains future work. Booking overlap concurrency protection is implemented in Phase 6.
