import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests-e2e',
  testMatch: /(wave-14-dynamo-(library-model|runtime-binding-projection|runtime-state-resolution)|visual-editor-palette-contract|visual-dynamo-v4-contract)\.spec\.ts/,
  workers: 1,
  retries: 0,
  reporter: 'line'
});
