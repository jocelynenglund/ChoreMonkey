# ChoreMonkey TODO
_Last updated: 2026-04-07_

## 🐛 Known Bugs / Fixes Needed

- [ ] **ClosePeriod: February with no events** — closing a period that has zero chore completions works but produces empty payslips. Consider showing a warning "No activity found for this period" before closing.
- [ ] **Available periods list may miss old periods** — `GetAvailablePeriods` walks back 24 months max and uses `HouseholdCreated` timestamp to limit. If the timestamp parsing fails (e.g. non-UTC format), periods before a certain date may not appear. Add a test for this.
- [ ] **Duplicate PeriodClosed events** — possible to close the same period twice in quick succession (race condition). Backend now handles it gracefully but we should add an idempotency check at the command level.
- [ ] **SignalR disabled on Azure Free tier** — WebSockets not supported. Either upgrade to Azure Basic (~13 USD/month) or switch to polling fallback. `ConnectionStatus` component shows this gracefully already.
- [ ] **SalaryAdmin `as any` cast** — `SalaryAdmin.tsx:56` uses `(historyData as any)?.periods` to handle an unexpected response shape. This hides potential API contract mismatches; type the response properly or fix the API response.
- [ ] **Slug uniqueness error message** — when slug is already taken, the API returns an error but the message isn't very user-friendly. Improve error text.

## 🏗️ Architecture / Tech Debt

- [ ] **`src/types/household.ts` + `src/stores/householdStore.ts`** — both have TODO comments to migrate to feature-based imports. Low priority but should happen in Step 6 (folder reorganisation).
- [ ] **`ChoreManagement.tsx` imports from `../../store`** — should import from `@/stores/householdStore` for consistency.
- [ ] **`FamilyQuest` feature — dead code in backend** — 5 query handlers (Party, XP, Quests, Victories, Calendar) exist in `ChoreMonkey.Core/Feature/FamilyQuest/` but have zero frontend consumers (no imports in any `.ts`/`.tsx` files). Confirm if abandoned or upcoming; clean up or wire up.
- [ ] **`Stats/PlatformStats` query unused** — `ChoreMonkey.Core/Feature/Stats/Queries/PlatformStats` exists but doesn't appear to have a frontend consumer. Decide if this is admin-only or dead code.
- [ ] **Step 6 (folder reorganisation)** — move components into feature folders (chores/, household/, salary/). Low urgency since it's cosmetic, but keeps the codebase honest. ~15 components still in `src/components/` that could live in feature folders.
- [ ] **`SalaryAdmin.tsx` fallback render** — the "fallback to history" path in SalaryAdmin is a workaround for `GetAvailablePeriods` failures. Once the root cause is fixed, the fallback can be removed.
- [ ] **Console logging cleanup** — 9 `console.log/warn/error` calls scattered across 5 frontend files (`signalr.ts`, `JoinHousehold.tsx`, `NotFound.tsx`, `WhatsNewDialog.tsx`, `CompletionTimeline.tsx`). Replace with a proper logger or remove before production.

## 🧪 Missing Tests

- [ ] **ClosePeriod integration tests** — `Salary/` folder only has `ChoreRatesTests.cs`. Need tests for:
  - Closing a period that hasn't ended (should 400)
  - Closing an already-closed period (should 400)
  - Closing a specific past period by date
  - `GetAvailablePeriods` — returns correct closed/open state
- [ ] **Vanity URL tests** — `SetHouseholdSlug` + `GetHouseholdBySlug` have no integration test coverage.
- [ ] **`GetCurrentPeriod` with payday boundary** — test that period boundaries flip correctly on payday day itself.
- [ ] **Frontend unit tests are empty** — `src/test/example.test.ts` is a placeholder (`expect(true).toBe(true)`). No real unit tests exist for any frontend logic (stores, API helpers, components).
- [ ] **CI does not run frontend tests** — the CI workflow builds the frontend and runs e2e, but never runs `npm run test` (vitest unit tests). Add a `test-frontend` job.
- [ ] **FamilyQuest endpoints untested** — 5 backend query handlers with zero test coverage. If they're staying, they need tests.

## ✨ Features / Enhancements

- [ ] **Vanity URL in onboarding** — slug setup offered during household creation, not just in settings after the fact. (Agreed in design doc.)
- [x] **Payday configurator UI** — Added to Admin → Settings tab. _(2026-04-03)_
- [ ] **Profile sheet with logout** — per design doc, avatar → Profile Sheet (slides up) with logout at bottom. Currently logout is a button in the header.
- [ ] **Acknowledge-missed UI** — `AcknowledgeMissed` command exists in backend (with integration tests), no UI. Useful for "we agreed to skip this week".
- [ ] **Chore history view** — `ChoreHistory` query exists, not exposed in UI. Could be useful in chore detail or admin view.
- [x] **Salary: deduction multiplier display** — multipliers now pre-filled from last saved values. _(2026-04-03)_
- [x] **Mobile: bottom tab bar safe area** — fixed with `env(safe-area-inset-bottom)` + `viewport-fit=cover`. _(2026-04-03)_
- [ ] **Admin tab visibility** — currently only shows if `isAdmin` from store. If someone logs in as a member then the admin logs in on the same device, the tab won't appear without re-login. Consider showing tab always but gating at the route level only.
- [ ] **FamilyQuest frontend** — if the backend feature (Party, XP, Quests, Victories, Calendar) is intended to ship, it needs a full frontend implementation. Otherwise, remove the backend code.

## 🚀 Infrastructure

- [ ] **Azure upgrade for SignalR** — Free → Basic tier to enable WebSockets and real-time updates.
- [ ] **CI: add frontend unit test job** — `npm run test` is never executed in CI. Add a `test-frontend` step to the CI workflow alongside `test-backend`.
- [ ] **CI: add integration test run** — tests exist but CI only runs them on PRs. Consider running on push to main as well.
- [ ] **Frontend deploy: cache busting** — FTP deploy via `SamKirkland/FTP-Deploy-Action` to Simply.com. Verify that browser cache is properly busted for JS chunks after deploy (Vite uses content hashes, but old `index.html` may be cached).
- [ ] **E2E cold-start fragility** — e2e CI job pings the Azure Free tier API with 3 retries + 10s sleep to warm it up. This is fragile; consider a health-check wait loop or upgrading the tier.
- [ ] **Staging deploy triggers** — staging workflow triggers on `feature/**` and `refactor/**` branches but not on `fix/**` or `claude/**` branches. Consider broadening the trigger pattern.
