# Feature: Event Lifecycle

**Status:** Done
**Release:** MVP (co-hosts are Phase 2)
**API:** Done
**UI:** Done
**Source:** [FRD §4](../functional-requirements.md#4-event-lifecycle), BR-01, BR-02, BR-08, AC-01, AC-07
**Depends on:** [01 Accounts](01-accounts.md)

> Reviewed and marked Ready on 2026-11-03. The detail added beyond the FRD (fields, status rules, endpoints, status codes) is accepted with the changes recorded under Decisions.
>
> Implemented on 2026-11-03. Every acceptance criterion below is verified by automated tests. Sending change and cancellation notices waits for [07](07-communication.md). What the build adds is recorded under Decisions → Implementation notes.

## Summary

Organizers create an event as a draft, fill in details at their own pace, then publish it once required fields are valid. Published events can be edited, cancelled and archived; any event can be duplicated as a new draft, and a draft can be deleted.

## User stories

- As an organizer, I can save an incomplete event as a draft and come back to it, or delete it.
- As an organizer, I can publish an event, and I'm told exactly which required fields are missing if I can't.
- As an organizer, I can choose whether my event is in person, virtual or hybrid, and enter the matching location details.
- As an organizer, I can set an RSVP deadline, a capacity limit, whether plus-ones are allowed, and the event's visibility.
- As an organizer, I can edit, cancel, archive or duplicate an event.

## Functional requirements

- **FR-006** Create, edit, duplicate, publish, cancel, archive and delete events.
  - Statuses: `Draft` → `Published` → `Cancelled`; `Draft`, `Published` or `Cancelled` → `Archived`. No other transitions. A cancelled event can't be re-published; duplicate it instead.
  - Archiving is manual only.
  - Only a `Draft` can be deleted, and the delete is permanent. Events in any other status are archived instead.
  - Editing is allowed in `Draft` and `Published`. A `Cancelled` or `Archived` event is read-only. A published event can still be edited after it has started or ended.
  - Editing a *material change* on a published event offers to notify guests ([07](07-communication.md), FR-036). Material change = `StartsAt`, `EndsAt`, `TimeZone`, `Format`, `VenueName`, `Address` or `VirtualUrl`. Changes to the RSVP deadline, capacity, plus-ones or visibility are not material and notify no one.
  - Cancelling requires confirmation, stops reminders and sends cancellation notices (BR-08, AC-07); RSVPs close immediately.
  - Duplicate copies details and RSVP settings into a new `Draft` titled "<title> (copy)". Guests, invitations and RSVPs are **not** copied. The design and RSVP questions are copied too once [03](03-invitation-design.md) and [06](06-rsvp.md) exist; each of those features adds that step.
- **FR-007** Capture title, description, category, start/end, time zone and location.
  - Start/end are entered as local date-times in the event's time zone (IANA, e.g. `America/New_York`). Deadlines are evaluated in that zone (BR-02).
  - The server also stores the UTC equivalent of `StartsAt` and `RsvpDeadline`, recomputed on every save, for filtering, sorting and deadline checks. In a DST gap (a local time that doesn't exist) the time moves forward by the gap length. In a DST overlap (a local time that happens twice), the earlier of the two is used.
  - Category comes from the shared category list (see Data). Events and templates ([03](03-invitation-design.md)) use the same list. Admins manage it in [09](09-administration.md).
- **FR-008** Physical, virtual and hybrid formats.
  - `Physical` needs venue name and address. `Virtual` needs a meeting URL. `Hybrid` needs both.
- **FR-009** RSVP deadline, capacity, plus-ones and visibility.
  - Capacity: maximum number of attendees (guests plus their plus-ones, BR-03). Empty means unlimited.
  - Plus-ones: on/off, plus a default maximum per guest (overridable per guest in [04](04-guest-lists.md)).
  - Visibility: `Private` (only invited guests, via their link) or `Public` (anyone with the event's share link). Public events are reachable only by URL; there is no searchable listing. In this feature Visibility is only stored. The share link that uses it is built in [05](05-invitation-delivery.md).
- **FR-010** Save drafts and validate required fields before publishing.
  - Saving a draft needs a title. Every field sent must also pass its own format rule (length, URL, known time zone, existing active category, number ranges). Rules that compare fields (end after start, deadline before start) are not checked on drafts.
  - Publishing needs: title, category, start, time zone, format and the location fields for that format. Start must be in the future. End (if given) must be after start. RSVP deadline (if given) must be before start. Capacity (if given) must be at least 1. The organizer's email must be verified ([01](01-accounts.md)).
  - Editing a published event re-checks every publish rule except "start must be in the future".
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
| CategoryId | int? | FK → Category; must be active when set; required to publish |
| Format | enum? | `Physical`, `Virtual`, `Hybrid`; required to publish |
| StartsAt | DateTime? | local date-time in `TimeZone`; required to publish; in the future to publish |
| StartsAtUtc | DateTime? | server-computed from `StartsAt` + `TimeZone`; not in the API |
| EndsAt | DateTime? | local; after `StartsAt` |
| TimeZone | string? | IANA zone; required to publish; defaults to organizer's profile zone |
| VenueName | string? | max 200 |
| Address | string? | max 500 |
| VirtualUrl | string? | absolute https URL, max 2,000 |
| RsvpDeadline | DateTime? | local, in `TimeZone`; before `StartsAt` |
| RsvpDeadlineUtc | DateTime? | server-computed; not in the API; used by [06](06-rsvp.md) |
| Capacity | int? | ≥ 1; null = unlimited |
| AllowPlusOnes | bool | default false |
| DefaultMaxPlusOnes | int | 0–10, default 1 |
| Visibility | enum | `Private` (default), `Public` |
| PublishedAt / CancelledAt / ArchivedAt | DateTime? (UTC) | set on transition |
| CreatedAt / UpdatedAt | DateTime (UTC) | set by server |

Entity `Category` (new, shared with templates in [03](03-invitation-design.md); admin management in [09](09-administration.md)).

| Field | Type | Rules |
|---|---|---|
| Id | int | key |
| Name | string | required, max 100 |
| Slug | string | required, unique, max 100 |
| SortOrder | int | |
| IsActive | bool | default true; inactive categories can't be chosen for new or edited events, but events that already use one keep it |

Seeded by the migration: Birthday, Wedding, Baby Shower, Holiday, Dinner Party, Business, Other.

## API

All event endpoints require sign-in and only work on the caller's own events (otherwise **404**, FR-004). Try them at http://localhost:5207/scalar while the API is running.

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
  "cancelledAt": null,
  "archivedAt": null,
  "createdAt": "2026-11-02T15:04:00Z",
  "updatedAt": "2026-11-02T15:04:00Z"
}
```

### `GET /api/categories` (no sign-in)

Active categories, ordered by `SortOrder`.

- **200 OK** — `[{ "id": 3, "name": "Birthday", "slug": "birthday" }]`

### `GET /api/events` — my events

Query: `status` (optional), `when=upcoming|past` (optional; compares `StartsAtUtc` with now), `search` (title contains).

- Without `status`, `Archived` events are left out. `status=Archived` returns only archived events.
- **200 OK** — array of event objects, ordered by start time (`StartsAtUtc`), with drafts that have no start date last

### `GET /api/events/{id}`

- **200 OK** — event object
- **404** — not found or not yours

### `POST /api/events` — create a draft

```json
{ "title": "Maya's 30th Birthday" }
```

Any other event field may also be sent. If `timeZone` is omitted, the organizer's profile time zone is used.

- **201 Created** — event object with `"status": "Draft"`; `Location` header points to it
- **400** — title missing or a field fails its format rule (e.g. bad URL, unknown time zone, unknown or inactive category)

### `PUT /api/events/{id}` — edit

Body: the full set of editable fields (same names as the event object, without `id`, `status` or timestamps). Omitted fields are cleared. For published events, the body can also include:

```json
{ "...": "...", "notifyGuests": true, "changeMessage": "We moved to 8pm, see you there!" }
```

`changeMessage` is optional, max 1,000 characters.

- **200 OK** — updated event. When a published event has a material change and `notifyGuests` is true, change notices are queued ([07](07-communication.md)). Until 07 is built, both fields are accepted and validated but nothing is sent.
- **400** — validation. A draft is checked against format rules; a published event against all publish rules except "start in the future".
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

The body and `message` are optional; `message` is max 1,000 characters.

- **200 OK** — event with `"status": "Cancelled"`; cancellation notices queued ([07](07-communication.md), not sent until 07 is built)
- **400** — message too long
- **409** — event is not `Published`

### `POST /api/events/{id}/archive`

- **200 OK** — event with `"status": "Archived"`
- **409** — already archived

### `POST /api/events/{id}/duplicate`

Works on an event in any status.

- **201 Created** — new draft event object; `Location` header points to it

### `DELETE /api/events/{id}`

- **204 No Content** — draft permanently deleted
- **404** — not found or not yours
- **409** — event is not a `Draft`

## UI

### Screen: Event editor (`/events/new`, `/events/:id/edit`)

- Sections: Basics (title, description, category), When (start, end, time zone), Where (format choice shows the matching fields), RSVP settings (deadline, capacity, plus-ones, visibility).
- Time zone defaults to the organizer's profile zone.
- "Save draft" is always available. "Publish" shows a checklist of missing or invalid fields next to the relevant inputs.
- When saving a material change to a published event, a dialog asks "Notify guests about this change?" with an optional message. The dialog shows the number of guests once [04](04-guest-lists.md) exists.

### Screen: Event overview (`/events/:id`)

- Status badge, key details, and actions that match the status: Edit, Publish, Cancel (with confirmation and optional message), Archive, Duplicate, Delete (drafts only, with confirmation).
- Tabs that link to Design ([03](03-invitation-design.md)), Guests ([04](04-guest-lists.md)), Responses ([06](06-rsvp.md)) and Messages ([07](07-communication.md)). Each tab is added with its feature.
- The event list itself lives on the dashboard ([08](08-dashboard.md)).

## Acceptance criteria

- [x] **AC-01** An organizer can save an event with only a title as a Draft; publishing it returns 400 listing every missing required field.
- [x] A Physical event without an address cannot be published; a Virtual one without a meeting URL cannot be published.
- [x] Publishing with an end before the start, an RSVP deadline after the start, or a start in the past returns 400.
- [x] A draft with an end before its start can be saved; the problem is reported only on publish.
- [x] An unverified organizer gets 403 when publishing.
- [x] **AC-07** (event part) Cancelling a published event sets it to Cancelled, and later edits return 409. RSVP blocking and notices are tested in [06](06-rsvp.md) and [07](07-communication.md).
- [x] A Cancelled event can't be published again (409).
- [x] Duplicating an event creates a new Draft with the same details and no guests.
- [x] A Draft can be deleted (204); deleting a Published event returns 409.
- [x] Archived events are left out of `GET /api/events` unless `status=Archived` is asked for.
- [x] `when=upcoming` and the sort order use the event's own time zone. An event at 19:00 in Tokyo sorts before one at 19:00 in New York on the same day.
- [x] Another organizer's event returns 404 on every endpoint.
- [x] `GET /api/categories` returns the seeded active categories without sign-in.

The change-notice criterion ("changing the time of a published event with notify on queues a notice per affected guest") is verified in [07](07-communication.md).

Automated tests (2026-11-03): `API/EventConnect.Tests/Services/EventServiceTests.cs` (rules, statuses, ownership, time zones and DST), `API/EventConnect.Tests/Api/EventsApiTests.cs` (routes, status codes, body shape), and `UI/src/pages/Event*Page.test.tsx` and `DashboardPage.test.tsx` (editor, overview, list). The lifecycle was also checked by hand against SQL Server: create, list with filters, publish, cancel, a 409 on editing a cancelled event, duplicate, archive and delete.

## Changes to the current implementation

The existing Events API (list, get, create; no owner, no status) was the scaffold example. Building this feature means:

- Adding the new columns and the `Category` table (with seed rows) in a migration. Existing event rows can be deleted, since it's development data only.
- Requiring sign-in and filtering by owner.
- Changing `POST` to create drafts.
- Updating `EventConnectAPI.http` and the view models to the new shape.

## Later (Phase 2)

- **FR-011** Co-hosts: invite another user to help manage an event, with permissions the owner grants (BR-01). Proposed endpoints: `GET/POST/DELETE /api/events/{id}/cohosts`.

## Decisions

### Review decisions (decided 2026-11-03)

- **Public events:** reachable only by URL for MVP; no searchable listing (FRD §18 decision). Visibility is stored here and used by the share link in [05](05-invitation-delivery.md).
- **Deleting drafts:** drafts can be deleted permanently (`DELETE /api/events/{id}`). Other statuses are archived instead, because they may have guests, invitations and history.
- **No un-cancel:** guests have already been told the event is off. To bring it back, the organizer duplicates it.
- **Manual archiving only:** auto-archiving would need a background job, and `when=past` already separates old events. It can be revisited later.
- **Deadline, capacity, plus-ones and visibility changes are not material:** they don't notify guests. The list of material fields matches [07](07-communication.md) FR-036.
- **One shared category list:** events and templates use the same `Category` entity and `GET /api/categories`. It's built here because publishing needs it. [03](03-invitation-design.md) and [09](09-administration.md) use the same list.
- **UTC copies of local times:** `StartsAtUtc` and `RsvpDeadlineUtc` are stored so that filtering, sorting across time zones and deadline checks (BR-02, [06](06-rsvp.md)) don't convert on every query. The local value and time zone remain the source of truth.
- **Validation tiers:** drafts get only per-field format checks, so organizers can save half-finished work. Rules that compare fields run at publish and on edits to published events. "Start in the future" applies only at publish, so a published event can still be corrected after it happens.
- **Features not built yet:** `notifyGuests`, `changeMessage` and the cancel `message` are accepted and validated now. Sending is wired up in [07](07-communication.md). Copying the design and the RSVP questions on duplicate is added by [03](03-invitation-design.md) and [06](06-rsvp.md).
- **Limits:** cancel and change messages are max 1,000 characters; `VirtualUrl` is max 2,000.

### Implementation notes (2026-11-03)

Added or decided while building; none of these change the samples above.

- **Local date-times carry no offset.** `startsAt`, `endsAt` and `rsvpDeadline` must be sent as plain local times (`2026-12-05T19:00:00`). A value with `Z` or an offset returns 400 on that field rather than being converted silently. `createdAt`, `updatedAt` and the status timestamps are UTC and end in `Z`.
- **DST.** A time in a DST gap is stored as the moved-forward time (02:30 on spring-forward night in New York becomes 03:30), so the event shows what will actually happen. `EndsAt` is adjusted the same way. A local time saved without a time zone has no UTC copy and sorts with the undated drafts.
- **Validation messages** use the field names from the sample (`CategoryId`, `Address`, `EndsAt`, …) as PascalCase keys. A publish failure has the title "Event is not ready to publish."; a failed edit of a published event uses the standard validation title. The 403 for an unverified organizer and every 409 carry their reason in `title`.
- **Check order** for publish: not yours (404), not a draft (409), unverified (403), then the field rules (400).
- **`Location` headers** for create and duplicate are relative (`/api/events/12`), as for register.
- **Clearing a time zone** (sending `""` or omitting it on edit) is allowed on a draft; publishing then reports `TimeZone`. Create still fills in the profile zone when `timeZone` is omitted.
- **UI.**
  - Until [08](08-dashboard.md) is built, the dashboard shows a plain list of the organizer's events (title, status, start) with a "New event" button. The menu's "Events" item is now "New event".
  - The editor saves before it publishes, so a draft is created even when publishing fails. Its problems are then listed in a checklist and under each field.
  - Saving a draft goes to the event's overview.
  - The notify dialog doesn't show a guest count yet; that waits for [04](04-guest-lists.md).
  - The overview has no Design/Guests/Responses/Messages tabs yet; each one is added with its feature.

## Open questions

None.
