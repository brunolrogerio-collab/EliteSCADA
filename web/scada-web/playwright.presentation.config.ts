import { defineConfig, devices } from '@playwright/test';

// Mounted presentation contracts mock their HTTP boundary; they do not require
// the live user's database, hardware, credentials or a second API instance.
export default defineConfig({
  testDir: './tests-e2e',
  testMatch: /wave-14-c26-runtime-viewport\.spec\.ts/,
  workers: 1,
  retries: 0,
  timeout: 30_000,
  reporter: 'line',
  use: { ...devices['Desktop Chrome'], baseURL: 'http://127.0.0.1:5175' },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 5175 --strictPort',
    url: 'http://127.0.0.1:5175',
    reuseExistingServer: false,
    timeout: 60_000
  }
});
