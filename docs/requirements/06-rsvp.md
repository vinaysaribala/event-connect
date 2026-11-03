# Feature: RSVP and Attendance

**Status:** Draft
**Release:** MVP (waitlist and organizer overrides are Phase 2)
**API:** Not started
**UI:** Not started
**Source:** [FRD §8](../functional-requirements.md#8-rsvp-and-attendance), FR-005, BR-02 to BR-07, AC-02, AC-03, AC-04, AC-06, AC-07
**Depends on:** [05 Invitation Delivery](05-invitation-delivery.md)

> Detail below the FRD's one-line requirements (question types, error codes, endpoints) is proposed and needs review before Status moves to Ready.

## Summary

A guest opens their personal link without signing in, sees the invitation, and answers Yes, No or Maybe, with plus-ones, a comment, and any custom questions the organizer added. They can change their answer until the deadline. Each change is checked against the deadline, eligibility and capacity, applied atomically, kept in history, and confirmed by email.

## User stories

- As a guest, I can open my invitation from the email and RSVP without creating an account.
- As a guest, I can say how many plus-ones I'm bringing and answer the organizer's questions.
- As a guest, I can change my RSVP later, until the deadline.
- As a guest, I get a clear message if RSVPs are closed or the event is full.
- As an organizer, I can add custom questions (for example meal choice or dietary needs).
- As an organizer, I can see every response, and each guest's history of changes.

## Functional requirements

- **FR-028** Yes/No/Maybe and later changes.
  - The latest valid RSVP is the current status; every submission is kept in history (BR-05).
- **FR-029** Plus-one count, comment and custom answers.
  - Plus-ones: only with `Yes`, from 0 up to the guest's allowance ([04](04-guest-lists.md)), and only if the event allows plus-ones.
  - Comment: optional, max 500 characters.
  - Custom questions: types `ShortText` (max 200), `LongText` (max 1,000), `SingleChoice`, `MultiChoice`. Each can be marked required, and required questions only apply to `Yes` answers. Max 10 questions per event.
  - Questions can't be deleted once answered; they can be hidden instead.
  - Duplicating an event ([02](02-events.md)) copies its questions, including hidden ones, with no answers.
- **FR-030** Validate token, deadline, eligibility and capacity. Checks run in this order and the first failure is returned:
  1. The token exists and isn't revoked; otherwise 404 "This invitation link is no longer valid" (AC-06).
  2. The event isn't `Cancelled` or `Archived` (AC-07).
  3. The RSVP deadline, evaluated in the event's time zone, hasn't passed (BR-02, AC-04).
  4. Capacity: if the new answer needs more seats than are left, it's rejected. A `Yes` needs 1 + plus-ones seats; `No` and `Maybe` need none (BR-03, BR-06). Changing from `Yes` to anything else frees seats immediately.
  - The capacity check and the save happen in one transaction, so concurrent RSVPs can't overbook (BR-04, AC-03).
- **FR-031** Update organizer counts and guest response history.
  - Counts (invited, responded, pending, Yes, No, Maybe, attendees) update immediately, and are shown on the dashboard ([08](08-dashboard.md)).
- **FR-032** Confirmation after a successful RSVP.
  - An email to the guest summarising their answer, with a link back to change it. Sent for the first RSVP and for every change.
  - Confirmation emails respect unsubscribe preferences, depending on an open question in [07](07-communication.md).
- **Reopening (AC-04):** an organizer "reopens" RSVPs by moving the deadline later, or clearing it, in the event editor ([02](02-events.md)).

## Data

| Entity | Field | Type | Rules |
|---|---|---|---|
| Rsvp | Id | int | key |
| | InvitationId | int | FK, unique (one current RSVP per invitation) |
| | Response | enum | `Yes`, `No`, `Maybe` |
| | PlusOnes | int | 0 unless `Yes` |
| | Comment | string? | max 500 |
| | RespondedAt | DateTime (UTC) | |
| RsvpHistory | Id, InvitationId, Response, PlusOnes, Comment, AnswersSnapshot (JSON), Source (`Guest`), CreatedAt | | append-only |
| RsvpQuestion | Id, EventId, Text, Type, Options (JSON), IsRequired, IsHidden, SortOrder | | max 10 per event |
| RsvpAnswer | Id, RsvpId, QuestionId, Value (JSON) | | unique (RsvpId, QuestionId) |
| Event | ReservedSeats | int | maintained in the same transaction as RSVP changes (BR-04) |

## API

### Guest endpoints (no sign-in; the token in the URL is the credential)

#### `GET /api/invitations/{token}` — what the guest sees

- **200 OK**

  ```json
  {
    "guestName": "Sam Carter",
    "event": {
      "title": "Maya's 30th Birthday",
      "status": "Published",
      "format": "Physical",
      "startsAt": "2026-12-05T19:00:00",
      "endsAt": "2026-12-05T23:00:00",
      "timeZone": "America/New_York",
      "venueName": "Skyline Rooftop",
      "address": "123 Main St, New York, NY 10001",
      "virtualUrl": null,
      "rsvpDeadline": "2026-11-28T23:59:00"
    },
    "design": { "versionNumber": 2, "templateId": 4, "customization": { "...": "..." } },
    "rsvpOpen": true,
    "closedReason": null,
    "maxPlusOnes": 2,
    "questions": [
      { "id": 9, "text": "Meal choice", "type": "SingleChoice", "options": ["Chicken", "Fish", "Vegetarian"], "isRequired": true },
      { "id": 10, "text": "Any dietary needs?", "type": "ShortText", "options": null, "isRequired": false }
    ],
    "currentRsvp": null
  }
  ```

  `closedReason` is one of `DeadlinePassed`, `EventCancelled`, `CapacityFull` (only when the guest has no seats yet), or `null`. The guest list and other guests' comments are never included (BR-07, BR-09).
- **404** — `{ "title": "This invitation link is no longer valid." }` for an unknown or revoked token

#### `PUT /api/invitations/{token}/rsvp` — submit or change

```json
{
  "response": "Yes",
  "plusOnes": 1,
  "comment": "Can't wait!",
  "answers": [
    { "questionId": 9, "value": "Vegetarian" },
    { "questionId": 10, "value": "No nuts please" }
  ]
}
```

- **200 OK** — the saved RSVP:

  ```json
  { "response": "Yes", "plusOnes": 1, "comment": "Can't wait!", "answers": [ … ], "respondedAt": "2026-11-10T14:22:00Z" }
  ```

- **400** — validation, e.g. `{ "errors": { "plusOnes": ["You can bring up to 2 guests."], "answers[9]": ["Meal choice is required."] } }`
- **404** — token unknown or revoked
- **409** — closed, with a machine-readable code:

  ```json
  { "title": "RSVPs for this event are closed.", "status": 409, "code": "DeadlinePassed" }
  ```

  Codes: `DeadlinePassed`, `EventCancelled`, `CapacityFull` (message: "Sorry, this event is full.").

Guest endpoints are rate-limited per token and per IP address (FRD §16 Security).

### Organizer endpoints (sign-in and ownership)

#### `GET /api/events/{id}/rsvp-questions` and `PUT /api/events/{id}/rsvp-questions`

PUT body is the full ordered list. Questions with an `id` are updated; questions without one are created.

```json
[
  { "id": 9, "text": "Meal choice", "type": "SingleChoice", "options": ["Chicken", "Fish", "Vegetarian"], "isRequired": true, "isHidden": false },
  { "text": "Song request?", "type": "ShortText", "isRequired": false, "isHidden": false }
]
```

- **200 OK** — saved list
- **400** — more than 10 questions, choice question without options, etc.
- **409** — tried to remove an answered question (hide it instead)

#### `GET /api/events/{id}/rsvps`

Query: `response` (`Yes`, `No`, `Maybe`).

- **200 OK** — `[{ "guestId": 31, "guestName": "Sam Carter", "response": "Yes", "plusOnes": 1, "comment": "…", "answers": [ … ], "respondedAt": "…" }]`

#### `GET /api/events/{id}/guests/{guestId}/rsvp-history`

- **200 OK** — every submission, newest first

## UI

### Screen: Guest invitation (`/invite/:token`), public and mobile-first

- The invitation design (current version), then the event details: date/time shown in the event's time zone, a map link for physical events, and a join link for virtual events (shown only to guests who answered Yes or Maybe).
- RSVP form:
  - Yes / No / Maybe buttons. A plus-ones selector appears for Yes.
  - Custom questions appear (required ones are marked).
  - A comment field and a Submit button.
- After submitting: a success state with a summary of the answer, a "Change my RSVP" button, and "Check your email for a confirmation".
- When closed: a clear message for the reason ("RSVPs closed on Nov 28", "This event has been cancelled", "This event is full") and no form.
- An invalid or revoked link shows a friendly "This invitation link is no longer valid — contact the organizer."
- Must meet WCAG 2.2 AA (FRD §16).

### Screen: Responses (`/events/:id/responses`)

- Count tiles (Yes / No / Maybe / no response / attendees) and a table of responses with answers. Clicking a row shows the guest's history.

### Panel: RSVP questions (in the event editor's RSVP settings, [02](02-events.md))

- Add, reorder, edit or hide questions; choice questions have an options editor.

## Acceptance criteria

- [ ] **AC-02** A guest with a valid link can RSVP without an account, and the organizer's counts reflect it straight away.
- [ ] Changing Yes (+1) to No frees 2 seats, keeps both entries in history, and sends a confirmation each time.
- [ ] **AC-03** With 1 seat left, two simultaneous Yes RSVPs result in exactly one success and one 409 `CapacityFull`.
- [ ] A Yes with plus-ones above the guest's allowance returns 400.
- [ ] Maybe RSVPs don't use capacity (BR-06).
- [ ] **AC-04** An RSVP after the deadline (in the event's time zone) returns 409 `DeadlinePassed`. After the organizer moves the deadline later, it succeeds.
- [ ] **AC-06** A revoked link returns 404 for both viewing and submitting.
- [ ] **AC-07** (RSVP part) After the event is cancelled, submitting returns 409 `EventCancelled`.
- [ ] A required question left unanswered on a Yes returns 400; on a No it's accepted.
- [ ] The guest response never contains other guests' names or comments.

## Later (Phase 2)

- **FR-033** Waitlist when capacity is reached.
- **FR-034** Organizer overrides with audit trail (e.g. marking a guest as attending). Would add `Source = Organizer` history entries and audit events ([09](09-administration.md)).

## Open questions

- **FRD decision:** should guests see the guest list or other attendees' comments? This file assumes no.
- **FRD decision:** can an organizer override capacity, and who is notified?
- BR-06 says Maybe doesn't reserve capacity "by default". Should that be a per-event setting?
- Can a guest change from Yes to No after the deadline (freeing a seat), even though new RSVPs are closed? Proposal: yes, declining is always allowed.
- Should the guest be able to give names for their plus-ones?
- Should confirmation emails include a calendar file (.ics, FRD §15)? Proposal: yes for Yes answers.
