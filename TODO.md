# ChoreMonkey TODO
_Last updated: 2026-04-03_

## 🐛 Known Bugs / Fixes Needed

- [ ] **ClosePeriod: February with no events** — closing a period that has zero chore completions works but produces empty payslips. Consider showing a warning "No activity found for this period" before closing.
- [ ] **Available periods list may miss old periods** — `GetAvailablePeriods` walks back 24 months max and uses `HouseholdCreated` timestamp to limit. If the timestamp parsing fails (e.g. non-UTC format), periods before a certain date may not appear. Add a test for this.
- [ ] **Duplicate PeriodClosed events** — possible to close the same period twice in quick succession (race condition). Backend now handles it gracefully but we should add an idempotency check at the command level.
- [ ] **SignalR disabled on Azure Free tier** — WebSockets not supported. Either upgrade to Azure Basic (~13 USD/month) or switch to polling fallback. `ConnectionStatus` component shows this gracefully already.

## 🏗️ Architecture / Tech Debt

- [ ] **`src/types/household.ts` + `src/stores/householdStore.ts`** — both have TODO comments to migrate to feature-based imports. Low priority but should happen in Step 6 (folder reorganisation).
- [ ] **`ChoreManagement.tsx` imports from `../../store`** — should import from `@/stores/householdStore` for consistency.
- [ ] **`FamilyQuest` feature** — Party, XP, Quests, Victories, Calendar endpoints exist in backend but appear unused in the frontend. Confirm if this is abandoned or upcoming. Clean up or wire up.
- [ ] **Step 6 (folder reorganisation)** — move components into feature folders (chores/, household/, salary/). Low urgency since it's cosmetic, but keeps the codebase honest.
- [ ] **`SalaryAdmin.tsx` fallback render** — the "fallback to history" path in SalaryAdmin is a workaround for `GetAvailablePeriods` failures. Once the root cause is fixed, the fallback can be removed.

## 🧪 Missing Tests

- [ ] **ClosePeriod integration tests** — `Salary/` folder only has `ChoreRatesTests.cs`. Need tests for:
  - Closing a period that hasn't ended (should 400)
  - Closing an already-closed period (should 400)
  - Closing a specific past period by date
  - `GetAvailablePeriods` — returns correct closed/open state
- [ ] **Vanity URL tests** — `SetHouseholdSlug` + `GetHouseholdBySlug` have no integration test coverage.
- [ ] **`GetCurrentPeriod` with payday boundary** — test that period boundaries flip correctly on payday day itself.

## ✨ Features / Enhancements

- [ ] **Vanity URL in onboarding** — slug setup offered during household creation, not just in settings after the fact. (Agreed in design doc.)
- [ ] **Payday configurator UI** — `SetPayday` endpoint exists in backend but there's no UI to change it. Currently hardcoded to 25th. Add to Admin → Settings tab.
- [ ] **Profile sheet with logout** — per design doc, avatar → Profile Sheet (slides up) with logout at bottom. Currently logout is a button in the header.
- [ ] **Acknowledge-missed UI** — `AcknowledgeMissed` command exists in backend, no UI. Useful for "we agreed to skip this week".
- [ ] **Chore history view** — `ChoreHistory` query exists, not exposed in UI. Could be useful in chore detail or admin view.
- [ ] **Salary: deduction multiplier display** — when editing a member's salary, the deduction/bonus multipliers reset to 1.0 every time (not loaded from current config). Should pre-fill with last saved values.
- [ ] **Mobile: bottom tab bar safe area** — on iOS the tab bar needs `padding-bottom: env(safe-area-inset-bottom)` to avoid the home indicator overlap.
- [ ] **Admin tab visibility** — currently only shows if `isAdmin` from store. If someone logs in as a member then the admin logs in on the same device, the tab won't appear without re-login. Consider showing tab always but gating at the route level only.
- [ ] **Slug uniqueness error message** — when slug is already taken, the API returns an error but the message isn't very user-friendly. Improve error text.

## 🚀 Infrastructure

- [ ] **Azure upgrade for SignalR** — Free → Basic tier to enable WebSockets and real-time updates.
- [ ] **CI: add integration test run** — tests exist but not clear if CI runs them. Check GitHub Actions workflow.
- [ ] **Frontend deploy: cache busting** — check if FTP deploy to Simply.com properly busts browser cache for JS chunks after deploy.
