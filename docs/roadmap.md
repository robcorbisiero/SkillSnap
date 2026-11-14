# SkillSnap development plan

## Milestone 1 — Discover (complete)
Blazor WebAssembly lists sessions from an ASP.NET Core API backed by SQLite and EF Core. Includes migrations, integration tests, setup documentation, and CI.

## Milestone 2 — Register and cancel (complete)

Deliver a complete local demo booking flow:

- Add a registration table with a foreign key to sessions and a unique participant/session constraint. Preserve existing session data through an additive EF migration.
- Let visitors register and cancel from session cards. Show remaining seats, registration status, and a My sessions filter.
- Remember the current browser with a random participant key in local storage. Store only its SHA-256 hash on the server. No names or emails. This key is a demo bearer credential; it is not a verified user account.
- Keep listing accessible without a key. Require a valid key on booking mutations. Never expose another browser's registration or a participant list.
- Make repeated register/cancel requests idempotent. Atomically enforce session capacity and reject registration after the start time.
- Handle errors without losing the displayed sessions. Refresh server state after every mutation.
- Verify registration, cancellation, duplicate requests, capacity/concurrency, past sessions, participant isolation, restart persistence, and CORS. Check the actual browser flow and CI before completion.

Acceptance: register -> seat count falls -> My sessions includes it -> refresh preserves registration -> cancel -> count recovers and My sessions clears. A full or started session cannot be booked. Another browser cannot cancel this browser's registration.

## Milestone 3 — User accounts (complete)

- ASP.NET Core Identity accounts, password hashing, signup, login, and logout.
- HttpOnly/SameSite login cookies, CSRF protection, account lockout, and login/signup rate limits.
- Account-owned registrations available across browsers. Server-side authorization protects every booking mutation.
- Explicit possession-based import of earlier browser bookings; preserved data and no claims by email.
- Account isolation, cookie/CSRF, validation, lockout, rate-limit, cross-client, migration/import, and booking tests.
- Local account UI and documentation. Email ownership verification and recovery are deferred until real mail delivery is configured.

## Milestone 4 — Organizer tools (complete)

- Server-enforced role policy, checked against current database membership.
- Development-only local provisioning for existing accounts; no role selection on signup or HTTP role assignment.
- Organizer form and API for creating and editing programme sessions, with UTC date/time and server validation.
- Atomic capacity and booked-schedule protection, plus session revisions to reject conflicting edits.
- Authorization, provisioning, validation, concurrency, booking-protection, and migration tests.
- No session deletion or participant booking cancellation through organizer tools.

## Milestone 5 — Attendance (next)

Design organizer-managed attendance against existing account registrations, with access controls and tests. Keep attendance distinct from booking and avoid exposing private attendee details in public responses.

## Later milestones
Pagination, deployment over HTTPS, account mail verification/recovery, and operational monitoring. Each milestone gets its own reviewable change, tests, and updated documentation. This remains a new independent portfolio project, not the code submitted to Coursera.
