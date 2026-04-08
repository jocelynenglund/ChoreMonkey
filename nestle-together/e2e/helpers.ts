import { expect, type Page } from '@playwright/test';

export const uniqueId = () => Math.random().toString(36).substring(7);

/** Navigate the 2-step CreateHousehold form and wait for the dashboard. */
export async function createHousehold(page: Page, householdName: string, pin = '1234') {
  await page.goto('/');
  await page.click('text=Create Household');

  // Step 1: Household name + admin nickname
  await page.getByLabel('Household Name').fill(householdName);
  await page.getByLabel('Your Name').fill('Admin');
  await page.getByRole('button', { name: 'Continue' }).click();

  // Step 2: Admin PIN — both fields required
  await page.getByLabel('PIN Code').fill(pin);
  await page.getByLabel('Confirm Admin PIN').fill(pin);
  await page.getByRole('button', { name: 'Create Household' }).click();

  await page.waitForURL(/\/household\//, { timeout: 30000 });
  await expect(page.locator('h1')).toContainText(householdName, { timeout: 15000 });

  return page.url();
}

/** Fill the PinInput component (4 individual tel inputs, auto-submits on last digit). */
export async function fillPinInput(page: Page, pin: string) {
  const inputs = page.locator('input[type="tel"]');
  await inputs.first().waitFor({ state: 'visible', timeout: 10000 });
  for (let i = 0; i < pin.length; i++) {
    await inputs.nth(i).fill(pin[i]);
  }
}
