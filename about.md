# ChoreMonkey — Household Chore & Allowance Manager

I built ChoreMonkey to help our family keep track of chores at home. I also wanted the kids to get a preview of what salary day on the 25th feels like — and how skipping things you're obligated to do affects your pay. Hopefully it inspires them to find alternative ways of sustaining themselves financially and not be dependent on the whims of others. :D

This was my first AI-assisted app, where I applied event modeling to describe the system I wanted to build. I set up some initial vertical slices the way I wanted them to work and wired up a simple file-based event store I'd built to save on database costs in experimental apps. After getting a read and write slice working — following the vertical slice architecture that pairs so well with event sourcing — I pretty much let AI build the rest according to the event model.

**Features:**
- Flexible chore scheduling: daily, weekly, interval-based, or one-time
- Multi-member assignment with per-person completion tracking
- Automatic overdue detection with grace periods
- Built-in salary system: base pay, bonus rates for optional chores, deductions for missed ones
- Period-based payouts with history — kids see their own, parents see all
- Activity timeline, team dashboard, invite-based onboarding
- PIN-based access for parents and kids

**Tech:**
- .NET 10 Minimal API with event sourcing
- React + TypeScript + Tailwind CSS + shadcn/ui
- CQRS with MediatR, real-time via SignalR
- 88+ integration tests, Playwright E2E
- CI/CD via GitHub Actions
