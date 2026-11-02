# Feature: Events

**Status:** In progress
**API:** Done (list, get, create)
**UI:** Not started

## Summary

Hosts create events (a title, time and place) that invitations and RSVPs will later hang off. This is the reference feature for how the layers fit together.

## User stories

- As a host, I can create an event with a title, description, location and start/end time, so that I can invite people to it.
- As a host, I can see a list of events in date order, so that I know what is coming up.
- As a host, I can open one event to see its details.

## Functional requirements

- **FR-1:** Title is required, at most 200 characters.
- **FR-2:** Location is optional, at most 300 characters. Description is optional with no length limit.
- **FR-3:** Start time is required; end time is optional.
- **FR-4:** The list of events is ordered by start time, earliest first.
- **FR-5:** The server records when each event was created (UTC). This is stored but not returned by the API.

## Data

Entity `Event` in `EventConnectCore/Models/Event.cs`, table `Events`.

| Field | Type | Rules |
|---|---|---|
| Id | int | key, generated |
| Title | string | required, max 200 |
| Description | string? | optional |
| Location | string? | optional, max 300 |
| StartsAt | DateTime | required |
| EndsAt | DateTime? | optional |
| CreatedAt | DateTime | set by server (UTC), not exposed |

## API

### `GET /api/events` — list events

Responses:

- **200 OK** — array ordered by `startsAt` (empty array when there are none)

  ```json
  [
    {
      "id": 1,
      "title": "Launch party",
      "description": "EventConnect kickoff",
      "location": "Main hall",
      "startsAt": "2026-12-01T18:00:00",
      "endsAt": "2026-12-01T21:00:00"
    }
  ]
  ```

### `GET /api/events/{id}` — get one event

Responses:

- **200 OK** — same object shape as one item in the list above
- **404 Not Found** — no event with that id

### `POST /api/events` — create an event

Request:

```json
{
  "title": "Launch party",
  "description": "EventConnect kickoff",
  "location": "Main hall",
  "startsAt": "2026-12-01T18:00:00",
  "endsAt": "2026-12-01T21:00:00"
}
```

Responses:

- **201 Created** — the created event; `Location` header points to `GET /api/events/{id}`

  ```json
  {
    "id": 1,
    "title": "Launch party",
    "description": "EventConnect kickoff",
    "location": "Main hall",
    "startsAt": "2026-12-01T18:00:00",
    "endsAt": "2026-12-01T21:00:00"
  }
  ```

- **400 Bad Request** — validation failed (FR-1, FR-2), returned as a problem-details body:

  ```json
  {
    "title": "One or more validation errors occurred.",
    "status": 400,
    "errors": { "Title": ["The Title field is required."] }
  }
  ```

## UI

Not built yet. Proposed screens:

### Screen: Event list (`/events`)

- Table or cards of events in start-time order: title, date/time, location.
- "New event" button goes to the create screen.
- Clicking an event opens its detail screen.
- Empty state: "No events yet" with the "New event" button.

### Screen: Create event (`/events/new`)

- Fields: title, description, location, start date/time, end date/time.
- Title and start time are required; show field errors from the API's 400 response next to the fields.
- On success, go to the new event's detail screen.

### Screen: Event detail (`/events/:id`)

- Shows all fields.
- Unknown id shows "Event not found" with a link back to the list.

## Acceptance criteria

API (verified 2026-11-02 against local SQL Server):

- [x] Creating an event with title, location and start time returns 201 with the new id.
- [x] The created event is returned by `GET /api/events/{id}` and appears in `GET /api/events`.
- [x] `GET /api/events/999` (unknown id) returns 404.
- [x] Creating an event without a title returns 400 with an error for `Title`.

UI:

- [ ] Creating an event from `/events/new` shows it on `/events` in date order.
- [ ] Submitting without a title shows the error next to the title field.
- [ ] Opening `/events/999` shows "Event not found".

## Out of scope

- Editing and deleting events.
- Invitations and RSVPs (separate features).
- Authentication: there are no users or hosts yet, so every event is visible to everyone.

## Open questions

- Should the API reject an end time earlier than the start time? Currently it is accepted.
- Time zones: `startsAt`/`endsAt` are stored exactly as sent, with no zone. Decide whether to store UTC plus the event's time zone.
- Should past events be hidden from the list, or shown separately?
