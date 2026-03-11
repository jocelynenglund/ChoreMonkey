import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.E2E_BASE_URL || 'http://localhost:5173';
const isLocalPreview = baseURL.includes('localhost:4173');
const isLocalDev = baseURL.includes('localhost:5173');
const isExternal = !baseURL.includes('localhost');

// Determine webServer config based on target
function getWebServer() {
  if (isExternal) {
    // Testing against external URL (e.g., labs.itsybit.se) - no local server
    return undefined;
  }
  if (isLocalPreview) {
    // CI: serve built files with vite preview
    return {
      command: 'npm run preview',
      url: 'http://localhost:4173',
      reuseExistingServer: !process.env.CI,
    };
  }
  // Local dev: use vite dev server
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
  
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
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
