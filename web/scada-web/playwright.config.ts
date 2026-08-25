import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests-e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  retries: 1,
  reporter: [['line'], ['html', { outputFolder: 'playwright-report', open: 'never' }]],
  use: {
    baseURL: 'http://127.0.0.1:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ],
  webServer: [
    {
      command: 'dotnet run --project ../../src/Scada.Api/Scada.Api.csproj --no-launch-profile',
      url: 'http://127.0.0.1:5080/health',
      timeout: 60_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_URLS: 'http://127.0.0.1:5080',
        DOTNET_NOLOGO: 'true',
        DOTNET_CLI_TELEMETRY_OPTOUT: '1'
      }
    },
    {
      command: 'npm run dev -- --host 127.0.0.1',
      url: 'http://127.0.0.1:5173',
      timeout: 60_000,
      reuseExistingServer: false
    }
  ]
});
