# SkillSnap

A learning-session planner built with C#, Blazor WebAssembly, ASP.NET Core Web API, and SQLite through Entity Framework Core.

**Milestone 3: user accounts.** Create an account, sign in, register for sessions, cancel, and filter to My sessions. Registrations follow the signed-in account across browsers and devices. Public browsing remains available. Sample dates and instructors are fictional. All session times are displayed in UTC.

ASP.NET Core Identity stores accounts and password hashes in SQLite. Login uses an HttpOnly cookie, not browser-stored bearer tokens. Existing milestone 2 browser registrations remain in the database until their original browser key is explicitly imported into an account. See [accounts and the migration flow](docs/accounts.md) and the [development plan](docs/roadmap.md).

This is a new, independent portfolio project. **This repository was not the code submitted to Coursera.** It does not establish that the application described in earlier coursework was implemented at that time.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (the SDK, not only the runtime).
- Git and a modern browser. A separate SQLite installation is unnecessary.
- Any editor; Visual Studio Code is optional.

On Windows, you can install the SDK with PowerShell:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
```

Open a new terminal afterward and check `dotnet --version`. The checked-in `global.json` selects a stable .NET 10 SDK. Packages are pinned to specific versions.

## Run locally

Clone once (GitHub may prompt you to sign in if the repository is private):

```powershell
git clone https://github.com/robcorbisiero/SkillSnap.git
cd SkillSnap
dotnet restore SkillSnap.slnx
dotnet build SkillSnap.slnx --no-restore
dotnet test SkillSnap.slnx --no-build
```

In terminal 1, from the repository root:

```powershell
dotnet run --project src/SkillSnap.Api --launch-profile http
```

The API listens on `http://localhost:5080`. On first startup it applies the committed EF migrations, creates `src/SkillSnap.Api/App_Data/skillsnap.db`, and inserts three demo sessions. Earlier databases are upgraded without removing sessions or browser registrations. Subsequent starts retain sessions, accounts, and registrations without duplicating demo rows.

In terminal 2, from the same repository root:

```powershell
dotnet run --project src/SkillSnap.Client --launch-profile http
```

