# EventConnect

Digital invitations, event management and RSVP platform. Monorepo with a React frontend and a .NET backend.

## Projects

- `EventConnectUI/` – React 19 + Vite 8 + TypeScript SPA. Entry: `src/main.tsx` → `src/App.tsx`.
- `EventConnectAPI/` – ASP.NET Core Web API on .NET 10 (controller-based). Entry: `Program.cs`, controllers in `Controllers/`.

## Commands

Run from inside each project folder.

**UI (`EventConnectUI/`)**
- Install: `npm install`
- Dev server: `npm run dev` (http://localhost:5173)
- Build (type-check + bundle): `npm run build`
- Lint: `npm run lint` (oxlint, config in `.oxlintrc.json`)

**API (`EventConnectAPI/`)**
- Run: `dotnet run` (http://localhost:5207; `dotnet run --launch-profile https` adds https://localhost:7055)
- Build: `dotnet build`
- OpenAPI document (Development only): `/openapi/v1.json`
- Sample requests: `EventConnectAPI.http`

## Conventions

- UI: TypeScript only; function components and hooks; keep components in `src/`.
- API: one controller per resource in `Controllers/`, route `api/[controller]`; nullable reference types are enabled — don't suppress warnings.
- Calling the API from the UI requires CORS in `Program.cs` or a `server.proxy` entry in `vite.config.ts` (neither is set up yet).
- The `WeatherForecast` controller/model are template samples; remove them once real endpoints exist.
- Before finishing a change, build the project(s) you touched (`npm run build` / `dotnet build`).
