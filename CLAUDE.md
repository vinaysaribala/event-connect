# EventConnect

Digital invitations, event management and RSVP platform. Monorepo with a React frontend and a .NET backend.

## Projects

- `UI/` – EventConnectUI: React 19 + Vite 8 + TypeScript SPA. Entry: `src/main.tsx` → `src/App.tsx`.
- `API/` – .NET 10 backend, solution `API/EventConnect.slnx`, three layers (each only references the one below):
  - `EventConnectAPI/` – ASP.NET Core Web API: startup (`Program.cs`) and controllers (`Controllers/`). No business logic or EF Core queries here.
  - `EventConnectService/` – business logic: service interfaces (`Interfaces/`), implementations (`Implementations/`), view models (`ViewModels/`). Controllers receive and return view models, never entities.
  - `EventConnectCore/` – EF Core `EventConnectDbContext` (`Data/`) and entity models (`Models/`). SQL Server provider.
  - Each library exposes a `DependencyInjection.cs` extension (`AddEventConnectCore`, `AddEventConnectServices`) called from `Program.cs`.

## Requirements

The product is defined in `docs/functional-requirements.md` (the FRD, master copy). It is broken into one file per feature in `docs/requirements/` (index and build order: `README.md`; template: `_TEMPLATE.md`). Requirement IDs (FR-xxx, BR-xx, AC-xx) are shared between them. When implementing a feature:

- Read its requirements file first and build exactly what it specifies; ask about anything marked TBD or listed under Open questions rather than guessing.
- Only implement files whose Status is Ready, unless told otherwise. Build MVP items only; anything under "Later (Phase 2)" waits until asked.
- Follow the FRD's business rules (§11) and non-functional targets (§16) even when a feature file doesn't repeat them.
- Match the API request/response samples exactly (field names, status codes), and add every sample request to `API/EventConnectAPI/EventConnectAPI.http`.
- Verify each acceptance criterion, tick it off in the file, and update the Status/API/UI lines there and in the README table.
- If the implementation has to differ from the requirements, update the file and say why.

## Commands

Run from inside each project folder.

**UI (`UI/`)**
- Install: `npm install`
- Dev server: `npm run dev` (http://localhost:5173)
- Build (type-check + bundle): `npm run build`
- Lint: `npm run lint` (oxlint, config in `.oxlintrc.json`)
- Test: `npm test` (Vitest + Testing Library, jsdom; `npm run test:watch` to watch). Tests sit next to the code as `*.test.ts(x)`; shared helpers (`renderWithAuth`, `stubFetch`, `routeFetch`, `reply`) are in `src/test/utils.tsx`, sample events in `src/test/events.ts`.

**API (`API/`)**
- Build: `dotnet build EventConnect.slnx`
- Run: `dotnet run --project EventConnectAPI` (http://localhost:5207; add `--launch-profile https` for https://localhost:7055)
- Database: connection string `EventConnect` in `EventConnectAPI/appsettings.json` (local SQL Server Express, `localhost\SQLEXPRESS`, Windows auth)
- Migrations (`dotnet-ef` is a local tool in `API/dotnet-tools.json`; run `dotnet tool restore` once):
  `dotnet ef migrations add <Name> --project EventConnectCore --startup-project EventConnectAPI`, then
  `dotnet ef database update --project EventConnectCore --startup-project EventConnectAPI`
- API reference and try-it UI (Scalar, Development only): http://localhost:5207/scalar
- OpenAPI document (Development only): `/openapi/v1.json`
- Sample requests: `EventConnectAPI/EventConnectAPI.http`
- Test: `dotnet test --solution EventConnect.slnx` (xUnit v3 project `EventConnect.Tests` on Microsoft.Testing.Platform, opted in by the root `global.json`; no SQL Server or SMTP needed). One class: `dotnet test --project EventConnect.Tests --filter-class "*.AuthServiceTests"`. VS Code: "Tests: API …" launch configs debug the tests, `test: all` task runs both suites. `Services/` tests the service layer with real Identity on in-memory SQLite (`Support/ServiceTestHost`); `Api/` tests the HTTP contract through the real pipeline (`Support/ApiFactory`, a `WebApplicationFactory`). Both capture emails in `FakeEmailQueue`. If a running API locks the build output, add `--artifacts-path <temp dir>`.

## Conventions

- UI: TypeScript only; function components and hooks; keep components in `src/`.
- UI components: PrimeReact **v10** (MIT) with PrimeIcons and the default `lara-light-cyan` theme, imported in `src/main.tsx` under `PrimeReactProvider`. Don't upgrade to v11: it needs a commercial PrimeUI license key and is unstyled. Use PrimeReact components and theme CSS variables (`var(--primary-color)`, `var(--surface-ground)`, etc.) rather than custom CSS; `src/index.css` holds only base page styles.
- API: one controller per resource in `Controllers/`, route `api/[controller]`; nullable reference types are enabled — don't suppress warnings.
- New feature = entity in Core (+ `DbSet` in the DbContext and a migration), view models + interface + service in Service (register in its `DependencyInjection.cs`), thin controller in API. `Events` is the reference example.
- `System.Security.Cryptography.Pkcs` is pinned in `EventConnectCore.csproj` to override a vulnerable version pulled in by `Microsoft.Data.SqlClient`; drop the pin once SqlClient ships a fixed dependency.
- UI → API: always relative URLs (`/api/...`). In dev, Vite proxies `/api` to http://localhost:5207 (`server.proxy` in `UI/vite.config.ts`). Don't add CORS; UI and API stay same-origin in production too.
- Auth/sessions: HTTP-only cookie from ASP.NET Core Identity; API returns 401 (no login redirects). The UI never stores tokens; an `AuthProvider` context (`UI/src/auth/`) loads the user from `GET /api/me`, and all requests go through `apiFetch` in `UI/src/api.ts`. Wrap sign-in-only routes in `RequireAuth`. Full decision: `docs/requirements/01-accounts.md` → Decisions.
- Before finishing a change, build and test the project(s) you touched (`npm run build` + `npm test` / `dotnet build EventConnect.slnx` + `dotnet test --solution EventConnect.slnx`). New behaviour gets tests at the same levels as the accounts feature.
