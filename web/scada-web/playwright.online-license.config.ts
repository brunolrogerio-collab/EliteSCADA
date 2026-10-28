import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests-e2e', testMatch: ['online-license-confirmation.spec.ts', 'effective-capabilities-contract.spec.ts'], workers: 1, retries: 0,
  use: { ...devices['Desktop Chrome'], baseURL: 'http://127.0.0.1:4179' },
  webServer: { command: 'npx vite --host 127.0.0.1 --port 4179 --strictPort', url: 'http://127.0.0.1:4179', timeout: 60000, reuseExistingServer: false }
});
