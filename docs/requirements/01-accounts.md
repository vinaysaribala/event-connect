# Feature: Accounts and Access

**Status:** Draft
**Release:** MVP (social sign-in is Phase 2)
**API:** Not started
**UI:** Not started
**Source:** [FRD §3](../functional-requirements.md#3-accounts-and-access), BR-01, BR-07, BR-09
**Depends on:** nothing (build first)

> Detail below the FRD's one-line requirements (limits, expiry times, endpoints, status codes) is proposed and needs review before Status moves to Ready.

## Summary

Organizers register, verify their email and sign in. Every event is owned by its organizer, and only the owner (and later, co-hosts) can manage it. Guests never need an account; they use the secure link in their invitation, which is covered in [05](05-invitation-delivery.md) and [06](06-rsvp.md).

## User stories

- As a visitor, I can register with my email and a password, so that I can create events.
- As a new user, I must verify my email before I can publish events or send invitations.
- As an organizer, I can sign in, sign out, reset a forgotten password and edit my profile.
- As an organizer, I am the only one who can see or change my events and guest lists.

## Functional requirements

- **FR-001** Register with email/password and verify email.
  - Email is unique (case-insensitive). Password: at least 10 characters.
  - A verification email with a single-use link is sent on registration; the link expires after 24 hours and can be re-requested.
  - Unverified users can sign in and create draft events, but cannot publish events or send invitations.
- **FR-002** Sign in, sign out, reset password and manage profile.
  - Sign-in failures give one generic message ("Email or password is incorrect"), never saying which part was wrong.
  - Forgot-password always responds the same way, whether or not the email exists.
  - Reset links are single-use and expire after 1 hour.
  - Profile: display name and default time zone (used to prefill new events).
- **FR-004** Event-level ownership and authorization.
  - Each event has exactly one owner, the organizer who created it (BR-01).
  - Requests for an event the caller does not own return **404 Not Found**, not 403, so event ids can't be probed.
  - Co-host permissions (FR-011) are Phase 2.
- **FR-005** Token-based guest access without registration. Defined in [05](05-invitation-delivery.md) and [06](06-rsvp.md); listed here because it is part of the access model.
- **Roles:** `Organizer` (every registered user) and `Administrator` (assigned, see [09](09-administration.md)).

## Data

| Entity | Field | Type | Rules |
|---|---|---|---|
| User | Id | int | key |
| | Email | string | required, unique, max 256 |
| | EmailVerifiedAt | DateTime? (UTC) | null until verified |
| | PasswordHash | string | never returned by the API |
| | DisplayName | string | required, max 100 |
| | TimeZone | string | IANA zone, e.g. `America/New_York` |
| | CreatedAt / UpdatedAt | DateTime (UTC) | set by server |
| Role | Id, Name | | `Organizer`, `Administrator` |
| Event | OrganizerId | int | **new column**, FK → User, required |

## API

### `POST /api/auth/register` — create an account

```json
{ "email": "maria@example.com", "password": "correct-horse-42", "displayName": "Maria Lopez" }
```

- **201 Created**

  ```json
  { "id": 7, "email": "maria@example.com", "displayName": "Maria Lopez", "emailVerified": false }
  ```

- **400** — validation (invalid email, short password, missing name)
- **409 Conflict** — email already registered

### `POST /api/auth/verify-email` — confirm email from the link

```json
{ "token": "<token from email link>" }
```

- **204 No Content** — verified
- **400** — token invalid, used or expired

### `POST /api/auth/resend-verification` — send a new verification email (signed in)

- **202 Accepted**

### `POST /api/auth/login` — sign in

```json
{ "email": "maria@example.com", "password": "correct-horse-42" }
```

- **200 OK** — signed in; the response sets the session cookie (see Decisions)

  ```json
  { "id": 7, "email": "maria@example.com", "displayName": "Maria Lopez", "emailVerified": true, "roles": ["Organizer"] }
  ```

- **401 Unauthorized** — wrong email or password (generic message)

### `POST /api/auth/logout` — sign out

- **204 No Content**

### `POST /api/auth/forgot-password`

```json
{ "email": "maria@example.com" }
```

- **202 Accepted** — always, whether or not the email exists

### `POST /api/auth/reset-password`

```json
{ "token": "<token from email link>", "newPassword": "another-strong-pass" }
```

- **204 No Content**
- **400** — token invalid/expired, or password too weak

### `GET /api/me` — current user

- **200 OK** — same shape as login response plus `"timeZone": "America/New_York"`
- **401** — not signed in

### `PUT /api/me` — update profile

```json
{ "displayName": "Maria L.", "timeZone": "America/Chicago" }
```

- **200 OK** — updated profile
- **400** — validation (unknown time zone, empty name)

## UI

### Screen: Register (`/register`)

- Email, display name, password (with strength hint).
- On success, show "Check your email to verify your account" and sign the user in.

### Screen: Verify email (`/verify-email?token=…`)

- Calls verify on load; shows success with "Go to dashboard", or an error with "Send a new link".

### Screen: Sign in (`/login`)

- Email, password, "Forgot password?" link. On success go to `/dashboard`, or to the page the user originally tried to open.

### Screens: Forgot password (`/forgot-password`) and Reset password (`/reset-password?token=…`)

- Forgot: email field, then "If that address has an account, we've sent a reset link."
- Reset: new password twice; on success, go to sign in.

### Screen: Profile (`/profile`)

- Display name, time zone (searchable list), email (read-only), verification status with "Resend" button.

### Shared

- Unverified users see a banner: "Verify your email to publish events."
- Pages that need sign-in redirect to `/login`.

## Acceptance criteria

- [ ] Registering with a new email returns 201 and sends a verification email.
- [ ] Registering the same email again (any letter case) returns 409.
- [ ] A verification link works once; a second use or one older than 24 hours returns 400.
- [ ] Wrong password and unknown email both return the same 401 message.
- [ ] Forgot-password returns 202 for both known and unknown emails.
- [ ] An unverified user can create a draft event but gets an error when publishing.
- [ ] User A requesting `GET /api/events/{id}` for User B's event gets 404.
- [ ] Signed-out requests to organizer endpoints return 401.

## Later (Phase 2)

- **FR-003** Google/Apple sign-in.
- **FR-011** Co-hosts (see [02](02-events.md)).

## Decisions

### Sessions: server-side cookie, not tokens (decided 2026-11-02)

**API**
- ASP.NET Core Identity issues the session cookie on `POST /api/auth/login` and clears it on `POST /api/auth/logout`.
- The cookie is `HttpOnly`, `Secure` and `SameSite=Lax`, so JavaScript can never read it.
- Unauthenticated API requests return **401**, never a redirect to a login page. Identity's default redirect is overridden in `Program.cs`.

**UI**
- No tokens are stored in `localStorage`, `sessionStorage` or memory.
- An `AuthProvider` context holds the current user. It calls `GET /api/me` on app load (200 = signed in, 401 = signed out), and exposes `user`, `loading`, `login()` and `logout()`.
- One shared fetch helper treats a 401 from any request as "session ended": it clears the user and redirects to `/login?returnTo=…`.
- Protected routes wait for `loading`, then redirect to `/login` if there's no user. Admin routes also check `roles`.
- The guest page `/invite/:token` needs no session; the token is the credential (FR-005).

**Same origin**
- The UI calls the API with relative URLs (`/api/...`).
- Locally, the Vite dev server proxies `/api` to `http://localhost:5207` (`server.proxy` in `UI/vite.config.ts`). The browser only sees `localhost:5173`, so no CORS setup is needed.
- In production, the UI and API are served under one domain.

**Local development notes**
- Browsers treat `localhost` as secure, so the `Secure` cookie works over plain HTTP; the HTTPS launch profile isn't needed.
- Cookies ignore ports. Signing in through the app at `:5173` also signs in Scalar at `:5207/scalar` in the same browser.
- REST Client in VS Code keeps cookies between requests. Run the login request in `EventConnectAPI.http` first.

**Why:** an HTTP-only cookie can't be stolen by an injected script, and the server controls expiry and sign-out. A single-origin setup avoids cross-site cookie and CORS problems. Bearer tokens (JWT) would only be needed for a mobile app, third-party API clients, or a UI on a different domain from the API.

## Open questions
- Should unverified users be blocked from more than publishing and sending (for example, after 7 days)?
- Email provider for verification and reset emails; shared with [05](05-invitation-delivery.md).
- Account deletion and data export (FRD §16 Privacy) are not in the FR list. Are they MVP?
