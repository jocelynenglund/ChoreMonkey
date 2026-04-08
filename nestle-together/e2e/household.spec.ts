import { test, expect } from '@playwright/test';
import { createHousehold, fillPinInput, uniqueId } from './helpers';

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
    const url = page.url();
    const householdId = url.split('/household/')[1];

    // Wait for logout button to be visible (confirms dashboard is loaded)
    const logoutButton = page.getByRole('button', { name: /log out/i });
    await logoutButton.waitFor({ state: 'visible', timeout: 15000 });
    await logoutButton.click();

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

    // Wait for dialog and fill chore details
    await page.getByLabel(/chore name/i).waitFor({ state: 'visible' });
    await page.getByLabel(/chore name/i).fill('Clean garage');
    await page.getByLabel(/description/i).fill('Spring cleaning');

    // Scroll submit button into view and click
    const submitButton = page.getByRole('button', { name: /add chore/i }).last();
    await submitButton.scrollIntoViewIfNeeded();
    await submitButton.click();

    // Chore should appear
    await expect(page.locator('text=Clean garage')).toBeVisible({ timeout: 10000 });
  });

  test('can add a daily chore', async ({ page }) => {
    await page.getByRole('button', { name: /add chore/i }).click();

    // Wait for dialog to be visible
    await page.getByLabel(/chore name/i).waitFor({ state: 'visible' });
    await page.getByLabel(/chore name/i).fill('Make bed');

    // Select daily frequency
    await page.getByRole('combobox').click();
    await page.getByRole('option', { name: /every day/i }).click();

    // Scroll submit button into view and click
    const submitButton = page.getByRole('button', { name: /add chore/i }).last();
    await submitButton.scrollIntoViewIfNeeded();
    await submitButton.click();

    await expect(page.locator('text=Make bed')).toBeVisible({ timeout: 10000 });
  });

  test('can complete a chore', async ({ page }) => {
    // First add a chore
    await page.getByRole('button', { name: /add chore/i }).click();
    await page.getByLabel(/chore name/i).waitFor({ state: 'visible' });
    await page.getByLabel(/chore name/i).fill('Test completion');

    const submitButton = page.getByRole('button', { name: /add chore/i }).last();
    await submitButton.scrollIntoViewIfNeeded();
    await submitButton.click();

    await expect(page.locator('text=Test completion')).toBeVisible({ timeout: 10000 });

    // Find the chore card and click the complete button
    const choreCard = page.locator('[class*="card"]').filter({ hasText: 'Test completion' });
    await choreCard.getByRole('button').first().click();

    // Complete dialog should appear - click confirm
    await page.getByRole('button', { name: /complete|done|confirm/i }).click();

    // Activity timeline should show completion
    await expect(page.locator('text=/completed|finished/i')).toBeVisible({ timeout: 10000 });
  });

  test('can add an optional/bonus chore', async ({ page }) => {
    await page.getByRole('button', { name: /add chore/i }).click();

    // Wait for dialog
    await page.getByLabel(/chore name/i).waitFor({ state: 'visible' });
    await page.getByLabel(/chore name/i).fill('Bonus task');

    // Toggle the Bonus Chore switch
    await page.getByRole('switch', { name: /bonus chore/i }).click();

    // Scroll submit button into view and click
    const submitButton = page.getByRole('button', { name: /add chore/i }).last();
    await submitButton.scrollIntoViewIfNeeded();
    await submitButton.click();

    await expect(page.getByRole('heading', { name: 'Bonus task' })).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=/bonus chores/i')).toBeVisible({ timeout: 5000 });
  });
});

test.describe('Profile', () => {
  test.beforeEach(async ({ page }) => {
    const householdName = `Profile Test ${uniqueId()}`;
    await createHousehold(page, householdName);
  });

  test('can change nickname', async ({ page }) => {
    await page.getByRole('button', { name: /edit profile/i }).click();

    await page.getByLabel(/nickname/i).waitFor({ state: 'visible', timeout: 5000 });
    await page.getByLabel(/nickname/i).fill('NewName');
    await page.getByRole('button', { name: /save/i }).click();

    await expect(page.getByText('NewName', { exact: true })).toBeVisible({ timeout: 10000 });
  });

  test('can set and clear status', async ({ page }) => {
    await page.getByRole('button', { name: /edit profile/i }).click();

    await page.getByLabel(/status/i).waitFor({ state: 'visible', timeout: 5000 });
    await page.getByLabel(/status/i).fill('Busy testing!');
    await page.getByRole('button', { name: /save/i }).click();

    await page.waitForTimeout(1500);

    // Avatar should have status ring (pulsing)
    await expect(page.locator('.animate-pulse')).toBeVisible({ timeout: 5000 });

    // Reopen and clear
    await page.getByRole('button', { name: /edit profile/i }).click();
    await page.getByLabel(/status/i).waitFor({ state: 'visible', timeout: 5000 });
    await page.getByRole('button', { name: /clear/i }).click();
    await page.getByRole('button', { name: /save/i }).click();

    await page.waitForTimeout(1500);

    await expect(page.locator('.animate-pulse')).not.toBeVisible({ timeout: 5000 });
  });
});

test.describe('Invites', () => {
  test('can generate invite link', async ({ page }) => {
    const householdName = `Invite Test ${uniqueId()}`;
    await createHousehold(page, householdName);

    await page.getByRole('button', { name: /invite/i }).click();

    // Dialog opens - should show "Invite Family Member" title
    await expect(page.getByRole('heading', { name: /invite family member/i })).toBeVisible({ timeout: 5000 });

    // Click "Generate Invite Link" button
    await page.getByRole('button', { name: /generate invite link/i }).click();

    // Should show the invite code and readonly link input
    await expect(page.locator('input[readonly]')).toBeVisible({ timeout: 10000 });
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
