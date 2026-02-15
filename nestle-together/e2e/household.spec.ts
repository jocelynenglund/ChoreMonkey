import { test, expect } from '@playwright/test';

// Generate unique names to avoid conflicts between test runs
const uniqueId = () => Math.random().toString(36).substring(7);

test.describe('Household Creation', () => {
  test('can create a new household', async ({ page }) => {
    const householdName = `Test Family ${uniqueId()}`;
    
    await page.goto('/');
    
    // Click create household
    await page.click('text=Create Household');
    
    // Fill form
    await page.getByLabel(/household name/i).fill(householdName);
    await page.getByLabel(/your name|nickname/i).first().fill('Tester');
    await page.getByLabel(/admin.*pin|pin.*code/i).first().fill('1234');
    
    // Submit
    await page.getByRole('button', { name: /create/i }).click();
    
    // Should redirect to dashboard with household name
    await expect(page.locator('h1')).toContainText(householdName, { timeout: 10000 });
  });

  test('can access existing household with PIN', async ({ page }) => {
    // First create a household
    const householdName = `Access Test ${uniqueId()}`;
    
    await page.goto('/');
    await page.click('text=Create Household');
    await page.getByLabel(/household name/i).fill(householdName);
    await page.getByLabel(/your name|nickname/i).first().fill('Admin');
    await page.getByLabel(/admin.*pin|pin.*code/i).first().fill('5678');
    await page.getByRole('button', { name: /create/i }).click();
    
    // Wait for dashboard
    await expect(page.locator('h1')).toContainText(householdName, { timeout: 10000 });
    
    // Get the URL (contains household ID)
    const url = page.url();
    
    // Logout
    await page.getByRole('button', { name: /logout|log out/i }).click();
    
    // Navigate back and access with PIN
    await page.goto(url.replace('/dashboard/', '/access/'));
    await page.getByLabel(/pin/i).fill('5678');
    await page.getByRole('button', { name: /access|enter|submit/i }).click();
    
    // Should be back on dashboard
    await expect(page.locator('h1')).toContainText(householdName, { timeout: 10000 });
  });
});

test.describe('Chores', () => {
  let householdUrl: string;

  test.beforeEach(async ({ page }) => {
    // Create a fresh household for each test
    const householdName = `Chore Test ${uniqueId()}`;
    
    await page.goto('/');
    await page.click('text=Create Household');
    await page.getByLabel(/household name/i).fill(householdName);
    await page.getByLabel(/your name|nickname/i).first().fill('Tester');
    await page.getByLabel(/admin.*pin|pin.*code/i).first().fill('1234');
    await page.getByRole('button', { name: /create/i }).click();
    
    await expect(page.locator('h1')).toContainText(householdName, { timeout: 10000 });
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
    
    await page.goto('/');
    await page.click('text=Create Household');
    await page.getByLabel(/household name/i).fill(householdName);
    await page.getByLabel(/your name|nickname/i).first().fill('Original');
    await page.getByLabel(/admin.*pin|pin.*code/i).first().fill('1234');
    await page.getByRole('button', { name: /create/i }).click();
    
    await expect(page.locator('h1')).toContainText(householdName, { timeout: 10000 });
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
    
    await page.goto('/');
    await page.click('text=Create Household');
    await page.getByLabel(/household name/i).fill(householdName);
    await page.getByLabel(/your name|nickname/i).first().fill('Admin');
    await page.getByLabel(/admin.*pin|pin.*code/i).first().fill('1234');
    await page.getByRole('button', { name: /create/i }).click();
    
    await expect(page.locator('h1')).toContainText(householdName, { timeout: 10000 });
    
    // Click invite
    await page.getByRole('button', { name: /invite/i }).click();
    
    // Should show invite dialog with link
    await expect(page.locator('text=/invite|link|share/i')).toBeVisible();
    await expect(page.locator('input[readonly]')).toBeVisible();
  });
});

test.describe('What\'s New', () => {
  test('can open changelog dialog', async ({ page }) => {
    const householdName = `Changelog Test ${uniqueId()}`;
    
    await page.goto('/');
    await page.click('text=Create Household');
    await page.getByLabel(/household name/i).fill(householdName);
    await page.getByLabel(/your name|nickname/i).first().fill('Tester');
    await page.getByLabel(/admin.*pin|pin.*code/i).first().fill('1234');
    await page.getByRole('button', { name: /create/i }).click();
    
    await expect(page.locator('h1')).toContainText(householdName, { timeout: 10000 });
    
    // Click monkey logo
    await page.locator('text=🐵').click();
    
    // Should show What's New dialog
    await expect(page.locator('text=/what\'s new/i')).toBeVisible();
    await expect(page.locator('text=/web:/i')).toBeVisible();
    await expect(page.locator('text=/api:/i')).toBeVisible();
  });
});
