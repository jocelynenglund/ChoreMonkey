import { test, expect } from '@playwright/test';

// Skip by default - this is a debug test for diagnosing failures
test.skip('debug household creation', async ({ page }) => {
  // Capture console messages
  const consoleLogs: string[] = [];
  page.on('console', msg => {
    consoleLogs.push(`[${msg.type()}] ${msg.text()}`);
  });

  // Capture network requests
  const requests: string[] = [];
  page.on('request', request => {
    requests.push(`${request.method()} ${request.url()}`);
  });
  
  // Capture network responses
  const responses: string[] = [];
  page.on('response', response => {
    responses.push(`${response.status()} ${response.url()}`);
  });

  // Capture page errors
  const errors: string[] = [];
  page.on('pageerror', error => {
    errors.push(`PAGE ERROR: ${error.message}`);
  });

  // Step 1: Go to home
  await page.goto('/');
  console.log('Navigated to home');
  
  // Step 2: Click create
  await page.click('text=Create Household');
  console.log('Clicked Create Household');
  
  // Step 3: Fill name
  const householdName = 'Debug Test ' + Date.now();
  await page.getByLabel('Household Name').fill(householdName);
  console.log('Filled household name');
  
  // Step 4: Click continue
  await page.getByRole('button', { name: 'Continue' }).click();
  console.log('Clicked Continue');
  
  // Wait for step 2 to appear
  await expect(page.getByLabel('PIN Code')).toBeVisible({ timeout: 5000 });
  console.log('Step 2 visible');
  
  // Step 5: Fill PINs
  await page.getByLabel('PIN Code').fill('1234');
  await page.getByLabel('Confirm Admin PIN').fill('1234');
  console.log('Filled PINs');
  
  // Step 6: Click Create Household
  const createButton = page.getByRole('button', { name: 'Create Household' });
  await expect(createButton).toBeEnabled({ timeout: 5000 });
  console.log('Create button is enabled');
  
  // Capture current URL before click
  const urlBefore = page.url();
  console.log('URL before click:', urlBefore);
  
  // Click with force option
  await createButton.click();
  console.log('Clicked Create Household button');
  
  // Wait a bit for any response
  await page.waitForTimeout(3000);
  
  // Check URL after
  const urlAfter = page.url();
  console.log('URL after click:', urlAfter);
  
  // Output debug info
  console.log('\n=== CONSOLE LOGS ===');
  consoleLogs.forEach(log => console.log(log));
  
  console.log('\n=== NETWORK REQUESTS ===');
  requests.forEach(req => console.log(req));
  
  console.log('\n=== NETWORK RESPONSES ===');
  responses.forEach(res => console.log(res));
  
  console.log('\n=== PAGE ERRORS ===');
  errors.forEach(err => console.log(err));
  
  // Check if navigation happened
  expect(page.url()).toContain('/household/');
});
