# Requirements

The product is defined in the [Functional Requirements Document](../functional-requirements.md) (FRD v1.0), which is the master copy. This folder breaks it into one buildable file per feature. Each file covers what the feature does, its data, its API contract with sample requests and responses, its UI screens, and the acceptance criteria used to verify it. Requirement IDs (FR-xxx, BR-xx, AC-xx) match the FRD.

## Features

Listed in build order: each feature depends only on the ones above it.

| # | Feature | FRD | Release | Status | API | UI |
|---|---|---|---|---|---|---|
| 01 | [Accounts and Access](01-accounts.md) | §3 · FR-001–005 | MVP | In progress | Done | Done |
| 02 | [Event Lifecycle](02-events.md) | §4 · FR-006–011 | MVP | Draft | Partial | Not started |
| 03 | [Invitation Design](03-invitation-design.md) | §5 · FR-012–016 | MVP | Draft | Not started | Not started |
| 04 | [Guest Lists](04-guest-lists.md) | §6 · FR-017–021 | MVP | Draft | Not started | Not started |
| 05 | [Invitation Delivery](05-invitation-delivery.md) | §7 · FR-022–027 | MVP | Draft | Not started | Not started |
| 06 | [RSVP and Attendance](06-rsvp.md) | §8 · FR-028–034 | MVP | Draft | Not started | Not started |
| 07 | [Communication](07-communication.md) | §9 · FR-035–038 | MVP | Draft | Not started | Not started |
| 08 | [Organizer Dashboard](08-dashboard.md) | §10 · FR-039, 040, 044 | MVP | Draft | Not started | Not started |
| 09 | [Administration](09-administration.md) | §10 · FR-041–043 | MVP | Draft | Not started | Not started |

Phase 2 requirements are listed in each file under **Later (Phase 2)** and are not built until asked for.

**Cross-cutting**, applying to every feature and kept only in the FRD:
- Business rules ([§11](../functional-requirements.md#11-core-business-rules)), each also quoted where it applies
- Non-functional targets ([§16](../functional-requirements.md#16-nonfunctional-requirements-initial-targets)): security, accessibility (WCAG 2.2 AA), performance, observability

## Before building

Files start as **Draft**. The FRD gives one line per requirement. The detail added here (field limits, expiry times, endpoint shapes, status codes) is proposed and needs a review. For each file:

1. Read it, and correct anything that's wrong.
2. Answer its **Open questions**. Those marked **FRD decision** come from FRD §18.
3. Set **Status** to Ready.

## Adding or changing a feature

- New feature: add it to the FRD first, then copy [`_TEMPLATE.md`](_TEMPLATE.md) to `NN-feature-name.md` (next number), fill it in, and add a row above.
- Changed requirement: update the FRD and the feature file together so their IDs stay in step.
- To build one: *"Implement docs/requirements/NN-feature-name.md"*.

## Trying the API

With the API running in Development (`dotnet run --project EventConnectAPI` in `API/`, or **API: Debug (.NET)** in VS Code), open http://localhost:5207/scalar. Scalar lists every endpoint with its request and response shapes, and lets you send requests from the browser. Use it to check an implemented feature against the samples in its requirements file. The same samples are also in `API/EventConnectAPI/EventConnectAPI.http`.

## How a requirements file maps to the code

| Section | Becomes |
|---|---|
| Data | Entity in `API/EventConnectCore/Models/`, `DbSet` in `EventConnectDbContext`, and a migration |
| API → request/response samples | View models in `API/EventConnectService/ViewModels/` |
| Functional requirements | Validation and rules in the service (`API/EventConnectService/Implementations/`) |
| API → endpoints and status codes | Controller in `API/EventConnectAPI/Controllers/` |
| API → sample requests | Runnable requests in `API/EventConnectAPI/EventConnectAPI.http` |
| UI → screens | Components and routes in `UI/src/` |
| Acceptance criteria | What gets tested before the feature is marked Done |
