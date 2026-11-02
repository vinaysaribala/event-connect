# Feature: Invitation Design

**Status:** Draft
**Release:** MVP (premium templates and custom branding are Phase 2)
**API:** Not started
**UI:** Not started
**Source:** [FRD §5](../functional-requirements.md#5-invitation-design), FR-041 (template management, see [09](09-administration.md))
**Depends on:** [02 Events](02-events.md); templates are created by admins in [09](09-administration.md), so seed a few for development

> Detail below the FRD's one-line requirements (template rules, endpoints, status codes) is proposed and needs review before Status moves to Ready.

## Summary

Anyone can browse invitation templates. An organizer picks one for an event and customizes its text, fonts, colors and image, within the limits the template allows. They preview it as a phone and as a desktop screen. When the event is published, the design is frozen as a version, and that version is what guests see.

## User stories

- As a visitor or organizer, I can browse, search and filter templates and preview them, so that I can pick a look for my event.
- As an organizer, I can customize the chosen template's text, font, colors and image.
- As an organizer, I can preview my invitation as guests will see it on mobile and on desktop.
- As a guest, I always see the version of the invitation the organizer last published, not a half-edited draft.

## Functional requirements

- **FR-012** Browse, search, filter and preview templates.
  - No sign-in needed (Visitor persona).
  - Search by name. Filter by category. Only active templates are shown. Results are paged, 24 per page.
- **FR-013** Edit text, fonts, colors and imagery within template rules.
  - Each template defines its editable *slots*: text slots (with max length), allowed fonts, color slots (optionally limited to a palette), and image slots (with an aspect ratio).
  - Values outside the template's rules are rejected (400).
  - Images: JPEG, PNG or WebP, at most 5 MB, stored in object storage (FRD §15).
- **FR-014** Preview on mobile and desktop.
  - The preview uses the same rendering as the guest invitation page ([06](06-rsvp.md)), at phone (390 px) and desktop (1280 px) widths.
- **FR-015** Save a stable version of the published invitation.
  - Publishing the event ([02](02-events.md)) saves design version 1.
  - Design edits after publishing stay as a working copy until the organizer clicks "Update invitation", which saves version N+1.
  - Guests always see the latest saved version. Versions are never changed after saving.

## Data

| Entity | Field | Type | Rules |
|---|---|---|---|
| TemplateCategory | Id, Name, Slug, SortOrder | | managed by admins ([09](09-administration.md)) |
| Template | Id | int | key |
| | Name | string | required, max 100 |
| | CategoryId | int | FK |
| | ThumbnailUrl | string | |
| | Definition | JSON | slots and rules (see below) |
| | IsActive | bool | inactive templates are hidden from browsing |
| | IsPremium | bool | always false in MVP |
| InvitationDesign | EventId | int | key and FK; one working copy per event |
| | TemplateId | int | FK |
| | Customization | JSON | slot values |
| | UpdatedAt | DateTime (UTC) | |
| InvitationDesignVersion | Id, EventId, VersionNumber | | unique (EventId, VersionNumber) |
| | TemplateId, Customization | | frozen snapshot |
| | CreatedAt | DateTime (UTC) | |

Example template `Definition`:

```json
{
  "text": [
    { "key": "headline", "label": "Headline", "maxLength": 60, "default": "You're invited!" },
    { "key": "message", "label": "Message", "maxLength": 400, "default": "" }
  ],
  "fonts": ["Playfair Display", "Inter", "Caveat"],
  "colors": [
    { "key": "background", "label": "Background", "palette": ["#FFF8F0", "#1E2A3A", "#F4D6CC"] },
    { "key": "accent", "label": "Accent", "palette": null }
  ],
  "images": [{ "key": "hero", "label": "Main image", "aspectRatio": "3:2", "required": false }]
}
```

## API

### `GET /api/templates` — browse (no sign-in)

Query: `search`, `categoryId`, `page` (default 1).

- **200 OK**

  ```json
  {
    "items": [
      { "id": 4, "name": "Rooftop Glow", "categoryId": 3, "categoryName": "Birthday", "thumbnailUrl": "https://…/rooftop-glow.png" }
    ],
    "page": 1,
    "pageSize": 24,
    "totalCount": 57
  }
  ```

### `GET /api/templates/{id}` — template detail and rules (no sign-in)

- **200 OK** — list item fields plus `"definition"` (shape above)
- **404** — unknown or inactive

### `GET /api/template-categories` (no sign-in)

- **200 OK** — `[{ "id": 3, "name": "Birthday", "slug": "birthday" }]`

### `GET /api/events/{id}/design` — working copy

- **200 OK**

  ```json
  {
    "templateId": 4,
    "customization": {
      "text": { "headline": "Maya turns 30!", "message": "Join us for dinner and drinks." },
      "font": "Playfair Display",
      "colors": { "background": "#1E2A3A", "accent": "#F2B544" },
      "images": { "hero": "https://…/uploads/abc123.jpg" }
    },
    "hasUnpublishedChanges": true,
    "latestVersion": 2
  }
  ```

- **404** — no design chosen yet, or not your event

### `PUT /api/events/{id}/design` — choose a template or save customization

Body: `templateId` and `customization` (same shape as above).

- **200 OK** — updated working copy
- **400** — value breaks the template's rules, for example:

  ```json
  { "status": 400, "errors": { "text.headline": ["Max 60 characters."], "font": ["Font not allowed by this template."] } }
  ```

### `POST /api/events/{id}/design/versions` — "Update invitation" (published events)

- **201 Created** — `{ "versionNumber": 3, "createdAt": "2026-11-03T10:00:00Z" }`
- **409** — event not published (version 1 is created by publishing) or no changes since the latest version

### `GET /api/events/{id}/design/versions`

- **200 OK** — `[{ "versionNumber": 1, "createdAt": "…" }, …]`, newest first

### `POST /api/uploads/images` — upload an image (multipart, field `file`)

- **201 Created** — `{ "url": "https://…/uploads/abc123.jpg" }`
- **400** — wrong type or larger than 5 MB

## UI

### Screen: Template gallery (`/templates`, and step 1 of creating an event)

- Grid of thumbnails, search box, category chips, "Load more".
- Clicking a template opens a large preview with "Use this template" (goes to sign-in first if needed).

### Screen: Design editor (`/events/:id/design`)

- Left: controls built from the template's slots (text fields with character counters, font picker, color pickers or palettes, image upload with crop to the aspect ratio).
- Right: live preview with a **Mobile / Desktop** toggle (FR-014).
- Draft events save automatically. Published events show "You have unpublished design changes — Update invitation".
- "Change template" warns that slot values which don't exist in the new template will be lost.

## Acceptance criteria

- [ ] A signed-out visitor can search and filter templates and open a preview.
- [ ] Inactive templates don't appear in browsing and return 404 by id.
- [ ] Saving a headline longer than the template's maximum returns 400 naming the slot.
- [ ] Uploading a 6 MB image or a GIF returns 400.
- [ ] Publishing an event creates design version 1. The guest page shows version 1 even after further unsaved edits.
- [ ] "Update invitation" creates version 2, and the guest page then shows version 2.
- [ ] The preview at mobile width matches the guest page at the same width.

## Later (Phase 2)

- **FR-016** Premium templates and custom branding: `IsPremium` templates need an entitlement ([09](09-administration.md), FR-043); custom branding means uploaded logos and custom colors without palette limits.

## Open questions

- Who designs the templates, and in what format? The `Definition` JSON above covers editable slots, but the visual layout needs a rendering approach (for example, a React component per template, or an HTML/CSS template with placeholders).
- Image storage for development: local folder or a cloud storage emulator (e.g. Azurite)? Which cloud for production?
- Should guests be told when the invitation they're viewing has been updated since they last saw it?
- Is the event page for a Public event ([02](02-events.md)) the same rendering as the invitation?
