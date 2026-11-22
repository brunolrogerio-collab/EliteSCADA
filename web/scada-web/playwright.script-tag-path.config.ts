import { defineConfig, devices } from '@playwright/test';

// Isolate the readable TAG-path contract from API/database startup.
export default defineConfig({
  testDir: './tests-e2e',
  testMatch: /script-engineering-workspace-contract\.spec\.ts/,
  grep: /Script TAG dependencies present readable paths/,
  workers: 1,
  retries: 0,
  reporter: 'line',
  use: { ...devices['Desktop Chrome'] }
});
