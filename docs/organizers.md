# Organizer tools

## Access model

Attendees browse and manage their own registrations. Organizers additionally create and edit the entire learning programme. The role is programme-wide, not limited to sessions created by one organizer. No user can choose a role during signup or grant roles through HTTP.

The API's Organizer authorization policy checks the signed-in user's membership in the Identity database on every organizer request. Both a login and current Organizer membership are required; an old cookie cannot retain a removed role. `/api/auth/me` returns an informational `isOrganizer` flag so the UI can display the navigation link. The server enforces permissions independently of that flag.

Use the existing-account, Development-only provisioning command in the README. It creates the Organizer role if needed, assigns the intended existing account, and exits without listening for requests. It is idempotent and refuses Production or a missing account. This milestone does not add a role-administration web interface. Local database owners remain responsible for deciding who should receive elevated access.

## Create and edit

Open `/organizer`. Non-organizers see an access-required explanation and cannot use the editor. Organizers see a session form and the programme list. Create publishes immediately to the public listing; there is no draft workflow.

Required fields are title (up to 160 characters), description (2000), topic (80), instructor (120), and an explicit future UTC start time. Duration is 1–480 minutes and capacity is 1–1000. The UI offers defaults for date, duration, and capacity. The API validates independently, rejects blank/whitespace strings, requires a UTC timestamp, and trims text before saving.

The date/time input is explicitly **UTC**, not the browser's local timezone. For example, January 12 at 18:00 UTC is entered as January 12, 18:00. API clients send the trailing `Z` timestamp, such as `2027-01-12T18:00:00Z`.

Every session has an integer revision (`version`). Editing loads it into the form. A save must match the currently stored revision and increments it. Two organizers editing the same revision cannot silently overwrite each other: one succeeds and the other gets HTTP 409. The failed form retains its values; Reload selected session explicitly replaces them with the latest saved data. Refresh list does not silently replace the form.

Create is not an idempotent operation. If the network fails before its response is received, refresh the list before retrying to avoid duplicate sessions. The UI tells the organizer when a save succeeded but refreshing the list failed.

## Protect existing bookings

- Capacity can be increased or reduced to the current booking count, but not below it.
- Once any booking exists, start time and duration cannot change. This includes unclaimed legacy browser bookings.
- Title, description, topic, instructor, and valid capacity remain editable before the start time. Participants see updated details when they next load or refresh; notifications are not implemented.
- Started sessions are read-only, even if an organizer tries to reschedule them into the future.
- The editor does not delete sessions, remove participant registrations, or cancel a session. Participant cancellation remains account-owned; marked attendance must first be cleared by an organizer.

The API combines revision comparison, booking-count checks, schedule checks, and the update into one parameterized SQLite write. Registration itself also checks capacity in one write. SQLite serializes these writes, so a new booking cannot slip between checking capacity and saving a smaller capacity. The concurrency tests exercise both possible orders.

## API

All organizer routes require a login and the Organizer policy. POST and PUT additionally require a valid `X-CSRF-TOKEN` from `/api/auth/csrf`, with the matching cookies.

| Route | Result |
| --- | --- |
| `GET /api/organizer/learning-sessions/` | All programme sessions, including booking counts and revision; no attendee identities. |
| `GET /api/organizer/learning-sessions/{id}` | One editable session representation, or 404. |
| `POST /api/organizer/learning-sessions/` | Validate and create; 201 with representation and a resolvable Location. |
| `PUT /api/organizer/learning-sessions/{id}` | Validate and version-check; 204 on success, 404 if missing, 409 if a protection rule prevents the update. |

Missing login returns 401; an attendee account gets 403. Invalid form values or CSRF tokens return 400. Session representations contain `id`, title/description/topic/instructor, `startsAtUtc`, duration, capacity, `registeredCount`, and `version`. PUT includes the version loaded by the editor. POST ignores a supplied version and creates revision 0. Public listing remains separate and does not expose account IDs or participant data.

## Migration and scope

The revision migration adds a non-null integer column initialized to zero. It does not rewrite titles, times, capacity, registrations, accounts, or roles. Apply updates through normal API startup, not database reset. Keep a backup of important local data before schema changes.

Integration tests cover anonymous/attendee rejection, forged signup roles, grant/revoke behavior with the same login cookie, Development-only provisioning, create/edit visibility, validation, CSRF, protected bookings, past sessions, stale revisions, concurrent edits, and capacity/booking races. Organizer attendance rosters and corrections are documented in attendance.md. Email notifications, drafts, session deletion/cancellation, and public deployment remain future work.
