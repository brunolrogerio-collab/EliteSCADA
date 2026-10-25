import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests-e2e',
  testMatch: /(contextual-help-routing|wave-14-c25-contextual-help|help-learning|help-script-examples|database-topology-mounted)\.spec\.ts/,
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  retries: 0,
  reporter: 'line',
  use: { ...devices['Desktop Chrome'], baseURL: 'http://127.0.0.1:5178' },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 5178 --strictPort',
    url: 'http://127.0.0.1:5178', reuseExistingServer: false,
    timeout: 60_000, stdout: 'pipe'
  }
});
