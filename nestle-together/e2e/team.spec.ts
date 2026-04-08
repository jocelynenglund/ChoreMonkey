import { test, expect } from '@playwright/test';

const uniqueId = () => Math.random().toString(36).substring(7);

const ADMIN_PIN = '1234';
const MEMBER_PIN = '5678';

async function createHousehold(page: import('@playwright/test').Page) {
  const householdName = `Team Test ${uniqueId()}`;
  await page.goto('/');
  await page.click('text=Create Household');
  await page.getByLabel(/household name/i).fill(householdName);
  await page.getByLabel(/your name|nickname/i).first().fill('Admin');
  await page.getByRole('button', { name: /continue/i }).click();
  await page.getByLabel(/admin.*pin|pin.*code/i).first().fill(ADMIN_PIN);
  await page.getByRole('button', { name: /create/i }).click();
  await page.waitForURL(/\/household\//, { timeout: 45000 });
  await expect(page.locator('h1')).toContainText(householdName, { timeout: 10000 });
  return page.url();
}

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
    await createHousehold(page);
    await navigateToTeamTab(page);

    await expect(page.locator('text=Household Overview')).toBeVisible();
    await expect(page.locator('text=/click the gear icon to reassign/i')).toBeVisible();
  });

  test('shows gear icon when chore is assigned', async ({ page }) => {
    await createHousehold(page);

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
    const dashboardUrl = await createHousehold(page);
    await setMemberPin(page);

    // Logout and re-access with member PIN
    householdAccessUrl = dashboardUrl.replace('/household/', '/access/');
    await page.getByRole('button', { name: /logout|log out/i }).click();

    await page.goto(householdAccessUrl);
    await page.getByLabel(/pin/i).fill(MEMBER_PIN);
    await page.getByRole('button', { name: /access|enter|submit/i }).click();
    await page.waitForURL(/\/household\//, { timeout: 45000 });
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
