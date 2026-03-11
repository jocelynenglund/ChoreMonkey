# E2E Debug Session - 2026-03-10

## Problem
All 10 E2E tests fail at `createHousehold()` - timeout waiting for redirect to `/household/`

## Iteration Log

### Iteration 1
**What I did:** Set up local environment (API + frontend + Playwright), ran single test
**Went well:** Environment setup worked, able to reproduce the failure locally
**Went wrong:** API died during testing (killed accidentally). Test shows form is on step 2 with values filled but Create button click doesn't trigger navigation.
**Next approach:** Check if API is actually receiving requests, add console logging to see what's happening

### Iteration 2
**What I did:** Fixed API stability (nohup), verified API works (curl test succeeds), ran test again
**Went well:** API returns correct response when tested directly with curl
**Went wrong:** Test still times out. Page snapshot shows form still on step 2 after button click. API log shows no requests from the test.
**Hypothesis:** Either the click isn't registering, or there's a JS error preventing the API call
**Next approach:** Create a debug test to capture console errors and network activity

### Iteration 3
**What I did:** Created debug test with console/network capture, found CORS error
**Went well:** Debug test revealed the root cause - CORS blocking `localhost:4173` (preview port)
**What went wrong:** Nothing - found the issue!
**Root cause:** CORS policy only allows localhost:5173, but E2E uses localhost:4173 (preview server)
**Fix:** Added localhost:4173 to CORS allowed origins in Program.cs
**Result:** 9/11 tests pass! 2 failures are locator specificity issues (test bug, not app bug)

### Iteration 4 (2026-03-11)
**What I did:** Investigated remaining failures, found API URL mismatch
**Root cause:** Frontend built without `VITE_API_URL`, defaulted to `https://localhost:7422` instead of `http://localhost:5073`
**Fixes:**
1. Created `.env.local` with `VITE_API_URL=http://localhost:5073`
2. Fixed `playwright.config.ts` to allow `reuseExistingServer: !process.env.CI` for preview mode
3. Rebuilt frontend with correct API URL
**Result:** 11/11 tests pass! ✅

