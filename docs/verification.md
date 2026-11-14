# Milestone 1 verification

Verified on November 14, 2026 with .NET SDK 10.0.404 on Windows.

| Check | Result |
| --- | --- |
| Solution build | Passed, zero warnings and errors |
| xUnit API integration tests | All six passed |
| Release WebAssembly publish | Passed; optional wasm-tools workload not installed |
| Browser page load | Three SQLite-backed session cards displayed |
| Refresh | Issued a new request and displayed the session listing |
| API unavailable | Friendly error and Try again button displayed |
| Recovery | API restarted; Try again restored all three cards |
| Persistence | Integration test restarted the host and retained an inserted row without duplicating seed data |

The browser request reached the real ASP.NET Core API. Server logs showed the SQLite SELECT ordered by StartsAtUtc and Id. JSON deserialized into the shared contract and the UI rendered the matching titles, instructors, dates, duration, and capacity.

The six automated cases cover fresh migration/seed/UTC JSON, allowed origin, blocked origin, empty results, ordering of persisted rows, and restart persistence. Each uses an isolated SQLite file, never the developer's demo database.

Browser behavior was checked manually. Mobile layout and automated component/browser tests have not been verified in this milestone. GitHub Actions runs its own checks separately; use the repository's Actions tab for current remote results.

## Milestone 2 — Register and cancel

Verified on November 14, 2026 with the same SDK.

- Build: zero warnings and errors. WebAssembly release publish passed.
- All 19 integration test cases passed, including the original listing checks.
- An existing milestone 1 database upgraded to the registration schema and retained its edited session title.
- Concurrent requests for one remaining seat produced one successful booking and one conflict. Concurrent duplicates produced one stored registration.
- Browser: Register changed availability from 20 to 19, displayed You’re registered, and changed My sessions from 0 to 1.
- My sessions filtered to the registered card. A page reload retained registration status.
- Cancel registration restored availability to 20 and My sessions to 0. The empty My sessions view displayed its guidance.
- API tests cover cancellation isolation, invalid keys, full/started/missing sessions, idempotent retries, key hashing, persistence across API restarts, and POST/DELETE browser preflight.

Manual browser tests used a temporary demo registration and cancelled it afterward. No user registration was removed. The anonymous browser model and its limitations are documented in the README. Automated browser tests and mobile-specific verification remain future work.

## Milestone 3 — Accounts

Verified on November 14, 2026 with .NET SDK 10.0.404.

- Solution build passed with zero warnings/errors; WebAssembly release publish passed.
- All 32 integration cases passed. These include real Identity signup/login cookies, password hashing, validation, duplicate signup, logout, lockout, account rate limits, CSRF and identity-bound token renewal, account isolation, same-account access from a separate HTTP client, legacy imports/merges, and migration from earlier schemas. Existing capacity/concurrency tests now use authenticated accounts.
- The user created and signed in to a local account through the browser; no password was entered by the agent.
- The browser displayed the signed-in account, permitted registration, retained login and booking state after reload, filtered to My sessions, and allowed cancellation.
- Only the temporary browser test booking was cancelled. The user remained signed in and the public session list was restored.
- Old anonymous rows survive the migration. Integration tests proved that possession of the original key plus explicit authenticated import is required; matching email alone cannot claim bookings.

Cookie logout and access from a second browser-equivalent HTTP client were tested in the integration suite. A second physical browser/device, automated browser tests, mobile layout, public hosting, and mail verification/recovery have not been verified or implemented in this milestone. See `accounts.md` for local-development limits.

## Milestone 4 — Organizer tools

Verified on November 14, 2026 with the same SDK.

- Solution build passed with zero warnings/errors; release WebAssembly publish passed.
- All 49 API integration cases passed. New checks cover anonymous/attendee authorization, signup role spoofing, local provisioning and revocation with an existing cookie, Development-only provisioning, create/edit visibility, field/UTC validation, CSRF, booked schedule/capacity protection, missing/started sessions, concurrent edits, and concurrent registration/capacity changes.
- The revision migration was inspected: it adds Version initialized to zero, without changing existing session details or removing registrations/accounts.
- The historical-upgrade test was adapted to write the original schema directly rather than querying it through an entity containing newer columns. Original-data preservation remains asserted after upgrade.
- A signed-in ordinary account opened the real organizer page and correctly saw Organizer access required; no editor or privileged navigation was exposed.
- The local database was backed up before the revision migration. Database backups, account data, and Data Protection keys remain ignored by Git.

After organizer provisioning, the existing login immediately gained organizer access without signing in again. The browser rejected an empty creation form, then created a clearly labeled fictional practice session at capacity 20. Editing capacity to 24 saved successfully and advanced the revision to 1. The session appeared in the attendee listing and accepted a registration. With that booking present, the organizer editor disabled start-time and duration changes. Cancelling only the temporary test booking restored availability and unlocked the schedule. The new demo session was retained for local experimentation; the original three sessions were unchanged.

The organizer page was left open with the demo session selected. Mobile layout and automated browser tests remain future work.

## Milestone 5 — Attendance

Verified on November 14, 2026 with the same SDK.

- Solution build passed with zero warnings/errors; release WebAssembly publish passed.
- All 64 integration cases passed. Attendance checks cover organizer-only rosters, attendee/public privacy, manual recording/correction/clearing, server-derived actor/time, valid statuses, CSRF, session mismatch, legacy import, cancellation protection, concurrent corrections, cancellation races, historical migration, and restart persistence.
- The migration initializes existing registrations to NotRecorded/version 0 without removing earlier bookings, roles, accounts, or sessions. The local database was backed up before applying it; private data and backups remain ignored by Git.
- Browser walkthrough used a temporary booking in the existing fictional organizer demo session. It showed Not recorded in the roster, saved Attended, corrected it to Absent and back to Attended, and displayed the saved status in My sessions.
- The attendee view disabled cancellation and explained that attendance must first be cleared. Clearing through the organizer roster restored cancellation; cancelling only the temporary test booking removed it from the roster and restored the seat count.
- The session itself and original programme were retained, the account stayed signed in, and the empty roster was left open for local use.

No real attendee's booking or attendance was changed by this walkthrough. Automated browser tests, mobile-specific verification, an immutable attendance audit log, exports, and public deployment are outside this milestone.
