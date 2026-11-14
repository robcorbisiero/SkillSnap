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
