# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Backend (.NET)

```bash
# Run locally via Aspire orchestration (from repo root)
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet run --project ChoreMonkey.AppHost

# Run integration tests
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet test ChoreMonkey.IntegrationTests

# Run a single test
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet test ChoreMonkey.IntegrationTests --filter "FullyQualifiedName~TestClassName"
```

### Frontend (React)

```bash
cd nestle-together

npm run dev          # Development server
npm run build        # Production build (also generates changelog)
npm run lint         # ESLint
npm run test         # Vitest unit tests (single run)
npm run test:watch   # Vitest watch mode
npm run test:e2e     # Playwright E2E tests
npm run test:e2e:headed  # Playwright with browser visible
npm run test:e2e:staging # E2E against staging URL
```

### NuGet

The project uses a private NuGet feed at `https://nuget.pkg.github.com/jocelynenglund/index.json` (configured in `nuget.config`). This feed hosts `FileEventStore`.

## Architecture

### Overview

Event-sourced household chore management system. All state is stored as immutable events via **FileEventStore** (file-based, no SQL database). There is no traditional ORM or relational model — state is rebuilt by replaying events.

### Backend: Vertical Slices + Event Sourcing

`ChoreMonkey.Core/Feature/` contains vertical feature slices, each with its own commands, queries, handlers, and domain logic:
- `Chores/` — chore CRUD, completion, assignment, rates
- `Household/` — creation, access, invites, slugs
- `Members/` — join, remove, nickname, PIN auth, status
- `Salary/` — base salary, chore rates, period close, payout history
- `Activity/` — timeline, team overview

**Event flow:** Command → MediatR → Handler → Domain entity → Append events to FileEventStore → `PublishingEventStore` re-publishes events via MediatR → SignalR hub broadcasts to connected clients.

**Event streams** are keyed by household ID: `household-{id}`, `chores-{id}`, `salary-{id}`.

**`ChoreMonkey.Events/`** holds all 19 event type definitions (shared between core and consumers).

**`ChoreMonkey.ApiService/Program.cs`** (or `ChoreMonkey.Api/`) is the entry point — Minimal API endpoint registration, DI setup, SignalR hub mapping at `/hubs/household`.

**Security:** PIN-based auth (no accounts). PINs are PBKDF2+SHA256 hashed (100k iterations) in `Security/PinHasher.cs`. Rate limiting: 5 req/min for auth endpoints, 100 req/min general (production only).

### Frontend: Feature Modules

`nestle-together/src/` organized as:
- `features/` — feature-scoped components, hooks, API calls
  - `admin/` — admin panel (chore + salary management tabs)
  - `chores/` — chore list, completion, assignment UI
  - `salary/` — allowance views, payslips
  - `household/`, `members/`, `invites/`, `activity/`
- `components/` — shared UI (shadcn-ui wrappers + custom)
- `pages/` — route-level components
- `hooks/` — shared hooks, notably `useHouseholdRealtime` (SignalR)
- `stores/` — Zustand store (auth state + API action methods, persisted to localStorage)
- `types/` — shared TypeScript types

**State management:**
- **Zustand** (`stores/`) — auth/session state (memberId, householdId, PIN), persisted to localStorage
- **React Query** — server state caching for all API fetches
- **SignalR** (`useHouseholdRealtime`) — real-time push; invalidates React Query cache on events

**Routing** (React Router v6):
- `/` → Landing (create/join household)
- `/household/:id` → Dashboard (tabs: Chores, Team, Activity, Admin)
- `/h/:slug` → Vanity URL resolver → redirects to access page
- `/access/:id` → PIN authentication

**Path alias:** `@` maps to `src/` (configured in `tsconfig.json` and `vite.config.ts`).

### Real-time

SignalR hub at `/hubs/household`. Uses HTTP Long Polling (WebSockets not available on Azure Free tier). The frontend hook `useHouseholdRealtime` subscribes to household-scoped events and invalidates React Query caches on receipt.

### Deployment

- **API:** Azure App Service — push to `main` triggers GitHub Actions deploy
- **Frontend:** Simply.com via FTP — push to `main` triggers GitHub Actions FTP deploy to `/choremonkey/`
- **Event store data:** persisted at `EVENTSTORE_PATH` env var (default: `./data`)

### Git Remotes

- `origin` → `itsybit-agent/ChoreMonkey` (fork)
- `upstream` → `jocelynenglund/ChoreMonkey` (canonical)
