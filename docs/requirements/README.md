# Requirements

One file per feature. Each describes what the feature does (user stories, numbered functional requirements), its data, its API contract with sample requests and responses, its UI screens, and the acceptance criteria used to verify it.

## Features

| # | Feature | Status | API | UI |
|---|---|---|---|---|
| 01 | [Events](01-events.md) | In progress | Done | Not started |

## Adding a feature

1. Copy [`_TEMPLATE.md`](_TEMPLATE.md) to `NN-feature-name.md` (next number, lowercase, hyphens).
2. Fill it in. Bullets are fine; write TBD for anything undecided.
3. Add a row to the table above.
4. Set **Status** to Ready and ask: *"Implement docs/requirements/NN-feature-name.md"*.

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
