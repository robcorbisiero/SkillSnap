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

## Milestone 3 — User accounts
Introduce ASP.NET Core Identity, verified server-side user identity, and organizer permissions. Design a deliberate transition from anonymous demo registrations to accounts; do not automatically claim a registration by email. Add authentication and authorization tests before exposing personal data.

## Later milestones
Validated organizer session creation/editing, attendance management, pagination, deployment over HTTPS, and operational monitoring. Each milestone gets its own reviewable change, tests, and updated documentation. This remains a new independent portfolio project, not the code submitted to Coursera.
