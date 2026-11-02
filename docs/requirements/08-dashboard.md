# Feature: Organizer Dashboard

**Status:** Draft
**Release:** MVP (aggregate report export is Phase 2)
**API:** Not started
**UI:** Not started
**Source:** [FRD §10](../functional-requirements.md#10-dashboards-and-administration) (FR-039, FR-040, FR-044), FR-031, BR-03
**Depends on:** [06 RSVP](06-rsvp.md); can start earlier with partial metrics

> Detail below the FRD's one-line requirements (metric definitions, endpoints) is proposed and needs review before Status moves to Ready.

## Summary

The organizer's home page after signing in. It lists their events with filters and shows, for each one, how many people were invited, how many responded, how many are pending, and how many will actually attend. Attendees are counted separately from accepted invitations, so plus-ones are included.

## User stories

- As an organizer, I land on a list of my events and can filter by status and upcoming or past.
- As an organizer, I can see each event's response numbers at a glance.
- As an organizer, I can tell how many *people* are coming, not just how many invitations said yes.

## Functional requirements

- **FR-039** Event list, filters, and invited/responded/pending metrics.
  - Filters: status (`Draft`, `Published`, `Cancelled`, `Archived`; archived hidden by default), `upcoming` or `past` (by start date in the event's time zone), and title search.
  - Default sort: upcoming events by start date, then drafts, then past events, newest first.
- **FR-040** Count attendees separately from accepted invitations. Metric definitions:
  - **Invited** = guests whose invitation status is anything other than `NotSent` or `Revoked`.
  - **Responded** = invited guests with a current RSVP.
  - **Pending** = invited − responded.
  - **Yes / No / Maybe** = current RSVPs by response.
  - **Accepted invitations** = Yes count.
  - **Attendees** = sum of (1 + plus-ones) over Yes RSVPs (BR-03).
  - **Remaining capacity** = capacity − attendees, or `null` when capacity is unlimited.
- **FR-031** (display part) Metrics reflect the latest RSVPs immediately; no overnight batch.

## Data

No new entities. Metrics are computed from Guest, Invitation and Rsvp. Add indexes on (EventId, Status) and (EventId, Response) if queries are slow (FRD §16: 95th percentile reads under 500 ms).

## API

### `GET /api/dashboard/events` (sign-in)

Query: `status` (repeatable), `when=upcoming|past`, `search`, `page` (20 per page).

- **200 OK**

  ```json
  {
    "items": [
      {
        "id": 12,
        "title": "Maya's 30th Birthday",
        "status": "Published",
        "startsAt": "2026-12-05T19:00:00",
        "timeZone": "America/New_York",
        "metrics": {
          "invited": 45, "responded": 30, "pending": 15,
          "yes": 22, "no": 6, "maybe": 2,
          "acceptedInvitations": 22, "attendees": 31,
          "capacity": 40, "remainingCapacity": 9
        }
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 6
  }
  ```

  Draft events return all metrics as 0.

### `GET /api/events/{id}/summary` (sign-in and ownership)

- **200 OK** — the `metrics` object above, plus delivery counts:

  ```json
  { "...metrics": "...", "delivery": { "notSent": 3, "queued": 0, "sent": 2, "delivered": 40, "bounced": 1, "failed": 0, "revoked": 1 } }
  ```

## UI

### Screen: Dashboard (`/dashboard`), the default page after sign-in

- Header with a "Create event" button.
- Filter bar: status chips, Upcoming / Past toggle, search.
- Event cards or rows: title, date (event time zone), status badge, a small bar of Yes / Maybe / No / Pending, and "31 attending · 9 spots left".
- Empty state for new organizers: "Create your first event", linking to templates.

### Event overview tiles (`/events/:id`, see [02](02-events.md))

- Tiles: Invited, Responded, Pending, Attending (with capacity), and delivery problems ("1 bounced", linking to the guest list filtered by status).

## Acceptance criteria

- [ ] An event with 3 Yes RSVPs bringing 0, 1 and 2 plus-ones shows accepted invitations = 3 and attendees = 6 (FR-040).
- [ ] Maybe RSVPs aren't counted in attendees.
- [ ] A guest added but not yet invited isn't counted in invited or pending.
- [ ] After a guest changes from Yes to No, the dashboard shows the new counts on the next load.
- [ ] Archived events are hidden unless the Archived filter is chosen.
- [ ] The dashboard only lists the signed-in organizer's events.
- [ ] With 50 events of 500 guests each, `GET /api/dashboard/events` responds in under 500 ms locally.

## Later (Phase 2)

- **FR-044** Export aggregate event reports (e.g. CSV/PDF across events, response rates over time).

## Open questions

- Should Maybe count toward "pending" for planning purposes? This file counts it as responded.
- Should the dashboard show anything for organizers with co-host access to others' events (Phase 2)?
