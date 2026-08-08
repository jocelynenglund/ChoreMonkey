# ChoreMonkey TODO
_Last updated: 2026-05-01_

## 🔒 Security (in progress)

- [x] **Server-issued session cookie** — `cm.session` cookie + `IHouseholdPrincipal` middleware (PR-A, #43). Additive only; nothing enforces it yet. _(2026-05-01)_
- [ ] **Endpoint authorization (PR-B)** — admin/mutation endpoints (`SetMemberSalary`, `SetChoreRates`, `SetPayday`, `ClosePeriod`, `AddChore`, `ChangeMemberNickname`, etc.) currently accept anyone with the household ID. Add `.RequireAdmin()` / `.RequireMember()` filters reading from `IHouseholdPrincipalAccessor`.
- [ ] **Drop client-side PIN storage (PR-B)** — `currentPinCode` is persisted in localStorage so the SPA can re-send it on every mutation. Once endpoints read identity from the cookie, remove `currentPinCode` from Zustand `partialize` and stop sending it in request bodies / `X-Pin-Code` headers.
- [ ] **`GetOfficialSalarySlip` leaks any member's slip (PR-C)** — `GET /api/households/{id}/salary/periods/{periodId}/slip/{memberId}` has no auth check; any caller with the IDs gets the payslip. Require admin OR `memberId == principal.MemberId`.
- [ ] **Rate-limit mutations (PR-D)** — only `auth` endpoints have a rate limit; admin mutations are unbounded. Apply `api` policy globally and tighten `auth` to a sliding window.

## ✅ Correctness

- [ ] **Payday boundary inconsistency** — `GetCurrentPeriod/Handler.cs:195` uses `today <= paydayThisMonth`; `ClosePeriod/Handler.cs:239` uses `today >= paydayThisMonth`. On the exact payday the two handlers disagree about which period is "current" vs "completed". Pick one convention and centralize.
- [ ] **Grace period asymmetry** — `GetCurrentPeriod` has `GracePeriodDays = 2` before counting a daily chore as missed; `ClosePeriod` uses zero grace. Preview slips and official slips therefore disagree on borderline cases — exactly what the preview is meant to predict.
- [ ] **`ExpectedVersion.Any` everywhere** — ~20 `AppendToStreamAsync(..., ExpectedVersion.Any)` calls disable optimistic concurrency. Two simultaneous "Close Period" or salary edits will silently double-write. Capture `ExpectedVersion` from the read where it matters.
- [ ] **No event versioning story** — events in `ChoreMonkey.Events/` are bare records. Adding a field will break replays of old events. Tag events with a discriminator/version while streams are still small.
- [ ] **Duplicate PeriodClosed events** — possible to close the same period twice in quick succession (race condition). Backend now handles it gracefully but we should add an idempotency check at the command level.

## 🐛 Known Bugs / Fixes Needed

- [x] **ClosePeriod: February with no events** — closing a period that has zero chore completions works but produces empty payslips. Warning shown before closing if no activity detected. _(2026-04-08)_
- [ ] **Available periods list may miss old periods** — `GetAvailablePeriods` walks back 24 months max and uses `HouseholdCreated` timestamp to limit. If the timestamp parsing fails (e.g. non-UTC format), periods before a certain date may not appear. Add a test for this.
- [ ] **SignalR disabled on Azure Free tier** — WebSockets not supported. Either upgrade to Azure Basic (~13 USD/month) or switch to polling fallback. `ConnectionStatus` component shows this gracefully already.

## 🏗️ Architecture / Tech Debt

- [ ] **`src/types/household.ts` + `src/stores/householdStore.ts`** — both have TODO comments to migrate to feature-based imports. Low priority but should happen in Step 6 (folder reorganisation).
- [x] **`ChoreManagement.tsx` imports from `../../store`** — fixed to import from `@/stores/householdStore`. _(2026-04-08)_
- [x] **`FamilyQuest` integration** — Party, XP, Quests, Victories, Calendar endpoints are part of an external integration; they exist in the backend on purpose and have no frontend wiring by design. Don't flag as dead code. _(2026-05-01)_
- [ ] **Step 6 (folder reorganisation)** — move components into feature folders (chores/, household/, salary/). Low urgency since it's cosmetic, but keeps the codebase honest.
- [ ] **`SalaryAdmin.tsx` fallback render** — the "fallback to history" path in SalaryAdmin is a workaround for `GetAvailablePeriods` failures. Once the root cause is fixed, the fallback can be removed.
- [ ] **Duplicated chore/missed-chore math** — `ChoreCreated → apply ChoreUpdated → filter ChoreDeleted` is copy-pasted across ~13 handlers; `CalculateDailyMissed/Weekly/Interval` are identical in `GetCurrentPeriod` and `ClosePeriod`. Extract to a shared projection/util so the grace-period asymmetry above becomes a one-line config.

## 🧪 Missing Tests

- [ ] **ClosePeriod integration tests** — `Salary/` folder only has `ChoreRatesTests.cs`. Need tests for:
  - Closing a period that hasn't ended (should 400)
  - Closing an already-closed period (should 400)
  - Closing a specific past period by date
  - `GetAvailablePeriods` — returns correct closed/open state
- [ ] **Vanity URL tests** — `SetHouseholdSlug` + `GetHouseholdBySlug` have no integration test coverage.
- [ ] **`GetCurrentPeriod` with payday boundary** — test that period boundaries flip correctly on payday day itself (covers the correctness item above).
- [ ] **Frontend unit tests** — `nestle-together/src/test/example.test.ts` is a stub. No coverage of `SalaryAdmin` form validation, period selector, or `useHouseholdRealtime` cache invalidation.
- [ ] **E2E: salary admin** — preview slip flow, close period button state, period dropdown. Currently no e2e coverage of the salary feature.

## ✨ Features / Enhancements

- [ ] **Vanity URL in onboarding** — slug setup offered during household creation, not just in settings after the fact. (Agreed in design doc.)
- [x] **Payday configurator UI** — Added to Admin → Settings tab. _(2026-04-03)_
- [ ] **Profile sheet with logout** — per design doc, avatar → Profile Sheet (slides up) with logout at bottom. Currently logout is a button in the header.
- [x] **Acknowledge-missed UI** — `MyChoresSection.tsx` has the "Didn't do – acknowledge and dismiss" button wired to the `acknowledgeMissed` store action. _(2026-05-01)_
- [x] **Chore history view** — `ChoreCard.tsx` lazily loads `fetchChoreHistory` when expanded. _(2026-05-01)_
- [x] **Salary: deduction multiplier display** — multipliers now pre-filled from last saved values. _(2026-04-03)_
- [x] **Mobile: bottom tab bar safe area** — fixed with `env(safe-area-inset-bottom)` + `viewport-fit=cover`. _(2026-04-03)_
- [x] **Admin tab visibility** — tab always rendered; visually dimmed and non-interactive for non-admins (`pointer-events-none`, `tabIndex=-1`). Route still gates access. _(2026-04-08)_
- [x] **Slug uniqueness error message** — maps "taken/conflict/already/exist" responses to "That URL is already taken, please try another." _(2026-04-08)_
- [x] **`/changelog` page + landing footer link** — full project history at `/changelog`, in-app modal still shows recent slice. _(2026-05-01)_

## 🚀 Infrastructure

- [ ] **Azure upgrade for SignalR** — Free → Basic tier to enable WebSockets and real-time updates.
- [x] **CI: integration test run** — `.github/workflows/ci.yml` `test-backend` job runs `dotnet test ChoreMonkey.IntegrationTests` on every PR. _(2026-05-01)_
- [x] **CI: e2e job waits for /health** — replaced `sleep 10 + curl || echo skipped` with a 60s polling loop and uploads API logs on failure. _(2026-04-26, #42)_
- [x] **Frontend deploy: cache busting** — Vite emits hash-suffixed asset filenames (`index-<hash>.js/css`); the index.html references the hashed names so each deploy invalidates automatically. _(2026-05-01)_

## 📝 Conventions

- **Skip the public changelog for a commit** — include the literal token `[skip-changelog]` anywhere in the commit subject. The generator (`scripts/generate-changelog.js`) drops those entries before writing `public/changelog.json`. Useful for security fixes pre-rollout, internal refactors, dependency bumps.
