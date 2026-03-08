import { test, expect, type Page } from '@playwright/test';

// Generate unique names to avoid conflicts between test runs
const uniqueId = () => Math.random().toString(36).substring(7);

// Helper: navigate the 2-step CreateHousehold form and wait for the dashboard
async function createHousehold(page: Page, householdName: string, pin = '1234', yourName = 'Tester') {
  await page.goto('/');
  await page.click('text=Create Household');

  // Step 1: Household name + Your name
  await page.getByLabel('Household Name').fill(householdName);
  await page.getByLabel('Your Name').fill(yourName);
  await page.getByRole('button', { name: 'Continue' }).click();

  // Step 2: Admin PIN (both fields must match)
  await page.getByLabel('PIN Code').fill(pin);
  await page.getByLabel('Confirm Admin PIN').fill(pin);
  await page.getByRole('button', { name: 'Create Household' }).click();

  // Wait for redirect to dashboard
  await page.waitForURL(/\/household\//, { timeout: 30000 });
  await expect(page.locator('h1')).toContainText(householdName, { timeout: 15000 });
}

// Helper: fill the PinInput component (4 individual tel inputs, auto-submits when complete)
async function fillPinInput(page: Page, pin: string) {
  const inputs = page.locator('input[type="tel"]');
  await inputs.first().waitFor({ state: 'visible', timeout: 10000 });
  for (let i = 0; i < pin.length; i++) {
    await inputs.nth(i).fill(pin[i]);
  }
}

test.describe('Household Creation', () => {
  test('can create a new household', async ({ page }) => {
    const householdName = `Test Family ${uniqueId()}`;
    await createHousehold(page, householdName);
  });

  test('can access existing household with PIN', async ({ page }) => {
    const householdName = `Access Test ${uniqueId()}`;
    const pin = '5678';

    await createHousehold(page, householdName, pin);

    // Extract household ID from the URL
    const householdId = page.url().split('/household/')[1];

    // Log out — button has aria-label="Log out"
    await page.getByRole('button', { name: /log out/i }).click();

    // App redirects to /access/:id automatically
    await page.waitForURL(`**/access/${householdId}`, { timeout: 15000 });

    // Only one member was created so it is auto-selected; just enter the PIN
    await fillPinInput(page, pin);

    // Should redirect back to the dashboard
    await page.waitForURL(/\/household\//, { timeout: 15000 });
    await expect(page.locator('h1')).toContainText(householdName, { timeout: 10000 });
  });
});

test.describe('Chores', () => {
  let householdUrl: string;

  test.beforeEach(async ({ page }) => {
    const householdName = `Chore Test ${uniqueId()}`;
    await createHousehold(page, householdName);
    householdUrl = page.url();
  });

  test('can add a one-time chore', async ({ page }) => {
    // Click add chore button
    await page.getByRole('button', { name: /add chore/i }).click();

    // Fill chore details
    await page.getByLabel(/name|title/i).fill('Clean garage');
    await page.getByLabel(/description/i).fill('Spring cleaning');

    // Submit
    await page.getByRole('button', { name: /add|create|save/i }).last().click();

    // Chore should appear
    await expect(page.locator('text=Clean garage')).toBeVisible({ timeout: 5000 });
  });

  test('can add a daily chore', async ({ page }) => {
    await page.getByRole('button', { name: /add chore/i }).click();

    await page.getByLabel(/name|title/i).fill('Make bed');

    // Select daily frequency
    await page.getByRole('combobox').first().click();
    await page.getByRole('option', { name: /daily/i }).click();

    await page.getByRole('button', { name: /add|create|save/i }).last().click();

    await expect(page.locator('text=Make bed')).toBeVisible({ timeout: 5000 });
  });

  test('can complete a chore', async ({ page }) => {
    // First add a chore
    await page.getByRole('button', { name: /add chore/i }).click();
    await page.getByLabel(/name|title/i).fill('Test completion');
    await page.getByRole('button', { name: /add|create|save/i }).last().click();

    await expect(page.locator('text=Test completion')).toBeVisible({ timeout: 5000 });

    // Find the chore card and click complete
    const choreCard = page.locator('[class*="card"]').filter({ hasText: 'Test completion' });
    await choreCard.getByRole('button').first().click();

    // Complete dialog should appear
    await page.getByRole('button', { name: /complete|done|confirm/i }).click();

    // Activity timeline should show completion
    await expect(page.locator('text=/completed|finished/i')).toBeVisible({ timeout: 5000 });
  });

  test('can add an optional/bonus chore', async ({ page }) => {
    await page.getByRole('button', { name: /add chore/i }).click();

    await page.getByLabel(/name|title/i).fill('Bonus task');

    // Check optional checkbox
    await page.getByLabel(/optional|bonus/i).check();

    await page.getByRole('button', { name: /add|create|save/i }).last().click();

    // Should appear in bonus section
    await expect(page.locator('text=Bonus task')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('text=/bonus chores/i')).toBeVisible();
  });
});

test.describe('Profile', () => {
  test.beforeEach(async ({ page }) => {
    const householdName = `Profile Test ${uniqueId()}`;
    await createHousehold(page, householdName);
  });

  test('can change nickname', async ({ page }) => {
    // Click avatar to open profile
    await page.locator('[class*="avatar"]').first().click();

    // Change nickname
    await page.getByLabel(/nickname/i).fill('NewName');
    await page.getByRole('button', { name: /save/i }).click();

    // Wait for dialog to close and verify change
    await expect(page.locator('text=NewName')).toBeVisible({ timeout: 5000 });
  });

  test('can set and clear status', async ({ page }) => {
    // Click avatar to open profile
    await page.locator('[class*="avatar"]').first().click();

    // Set status
    await page.getByLabel(/status/i).fill('Busy testing!');
    await page.getByRole('button', { name: /save/i }).click();

    // Avatar should have status ring (pulsing)
    await expect(page.locator('[class*="pulse"]')).toBeVisible({ timeout: 5000 });

    // Reopen and clear
    await page.locator('[class*="avatar"]').first().click();
    await page.getByRole('button', { name: /clear/i }).click();
    await page.getByRole('button', { name: /save/i }).click();

    // Ring should be gone
    await expect(page.locator('[class*="pulse"]')).not.toBeVisible({ timeout: 5000 });
  });
});

test.describe('Invites', () => {
  test('can generate invite link', async ({ page }) => {
    const householdName = `Invite Test ${uniqueId()}`;
    await createHousehold(page, householdName);

    // Click invite
    await page.getByRole('button', { name: /invite/i }).click();

    // Should show invite dialog with link
    await expect(page.locator('text=/invite|link|share/i')).toBeVisible();
    await expect(page.locator('input[readonly]')).toBeVisible();
  });
});

test.describe("What's New", () => {
  test('can open changelog dialog', async ({ page }) => {
    const householdName = `Changelog Test ${uniqueId()}`;
    await createHousehold(page, householdName);

    // Click monkey logo
    await page.locator('text=🐵').click();

    // Should show What's New dialog
    await expect(page.locator("text=/what's new/i")).toBeVisible();
    await expect(page.locator('text=/web:/i')).toBeVisible();
    await expect(page.locator('text=/api:/i')).toBeVisible();
  });
});

