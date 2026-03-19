# ChoreMonkey - Household Chore Management

Event-sourced household chore management with salary/allowance tracking.

## Repos

| Component | Repo | Local Path |
|-----------|------|------------|
| **Backend + Frontend** | [jocelynenglund/ChoreMonkey](https://github.com/jocelynenglund/ChoreMonkey) (upstream) | `~/ChoreMonkey/` |
| **Fork** | [itsybit-agent/ChoreMonkey](https://github.com/itsybit-agent/ChoreMonkey) | same |

**Git remotes:**
- `origin` → itsybit-agent/ChoreMonkey (fork)
- `upstream` → jocelynenglund/ChoreMonkey (canonical)

## Project Structure

```
ChoreMonkey/
├── ChoreMonkey.Api/              # .NET Minimal API
├── ChoreMonkey.Core/             # Business logic
│   └── Feature/                  # Vertical slices
│       ├── Chores/               # Chore CRUD, completion, assignment
│       ├── Household/            # Household management
│       ├── Members/              # Member profiles, PINs
│       ├── Salary/               # Allowance system
│       └── Activity/             # Timeline, team overview
├── ChoreMonkey.Events/           # Event definitions
├── ChoreMonkey.IntegrationTests/ # 88 integration tests
├── nestle-together/              # React frontend
│   └── src/
│       ├── features/             # Feature modules
│       │   ├── admin/            # Admin panel (chores + salary tabs)
│       │   ├── chores/           # Chore types, API
│       │   └── salary/           # Allowance components
│       ├── components/           # Shared UI components
│       └── pages/                # Page components
└── data/                         # Local FileEventStore data
```

## Deployment

### API (.NET/Azure)

| Environment | URL |
|-------------|-----|
| **Production** | https://itsybitsylist-api.azurewebsites.net |

**Health check:** `curl https://itsybitsylist-api.azurewebsites.net/health`

**Deploy:** Push to `main` → GitHub Actions builds & deploys to Azure

### Frontend (React/Simply.com)

| Environment | URL |
|-------------|-----|
| **Production** | http://choremonkey.itsybit.se |

**Deploy:** Push to `main` → GitHub Actions builds & deploys via FTP (requires `FTP_USERNAME` + `FTP_PASSWORD` secrets)

## Development

### Run API locally
```bash
cd ~/ChoreMonkey
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet run --project ChoreMonkey.Api
```

### Run tests
```bash
cd ~/ChoreMonkey
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet test ChoreMonkey.IntegrationTests
```

### Run frontend locally
```bash
cd ~/ChoreMonkey/nestle-together
npm run dev
```

## Key Features

### Chores
- Daily, weekly, interval, one-time frequencies
- Multi-assignee with per-member completion tracking
- Optional (bonus) vs Required chores
- Overdue detection based on frequency

### Salary/Allowance System
- Per-member base salary + multiplier
- Per-chore rates: `deductionRate` (required) or `bonusRate` (optional)
- Auto-missed detection (>2 days overdue)
- Period close with payout history
- Kids see own salary, admins see all

### Events (FileEventStore)
- `HouseholdCreated`, `MemberJoinedHousehold`
- `ChoreCreated`, `ChoreAssigned`, `ChoreCompleted`, `ChoreDeleted`
- `MemberSalarySet`, `ChoreRatesSet`, `PeriodClosed`

## API Endpoints

Base: `https://itsybitsylist-api.azurewebsites.net/api`

### Households
- `POST /households` - Create household
- `POST /households/{id}/invite` - Generate invite
- `POST /households/{id}/join` - Join via invite

### Chores
- `GET /households/{id}/chores` - List chores (includes rates)
- `POST /households/{id}/chores` - Add chore
- `POST /households/{id}/chores/{choreId}/complete` - Mark complete
- `POST /households/{id}/chores/{choreId}/assign` - Assign members
- `DELETE /households/{id}/chores/{choreId}` - Delete chore

### Salary
- `POST /households/{id}/chores/{choreId}/rates` - Set chore rates
- `POST /households/{id}/members/{memberId}/salary` - Set member salary
- `GET /households/{id}/salary/current` - Get current period preview
- `POST /households/{id}/salary/close` - Close period, finalize payouts
- `GET /households/{id}/salary/history` - Past payouts

## Notes

- Uses FileEventStore (file-based event sourcing)
- NuGet feed: `https://nuget.pkg.github.com/jocelynenglund/index.json`
- SignalR disabled (Azure Free tier doesn't support WebSockets)
- Frontend uses client-generated IDs for idempotency
