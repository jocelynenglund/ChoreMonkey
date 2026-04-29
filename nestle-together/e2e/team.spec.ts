import { test, expect } from '@playwright/test';
import { createHousehold, fillPinInput, navigateToTab, uniqueId } from './helpers';

const ADMIN_PIN = '1234';
const MEMBER_PIN = '5678';

async function setMemberPin(page: import('@playwright/test').Page) {
  await navigateToTab(page, 'admin');
  await page.getByRole('button', { name: /settings/i }).click();

  await page.locator('#newMemberPin').fill(MEMBER_PIN);
  await page.getByRole('button', { name: /set member pin/i }).click();
  await expect(page.locator('text=/member pin updated/i')).toBeVisible({ timeout: 5000 });
}

async function goToTeamTab(page: import('@playwright/test').Page) {
  await navigateToTab(page, 'team');
  await expect(page.locator('text=Household Overview')).toBeVisible({ timeout: 10000 });
}

test.describe('Team tab — admin view', () => {
  test('shows Household Overview with reassignment hint', async ({ page }) => {
    await createHousehold(page, `Team Test ${uniqueId()}`);
    await goToTeamTab(page);

    await expect(page.locator('text=Household Overview')).toBeVisible();
    await expect(page.locator('text=/click the gear icon to reassign/i')).toBeVisible();
  });

  test('shows gear icon when chore is assigned', async ({ page }) => {
    await createHousehold(page, `Team Test ${uniqueId()}`);

    // Chores are added from the main Chores tab, not /admin.
    await navigateToTab(page, 'chores');
    await page.getByRole('button', { name: /add chore/i }).click();
    await page.getByLabel(/chore name/i).waitFor({ state: 'visible' });
    await page.getByLabel(/chore name/i).fill('Dishes');
    const submit = page.getByRole('button', { name: /add chore/i }).last();
    await submit.scrollIntoViewIfNeeded();
    await submit.click();
    await expect(page.locator('text=Dishes')).toBeVisible({ timeout: 5000 });

    // Assign it to everyone via the admin Chores tab so it appears under a
    // member's accordion in the Team overview (the gear only renders for
    // assigned chores).
    await navigateToTab(page, 'admin');
    await page.getByRole('button', { name: /^chores$/i }).click();
    await page.getByTitle('Assign').first().click();
    await page.getByRole('checkbox', { name: /assign to everyone/i }).check();
    await page.getByRole('button', { name: /save/i }).click();
    await expect(page.locator('text=Everyone').first()).toBeVisible({ timeout: 5000 });

    await goToTeamTab(page);

    // Expand admin's accordion entry
    await page.locator('[data-radix-collection-item]').first().click();

    // Gear icon should be present for assigned chores
    await expect(page.locator('[title="Edit assignment"]').first()).toBeVisible({ timeout: 5000 });
  });
});

test.describe('Team tab — member view', () => {
  let householdAccessUrl: string;

  test.beforeEach(async ({ page }) => {
    const dashboardUrl = await createHousehold(page, `Team Test ${uniqueId()}`, ADMIN_PIN);
    await setMemberPin(page);

    // Logout and re-access with member PIN
    householdAccessUrl = dashboardUrl.replace('/household/', '/access/');
    await page.getByRole('button', { name: /log out/i }).click();

    await page.goto(householdAccessUrl);
    await fillPinInput(page, MEMBER_PIN);
    await page.waitForURL(/\/household\//, { timeout: 30000 });
  });

  test('shows Household Overview section', async ({ page }) => {
    await goToTeamTab(page);
    await expect(page.locator('text=Household Overview')).toBeVisible();
  });

  test('does not show reassignment hint or gear icons', async ({ page }) => {
    await goToTeamTab(page);

    await expect(page.locator('text=/click the gear icon to reassign/i')).not.toBeVisible();
    await expect(page.locator('[title="Edit assignment"]')).not.toBeVisible();
  });
});
