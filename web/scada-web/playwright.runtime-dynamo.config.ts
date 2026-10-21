import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests-e2e',
  testMatch: /(?:runtime-logical-canvas|wave-14-dynamo-runtime-(?:binding-projection|state-resolution)|wave-14-runtime-dynamo-state-host-contract)\.spec\.ts/,
  workers: 1,
  retries: 0,
  reporter: 'line'
});
