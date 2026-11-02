# Feature: Event Lifecycle

**Status:** Draft
**Release:** MVP (co-hosts are Phase 2)
**API:** Partially built (create/list/get without owners, statuses or most fields)
**UI:** Not started
**Source:** [FRD §4](../functional-requirements.md#4-event-lifecycle), BR-01, BR-02, BR-08, AC-01, AC-07
**Depends on:** [01 Accounts](01-accounts.md)

> Detail below the FRD's one-line requirements (fields, status rules, endpoints, status codes) is proposed and needs review before Status moves to Ready.

## Summary

Organizers create an event as a draft, fill in details at their own pace, then publish it once required fields are valid. Published events can be edited, cancelled and archived; any event can be duplicated as a new draft.

## User stories

- As an organizer, I can save an incomplete event as a draft and come back to it.
- As an organizer, I can publish an event, and I'm told exactly which required fields are missing if I can't.
- As an organizer, I can choose whether my event is in person, virtual or hybrid, and enter the matching location details.
- As an organizer, I can set an RSVP deadline, a capacity limit, whether plus-ones are allowed, and the event's visibility.
- As an organizer, I can edit, cancel, archive or duplicate an event.

## Functional requirements

- **FR-006** Create, edit, duplicate, publish, cancel and archive events.
  - Statuses: `Draft` → `Published` → `Cancelled`; `Draft`, `Published` or `Cancelled` → `Archived`. No other transitions.
  - Editing is allowed in `Draft` and `Published`. A `Cancelled` or `Archived` event is read-only.
  - Editing the date, time or location of a published event is a *material change* and offers to notify guests ([07](07-communication.md), FR-036).
  - Cancelling requires confirmation, stops reminders and sends cancellation notices (BR-08, AC-07); RSVPs close immediately.
  - Duplicate copies details, RSVP settings, design and RSVP questions into a new `Draft` titled "<title> (copy)". Guests, invitations and RSVPs are **not** copied.
- **FR-007** Capture title, description, category, start/end, time zone and location.
  - Start/end are entered as local date-times in the event's time zone (IANA, e.g. `America/New_York`). Deadlines are evaluated in that zone (BR-02).
  - Category comes from the admin-managed list ([09](09-administration.md)).
- **FR-008** Physical, virtual and hybrid formats.
  - `Physical` needs venue name and address. `Virtual` needs a meeting URL. `Hybrid` needs both.
- **FR-009** RSVP deadline, capacity, plus-ones and visibility.
  - Capacity: maximum number of attendees (guests plus their plus-ones, BR-03). Empty means unlimited.
  - Plus-ones: on/off, plus a default maximum per guest (overridable per guest in [04](04-guest-lists.md)).
  - Visibility: `Private` (only invited guests, via their link) or `Public` (anyone with the event's share link). See Open questions.
- **FR-010** Save drafts and validate required fields before publishing.
  - Saving a draft only needs a title.
  - Publishing needs: title, category, start, time zone, format and the location fields for that format. End (if given) must be after start. RSVP deadline (if given) must be before start. Capacity (if given) must be at least 1. The organizer's email must be verified ([01](01-accounts.md)).
  - A failed publish returns every problem at once, not just the first.

## Data

Entity `Event` (extends the current `EventConnectCore/Models/Event.cs`).

| Field | Type | Rules |
|---|---|---|
| Id | int | key |
| OrganizerId | int | FK → User, required (BR-01) |
| Status | enum | `Draft`, `Published`, `Cancelled`, `Archived` |
| Title | string | required, max 200 |
| Description | string? | max 5,000 |
| CategoryId | int? | FK → Category; required to publish |
| Format | enum? | `Physical`, `Virtual`, `Hybrid`; required to publish |
| StartsAt | DateTime? | local date-time in `TimeZone`; required to publish |
| EndsAt | DateTime? | after `StartsAt` |
| TimeZone | string? | IANA zone; required to publish; defaults to organizer's profile zone |
| VenueName | string? | max 200 |
| Address | string? | max 500 |
| VirtualUrl | string? | absolute https URL |
| RsvpDeadline | DateTime? | local, in `TimeZone`; before `StartsAt` |
| Capacity | int? | ≥ 1; null = unlimited |
| AllowPlusOnes | bool | default false |
| DefaultMaxPlusOnes | int | 0–10, default 1 |
| Visibility | enum | `Private` (default), `Public` |
| PublishedAt / CancelledAt / ArchivedAt | DateTime? (UTC) | set on transition |
| CreatedAt / UpdatedAt | DateTime (UTC) | set by server |

## API

All endpoints require sign-in and only work on the caller's own events (otherwise **404**, FR-004). Try them at http://localhost:5207/scalar while the API is running.

### Event object (returned by every endpoint below)

```json
{
  "id": 12,
  "status": "Draft",
  "title": "Maya's 30th Birthday",
  "description": "Dinner and drinks on the rooftop.",
  "categoryId": 3,
  "format": "Physical",
  "startsAt": "2026-12-05T19:00:00",
  "endsAt": "2026-12-05T23:00:00",
  "timeZone": "America/New_York",
  "venueName": "Skyline Rooftop",
  "address": "123 Main St, New York, NY 10001",
  "virtualUrl": null,
  "rsvpDeadline": "2026-11-28T23:59:00",
  "capacity": 40,
  "allowPlusOnes": true,
  "defaultMaxPlusOnes": 1,
  "visibility": "Private",
  "publishedAt": null,
  "createdAt": "2026-11-02T15:04:00Z",
  "updatedAt": "2026-11-02T15:04:00Z"
}
```

### `GET /api/events` — my events

Query: `status` (optional), `when=upcoming|past` (optional), `search` (title contains).

- **200 OK** — array of event objects, ordered by `startsAt` (drafts without a start date last)

### `GET /api/events/{id}`

- **200 OK** — event object
- **404** — not found or not yours

### `POST /api/events` — create a draft

```json
{ "title": "Maya's 30th Birthday" }
```

Any other event field may also be sent.

- **201 Created** — event object with `"status": "Draft"`
- **400** — title missing or a field fails its format rule (e.g. bad URL, unknown time zone)

### `PUT /api/events/{id}` — edit

Body: the full set of editable fields (same names as the event object, without `id`, `status` or timestamps), plus for published events:

```json
{ "...": "...", "notifyGuests": true, "changeMessage": "We moved to 8pm, see you there!" }
```

- **200 OK** — updated event. When a published event's date, time or location changed and `notifyGuests` is true, change notices are queued ([07](07-communication.md)).
- **400** — validation. A published event must still pass all publish rules.
- **409 Conflict** — event is `Cancelled` or `Archived`

### `POST /api/events/{id}/publish`

- **200 OK** — event with `"status": "Published"`
- **400** — not publishable; lists every problem:

  ```json
  {
    "title": "Event is not ready to publish.",
    "status": 400,
    "errors": {
      "CategoryId": ["Choose a category."],
      "Address": ["An address is required for in-person events."],
      "EndsAt": ["End must be after start."]
    }
  }
  ```

- **403 Forbidden** — organizer's email not verified
- **409** — event is not a `Draft`

### `POST /api/events/{id}/cancel`

```json
{ "message": "Sorry, we have to call this off because of the storm." }
```

- **200 OK** — event with `"status": "Cancelled"`; cancellation notices queued
- **409** — event is not `Published`

### `POST /api/events/{id}/archive`

- **200 OK** — event with `"status": "Archived"`
- **409** — already archived

### `POST /api/events/{id}/duplicate`

- **201 Created** — new draft event object; `Location` header points to it

## UI

### Screen: Event editor (`/events/new`, `/events/:id/edit`)

- Sections: Basics (title, description, category), When (start, end, time zone), Where (format choice shows the matching fields), RSVP settings (deadline, capacity, plus-ones, visibility).
- "Save draft" is always available. "Publish" shows a checklist of missing or invalid fields next to the relevant inputs.
- When saving date, time or location changes to a published event, a dialog asks: "Notify N guests about this change?" with an optional message.

### Screen: Event overview (`/events/:id`)

- Status badge, key details, and actions that match the status: Edit, Publish, Cancel (with confirmation and message), Archive, Duplicate.
- Tabs that link to Design ([03](03-invitation-design.md)), Guests ([04](04-guest-lists.md)), Responses ([06](06-rsvp.md)) and Messages ([07](07-communication.md)).
- The event list itself lives on the dashboard ([08](08-dashboard.md)).

## Acceptance criteria

- [ ] **AC-01** An organizer can save an event with only a title as a Draft; publishing it returns 400 listing every missing required field.
- [ ] A Physical event without an address cannot be published; a Virtual one without a meeting URL cannot be published.
- [ ] Publishing with an end before the start, or an RSVP deadline after the start, returns 400.
- [ ] An unverified organizer gets 403 when publishing.
- [ ] **AC-07** (event part) Cancelling a published event sets it to Cancelled, and later edits return 409. RSVP blocking and notices are tested in [06](06-rsvp.md) and [07](07-communication.md).
- [ ] Duplicating an event creates a new Draft with the same details and no guests.
- [ ] Another organizer's event returns 404 on every endpoint.
- [ ] Changing the time of a published event with `notifyGuests: true` queues one change notice per affected guest.

## Changes to the current implementation

The existing Events API (list, get, create; no owner, no status) was the scaffold example. Building this feature means:

- Adding the new columns with a migration. Existing rows can be deleted, since it's development data only.
- Requiring sign-in and filtering by owner.
- Changing `POST` to create drafts.
- Updating `EventConnectAPI.http` and the view models to the new shape.

## Later (Phase 2)

- **FR-011** Co-hosts: invite another user to help manage an event, with permissions the owner grants (BR-01). Proposed endpoints: `GET/POST/DELETE /api/events/{id}/cohosts`.

## Open questions

- **FRD decision:** are public events discoverable (searchable listing) or only reachable by URL? Proposal: only by URL for MVP.
- Can a Draft be deleted outright, or only archived?
- Can a Cancelled event be un-cancelled (re-published)?
- Should archiving happen automatically some time after the event ends?
- Is changing the RSVP deadline or capacity of a published event a material change that notifies guests? Proposal: no.
