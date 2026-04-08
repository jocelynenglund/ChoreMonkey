import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.E2E_BASE_URL || 'http://localhost:5173';
const isLocalPreview = baseURL.includes('localhost:4173');
const isExternal = !baseURL.includes('localhost');

// When running against staging (Azure Free tier), the API cold-starts can add 20-30s.
const isStagingCI = isExternal && !!process.env.CI;

// Determine webServer config based on target
function getWebServer() {
  if (isExternal) {
    return undefined;
  }
  if (isLocalPreview) {
    return {
      command: 'npm run preview',
      url: 'http://localhost:4173',
      reuseExistingServer: !process.env.CI,
    };
  }
  return {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
  };
}

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  timeout: isStagingCI ? 60000 : 30000,

  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    actionTimeout: isStagingCI ? 20000 : 5000,
    navigationTimeout: isStagingCI ? 45000 : 15000,
  },

  projects: [
    {
      name: 'chromium',
      use: { 
        ...devices['Desktop Chrome'],
        viewport: { width: 1280, height: 900 }, // Taller viewport for dialogs
      },
    },
  ],

  webServer: getWebServer(),
});
