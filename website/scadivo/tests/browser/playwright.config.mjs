import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.mjs',
  workers: 1,
  retries: 0,
  timeout: 30_000,
  reporter: 'list',
  use: { browserName: 'chromium', screenshot: 'only-on-failure', trace: 'retain-on-failure' }
});
