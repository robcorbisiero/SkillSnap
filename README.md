# SkillSnap

A learning-session planner built with C#, Blazor WebAssembly, ASP.NET Core Web API, and SQLite through Entity Framework Core.

**Milestone 2: register and cancel.** Browse database-backed sessions, see available seats, register, cancel, and filter to My sessions. The responsive UI handles loading, empty results, failed requests, and retry. Sample dates and instructors are fictional. All session times are displayed in UTC.

Registrations belong to this browser through a random key saved in local storage. They survive refreshes and API restarts. No name or email is collected. Clearing browser storage loses access to those registrations; another browser/device has a different identity. This is an anonymous local demo, not user authentication. See the [development plan](docs/roadmap.md) for the account milestone.

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

The API listens on `http://localhost:5080`. On first startup it applies the committed EF migrations, creates `src/SkillSnap.Api/App_Data/skillsnap.db`, and inserts three demo sessions. Existing milestone 1 databases are upgraded without removing sessions. Subsequent starts retain sessions and registrations without duplicating demo rows.

In terminal 2, from the same repository root:

```powershell
dotnet run --project src/SkillSnap.Client --launch-profile http
```

Open [http://localhost:5180](http://localhost:5180). Expect three session cards. Click Register for session: the available count drops and the card shows You’re registered. My sessions shows your bookings. Refresh the page to confirm the booking remains, then click Cancel registration to release the seat. Refresh sessions requests the latest server state, including changes made by other visitors. Stop either app with Ctrl+C in its terminal.

You can also inspect the API directly:

```powershell
Invoke-RestMethod http://localhost:5080/api/learning-sessions | Format-Table
```

On macOS/Linux, use the same `dotnet` commands and `curl http://localhost:5080/api/learning-sessions` to inspect the API.

## How the parts communicate

```text
Browser: Blazor WebAssembly (localhost:5180)
   Home.razor -> LearningSessionService -> HttpClient
                           |
                 GET sessions / POST or DELETE registration
                           v
ASP.NET Core API (localhost:5080)
   Program.cs endpoint -> SkillSnapDbContext -> SQLite file
                           |
                 JSON LearningSessionDto[]
                           v
Blazor renders session cards
```

- `src/SkillSnap.Client`: the C# UI runs in the browser. Its service calls the API and deserializes JSON; the component displays the result. Database access never runs in the browser.
- `src/SkillSnap.Api`: a separate ASP.NET Core process exposes listing and registration endpoints in `Endpoints/SessionEndpoints.cs`. EF Core reads sessions and registration counts from SQLite. The booking insert checks capacity and start time in one parameterized SQLite statement, preventing concurrent requests from overbooking. A unique participant/session index also enforces one registration per browser per session.
- `src/SkillSnap.Contracts`: a small shared DTO library defines the response shape without exposing database entities or EF Core to the UI.
- `tests/SkillSnap.Api.Tests`: xUnit integration tests run the real API pipeline against isolated SQLite files, including migrations. They cover listings, registration/cancellation, duplicate and concurrent requests, capacity, participant isolation, CORS, and restart persistence.

The API allows browser requests from `http://localhost:5180` using CORS. CORS is a browser policy, not authentication. Accounts, attendance, organizer administration, and deployment are later milestones. This remains a local development foundation.

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

The response includes all sessions, including past dates. `capacity` is the configured group size; `registeredCount` and `seatsRemaining` reflect saved bookings. With no participant header, `isRegistered` is false. The API never returns participant keys, hashes, or lists of attendees.

| Method and route | Behavior |
| --- | --- |
| `POST /api/learning-sessions/{id}/registration` | Register this browser. Returns 204; repeated requests are idempotent. New bookings return 409 for a full or started session. |
| `DELETE /api/learning-sessions/{id}/registration` | Cancel only this browser's booking. Returns 204, including when already cancelled. Cancellation is allowed after a session starts. |

Both mutations require one `X-Participant-Key` header containing a nonempty UUID. Invalid/missing keys return 400; nonexistent sessions return 404. Listing accepts the same header to include personal registration status (an invalid supplied key returns 400). The server normalizes the UUID and stores its SHA-256 hash. The raw key is a demo bearer credential: anyone who obtains it can manage those registrations. Keys are kept out of URLs and server responses. Production accounts, HTTPS, abuse prevention, and authorization are required before public use.

For a manual API exercise in PowerShell:

```powershell
$headers = @{ 'X-Participant-Key' = [guid]::NewGuid().ToString('N') }
Invoke-WebRequest http://localhost:5080/api/learning-sessions/1/registration -Method Post -Headers $headers
Invoke-RestMethod http://localhost:5080/api/learning-sessions -Headers $headers | Format-Table
Invoke-WebRequest http://localhost:5080/api/learning-sessions/1/registration -Method Delete -Headers $headers
```

This terminal key is separate from the browser key. Bookings are available until the session's UTC start time. On network failure the UI recommends refreshing, because a request may have reached the server even if its response was lost. Retrying is safe. If a change is saved but refreshing fails, the UI says so explicitly. Availability refreshes on page load, manual refresh, and successful registration changes; it is not a live push feed.

## Configuration and troubleshooting

- API port and environment: `src/SkillSnap.Api/Properties/launchSettings.json`.
- UI port: `src/SkillSnap.Client/Properties/launchSettings.json`.
- Browser API address: `src/SkillSnap.Client/wwwroot/appsettings.json` (`ApiBaseUrl`, ending in `/`). This is public browser configuration; never put secrets here.
- Allowed UI origin: API `appsettings.json` (`AllowedOrigins`). If you change the UI port, update this too. `localhost` and `127.0.0.1` are different origins.
- Database: API connection string `ConnectionStrings:SkillSnap`. Relative SQLite paths are resolved against the API content root. For an absolute custom path, create its parent directory first.
- If no cards load, open the API URL directly, check both terminals, and verify that ports and origins match. Stop the API and click Refresh sessions to see the error/retry state; restart it and click Try again to recover.
- If a port is occupied, stop the other process or update the matching configuration values.
- Local HTTP avoids certificate setup. A hosted version needs HTTPS and a deployment-specific configuration.
- Build errors about a missing SDK: install the .NET 10 **SDK**, reopen the terminal, and run `dotnet --list-sdks`.

## Database migrations

The initial and registration migrations and the model snapshot are committed. Normal startup applies migrations automatically for this local milestone. No manual EF command is needed to run it. Production migration execution should be a controlled deployment step.

To change the schema later, install the repository-local EF tool and add a migration:

```powershell
dotnet tool restore
dotnet ef migrations add YourChange --project src/SkillSnap.Api
dotnet run --project src/SkillSnap.Api --launch-profile http
```

Keep migrations in Git. Do not mix `EnsureCreated` with this migration workflow. The database file is ignored by Git; it is generated locally, not checked in. To reset only your demo data, stop the API, back up the database if needed, and remove its `skillsnap.db`, `skillsnap.db-shm`, and `skillsnap.db-wal` files from `App_Data`. Restart the API to recreate the demo database. Resetting discards local data.

## Validation and next steps

```powershell
dotnet test SkillSnap.slnx
dotnet publish src/SkillSnap.Client -c Release -o artifacts/client
```

GitHub Actions restores, builds, runs integration tests, and publishes the WebAssembly client on pushes and pull requests. Browser interactions are checked manually; automated UI tests are a future addition.

Next milestone: user identity and authorization, followed by validated organizer session creation, attendance, and deployment. See [the roadmap](docs/roadmap.md). Add these in small changes with tests as the project grows.
