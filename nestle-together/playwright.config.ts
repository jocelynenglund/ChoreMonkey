import { defineConfig, devices } from '@playwright/test';

// When running against staging (Azure Free tier), the API cold-starts can add 20-30s.
// Use generous timeouts in CI staging mode.
const isStagingCI = !!process.env.E2E_BASE_URL && !!process.env.CI;

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  timeout: isStagingCI ? 60000 : 30000,

  use: {
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:5173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    actionTimeout: isStagingCI ? 20000 : 5000,
    navigationTimeout: isStagingCI ? 45000 : 15000,
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  // Run local dev server before tests (only if not testing staging)
  webServer: process.env.E2E_BASE_URL ? undefined : {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
  },
});
