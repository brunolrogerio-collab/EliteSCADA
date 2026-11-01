import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests-e2e',
  testMatch: /(?:wave-15-intelligence-(?:stage2|operating-context|permit-rearm|dashboard|profile-coverage|profile-coverage-runtime|performance-runtime|process-stage-insights-runtime)-mounted|wave-15-resource-organizer-mounted|intelligence-dashboard-generator|visual-builtin-object-schemas|visual-editor-palette-contract|installation-network-profile)\.spec\.ts/,
  workers: 1,
  timeout: 30_000,
  expect: { timeout: 10_000 },
  retries: 0,
  reporter: 'line',
  use: {
    baseURL: 'http://127.0.0.1:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ...devices['Desktop Chrome']
  },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1',
    url: 'http://127.0.0.1:5173',
    timeout: 30_000,
    reuseExistingServer: false
  }
});
