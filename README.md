# EventConnect

Digital invitations, event management and RSVP platform.

Organizers create events, design and send invitations, manage guest lists, track RSVPs and message their guests. Guests open their invitation and RSVP through a personal link without creating an account. Administrators manage users, templates and support.

> **Status:** early development. Accounts (registration, email verification, sign-in, password reset, profile) are built. The other features are specified and are being built in order. See [docs/requirements](docs/requirements/README.md) for progress.

## Tech stack

| Part | Stack |
|---|---|
| Frontend (`UI/`) | React 19, TypeScript, Vite, PrimeReact 10, React Router |
| Backend (`API/`) | .NET 10, ASP.NET Core Web API, ASP.NET Core Identity (cookie sessions), EF Core |
| Database | SQL Server |
| Tests | Vitest + Testing Library (UI), xUnit v3 (API) |

The UI and API share one origin. In development, Vite proxies `/api` to the API.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (current LTS) and npm
- SQL Server. The default connection string expects SQL Server Express at `localhost\SQLEXPRESS` with Windows authentication. Change `ConnectionStrings:EventConnect` in `API/EventConnectAPI/appsettings.json` to use a different server.

### 1. Set up the database

```sh
cd API
dotnet tool restore
dotnet ef database update --project EventConnectCore --startup-project EventConnectAPI
```

### 2. Run the API

```sh
cd API
dotnet run --project EventConnectAPI
```

The API runs at http://localhost:5207. The interactive API reference (Scalar) is at http://localhost:5207/scalar.

### 3. Run the UI

```sh
cd UI
npm install
npm run dev
```

Open http://localhost:5173.

### Email (optional)

If SMTP isn't configured, the API writes outgoing emails (such as verification and password reset links) to the log, so you can still test those flows locally. To send real email, set the SMTP credentials with user secrets:

```sh
cd API/EventConnectAPI
dotnet user-secrets set "Email:Smtp:UserName" "you@example.com"
dotnet user-secrets set "Email:Smtp:Password" "<app password>"
dotnet user-secrets set "Email:Smtp:FromAddress" "you@example.com"
```

### VS Code

The repo includes launch configurations and tasks. **Full stack: API + UI (Chrome)** starts both apps with the debugger attached, and the **test: all** task runs both test suites.

## Running tests

```sh
cd API && dotnet test --solution EventConnect.slnx   # API: no SQL Server or SMTP needed
cd UI && npm test                                     # UI
```

## Project structure

```
API/
  EventConnectAPI/       ASP.NET Core Web API: startup and controllers
  EventConnectService/   Business logic: services and view models
  EventConnectCore/      EF Core DbContext, entities and migrations
  EventConnect.Tests/    Service and HTTP-level tests
UI/
  src/                   React app
docs/
  functional-requirements.md   Product specification (FRD)
  requirements/                One buildable spec per feature
```

## Documentation

- [Functional Requirements Document](docs/functional-requirements.md): the full product specification
- [Feature requirements and build status](docs/requirements/README.md): per-feature specs with API contracts, UI screens and acceptance criteria
- [CLAUDE.md](CLAUDE.md): coding conventions and the full list of commands

## License

[MIT](LICENSE)
