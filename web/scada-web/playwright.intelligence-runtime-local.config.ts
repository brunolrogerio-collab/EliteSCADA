import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests-e2e',
  testMatch: /(intelligence-dashboard-generator|visual-builtin-object-schemas|visual-editor-palette-contract|wave-15-intelligence-contextual-deviation-runtime-mounted)\.spec\.ts/,
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  retries: 0,
  reporter: 'line',
  use: { ...devices['Desktop Chrome'], baseURL: 'http://127.0.0.1:5174' },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 5174 --strictPort',
    url: 'http://127.0.0.1:5174',
    timeout: 60_000,
    reuseExistingServer: false,
    stdout: 'pipe'
  }
});
