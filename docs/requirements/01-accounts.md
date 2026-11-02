# Feature: Accounts and Access

**Status:** Ready
**Release:** MVP (social sign-in is Phase 2)
**API:** Not started
**UI:** Not started
**Source:** [FRD §3](../functional-requirements.md#3-accounts-and-access), BR-01, BR-07, BR-09
**Depends on:** nothing (build first)

> Reviewed and marked Ready on 2026-11-02. The detail added beyond the FRD (limits, expiry times, endpoints, status codes) is accepted as written; see Decisions.

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
  - This feature provides the signed-in user and roles. Applying ownership to events (the `OrganizerId` column and owner checks) is built in [02 Events](02-events.md).
  - Co-host permissions (FR-011) are Phase 2.
- **FR-005** Token-based guest access without registration. Defined in [05](05-invitation-delivery.md) and [06](06-rsvp.md); listed here because it is part of the access model.
- **Roles:** `Organizer` (every registered user) and `Administrator`.
  - The first Administrator comes from configuration: when a user registers (or verifies) with the email in `Admin:SeedEmail`, they also get the `Administrator` role. Managing roles after that is in [09](09-administration.md).
- **Emails sent by this feature:** verification and password reset. Both are sent through one email-sending interface, so the provider can change without touching the rest of the code (see Decisions).

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

`Event.OrganizerId` is added in [02 Events](02-events.md), not here.

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
- [ ] Forgot-password returns 202 for both known and unknown emails; only a known email receives a reset email.
- [ ] A reset link works once and changes the password; the old password no longer signs in.
- [ ] Signed-out requests to `GET /api/me` and `PUT /api/me` return 401, not a redirect.
- [ ] After login, `GET /api/me` returns the user; after logout it returns 401.
- [ ] Registering with the `Admin:SeedEmail` address gives the user the `Administrator` role.
- [ ] Verification and reset emails arrive in a real inbox through Gmail SMTP. With no SMTP settings, the email and its link are logged instead and the request still succeeds.
- [ ] UI: register → verify (via the emailed link) → sign in → edit profile → sign out works end to end in the browser; protected pages redirect to `/login` when signed out; the unverified banner shows until the email is verified.

Ownership checks on events (unverified users can't publish; another organizer's event returns 404) are acceptance criteria of [02 Events](02-events.md).

## Later (Phase 2)

- **FR-003** Google/Apple sign-in.
- **FR-011** Co-hosts (see [02](02-events.md)).
- Account deletion and personal data export (FRD §16 Privacy). Deferred because they touch events, guests and RSVPs, so they're built once those exist.

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

### Email: Gmail SMTP for local development (decided 2026-11-02)

- Emails are sent through an `IEmailSender` interface in `EventConnectService`. The first implementation sends via SMTP; a production provider can replace it later without other changes. [05](05-invitation-delivery.md) reuses it.
- Local development uses Google's SMTP server: `smtp.gmail.com`, port 587, STARTTLS.
  - Settings: `Email:Smtp:Host`, `Port`, `UserName`, `Password`, `FromAddress`, `FromName`.
  - The password is a Google **App Password** (the Google account needs 2-Step Verification), not the account's normal password.
  - The user name, password and from-address are kept in **.NET user secrets** (`dotnet user-secrets` on the `EventConnectAPI` project), never in `appsettings*.json` or git. `appsettings.json` holds only host, port and from-name.
- If the SMTP settings are missing, the email (with its link) is written to the API log instead, so the app still runs for anyone who hasn't set up Gmail.
- Links in emails point at the UI (`App:BaseUrl`, `http://localhost:5173` in Development).
- Gmail limits sending to roughly 500 recipients a day, which is fine for development. Production needs a transactional email provider; to be chosen before launch ([05](05-invitation-delivery.md)).

### Unverified users (decided 2026-11-02)

- No limits beyond "can't publish events or send invitations". Add more only if abuse appears.

### Scope boundary with Events (decided 2026-11-02)

- This feature builds accounts, sessions, roles and profile only. The existing Events API is left unchanged; making events owned and sign-in-only is done in [02 Events](02-events.md).

### First Administrator (decided 2026-11-02)

- Set by configuration: `Admin:SeedEmail` in `appsettings.Development.json` (or user secrets). That user gets the `Administrator` role on registration or verification. Also recorded in [09](09-administration.md).

### Account deletion and data export (decided 2026-11-02)

- Not in this feature; moved to Later (Phase 2).

## Open questions

None. All questions are answered under Decisions.
