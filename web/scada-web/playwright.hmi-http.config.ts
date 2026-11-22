import { defineConfig, devices } from '@playwright/test';

// A non-localhost HTTP origin reproduces LAN/public-IP browser capabilities.
// All API responses in this suite are mocked; no database or login is needed.
export default defineConfig({
  testDir: './tests-e2e',
  testMatch: /wave-15-hmi-rich-command\.spec\.ts/,
  workers: 1,
  timeout: 30_000,
  expect: { timeout: 10_000 },
  retries: 0,
  reporter: 'line',
  use: {
    ...devices['Desktop Chrome'],
    baseURL: 'http://elitescada-http.test:5179',
    launchOptions: {
      args: ['--host-resolver-rules=MAP elitescada-http.test 127.0.0.1', '--no-proxy-server']
    },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 5179',
    url: 'http://127.0.0.1:5179',
    timeout: 30_000,
    reuseExistingServer: false,
    env: { __VITE_ADDITIONAL_SERVER_ALLOWED_HOSTS: 'elitescada-http.test' }
  }
});
