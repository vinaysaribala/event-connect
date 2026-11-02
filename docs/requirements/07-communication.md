# Feature: Communication

**Status:** Draft
**Release:** MVP (announcements to guest segments are Phase 2)
**API:** Not started
**UI:** Not started
**Source:** [FRD §9](../functional-requirements.md#9-communication), BR-08, BR-10, AC-07
**Depends on:** [05 Invitation Delivery](05-invitation-delivery.md), [06 RSVP](06-rsvp.md)

> Detail below the FRD's one-line requirements (reminder schedule, who counts as affected, endpoints) is proposed and needs review before Status moves to Ready.

## Summary

The system sends three kinds of guest email apart from the invitation itself: reminders to guests who haven't responded, notices when an event's date, time or location changes, and notices when it's cancelled. Every email has an unsubscribe link, and the unsubscribe is always respected. All sends are recorded and safe to retry.

## User stories

- As an organizer, I can turn on automatic reminders for guests who haven't responded, and choose when they go out.
- As an organizer, when I change the date, time or location, I can notify affected guests with a short message.
- As an organizer, when I cancel, every guest who might come is told automatically.
- As a guest, I can unsubscribe from EventConnect emails with one click.
- As an organizer, I can see which messages were sent to whom, and whether any failed.

## Functional requirements

- **FR-035** Configurable RSVP reminders to non-responders.
  - Per event: on/off, up to 3 reminder times expressed as days before the RSVP deadline (or before the start, if there's no deadline), and an optional custom message.
  - Sent only to guests whose invitation was sent and who haven't responded, and never to unsubscribed or revoked guests.
  - Times are evaluated in the event's time zone; reminders go out at 10:00 local time on the chosen day.
  - Reminders stop when the event is cancelled or archived (BR-08), or when the deadline passes.
- **FR-036** Notify attendees of material changes or cancellation.
  - Material change = start, end, time zone, format, venue, address or meeting URL of a `Published` event.
  - "Affected guests" = guests whose current RSVP is Yes or Maybe, plus invited guests who haven't responded. Guests who answered No are not notified.
  - Change notices go out only when the organizer confirms (the `notifyGuests` flag in [02](02-events.md)). They show what changed, old and new, plus the optional message.
  - Cancellation notices always go to affected guests, with the organizer's message (BR-08, AC-07).
  - Results are recorded per guest (journey "Organizer updates event": record notification results).
- **FR-038** Respect unsubscribe (SMS opt-out comes with SMS in Phase 2).
  - Every email has a one-click unsubscribe link and the matching email headers.
  - Unsubscribing is per email address, across all EventConnect events. Unsubscribed addresses get no invitations, reminders, change notices or announcements.
  - RSVP confirmations ([06](06-rsvp.md)) and cancellation notices are treated as service messages and still go out (see Open questions).
- **BR-10** Retries are idempotent: each notification is one record, retried in place, and sent at most once.

## Data

| Entity | Field | Type | Rules |
|---|---|---|---|
| ReminderSettings | EventId | int | key and FK |
| | Enabled | bool | |
| | DaysBefore | int[] (JSON) | up to 3 values, 1–30 |
| | Message | string? | max 500 |
| Notification | Id | int | key |
| | EventId, GuestId | int | FK |
| | Type | enum | `Invitation`, `Reminder`, `RsvpConfirmation`, `EventChanged`, `EventCancelled` |
| | Channel | enum | `Email` |
| | Status | enum | `Queued`, `Sent`, `Delivered`, `Bounced`, `Failed`, `Skipped` |
| | SkipReason | string? | e.g. `Unsubscribed` |
| | DedupeKey | string | unique; e.g. `reminder:{eventId}:{guestId}:{date}` |
| | CreatedAt / SentAt | DateTime (UTC) | |
| NotificationPreference | Email | string | key (normalized, lowercase) |
| | UnsubscribedAt | DateTime? (UTC) | |
| | UnsubscribeTokenHash | string | |

Invitation sends ([05](05-invitation-delivery.md)) also create `Notification` rows, so the message log covers everything.

## API

### `GET /api/events/{id}/reminders` and `PUT /api/events/{id}/reminders` (sign-in and ownership)

```json
{ "enabled": true, "daysBefore": [7, 2], "message": "Hope you can make it, let us know by Friday!" }
```

- **200 OK** — saved settings plus `"nextReminderAt": "2026-11-21T10:00:00-05:00"`
- **400** — more than 3 values, or values outside 1–30

### Change notices

Sent through `PUT /api/events/{id}` with `notifyGuests` and `changeMessage` ([02](02-events.md)). Cancellation notices are sent through `POST /api/events/{id}/cancel`.

### `GET /api/events/{id}/notifications` — message log (sign-in and ownership)

Query: `type`, `status`, `page`.

- **200 OK**

  ```json
  {
    "items": [
      { "id": 301, "guestName": "Sam Carter", "type": "EventChanged", "status": "Delivered", "sentAt": "2026-11-15T15:00:00Z", "skipReason": null }
    ],
    "page": 1,
    "pageSize": 50,
    "totalCount": 64
  }
  ```

### `GET /api/unsubscribe/{token}` and `POST /api/unsubscribe/{token}` (no sign-in)

- GET **200 OK** — `{ "email": "s***@example.com", "unsubscribed": false }` (masked)
- POST **204 No Content** — unsubscribed. Also accepts the one-click POST that mail clients send.
- **404** — unknown token

### `POST /api/unsubscribe/{token}/resubscribe` (no sign-in)

- **204 No Content**

## UI

### Panel: Reminders (event overview, Messages tab)

- On/off switch, up to three "N days before the deadline" fields, optional message, and the next scheduled send time.

### Dialog: Notify guests of change (when saving a material change in [02](02-events.md))

- Shows "You changed: Start time 7:00 PM → 8:00 PM", "Notify 34 affected guests?", an optional message, then Save and notify or Save without notifying.

### Messages tab (`/events/:id/messages`)

- Message log with type and status filters; failed rows show the error.

### Page: Unsubscribe (`/unsubscribe/:token`), public

- "Unsubscribe s***@example.com from EventConnect emails?", a confirm button, and a confirmation with "Resubscribe".

## Acceptance criteria

- [ ] With reminders set to 2 days before the deadline, only invited guests who haven't responded get one, at 10:00 event-local time that day.
- [ ] A guest who RSVPs before the reminder time doesn't get it.
- [ ] Running the reminder job twice for the same day sends each reminder once.
- [ ] Changing the start time with notify on sends change notices to Yes, Maybe and unanswered guests, not to No guests, and the log shows them.
- [ ] **AC-07** (notice part) Cancelling queues cancellation notices to affected guests, and cancels pending reminders.
- [ ] An unsubscribed address receives no reminders or change notices; the log shows `Skipped` / `Unsubscribed`.
- [ ] One-click unsubscribe from an email works without signing in.

## Later (Phase 2)

- **FR-037** Organizer announcements to selected guest segments (needs guest groups or tags; see [04](04-guest-lists.md) Open questions).
- SMS opt-out handling (with FR-026).

## Open questions

- Unsubscribe scope: all of EventConnect (proposed), or per organizer or per event?
- Should RSVP confirmations and cancellation notices go to unsubscribed addresses as service messages? Check what's legally required in your target countries.
- Is 10:00 local time a good fixed send time for reminders, or should organizers choose it?
- Should Maybe guests also get reminders, to firm up their answer?
