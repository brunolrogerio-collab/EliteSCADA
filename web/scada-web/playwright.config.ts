import { defineConfig, devices } from '@playwright/test';
import {
  createE2eJwt,
  E2E_AUTH_AUDIENCE,
  E2E_AUTH_ISSUER,
  E2E_AUTH_SIGNING_KEY
} from './tests-e2e/jwt';

const developerToken = createE2eJwt('e2e-developer', ['developer'], 'E2E Developer');

export default defineConfig({
  testDir: './tests-e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  retries: 1,
  reporter: [['line'], ['html', { outputFolder: 'playwright-report', open: 'never' }]],
  use: {
    baseURL: 'http://127.0.0.1:5173',
    extraHTTPHeaders: {
      Authorization: `Bearer ${developerToken}`
    },
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
        DOTNET_CLI_TELEMETRY_OPTOUT: '1',
        Authentication__Enabled: 'true',
        Authentication__Jwt__Issuer: E2E_AUTH_ISSUER,
        Authentication__Jwt__Audience: E2E_AUTH_AUDIENCE,
        Authentication__Jwt__SigningKey: E2E_AUTH_SIGNING_KEY,
        Authentication__Local__Enabled: 'true',
        Authentication__Local__SecureCookie: 'false',
        Authentication__Local__Bootstrap__Username: 'local-developer',
        Authentication__Local__Bootstrap__DisplayName: 'Local Developer',
        Authentication__Local__Bootstrap__Password: 'E2E-local-password-123!',
        Authentication__Local__Bootstrap__Roles__0: 'developer'
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
