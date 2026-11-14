# Accounts and registration ownership

## Browser flow

1. Browse sessions without signing in. No new browser key is created.
2. Open Account and choose Create account or Sign in. Signup requires email and a 10–128 character password with uppercase, lowercase, a number, and a symbol. Password confirmation is checked in the UI; Identity validates the actual password on the server.
3. The API creates an Identity user and stores a password hash in SQLite. A successful signup or login issues an HttpOnly, SameSite=Strict cookie. JavaScript never reads the login cookie or stores a login token.
4. The UI reads `/api/auth/me` to show current account state. Registration mutations require server authentication and use its user ID as the owner. Supplying another email, user ID, or browser key cannot change this owner.
5. Sign in to the same account in another browser to see the same registrations. Sign out removes the current browser's login cookie; it does not cancel bookings or sign out other devices.

The login cookie lasts eight hours with sliding renewal. Data Protection keys persist under the API's ignored `App_Data/keys` directory so cookies survive restarts. These local keys are security-sensitive; production needs protected persistent storage, HTTPS, and key backup/rotation appropriate to the host. Cookie sign-out is not a universal stolen-cookie revocation system.

## Request protection

Every POST/DELETE uses a fresh token from `GET /api/auth/csrf`, sent as `X-CSRF-TOKEN` alongside the browser's cookies. ASP.NET Core validates that the token belongs to the current cookie and identity. The UI fetches a fresh token for each mutation, avoiding reuse across login/logout transitions. Missing/invalid tokens return 400. Login and signup also require CSRF validation.

Only configured frontend origins receive credentialed CORS access. Account login/signup is limited to ten requests per IP per five minutes. Five failed password attempts lock an account for fifteen minutes. Neither invalid login nor nonexistent account returns account information. These controls are tested; they do not replace a full public-deployment security review.

The cookie design follows [Microsoft's guidance for standalone Blazor WebAssembly with Identity](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/standalone-with-identity/?view=aspnetcore-10.0). Antiforgery handling follows [ASP.NET Core antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0). The app exposes only its required custom endpoints rather than enabling unused identity management routes.

## Earlier browser bookings

The additive Identity migration retains previous sessions and anonymous registrations. Each registration has exactly one owner: a legacy browser-key hash or an Identity user ID. Database checks and unique indexes enforce this.

After login, the account page reads the old key from this browser's local storage, if present, and asks `/api/registrations/legacy-count` how many unclaimed bookings it owns. An explicit Import browser bookings action sends that key with the authenticated cookie and CSRF token. A transaction transfers only matching unclaimed rows into the current account. Already-owned duplicates are merged, freeing the redundant seat, and retries are safe.

This is a possession-based transition: anyone holding the old browser key can import its unclaimed demo bookings. There is no email matching or implicit claim on login. The original key is removed from browser storage only after a successful import response. If the response is lost, retain the key and safely retry. A key cannot take registrations already assigned to another account. Anonymous bookings from another browser stay unclaimed unless their original key is available; no key recovery or administrative claim is implemented.

## API routes

| Route | Requirements and response |
| --- | --- |
| `GET /api/auth/csrf` | Public; sets antiforgery cookie, returns `{ token }`; not cached. |
| `POST /api/auth/register` | CSRF + `{ email, password }`; returns account email and signs in, or 400 validation error. |
| `POST /api/auth/login` | CSRF + `{ email, password }`; returns account email and signs in, or 401. |
| `GET /api/auth/me` | Login; returns `{ email }`, or 401. Not cached. |
| `POST /api/auth/logout` | Login + CSRF; clears login cookie, 204. |
| `GET /api/registrations/legacy-count` | Login; optional old `X-Participant-Key`; returns `{ count }`, 0 if no key, 400 if malformed. |
| `POST /api/registrations/import` | Login + CSRF + old key; transfers bookings and returns `{ count }` for transferred rows (merged duplicates excluded). |

Public session listing remains available and never returns user IDs or emails. It marks `isRegistered` only for the authenticated account. Counts include unclaimed legacy bookings. The old header no longer authenticates normal registration/cancellation.

## Scope

This is a local portfolio implementation. Email is a login identifier; email ownership is not verified. Email confirmation, password recovery/change, MFA, account deletion, organizer roles/UI, and public deployment are future work. No mail is sent. Role tables are part of the standard Identity schema, but users cannot grant themselves organizer permissions and no organizer endpoints exist yet. Do not use this milestone as a public account service without completing those deployment requirements.
