import { defineConfig, devices } from '@playwright/test';

// Focused mounted Trend coverage mocks the Historian/runtime boundaries and
// needs only Vite; it must not depend on a developer's database or preview.
export default defineConfig({
  testDir: './tests-e2e',
  testMatch: /wave-14-c15-trend-(?:multipen|multipen-mounted|i18n-mounted)\.spec\.ts/,
  workers: 1,
  timeout: 30_000,
  expect: { timeout: 10_000 },
  retries: 0,
  reporter: 'line',
  use: {
    ...devices['Desktop Chrome'],
    baseURL: 'http://127.0.0.1:5178',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 5178 --strictPort',
    url: 'http://127.0.0.1:5178',
    timeout: 30_000,
    reuseExistingServer: false
  }
});
