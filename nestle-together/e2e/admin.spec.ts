import { test, expect } from '@playwright/test';
import { createHousehold, uniqueId } from './helpers';

async function createHouseholdAsAdmin(page: import('@playwright/test').Page) {
  const householdName = `Admin Test ${uniqueId()}`;
  await createHousehold(page, householdName);
  return page.url();
}

test.describe('Admin tab', () => {
  test('is visible for admin', async ({ page }) => {
    await createHouseholdAsAdmin(page);
    await expect(page.getByRole('link', { name: /admin/i })).toBeVisible();
  });

  test('navigates to admin panel', async ({ page }) => {
    await createHouseholdAsAdmin(page);
    await page.getByRole('link', { name: /admin/i }).click();
    await page.waitForURL(/\/admin/, { timeout: 10000 });
  });
});

test.describe('Settings: household slug', () => {
  test.beforeEach(async ({ page }) => {
    await createHouseholdAsAdmin(page);
  });

  test('shows validation error for slug shorter than 3 characters', async ({ page }) => {
    await page.getByRole('button', { name: /settings/i }).click();
    await expect(page.getByText('Settings').first()).toBeVisible();

    await page.locator('#slugInput').fill('ab');
    await page.getByRole('button', { name: /save url/i }).click();

    await expect(page.locator('.text-destructive')).toContainText(/at least 3 characters/i);
  });

  test('saves a valid slug successfully', async ({ page }) => {
    const slug = `test-${uniqueId()}`;

    await page.getByRole('button', { name: /settings/i }).click();
    await expect(page.getByText('Settings').first()).toBeVisible();

    await page.locator('#slugInput').fill(slug);
    await page.getByRole('button', { name: /save url/i }).click();

    await expect(page.getByRole('button', { name: /saved/i })).toBeVisible({ timeout: 5000 });
    await expect(page.locator('.text-destructive')).not.toBeVisible();
  });

  test('shows a friendly error when slug is already taken', async ({ page, browser }) => {
    const slug = `taken-${uniqueId()}`;

    // Set the slug on the first household
    await page.getByRole('button', { name: /settings/i }).click();
    await page.locator('#slugInput').fill(slug);
    await page.getByRole('button', { name: /save url/i }).click();
    await expect(page.getByRole('button', { name: /saved/i })).toBeVisible({ timeout: 5000 });
    await page.keyboard.press('Escape');

    // Create a second household in a new context and try the same slug
    const context2 = await browser.newContext();
    const page2 = await context2.newPage();
    await createHouseholdAsAdmin(page2);
    await page2.getByRole('button', { name: /settings/i }).click();
    await page2.locator('#slugInput').fill(slug);
    await page2.getByRole('button', { name: /save url/i }).click();

    const errorEl = page2.locator('.text-destructive');
    await expect(errorEl).toBeVisible({ timeout: 5000 });
    await expect(errorEl).toContainText(/taken|unavailable|already/i);

    await context2.close();
  });
});

test.describe('Salary admin', () => {
  test('renders salary management section for admin', async ({ page }) => {
    await createHouseholdAsAdmin(page);
    await page.getByRole('link', { name: /admin/i }).click();
    await page.waitForURL(/\/admin/, { timeout: 10000 });

    // Salary tab should be accessible
    await page.getByRole('tab', { name: /salary/i }).click();
    await expect(page.locator('text=/salary management/i')).toBeVisible({ timeout: 5000 });
  });

  test('close period button is disabled when period has not ended', async ({ page }) => {
    await createHouseholdAsAdmin(page);
    await page.getByRole('link', { name: /admin/i }).click();
    await page.waitForURL(/\/admin/, { timeout: 10000 });

    await page.getByRole('tab', { name: /salary/i }).click();

    // The close period button should be disabled — period hasn't ended yet
    const closeBtn = page.locator('button.close-period-btn');
    if (await closeBtn.isVisible()) {
      await expect(closeBtn).toBeDisabled();
    } else {
      // Period not ended note should be visible instead
      await expect(page.locator('text=/period ends|no completed periods/i')).toBeVisible({ timeout: 5000 });
    }
  });
});
