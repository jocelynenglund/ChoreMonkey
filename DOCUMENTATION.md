# ChoreMonkey — Full Project Documentation

## Table of Contents

1. [Overview](#overview)
2. [Technology Stack](#technology-stack)
3. [Project Structure](#project-structure)
4. [Architecture](#architecture)
5. [Features](#features)
6. [API Reference](#api-reference)
7. [Data Model (Events)](#data-model-events)
8. [Frontend](#frontend)
9. [Real-time (SignalR)](#real-time-signalr)
10. [Security](#security)
11. [Testing](#testing)
12. [Deployment](#deployment)
13. [Development Setup](#development-setup)

---

## Overview

ChoreMonkey is a household chore management system designed for families. It allows households to track chores, assign them to members, and manage a salary/allowance system with deductions for missed chores and bonuses for optional ones.

**Live URLs:**
- Frontend: `http://choremonkey.itsybit.se`
- API: `https://itsybitsylist-api.azurewebsites.net`

---

## Technology Stack

### Backend
| Technology | Version | Purpose |
|---|---|---|
| .NET | 10.0 | Runtime |
| ASP.NET Core Minimal APIs | 10.0 | HTTP layer (no MVC/controllers) |
| MediatR | 12.4.1 | Command/Query/Event dispatching |
| FileEventStore | 1.1.1 | File-based event persistence |
| SignalR | 10.0 | Real-time push to clients |
| .NET Aspire | 9.3.1 | Dev orchestration & service defaults |

### Frontend
| Technology | Version | Purpose |
|---|---|---|
| React | 18.3.1 | UI framework |
| TypeScript | 5.8.3 | Type safety |
| Vite | 7.3.1 | Build tool |
| Tailwind CSS | 3.4.17 | Styling |
| shadcn-ui (Radix UI) | — | Component library |
| React Router | 6.30.1 | Client-side routing |
| TanStack React Query | 5.83.0 | Server state / caching |
| Zustand | 5.0.10 | Global client state (persisted) |
| SignalR Client | 10.0.0 | Real-time connection |
| Playwright | — | E2E testing |

---

## Project Structure

```
ChoreMonkey/
├── ChoreMonkey.ApiService/          # API entry point — registers endpoints, middleware
├── ChoreMonkey.Core/                # All business logic (vertical slices)
│   ├── Features/
│   │   ├── Households/              # Household create, access, invite
│   │   ├── Members/                 # Join, remove, nickname, status
│   │   ├── Chores/                  # CRUD, complete, assign, overdue
│   │   ├── Salary/                  # Allowance config, period close
│   │   └── Activity/                # Timeline, team overview
│   └── Infrastructure/
│       ├── PublishingEventStore.cs  # Decorator: stores + publishes events via MediatR
│       └── Security/PinHasher.cs   # PBKDF2 PIN hashing
├── ChoreMonkey.Events/              # Shared event record definitions (19 types)
├── ChoreMonkey.ServiceDefaults/     # Shared Aspire service config (health, OTLP, etc.)
├── ChoreMonkey.AppHost/             # Aspire dev-time orchestration host
├── ChoreMonkey.Tests/               # Unit tests
├── ChoreMonkey.IntegrationTests/    # 88+ integration tests (xUnit)
├── nestle-together/                 # React frontend
│   └── src/
│       ├── features/                # Feature modules (api.ts, types.ts, components/)
│       ├── pages/                   # Route-level components
│       ├── components/              # Shared UI components
│       ├── hooks/                   # Custom hooks (SignalR, etc.)
│       └── stores/                  # Zustand store
└── docs/
    ├── choremonkey.eno              # Event model diagram
    └── choremonkey.drawio           # Architecture diagram
```

---

## Architecture

### Backend: Vertical Slices + Event Sourcing

The backend is organized by **vertical slices**, where each feature owns its commands, queries, and handlers — rather than being split by technical layer.

```
Feature/
└── Chores/
    ├── Commands/
    │   ├── CreateChore/Handler.cs      # Command record + Endpoint + MediatR handler
    │   ├── CompleteChore/Handler.cs
    │   └── AssignChore/Handler.cs
    └── Queries/
        ├── GetChores/Handler.cs
        └── GetMyChores/Handler.cs
```

**Event flow:**
1. HTTP request arrives → Minimal API endpoint extracts params
2. `IMediator.Send(command)` dispatched
3. Handler validates, builds domain logic, appends event to store
4. `PublishingEventStore` decorator re-publishes event via MediatR
5. MediatR event handlers broadcast via SignalR
6. Client receives real-time notification

**Event Sourcing:**
There is no SQL database. All state is derived from a sequential log of immutable events. Aggregates (`HouseholdAggregate`, `ChoreAggregate`, `SalaryAggregate`) are rebuilt by replaying the event stream on each request.

**Stream naming:**
- `household-{householdId}` — household & member events
- `chores-{householdId}` — chore lifecycle events
- `salary-{householdId}` — salary config & period events

---

### Frontend: Feature Modules + Zustand Store

```
features/
├── chores/
│   ├── api.ts          # Axios/fetch calls to backend
│   ├── types.ts        # TypeScript interfaces
│   └── components/     # Chore-specific React components
├── members/
├── salary/
├── activity/
└── store.ts            # Global Zustand store (auth state + API actions)

hooks/
└── useHouseholdRealtime.ts   # SignalR connection + event dispatch

pages/
└── HouseholdDashboard.tsx    # Main app page
```

**State flow:**
```
User action → Zustand action → API call → Backend event → SignalR push → React Query refetch → UI update
```

---

## Features

### Household Management
- Create a household with admin PIN and optional member PIN
- Generate shareable invite links (7-day expiry)
- Join a household via invite code
- Two access levels: **admin** (full control) and **member** (personal view only)
- Remove members (admin only, requires admin PIN re-authentication)

### Chore Management
- **Create chores** with:
  - Name and description
  - Frequency: `daily`, `weekly` (any day or specific days), `interval` (every N days), `once`
  - Type: optional or required
  - Start date
  - Missed deduction amount (default: 10)
- **Assign chores** to specific members or all members
- **Complete chores** — with optional backdated or future completion dates
- **Delete chores** (admin only)
- **View history** per chore (all completions)

### Salary / Allowance System
Each member has:
- `baseSalary` — base payout per period
- `deductionMultiplier` — scales missed chore penalties
- `bonusMultiplier` — scales optional chore bonuses

Each chore has:
- `deductionRate` — amount deducted per missed occurrence
- `bonusRate` — amount added per optional chore completed

**Period calculation (monthly):**
```
projectedSalary = baseSalary
                - sum(missedRequired × deductionRate × deductionMultiplier)
                + sum(completedOptional × bonusRate × bonusMultiplier)
                (minimum 0)
```

A 2-day **grace period** applies — chores missed within the last 2 days are not yet penalized.

**Period closure:** Admin closes the month, creating an immutable `PeriodClosed` event with final payout amounts per member.

### Visibility & Overdue
- **My Chores** — each member sees their own pending, overdue, and completed chores
- **Team Overview** — admins see all members' overdue chores
- **Timeline** — recent completions across the household
- **Member Statuses** — members can set a short status string (e.g., "at school")

---

## API Reference

**Base URL:** `https://itsybitsylist-api.azurewebsites.net/api`

### Households
| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/households` | Create a new household |
| `POST` | `/households/{id}/access` | Authenticate (admin or member PIN) |
| `GET` | `/households/{id}` | Get household details |
| `POST` | `/households/{id}/invite` | Generate invite link |
| `POST` | `/households/{id}/members/{memberId}/pin` | Set member PIN |
| `POST` | `/households/{id}/admin-pin` | Change admin PIN |

### Members
| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/households/{id}/members` | List all members |
| `POST` | `/households/{id}/join` | Join via invite code |
| `POST` | `/households/{id}/members/{memberId}/nickname` | Change nickname |
| `POST` | `/households/{id}/members/{memberId}/status` | Set status text |
| `POST` | `/households/{id}/members/{memberId}/remove` | Remove member (admin) |

### Chores
| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/households/{id}/chores` | List all chores (with rates & completions) |
| `POST` | `/households/{id}/chores` | Create a chore |
| `POST` | `/households/{id}/chores/{choreId}/complete` | Mark chore complete |
| `POST` | `/households/{id}/chores/{choreId}/assign` | Assign to members |
| `POST` | `/households/{id}/chores/{choreId}/delete` | Delete chore (admin) |
| `GET` | `/households/{id}/chores/{choreId}/history` | Completion history |
| `POST` | `/households/{id}/chores/{choreId}/acknowledge-missed` | Acknowledge missed |
| `GET` | `/households/{id}/my-chores` | My pending/overdue/completed chores |
| `GET` | `/households/{id}/overdue` | Team overdue chores (admin) |

### Salary
| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/households/{id}/members/{memberId}/salary` | Set salary config |
| `POST` | `/households/{id}/chores/{choreId}/rates` | Set chore deduction/bonus rates |
| `GET` | `/households/{id}/salary/current` | Preview current period |
| `POST` | `/households/{id}/salary/close` | Close period (finalize payouts) |
| `GET` | `/households/{id}/salary/history` | Payout history |

### Activity
| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/households/{id}/timeline` | Recent completion timeline |
| `GET` | `/households/{id}/team-overview` | Team summary (admin) |

### System
| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health` | Health check |
| `GET` | `/api/version` | Build version info |
| `POST` | `/hubs/household` | SignalR hub (WebSocket/LongPolling) |

---

## Data Model (Events)

All state is stored as an ordered sequence of events — no SQL schema.

| Event | Key Fields |
|---|---|
| `HouseholdCreated` | householdId, name, adminPinHash, memberPinHash |
| `MemberJoinedHousehold` | memberId, householdId, inviteId, nickname |
| `MemberRemoved` | memberId, householdId, removedByMemberId |
| `MemberNicknameChanged` | memberId, householdId, newNickname |
| `MemberStatusChanged` | memberId, householdId, status |
| `AdminPinChanged` | householdId, newPinHash |
| `MemberPinChanged` | householdId, newPinHash |
| `InviteGenerated` | householdId, inviteId, inviteCode |
| `ChoreCreated` | choreId, householdId, displayName, description, frequency, isOptional, missedDeduction, startDate |
| `ChoreAssigned` | choreId, householdId, assignedToMemberIds, assignToAll |
| `ChoreCompleted` | choreId, householdId, completedByMemberId, completedAt |
| `ChoreDeleted` | choreId, householdId |
| `ChoreMissedAcknowledged` | choreId, householdId, memberId, period |
| `ChoreRatesSet` | choreId, householdId, deductionRate, bonusRate, setAt |
| `MemberSalarySet` | memberId, householdId, baseSalary, deductionMultiplier, bonusMultiplier, setAt |
| `PeriodClosed` | periodId, householdId, periodStart, periodEnd, payouts[], closedAt |

---

## Frontend

### Routing (`App.tsx`)
| Path | Page | Description |
|---|---|---|
| `/` | `LandingPage` | Create or join household |
| `/household/:id` | `HouseholdDashboard` | Main app view |
| `/household/:id/chores` | `ChoresPage` | Full chore list |
| `/household/:id/salary` | `SalaryPage` | Allowance management |

### Zustand Store (`features/store.ts`)
The global store holds:
- `householdId`, `memberId`, `isAdmin` — auth state (persisted to localStorage)
- API action methods: `completeChore()`, `createChore()`, `assignChore()`, etc.

### Key Components
| Component | Location | Purpose |
|---|---|---|
| `HouseholdDashboard` | `pages/` | Main layout shell |
| `ChoreCard` | `features/chores/components/` | Single chore item |
| `MemberList` | `features/members/components/` | Household member display |
| `SalaryOverview` | `features/salary/components/` | Period preview and history |
| `Timeline` | `features/activity/components/` | Completion feed |

---

## Real-time (SignalR)

The backend hub is at `/hubs/household`. The frontend connects via `useHouseholdRealtime.ts`.

**Events broadcast to clients:**

| Event | Trigger |
|---|---|
| `ChoreCompleted` | A chore is marked complete |
| `ChoreCreated` | A new chore is added |
| `ChoreAssigned` | Chore assignment changes |
| `ChoreDeleted` | A chore is removed |
| `MemberJoined` | Someone joins via invite |
| `MemberRemoved` | A member is removed |
| `MemberStatusChanged` | Member updates their status |
| `MemberNicknameChanged` | Member changes their name |

> **Note:** SignalR uses HTTP Long Polling on Azure Free tier (WebSockets not available). On other hosting, WebSocket transport is used.

---

## Security

### PIN Hashing
PINs are hashed with **PBKDF2 + SHA-256**, 100,000 iterations, with a cryptographically random salt. Plain-text PINs are never stored.

### Rate Limiting
- Auth endpoints (`/access`, `/join`): **5 requests/minute**
- All other API endpoints: **100 requests/minute**

This prevents PIN brute-force attacks.

### Authorization Model
- **Admin** — full access: create/delete chores, manage members, close salary periods, view team overdue
- **Member** — restricted: complete own chores, view own salary, update own status/nickname
- All protected endpoints verify the caller's identity against the stored PIN hash

---

## Testing

### Integration Tests (88+ cases)
Located in `ChoreMonkey.IntegrationTests/`. Uses xUnit with an in-memory test server.

Covers:
- Household lifecycle (create, authenticate, invite)
- Member management (join, remove, PIN change)
- Chore CRUD + completion
- Frequency edge cases (daily, weekly specific-day, interval, once)
- Missed chore detection + acknowledgment
- Salary calculations and period closure
- Overdue chore logic

Run with:
```bash
dotnet test ChoreMonkey.IntegrationTests
```

### E2E Tests (Playwright)
Located in `nestle-together/`.

```bash
# Run against local dev
npm run test:e2e

# Run against staging
E2E_BASE_URL=https://labs.itsybit.se npm run test:e2e
```

---

## Deployment

### Backend — Azure App Service
- **URL:** `https://itsybitsylist-api.azurewebsites.net`
- **CI/CD:** GitHub Actions on push to `main`
- **Event store path:** set via `EVENTSTORE_PATH` env var (default: `./data`)

### Frontend — FTP (Simply.com)
- **URL:** `http://choremonkey.itsybit.se`
- **Deploy root:** `/choremonkey/` (not `/public_html/`)
- **CI/CD:** GitHub Actions using `FTP_USERNAME` + `FTP_PASSWORD` secrets
- **Build command:** `npm run build` (outputs to `dist/`)

---

## Development Setup

### Backend
```bash
# From repo root
dotnet run --project ChoreMonkey.AppHost
# or run the API directly:
dotnet run --project ChoreMonkey.ApiService
```

The Aspire AppHost starts the API and any dependent services together with a dev dashboard.

### Frontend
```bash
cd nestle-together
npm install
npm run dev
```

The Vite dev server proxies API requests. Configure the target URL in `vite.config.ts`.

### Environment Variables (API)
| Variable | Description | Default |
|---|---|---|
| `EVENTSTORE_PATH` | Path for JSON event files | `./data` |
| `ASPNETCORE_URLS` | Listening addresses | `http://+:8080` |

---

*Generated from codebase analysis — March 2026*
