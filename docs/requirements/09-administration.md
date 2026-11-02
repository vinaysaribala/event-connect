# Feature: Administration

**Status:** Draft
**Release:** MVP (subscriptions and payments are Phase 2)
**API:** Not started
**UI:** Not started
**Source:** [FRD §10](../functional-requirements.md#10-dashboards-and-administration) (FR-041, FR-042, FR-043), BR-09, FRD §2 Administrator persona
**Depends on:** [01 Accounts](01-accounts.md). Template management is needed before [03](03-invitation-design.md) can be used for real (seed data works until then).

> Detail below the FRD's one-line requirements (audited actions, search scope, endpoints) is proposed and needs review before Status moves to Ready.

## Summary

An admin area for Administrators: managing invitation templates and categories, searching for users, events and guests to answer support requests, and reviewing an audit log of sensitive actions.

## User stories

- As an administrator, I can create, edit, activate and deactivate templates and categories.
- As an administrator, I can search by email, name or id to find a user, an event or a guest when helping with a support request.
- As an administrator, I can see an audit trail of who did what, and when, for sensitive actions.

## Functional requirements

- **FR-041** Manage templates and categories.
  - Categories: name, slug (unique), sort order, active flag.
  - Templates: name, category, thumbnail image, `Definition` JSON ([03](03-invitation-design.md)), active flag. The `Definition` is validated on save.
  - Templates and categories in use are deactivated, never deleted. Events already using a deactivated template keep their design versions.
- **FR-042** Admin audit log and support search.
  - Audited actions (who, what, when, from which IP address):
    - sign-in successes and failures
    - role changes
    - template and category changes
    - event publish, cancel and archive
    - guest export ([04](04-guest-lists.md))
    - guest link revoke and reissue
    - every admin view of a guest's personal data (BR-09)
    - Phase 2: RSVP overrides (FR-034)
  - Audit entries are append-only. Retention is an open question.
  - Support search: one search box matching user email or name, event title or id, and guest email. Results are grouped by type.
  - Admins can view an event's details and guest list read-only. Admins can't edit organizers' events in MVP.
- **Roles:** the first Administrator comes from configuration (`Admin:SeedEmail`, built in [01 Accounts](01-accounts.md)); after that, admins can grant or remove the Administrator role.

## Data

| Entity | Field | Type | Rules |
|---|---|---|---|
| AuditEvent | Id | long | key |
| | ActorUserId | int? | null for guests or system |
| | ActorType | enum | `User`, `Admin`, `Guest`, `System` |
| | Action | string | e.g. `Event.Published`, `Guests.Exported`, `Admin.ViewedGuestPii` |
| | EntityType, EntityId | string | |
| | Data | JSON? | small details, never passwords or tokens |
| | IpAddress | string? | |
| | CreatedAt | DateTime (UTC) | indexed |
| UserRole | UserId, RoleId | | |

Template and TemplateCategory are defined in [03](03-invitation-design.md).

## API

All endpoints require the `Administrator` role (otherwise **403**).

### Templates and categories

- `GET /api/admin/templates` (includes inactive), `POST /api/admin/templates`, `PUT /api/admin/templates/{id}`, `POST /api/admin/templates/{id}/deactivate`, `POST /api/admin/templates/{id}/activate`
- The same pattern under `/api/admin/template-categories`

`POST /api/admin/templates`:

```json
{
  "name": "Rooftop Glow",
  "categoryId": 3,
  "thumbnailUrl": "https://…/uploads/rooftop-glow.png",
  "definition": { "text": [ … ], "fonts": [ … ], "colors": [ … ], "images": [ … ] },
  "isActive": false
}
```

- **201 Created** — template including `id`
- **400** — invalid `definition` (e.g. a slot missing `key`), or an unknown category

### `GET /api/admin/search?q=sam@example.com`

- **200 OK**

  ```json
  {
    "users": [{ "id": 7, "email": "maria@example.com", "displayName": "Maria Lopez" }],
    "events": [{ "id": 12, "title": "Maya's 30th Birthday", "organizerEmail": "maria@example.com", "status": "Published" }],
    "guests": [{ "guestId": 31, "eventId": 12, "eventTitle": "Maya's 30th Birthday", "name": "Sam Carter", "email": "sam@example.com" }]
  }
  ```

  Returning guest results writes an `Admin.ViewedGuestPii` audit event.

### `GET /api/admin/events/{id}` and `GET /api/admin/events/{id}/guests`

- **200 OK** — read-only event and guest list (audited)

### `GET /api/admin/audit`

Query: `actorUserId`, `entityType`, `entityId`, `action`, `from`, `to`, `page`.

- **200 OK**

  ```json
  {
    "items": [
      { "id": 9001, "createdAt": "2026-11-12T09:00:00Z", "actorType": "User", "actorUserId": 7, "action": "Guests.Exported", "entityType": "Event", "entityId": "12", "ipAddress": "203.0.113.5" }
    ],
    "page": 1,
    "pageSize": 50,
    "totalCount": 1203
  }
  ```

### `PUT /api/admin/users/{id}/roles`

```json
{ "roles": ["Organizer", "Administrator"] }
```

- **200 OK**
- **400** — removing the last Administrator

## UI

Admin area under `/admin`, only shown to Administrators, with its own navigation:

- **Templates** (`/admin/templates`) — table with active toggle; editor with fields, thumbnail upload, a JSON editor for `Definition` with validation messages, and a live preview using the [03](03-invitation-design.md) renderer.
- **Categories** (`/admin/categories`) — table with inline edit and drag to reorder.
- **Support search** (`/admin/search`) — one box, results grouped by Users / Events / Guests; links to read-only event and guest views.
- **Audit log** (`/admin/audit`) — filterable table; clicking an entry shows its details.

## Acceptance criteria

- [ ] A non-admin calling any `/api/admin/*` endpoint gets 403, and the `/admin` UI isn't reachable for them.
- [ ] A template with an invalid `Definition` can't be saved (400 with a reason).
- [ ] A deactivated template disappears from public browsing; an event already using it still renders for guests.
- [ ] Searching by a guest's email finds them; the search is recorded as `Admin.ViewedGuestPii`.
- [ ] Exporting a guest list ([04](04-guest-lists.md)) creates a `Guests.Exported` audit event visible in the audit log.
- [ ] The last Administrator can't remove their own admin role.

## Later (Phase 2)

- **FR-043** Subscriptions, payments, receipts and entitlements (premium templates FR-016, higher limits). Needs a payment processor (FRD §15).

## Decisions

### First Administrator (decided 2026-11-02)

- The user whose email matches `Admin:SeedEmail` gets the `Administrator` role on registration or verification. This is built in [01 Accounts](01-accounts.md).

## Open questions

- Is there a separate **Support** role with less access than Administrator? BR-09 mentions "support roles", but the personas only list Administrator.
- User management: the Administrator persona says "manage users", but there's no FR for it. Is disabling or deleting user accounts MVP?
- **FRD decision:** how long are events, guest data and audit history kept?
