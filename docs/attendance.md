# Attendance management

## Booking and attendance are separate

Every registration starts at NotRecorded. An organizer can mark Attended or Absent, correct it later, or clear it back to NotRecorded. A booking alone does not establish attendance. The app never automatically marks absence and imposes no time window for manual recording; organizers are responsible for accuracy.

Attendance changes do not consume seats, change a session's revision, or alter its schedule. A separate attendance revision protects each registration. Two organizers saving the same loaded revision cannot overwrite each other silently: one succeeds and the other receives 409 and must refresh.

The latest update timestamp and acting organizer's account ID are derived by the server. The roster shows the UTC timestamp but not the actor's ID. These fields describe the last change, not an immutable audit log. Clearing attendance permits attendee cancellation, which removes the registration and its last-change fields. No historical attendance export or event log is implemented.

## Organizer flow

The existing programme-wide Organizer role grants roster access for all sessions. From Organizer, choose Manage attendance. Empty sessions show No registered attendees yet. Account registrations show their login email identifier, status, and the last-updated UTC time. Emails are not verified display names.

Use Mark attended, Mark absent, or Clear attendance. The app refreshes the roster after a successful change. If the response is lost or refreshing fails, refresh before retrying. An old version cannot be replayed to overwrite newer status. There is no bulk operation or ability to mark someone who is not registered.

Earlier anonymous bookings appear as Unclaimed browser booking. Their keys and hashes are never shown. They must be explicitly imported into an account by their original browser before attendance can be recorded. This prevents an organizer assigning attendance to an unidentified booking.

## Attendee privacy and cancellation

Public session listing contains only a signed-in attendee's own attendanceStatus. Guests and unrelated accounts receive null; attendee emails, registration IDs, and audit fields are excluded. Attendance rosters require the Organizer policy, even when the caller owns one of the registrations. Role revocation is checked through the same database-backed policy used by session editing. Responses containing roster data use Cache-Control: no-store.

Ordinary attendees cannot mark their own attendance. My sessions displays Not recorded, Attended, or Absent. Once attendance is recorded, cancellation returns 409 and the UI disables it with an explanation. Ask an organizer to clear attendance first. Cancellation of an unmarked registration remains idempotent and removes it from the roster.

Recording and cancellation use conditional SQLite writes. If recording wins a race, cancellation cannot delete the marked row. If cancellation wins, recording returns 404 rather than creating a new row or attaching attendance to a different booking. A later re-registration starts fresh.

## API

All attendance endpoints require a current Organizer account. PUT additionally requires the matching CSRF token and cookies.

| Route | Behavior |
| --- | --- |
| `GET /api/organizer/learning-sessions/{id}/attendance/` | Session metadata and registrations. Empty roster is 200; missing session is 404. |
| `PUT /api/organizer/learning-sessions/{id}/attendance/{registrationId}` | Accepts `{ "status": "Attended", "version": 0 }`. Returns 204 when the version matches. |

Allowed status strings are exactly NotRecorded, Attended, and Absent. Invalid status or negative version returns 400; missing login returns 401; an attendee account gets 403. A registration belonging to another session is treated as missing (404). Unclaimed legacy bookings or stale attendance revisions return 409. Missing/invalid CSRF returns 400.

Roster entries expose registrationId, email (null for legacy), status, version, updatedAtUtc, and isLegacy. They do not expose login cookies, browser keys/hashes, user IDs, or actor IDs. No personal data is added to public organizer session summaries.

## Persistence and verification

The attendance migration preserves existing registrations and initializes their status/revision to NotRecorded/0. It adds nullable last-update fields and a database check for valid status and revision values. Accounts, organizer roles, session data, and existing booking counts are retained.

Integration tests exercise permissions, privacy, state corrections, server-derived actor/time, invalid input, CSRF, session/registration mismatch, legacy import requirements, cancellation protection, concurrent corrections, cancellation races, earlier-schema upgrades, and API restart persistence. Browser verification is recorded in verification.md. This remains a local portfolio app; public hosting and account email verification/recovery are separate work.
