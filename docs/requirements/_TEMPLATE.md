# Feature: <Name>

**Status:** Draft <!-- Draft | Ready | In progress | Done -->
**API:** Not started <!-- Not started | In progress | Done -->
**UI:** Not started <!-- Not started | In progress | Done -->

<!--
How to use:
1. Copy this file to NN-feature-name.md and add a row to README.md.
2. Fill in what you know. Bullets are fine. Write TBD for anything undecided.
3. Set Status to Ready, then ask Claude: "Implement docs/requirements/NN-feature-name.md".
Describe behaviour, not code. Delete these comments when done.
-->

## Summary

One or two sentences: what this feature does and who it is for.

## User stories

- As a <role>, I can <action>, so that <benefit>.

## Functional requirements

- **FR-1:** <rule the system must follow>
- **FR-2:**

## Data

New or changed entities (become models in `EventConnectCore`).

| Field | Type | Rules |
|---|---|---|
| Id | int | key |
|  |  |  |

## API

One subsection per endpoint. Sample bodies become the view models and go into `EventConnectAPI.http`.

### `METHOD /api/<resource>` — <what it does>

Request:

```json
{ }
```

Responses:

- **200 OK** / **201 Created**

  ```json
  { }
  ```

- **400 Bad Request** — <when>
- **404 Not Found** — <when>

## UI

One subsection per screen.

### Screen: <name> (`/route`)

- What is shown.
- What the user can do, and what happens on success and on error.

## Acceptance criteria

Checked off once verified.

- [ ] <Given / when / then, or a concrete example>

## Out of scope

- <What this feature deliberately does not cover>

## Open questions

- <Anything undecided>
