import { test, expect } from '@playwright/test';
import { createHousehold, fillPinInput, uniqueId } from './helpers';

const ADMIN_PIN = '1234';
const MEMBER_PIN = '5678';

async function setMemberPin(page: import('@playwright/test').Page) {
  // Navigate to Admin → Settings tab to set member PIN
  await page.getByRole('link', { name: /admin/i }).click();
  await page.waitForURL(/\/admin/, { timeout: 10000 });
  await page.getByRole('tab', { name: /settings/i }).click();

  await page.locator('#newMemberPin').fill(MEMBER_PIN);
  await page.getByRole('button', { name: /set member pin/i }).click();
  await expect(page.locator('text=/member pin updated/i')).toBeVisible({ timeout: 5000 });
}

async function navigateToTeamTab(page: import('@playwright/test').Page) {
  await page.getByRole('link', { name: /team/i }).click();
  await expect(page.locator('text=Household Overview')).toBeVisible({ timeout: 10000 });
}

test.describe('Team tab — admin view', () => {
  test('shows Household Overview with reassignment hint', async ({ page }) => {
    await createHousehold(page, `Team Test ${uniqueId()}`);
    await navigateToTeamTab(page);

    await expect(page.locator('text=Household Overview')).toBeVisible();
    await expect(page.locator('text=/click the gear icon to reassign/i')).toBeVisible();
  });

  test('shows gear icon when chore is assigned', async ({ page }) => {
    await createHousehold(page, `Team Test ${uniqueId()}`);

    // Add a chore via Admin → Chores tab
    await page.getByRole('link', { name: /admin/i }).click();
    await page.waitForURL(/\/admin/, { timeout: 10000 });
    await page.getByRole('button', { name: /add chore/i }).click();
    await page.getByLabel(/name|title/i).fill('Dishes');
    await page.getByRole('button', { name: /add|create|save/i }).last().click();
    await expect(page.locator('text=Dishes')).toBeVisible({ timeout: 5000 });

    await navigateToTeamTab(page);

    // Expand admin's accordion entry
    await page.locator('[data-radix-collection-item]').first().click();

    // Gear icon should be present for assigned chores
    await expect(page.locator('[title="Edit assignment"]')).toBeVisible({ timeout: 5000 });
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
    await navigateToTeamTab(page);
    await expect(page.locator('text=Household Overview')).toBeVisible();
  });

  test('does not show reassignment hint or gear icons', async ({ page }) => {
    await navigateToTeamTab(page);

    await expect(page.locator('text=/click the gear icon to reassign/i')).not.toBeVisible();
    await expect(page.locator('[title="Edit assignment"]')).not.toBeVisible();
  });
});
