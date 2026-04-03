# ChoreMonkey Frontend — Design Architecture Review
_Generated: 2026-04-03_

---

## 1. Feature Inventory

### Onboarding / Auth
- Create a new household (`/create`)
- Join an existing household via invite link (`/join`, `/join/:householdId/:inviteId`)
- Access household with PIN (`/access/:id`) — both admin PIN and member PIN
- Vanity URL entry point (`/h/:slug` → SlugResolver → `/access/:id`)

### Dashboard (`/household/:id`)
- Header: household name, member count, slug copy button, connection status, profile avatar, settings gear, logout
- Members strip: avatars, member status (marquee on click), remove member (admin, hover)
- Team Overview accordion (admin view of all members' overdue/progress)
- My Chores section (personal read model)
- Other Chores section (not assigned to me)
- Bonus Chores section (optional chores)
- Recent Activity timeline
- Add Chore button (admin)
- What's New modal (🐵 logo click)

### Dialogs launched from Dashboard
| Dialog | Trigger | Who sees it |
|--------|---------|------------|
| CompleteChoreDialog | Tap a chore | All |
| ProfileDialog | Tap own avatar | All |
| AllowanceDialog | From ProfileDialog | All |
| InviteDialog | Invite button in members strip | All |
| WhatsNewDialog | 🐵 logo | All |
| SettingsDialog | ⚙️ gear icon | Admin only |
| SalaryManagementDialog (AdminPanel) | Button inside SettingsDialog | Admin only |
| RemoveMemberDialog | Hover on member avatar (admin) | Admin only |

### Admin Panel (inside SalaryManagementDialog)
Two tabs: Chores | Salaries

**Chores tab (ChoreManagement.tsx):**
- List required + bonus chores
- Edit deduction/bonus rates per chore
- Assign chores to members or everyone
- Delete chores

**Salaries tab (SalaryManagement.tsx):**
- View current period (month)
- Set per-member base salary + deduction/bonus multipliers
- Close period & generate payslips

### Salary Feature (`features/salary/`)
- `SalaryAdmin.tsx` — full admin view: current period + payout history + payslip viewer (⚠️ NOT connected to any dialog)
- `MyAllowance.tsx` — member's own allowance/salary view (launched from ProfileDialog via AllowanceDialog)
- `SalarySlip.tsx` — renders an official payslip
- API: getCurrentPeriod, setMemberSalary, closePeriod, getPayoutHistory, getOfficialSalarySlip

### Settings (inside SettingsDialog)
- Set/change vanity URL slug
- Change admin PIN
- Set member PIN
- Button to open Admin Panel

---

## 2. Component Size Analysis

| File | Lines | Status |
|------|-------|--------|
| `pages/HouseholdDashboard.tsx` | 493 | 🔴 Way too big |
| `components/SettingsDialog.tsx` | ~260 | 🟠 Big |
| `features/admin/components/ChoreManagement.tsx` | 247 | 🟠 Getting large |
| `features/admin/components/SalaryManagement.tsx` | ~230 | 🟠 Getting large |
| `features/salary/components/SalaryAdmin.tsx` | ~280 | 🟠 Orphaned & duplicate |
| `pages/SlugResolver.tsx` | 51 | ✅ Fine |

### Key Problems Found

1. **Admin is buried 2 levels deep**: gear → Settings dialog → "Open Admin Panel" button → AdminPanel dialog. On mobile this is off-screen.

2. **Duplicate salary admin components**: `features/salary/SalaryAdmin.tsx` (full-featured, has payslip history) is never used. `features/admin/SalaryManagement.tsx` (simpler, no history) is used instead. Payslips exist in code but are unreachable in the UI.

3. **HouseholdDashboard manages too much state**: 10+ useState hooks, all dialog open/close state, all data fetching, all handlers. It's a God component.

4. **SettingsDialog does too much**: PIN changes + slug management + admin panel launcher all crammed into one dialog.

5. **Vanity URL is not first-class**: `/h/:slug` resolves correctly but slug management is buried inside Settings which is already buried.

---

## 3. Proposed Architecture

### Navigation Model: Bottom Tab Bar (mobile-first)

Replace the single dashboard page with a proper tab-based layout. The URL stays `/household/:id` but we render sub-views via a tab state (or sub-routes).

```
/household/:id
  → tab: chores     (default) — My Chores + Other + Bonus
  → tab: team       — Members strip + Team Overview
  → tab: activity   — Timeline
  → tab: admin      — Admin Panel (visible only to admins)
```

The **Admin tab** replaces the gear → Settings → Admin Panel chain. Admins see a 4th tab. Not hidden in a dialog.

### Folder Structure

```
src/
  pages/
    Index.tsx
    CreateHousehold.tsx
    JoinHousehold.tsx
    AccessHousehold.tsx
    NotFound.tsx
    SlugResolver.tsx        ← keep as-is, it's fine

  household/                ← new: replaces HouseholdDashboard
    HouseholdShell.tsx      ← layout: header + bottom tabs + outlet
    tabs/
      ChoresTab.tsx         ← My Chores + Other + Bonus sections
      TeamTab.tsx           ← Members strip + Team Overview accordion
      ActivityTab.tsx       ← CompletionTimeline
      AdminTab.tsx          ← admin-only, wraps ChoreManagement + SalaryAdmin

  features/
    chores/
      components/
        ChoreCard.tsx
        AddChoreDialog.tsx
        CompleteChoreDialog.tsx
        MyChoresSection.tsx
        OverdueAccordion.tsx
      ChoreManagement.tsx   ← moved here from features/admin/
    
    salary/
      components/
        SalaryAdmin.tsx     ← keep, DELETE SalaryManagement.tsx (duplicate)
        MyAllowance.tsx
        SalarySlip.tsx
      api.ts
      types.ts
    
    household/
      components/
        MemberStrip.tsx     ← extracted from dashboard
        RemoveMemberDialog.tsx
        InviteDialog.tsx
        ProfileDialog.tsx
        TeamOverviewAccordion.tsx
      api.ts
    
    settings/               ← new: split SettingsDialog into smaller pieces
      components/
        SlugSettings.tsx    ← vanity URL management
        PinSettings.tsx     ← admin + member PIN change
        SettingsSheet.tsx   ← thin wrapper (Sheet/drawer, not Dialog)

  components/
    ui/                     ← shadcn, keep as-is
    MemberAvatar.tsx
    ConnectionStatus.tsx
    NavLink.tsx
    WhatsNewDialog.tsx
    AllowanceDialog.tsx
```

### Header Simplification

Current header has: logo | name + slug | connection | avatar | settings gear | logout

Proposed: logo | name | spacer | avatar (→ Profile sheet)

- Settings moves to the **Admin tab** (no more buried gear icon)
- Vanity slug shown in header as read-only, copyable (keep current behaviour)
- Logout moves inside Profile sheet

### Vanity URL as First-Class Concern

- `SlugResolver` stays, already works well
- Slug management lives in **Settings section of Admin tab**, not buried in a dialog
- `choremonkey.itsybit.se/h/myfamily` lands → resolves → access page → dashboard

---

## 4. Migration Plan

Do this in small safe steps, one PR each:

### Step 1 — Delete the dead code
- Delete `features/salary/SalaryAdmin.tsx` orphan... wait, actually **keep SalaryAdmin, delete SalaryManagement** (SalaryAdmin has payout history + payslips, SalaryManagement doesn't)
- Delete `features/admin/components/SalaryManagement.tsx`
- Wire `AdminPanel` to use `SalaryAdmin` instead
- **Result**: payslip history becomes accessible immediately ✅

### Step 2 — Extract HouseholdDashboard state into a hook
- Create `hooks/useHouseholdData.ts` — data fetching + refresh logic
- Create `hooks/useHouseholdActions.ts` — all handlers (addChore, completeChore, etc.)
- HouseholdDashboard becomes a thin layout component
- **Result**: 493 lines → ~150 lines, logic testable in isolation

### Step 3 — Extract tab sections
- Create `ChoresTab.tsx` (My Chores + Other + Bonus)
- Create `TeamTab.tsx` (Members strip + Team Overview)
- Create `ActivityTab.tsx` (Timeline)
- Wire into HouseholdDashboard with simple tab state
- **Result**: Dashboard is now a shell, tabs are focused components

### Step 4 — Admin tab
- Create `AdminTab.tsx` — visible only to admins
- Move ChoreManagement + SalaryAdmin into it
- Move SettingsDialog content (PIN + slug) into AdminTab sections
- Add tab to nav (visible to admins only)
- Remove the gear icon from header
- **Result**: Admin features are a proper first-class section, not buried

### Step 5 — Clean up settings
- Split SettingsDialog into `SlugSettings` + `PinSettings` components
- Use a Sheet (drawer) instead of Dialog for settings — better on mobile
- **Result**: Each settings concern is independently maintainable

### Step 6 — Folder reorganisation
- Move components to feature folders (chores/, household/, salary/)
- Update imports
- **Result**: Codebase matches mental model

---

## 5. Open Questions

1. **Bottom tabs vs top tabs?** Bottom tab bar is more mobile-friendly (thumb reach). Top tabs keep the current aesthetic. Which do you prefer?
-- let's go with bottom tabs, i guess it would work in desktop too? 

2. **Admin tab vs separate route?** Could do `/household/:id/admin` as a full page instead of a tab. Easier to link to directly. Worth it?
-- yes this makes sense. We should do a check for the admin pin here 

3. **Profile + Logout placement?** Currently: avatar → ProfileDialog → inside dialog you can view allowance. Proposed: avatar → Profile Sheet (slides up), with logout at the bottom of the sheet. Ok?
-- yes!

4. **Vanity URL in onboarding?** Should slug setup be offered during household creation, or only discoverable in settings after the fact?
-- yes, this is a good enhancement for onboarding.

5. **SalaryAdmin component name?** It's used for admin management but there's also a member-facing `MyAllowance`. Renaming to `SalaryManagement` (dropping the duplicate) would be cleaner — agree?
-- yes that's much cleaner. 

