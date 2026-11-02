# Feature: Invitation Delivery

**Status:** Draft
**Release:** MVP (SMS and scheduled sending are Phase 2)
**API:** Not started
**UI:** Not started
**Source:** [FRD §7](../functional-requirements.md#7-invitation-delivery), FR-005, BR-07, BR-10, AC-05, AC-06
**Depends on:** [03 Invitation Design](03-invitation-design.md), [04 Guest Lists](04-guest-lists.md)

> Detail below the FRD's one-line requirements (token rules, endpoints, status codes) is proposed and needs review before Status moves to Ready.

## Summary

Organizers send personalized email invitations to guests on a published event. Each guest gets their own secret link, which can be revoked. Sending happens in the background, and every delivery attempt is tracked. Sending the same request twice never sends twice.

## User stories

- As an organizer, I can send invitations to all guests not yet invited, or to guests I select.
- As an organizer, I can see whether each invitation was queued, sent, delivered, bounced or failed.
- As an organizer, I can resend an invitation without risking a duplicate email.
- As an organizer, I can revoke a guest's link, and issue a new one if needed.
- As a guest, I receive an email addressed to me, with a button that opens my personal invitation.

## Functional requirements

- **FR-022** Send personalized invitations by email.
  - Only for `Published` events, and only to guests who haven't unsubscribed ([07](07-communication.md), FR-038).
  - The email contains: the guest's name, the event title, date/time with time zone, location or "Online", a rendering or image of the current design version ([03](03-invitation-design.md)), and an "RSVP" button linking to `/invite/{token}`.
  - Every email includes an unsubscribe link (FR-038).
  - Sending is queued and runs in the background (FRD §16 Scalability). The API answers immediately with what was queued and what was skipped.
- **FR-023** Unique, revocable guest links and a shareable event link.
  - Each guest has one invitation with a random token of at least 128 bits, URL-safe. Only a hash of the token is stored (BR-07).
  - Revoking makes the old link stop working at once (AC-06). "Reissue" revokes the old link and creates a new one, which needs to be sent.
  - The event share link (`/e/{shareToken}`) is also random and can be regenerated. What it allows depends on an open FRD decision (see Open questions).
- **FR-024** Record queued, sent, delivered, bounced and failed statuses when the provider supports them.
  - `Queued` → `Sent` when the provider accepts the email. `Delivered`, `Bounced` or `Failed` come from provider webhooks where available.
  - Each attempt is kept with its time, provider message id and error.
- **FR-025** Resend safely without duplicate processing.
  - Every send or resend request carries an `Idempotency-Key` header. Repeating a key within 24 hours returns the original result without sending again (AC-05, BR-10).
  - Background retries for temporary failures reuse the same attempt and never create a second email.
  - A guest can be resent to at most once every 10 minutes, as protection against accidental repeated clicks.

## Data

| Entity | Field | Type | Rules |
|---|---|---|---|
| Invitation | Id | int | key |
| | EventId, GuestId | int | unique (EventId, GuestId) |
| | TokenHash | string | hash of the guest token; unique |
| | Status | enum | `NotSent`, `Queued`, `Sent`, `Delivered`, `Bounced`, `Failed`, `Revoked` |
| | RevokedAt | DateTime? (UTC) | |
| | LastSentAt | DateTime? (UTC) | |
| InvitationDeliveryAttempt | Id | int | key |
| | InvitationId | int | FK |
| | Channel | enum | `Email` (`Sms` in Phase 2) |
| | Status | enum | `Queued`, `Sent`, `Delivered`, `Bounced`, `Failed` |
| | ProviderMessageId | string? | |
| | Error | string? | |
| | CreatedAt / UpdatedAt | DateTime (UTC) | |
| IdempotencyRecord | Key, UserId, RequestHash, Response (JSON), CreatedAt | | kept 24 hours |
| Event | ShareTokenHash | string? | added to Event |

## API

Organizer endpoints require sign-in and ownership (FR-004).

### `POST /api/events/{id}/invitations/send`

Header: `Idempotency-Key: 8f14e45f-ceea-467a-9575-7e2c1b0f1c11`

```json
{ "guestIds": [31, 32, 40] }
```

or, to invite everyone not yet invited:

```json
{ "allNotSent": true }
```

- **202 Accepted**

  ```json
  {
    "queued": 2,
    "skipped": [{ "guestId": 40, "reason": "Unsubscribed" }]
  }
  ```

  Skip reasons: `AlreadySent` (use resend), `Unsubscribed`, `Revoked`.
- **400** — no `Idempotency-Key`, or neither `guestIds` nor `allNotSent` given
- **409** — event is not `Published`
- Repeating the same key and body returns the same 202 body, and nothing is sent again.

### `POST /api/events/{id}/invitations/{invitationId}/resend`

Header: `Idempotency-Key`

- **202 Accepted** — `{ "queued": 1 }`
- **409** — revoked, guest unsubscribed, or resent less than 10 minutes ago (`"retryAfterSeconds": 412`)

### `POST /api/events/{id}/invitations/{invitationId}/revoke`

- **200 OK** — `{ "id": 55, "status": "Revoked", "revokedAt": "2026-11-12T09:00:00Z" }`

### `POST /api/events/{id}/invitations/{invitationId}/reissue`

- **200 OK** — new invitation with `"status": "NotSent"`. The old link is revoked and the new one must be sent.

### `GET /api/events/{id}/invitations`

- **200 OK**

  ```json
  [
    {
      "id": 55,
      "guestId": 31,
      "guestName": "Sam Carter",
      "status": "Delivered",
      "lastSentAt": "2026-11-05T12:00:00Z",
      "attempts": [
        { "status": "Delivered", "createdAt": "2026-11-05T12:00:00Z", "error": null }
      ]
    }
  ]
  ```

### `GET /api/events/{id}/share-link` and `POST /api/events/{id}/share-link/regenerate`

- **200 OK** — `{ "url": "http://localhost:5173/e/Xk3…" }`. Regenerating makes the old URL stop working.

### `POST /api/webhooks/email` — provider delivery callbacks (no sign-in; verified by provider signature)

- **200 OK** — updates attempt and invitation status
- **401** — signature invalid

Guest-facing link handling (`/api/invitations/{token}`) is in [06 RSVP](06-rsvp.md).

## UI

### Guest list additions (`/events/:id/guests`, see [04](04-guest-lists.md))

- Checkboxes plus "Send invitations to selected", and "Invite all not yet invited (N)".
- A confirmation dialog shows the count and an email preview, then a result toast: "18 queued, 2 skipped (unsubscribed)".
- An invitation status badge per guest, with a tooltip showing the last attempt and any error.
- Row menu: Resend, Revoke link, Reissue link, Copy link.

### Event overview (`/events/:id`)

- Share link with copy and regenerate buttons (only when the event's visibility makes it useful).

## Acceptance criteria

- [ ] Sending to a Draft event returns 409.
- [ ] **AC-05** Posting the same send request twice with the same `Idempotency-Key` queues emails once; the second response matches the first.
- [ ] A temporary provider failure is retried in the background without creating a second email.
- [ ] Unsubscribed guests are skipped with reason `Unsubscribed`.
- [ ] Each guest's email contains their own unique link, and links can't be guessed from each other.
- [ ] **AC-06** After revoking, the old link returns "no longer valid" for viewing and for RSVP ([06](06-rsvp.md)).
- [ ] After reissuing, the old link fails and the new link works once sent.
- [ ] A provider "bounced" webhook changes the invitation status to `Bounced`.

## Later (Phase 2)

- **FR-026** SMS invitations with opt-in and regulatory controls.
- **FR-027** Scheduled delivery (send at a future date and time).

## Open questions

- **FRD decision:** is the shareable event link an open RSVP link (anyone can add themselves as a guest) or only a way for invited guests to find the event? Proposal for MVP: it shows public event details only, and RSVP requires a personal link.
- Which email provider (e.g. SendGrid, Postmark, Azure Communication Services, Amazon SES)? For development, emails can go to a local SMTP catcher such as smtp4dev.
- Background processing: an in-process queue with a database outbox is enough for MVP; a separate worker and message queue come later. Confirm before production.
- Should invitations be sent from the organizer's name ("Maria Lopez via EventConnect")?
- Is a free-plan send limit needed in MVP (FRD decision: limits for free invitations)?
