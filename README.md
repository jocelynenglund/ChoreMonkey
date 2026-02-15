# 🐵 ChoreMonkey

Household chore management with event sourcing. Built with .NET 10, React, and vertical slice architecture.

## Features

- **Households** - Create and manage family households with PIN access
- **Members** - Invite family members, set nicknames and status
- **Chores** - Daily, weekly, interval, and one-time chores
- **Multi-assignee** - Assign chores to specific members or everyone
- **Optional/Bonus** - Mark chores as optional for extra credit
- **Overdue tracking** - See who's behind on their chores
- **Activity timeline** - Track completions and household activity
- **Real-time updates** - SignalR for live sync (requires Azure Basic tier)

## Architecture

```
ChoreMonkey/
├── ChoreMonkey.ApiService/     # Minimal API endpoints
├── ChoreMonkey.Core/           # Business logic (vertical slices)
│   └── Feature/
│       ├── Household/          # CreateHousehold, AccessHousehold, SetAdminPin
│       ├── Members/            # Join, Profile, Remove
│       ├── Invites/            # GenerateInvite, InviteLink
│       ├── Chores/             # CRUD, Complete, Overdue, MyChores
│       └── Activity/           # Timeline, TeamOverview
├── ChoreMonkey.Events/         # Event definitions
├── ChoreMonkey.IntegrationTests/  # 83 integration tests
├── nestle-together/            # React frontend
└── docs/                       # Event model documentation
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- Node.js 22+
- GitHub Packages access for FileEventStore

### Backend

```bash
# Configure NuGet (one time)
dotnet nuget update source github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_TOKEN \
  --store-password-in-clear-text

# Run
dotnet run --project ChoreMonkey.ApiService
```

### Frontend

```bash
cd nestle-together
npm install
npm run dev
```

### Tests

```bash
# Integration tests (83 tests)
dotnet test ChoreMonkey.IntegrationTests

# E2E tests (Playwright)
cd nestle-together
npx playwright install  # first time
npm run test:e2e
```

## Event Model

The system is documented using [Giraflow](https://github.com/SBortz/giraflow) - a human-readable format for event modeling.

### View the Event Model

```bash
# From giraflow directory
cd /path/to/giraflow
npm install        # first time
npm run build      # first time
npm start /path/to/ChoreMonkey/docs/choremonkey.giraflow.json
```

Opens at http://localhost:3000 with:
- **Info** - Overview of all events, commands, and views
- **Timeline** - Visual swimlane diagram
- **Slices** - Given-When-Then scenarios

### Event Model File

The event model is at `docs/choremonkey.giraflow.json` and includes:
- 5 modules (Household, Members, Invites, Chores, Activity)
- All commands and events
- State view projections
- Test scenarios

## Deployment

- **API**: Azure App Service (itsybitsylist-api.azurewebsites.net)
- **Frontend**: Simply.com (choremonkey.itsybit.se)
- **CI/CD**: GitHub Actions

## Tech Stack

- **Backend**: .NET 10, Minimal APIs, MediatR, FileEventStore
- **Frontend**: React 18, TypeScript, Vite, Tailwind, shadcn/ui
- **Real-time**: SignalR (Azure Basic tier required for WebSockets)
- **Testing**: xUnit, Playwright