Open [http://localhost:5180](http://localhost:5180). Expect three session cards. Choose Sign in / Create account, then Create account. Use an email and a password of 10–128 characters with uppercase, lowercase, a number, and a symbol. After signing in, return to Sessions and click Register for session: availability drops and the card shows You’re registered. My sessions shows your bookings. Refresh to confirm persistence, then Cancel registration to release the seat. Account has the Sign out button. Stop either app with Ctrl+C in its terminal.

If the account page finds earlier bookings in this browser, it offers Import browser bookings. Import only your own bookings; the transfer is explicit and does not happen merely because you supplied an email address. A fresh browser can sign in to the same account to see account-owned bookings. The local demo does not send verification or password-recovery email.

You can also inspect the API directly:

```powershell
Invoke-RestMethod http://localhost:5080/api/learning-sessions | Format-Table
```

On macOS/Linux, use the same `dotnet` commands and `curl http://localhost:5080/api/learning-sessions` to inspect the API.

## How the parts communicate

```text
Browser: Blazor WebAssembly (localhost:5180)
   Home / AccountPage -> account and session services -> ApiClient
                           |
                 credentialed requests + CSRF tokens for mutations
                           v
ASP.NET Core API (localhost:5080)
   Identity cookie -> account/session endpoints -> EF Core -> SQLite
                           |
                 JSON LearningSessionDto[]
                           v
Blazor renders session cards
```

- `src/SkillSnap.Client`: the C# UI runs in the browser. Its service calls the API and deserializes JSON; the component displays the result. Database access never runs in the browser.
- `src/SkillSnap.Api`: Identity authenticates the login cookie. Protected registration endpoints derive ownership from the authenticated user's ID, never a posted user ID or email. EF Core reads sessions and counts from SQLite. The booking insert checks capacity and start time in one parameterized SQLite statement; a unique account/session index prevents duplicate bookings.
- `src/SkillSnap.Contracts`: a small shared DTO library defines the response shape without exposing database entities or EF Core to the UI.
- `tests/SkillSnap.Api.Tests`: xUnit tests run the real API against isolated SQLite files, including migrations, Identity cookies, and CSRF tokens. They cover accounts, validation, account isolation, logout, lockout, rate limits, imports, concurrent bookings, CORS, and persistence.

The API allows credentialed browser requests only from configured frontend origins. CORS is a browser policy, not authentication. HttpOnly/SameSite cookies and per-request antiforgery validation protect account and registration mutations. Organizer permissions, attendance, email verification/recovery, and deployment remain future work. This is a local development foundation.

## API contract

`GET /api/learning-sessions` returns HTTP 200 with a JSON array (or `[]` when empty):

```json
[
  {
    "id": 1,
    "title": "Build your first Blazor component",
    "description": "Explore Razor components, parameters, and events by building a small interactive view.",
    "topic": "Blazor",
    "instructor": "Alex Morgan",
    "startsAtUtc": "2027-01-12T18:00:00Z",
    "durationMinutes": 60,
    "capacity": 20,
    "registeredCount": 0,
    "isRegistered": false,
    "seatsRemaining": 20
  }
]
```

The response includes all sessions, including past dates. `capacity` is group size; `registeredCount` and `seatsRemaining` reflect saved bookings, including unclaimed legacy bookings. Without login, `isRegistered` is false. The API never returns participant keys, hashes, account IDs, or attendee lists.

| Method and route | Behavior |
| --- | --- |
| `POST /api/learning-sessions/{id}/registration` | Register the signed-in account. Returns 204; repeated requests are idempotent. New bookings return 409 for full or started sessions. |
| `DELETE /api/learning-sessions/{id}/registration` | Cancel only the signed-in account's booking. Returns 204, including when already cancelled. Cancellation is allowed after the start time. |

Both mutations require a valid login cookie and a matching `X-CSRF-TOKEN` from `GET /api/auth/csrf`. Missing login returns 401; invalid/missing CSRF returns 400; missing sessions return 404. The old `X-Participant-Key` is no longer accepted as authentication for booking mutations or listing status. It is used only by the explicit legacy-import endpoints.

For a manual API exercise in PowerShell, create a local test account in the browser first. The following prompts for credentials, keeps its cookies in a separate terminal session, and does not hard-code a password:

```powershell
$base = 'http://localhost:5080'
$credentials = Get-Credential -Message 'SkillSnap local test account (email and password)'
$csrf = Invoke-RestMethod "$base/api/auth/csrf" -SessionVariable webSession
$body = @{ email = $credentials.UserName; password = $credentials.GetNetworkCredential().Password } | ConvertTo-Json
Invoke-RestMethod "$base/api/auth/login" -Method Post -WebSession $webSession -Headers @{ 'X-CSRF-TOKEN' = $csrf.token } -ContentType 'application/json' -Body $body
Remove-Variable body,credentials
$csrf = Invoke-RestMethod "$base/api/auth/csrf" -WebSession $webSession
$headers = @{ 'X-CSRF-TOKEN' = $csrf.token }
Invoke-WebRequest "$base/api/learning-sessions/1/registration" -Method Post -WebSession $webSession -Headers $headers
Invoke-RestMethod "$base/api/learning-sessions" -WebSession $webSession | Format-Table
Invoke-WebRequest "$base/api/learning-sessions/1/registration" -Method Delete -WebSession $webSession -Headers $headers
Invoke-WebRequest "$base/api/auth/logout" -Method Post -WebSession $webSession -Headers $headers
```

Fetch a new CSRF token whenever login identity changes. Bookings are available until the UTC start time. On network failure, refresh before retrying because the server may have saved the change. Registration/cancellation retries are idempotent. Availability refreshes on page load, manual refresh, and successful changes; it is not a live push feed.

## Configuration and troubleshooting

- API port and environment: `src/SkillSnap.Api/Properties/launchSettings.json`.
- UI port: `src/SkillSnap.Client/Properties/launchSettings.json`.
- Browser API address: `src/SkillSnap.Client/wwwroot/appsettings.json` (`ApiBaseUrl`, ending in `/`). This is public browser configuration; never put secrets here.
- Allowed UI origin: API `appsettings.json` (`AllowedOrigins`). If you change the UI port, update this too. `localhost` and `127.0.0.1` are different origins.
- Database: API connection string `ConnectionStrings:SkillSnap`. Relative SQLite paths are resolved against the API content root. For an absolute custom path, create its parent directory first.
- If no cards load, open the API URL directly, check both terminals, and verify that ports and origins match. Stop the API and click Refresh sessions to see the error/retry state; restart it and click Try again to recover.
- If a port is occupied, stop the other process or update the matching configuration values.
- Local HTTP avoids certificate setup. A hosted version needs HTTPS and a deployment-specific configuration.
- Cookies require the same site: use `localhost` for both apps, and the same scheme. Use the supplied `http` launch profiles for local development; outside Development cookies are Secure and require HTTPS. A deployment should serve the UI and API on the same site.
- Data Protection keys live in the ignored `src/SkillSnap.Api/App_Data/keys` folder so login cookies survive local API restarts. Keep that folder private and backed up with the database. It must never be committed or shared.
- After repeated failed logins, wait 15 minutes for account lockout to expire. Account endpoints additionally allow 10 login/signup attempts per IP per five minutes.
- Build errors about a missing SDK: install the .NET 10 **SDK**, reopen the terminal, and run `dotnet --list-sdks`.

## Database migrations

The initial, registration, and Identity migrations and the model snapshot are committed. Normal startup applies migrations automatically. Earlier sessions and anonymous bookings are preserved. No manual EF command is needed to run it. Production migration execution should be a controlled deployment step.

To change the schema later, install the repository-local EF tool and add a migration:

```powershell
dotnet tool restore
dotnet ef migrations add YourChange --project src/SkillSnap.Api
dotnet run --project src/SkillSnap.Api --launch-profile http
```

Keep migrations in Git. Do not mix `EnsureCreated` with this migration workflow. The database and key files are ignored by Git. To reset all local demo data, stop the API, back up anything needed, and remove only `skillsnap.db`, `skillsnap.db-shm`, and `skillsnap.db-wal` from `App_Data`. Restart to recreate the demo database. Resetting discards accounts and registrations too. Do not reset to apply normal updates.

## Validation and next steps

```powershell
dotnet test SkillSnap.slnx
dotnet publish src/SkillSnap.Client -c Release -o artifacts/client
```

GitHub Actions restores, builds, runs integration tests, and publishes the WebAssembly client on pushes and pull requests. Browser interactions are checked manually; automated UI tests are a future addition.

Next milestone: organizer permissions and validated session creation/editing, followed by attendance and deployment. Email verification and password recovery need a real mail setup before public account use. See [the roadmap](docs/roadmap.md).
