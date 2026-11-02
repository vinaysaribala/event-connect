# Feature: Guest Lists

**Status:** Draft
**Release:** MVP
**API:** Not started
**UI:** Not started
**Source:** [FRD §6](../functional-requirements.md#6-guest-lists), BR-07, BR-09, AC-08
**Depends on:** [02 Events](02-events.md)

> Detail below the FRD's one-line requirements (CSV format, duplicate rules, endpoints, status codes) is proposed and needs review before Status moves to Ready.

## Summary

Organizers build an event's guest list by hand or by importing a CSV, which is checked before anything is saved. Each guest has an email, an optional phone number, a record of consent, and their own plus-one allowance. The list shows each guest's invitation and RSVP status and can be exported.

## User stories

- As an organizer, I can add, edit, remove and search guests on my event.
- As an organizer, I can import guests from a CSV and see invalid rows and duplicates before anything is saved.
- As an organizer, I can give a specific guest a different plus-one allowance from the event default.
- As an organizer, I can see at a glance who has been invited and who has responded.
- As an organizer, I can export the guest list with responses.

## Functional requirements

- **FR-017** Add, edit, remove and search guests.
  - Search matches name or email (contains, case-insensitive). Filter by invitation status and RSVP status.
  - Removing a guest who has been invited revokes their link (BR-07) and frees any capacity they held (BR-03).
  - Guests can be added to Draft or Published events, but not to Cancelled or Archived ones.
- **FR-018** Import from CSV with validation and duplicate detection (AC-08).
  - Columns: `name` (required), `email` (required), `phone` (optional), `plusOnes` (optional, integer). The header row is required; column order doesn't matter.
  - Maximum 2,000 rows and 1 MB per file.
  - Two steps: **upload** returns a preview with valid rows, invalid rows (with reasons) and duplicates. Nothing is saved until **confirm**. An unconfirmed preview expires after 1 hour.
  - Duplicates are matched on email (case-insensitive): within the file, and against guests already on the event. Duplicates are skipped, never merged.
- **FR-019** Email and optional phone with consent metadata.
  - Email is required and unique per event. Phone, if given, is stored in E.164 format (`+15551234567`).
  - Consent metadata records how the contact was added (`Manual` or `CsvImport`), who added it, when, and the organizer's confirmation that they may contact this person. The confirmation is required to save.
- **FR-020** Invitation status and per-guest plus-one allowance.
  - Invitation status comes from delivery ([05](05-invitation-delivery.md)): `NotSent`, `Queued`, `Sent`, `Delivered`, `Bounced`, `Failed`, `Revoked`.
  - The plus-one allowance defaults to the event's `DefaultMaxPlusOnes`, and can be overridden from 0 to 10. It only applies if the event allows plus-ones.
- **FR-021** Export guest and response lists.
  - A CSV download with one row per guest: name, email, phone, plus-ones allowed, invitation status, RSVP response, attendee count, comment, responded at, and one column per custom RSVP question ([06](06-rsvp.md)).
  - Exports are recorded in the audit log ([09](09-administration.md)) because they contain personal data (BR-09).

## Data

| Entity | Field | Type | Rules |
|---|---|---|---|
| Guest | Id | int | key |
| | EventId | int | FK |
| | Name | string | required, max 200 |
| | Email | string | required, max 256; unique per event (case-insensitive) |
| | Phone | string? | E.164 |
| | MaxPlusOnes | int? | 0–10; null = use event default |
| | ConsentSource | enum | `Manual`, `CsvImport` |
| | ConsentConfirmedByUserId | int | FK → User |
| | ConsentConfirmedAt | DateTime (UTC) | |
| | CreatedAt / UpdatedAt | DateTime (UTC) | |
| GuestImport | Id (GUID), EventId, CreatedByUserId, ExpiresAt, Rows (JSON) | | temporary; deleted after confirm or expiry |

Invitation status and RSVP status are read from [05](05-invitation-delivery.md) and [06](06-rsvp.md); they are not stored on `Guest`.

## API

All endpoints require sign-in and ownership of the event (FR-004).

### Guest object

```json
{
  "id": 31,
  "name": "Sam Carter",
  "email": "sam@example.com",
  "phone": "+15551234567",
  "maxPlusOnes": 2,
  "effectiveMaxPlusOnes": 2,
  "invitationStatus": "Delivered",
  "rsvp": { "response": "Yes", "plusOnes": 1, "respondedAt": "2026-11-10T14:22:00Z" }
}
```

`rsvp` is `null` until the guest responds.

### `GET /api/events/{id}/guests`

Query: `search`, `invitationStatus`, `rsvpResponse` (`Yes`, `No`, `Maybe`, `None`), `page` (50 per page).

- **200 OK** — `{ "items": [ …guest objects ], "page": 1, "pageSize": 50, "totalCount": 132 }`

### `POST /api/events/{id}/guests`

```json
{ "name": "Sam Carter", "email": "sam@example.com", "phone": "+15551234567", "maxPlusOnes": 2, "consentConfirmed": true }
```

- **201 Created** — guest object
- **400** — validation (missing name or email, bad phone, `consentConfirmed` not true)
- **409** — email already on this event, or event Cancelled/Archived

### `PUT /api/events/{id}/guests/{guestId}`

Same body as POST (`consentConfirmed` not needed again).

- **200 OK** — guest object
- **400 / 404 / 409** — as above

### `DELETE /api/events/{id}/guests/{guestId}`

- **204 No Content** — removed; their link is revoked and any capacity is released

### `POST /api/events/{id}/guests/imports` — upload CSV (multipart, field `file`; form field `consentConfirmed=true`)

```csv
name,email,phone,plusOnes
Sam Carter,sam@example.com,+15551234567,2
Priya Shah,priya@example,,
Lee Wong,lee@example.com,,1
Lee W,LEE@example.com,,
```

- **200 OK** — preview, nothing saved yet:

  ```json
  {
    "importId": "5c1d9a0e-2f7b-4a51-9d1e-8b3f0c2a6e11",
    "expiresAt": "2026-11-02T17:00:00Z",
    "validCount": 2,
    "invalidRows": [{ "row": 3, "errors": ["email: not a valid email address"] }],
    "duplicates": [
      { "row": 5, "email": "LEE@example.com", "reason": "Duplicate of row 4 in this file" }
    ]
  }
  ```

  Duplicates of guests already on the event use the reason `"Already on the guest list"`.
- **400** — not a CSV, missing required header, over 2,000 rows or 1 MB

### `POST /api/events/{id}/guests/imports/{importId}/confirm`

- **200 OK** — `{ "added": 2, "skippedInvalid": 1, "skippedDuplicates": 1 }`
- **404** — unknown or expired import
- **409** — guests were added since the preview, creating new duplicates; upload again

### `GET /api/events/{id}/guests/export`

- **200 OK** — `text/csv` download named `<event-title>-guests-<yyyy-MM-dd>.csv`

## UI

### Screen: Guest list (`/events/:id/guests`)

- Table: name, email, plus-ones, invitation status badge, RSVP badge. Search box and status filters. Totals above the table.
- Buttons: "Add guest", "Import CSV", "Export", and "Send invitations" ([05](05-invitation-delivery.md)).
- Each row has edit and remove actions. Removing an invited guest warns: "Their invitation link will stop working."

### Dialog: Add/Edit guest

- Name, email, phone, plus-one override (shown only if the event allows plus-ones), and the consent checkbox "I have permission to contact this person."

### Wizard: Import CSV

1. Upload, with a downloadable template CSV and the consent checkbox.
2. Review: "2 guests ready to add", then expandable lists of invalid rows (row number and reason) and duplicates.
3. Confirm, then a result summary and a return to the list.

## Acceptance criteria

- [ ] Adding a guest whose email is already on the event (any letter case) returns 409.
- [ ] Saving a guest without the consent confirmation returns 400.
- [ ] **AC-08** Uploading a CSV with one bad email and one in-file duplicate returns a preview listing both, and adds no guests until confirm. Confirm adds only the valid, non-duplicate rows.
- [ ] A CSV row whose email is already on the event is reported as "Already on the guest list".
- [ ] An import preview older than 1 hour can't be confirmed (404).
- [ ] Removing an invited guest makes their invitation link return "no longer valid" ([06](06-rsvp.md)).
- [ ] The export contains every guest with RSVP data and custom answer columns, and creates an audit entry.
- [ ] Another organizer can't list, add to or export this event's guests (404).

## Open questions

- Is email always required, or can a guest have only a phone number (only useful once SMS exists in Phase 2)?
- Should organizers be able to group or tag guests (for example "Family" or "Work")? This would be needed for FR-037 announcements to segments in Phase 2.
- Should the organizer be able to see a guest's RSVP history from the guest row? Proposal: yes, read-only ([06](06-rsvp.md)).
- What exact consent wording is needed for the jurisdictions you're targeting (FRD §16 Privacy)?
