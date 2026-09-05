import { defineConfig, devices } from '@playwright/test';
import {
  createE2eJwt,
  E2E_AUTH_AUDIENCE,
  E2E_AUTH_ISSUER,
  E2E_AUTH_SIGNING_KEY
} from './tests-e2e/jwt';

const developerToken = createE2eJwt('c11-eee-package-developer', ['developer'], 'C11 EEE Package Developer');

export default defineConfig({
  testDir: './tests-wave11',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: 0,
  workers: 1,
  reporter: [['line'], ['html', { outputFolder: 'playwright-report-c11-package', open: 'never' }]],
  use: {
    baseURL: 'http://127.0.0.1:5174',
    extraHTTPHeaders: {
      Authorization: `Bearer ${developerToken}`
    },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  projects: [
    {
      name: 'chromium-c11-eee-package-portability',
      testMatch: /c11-eee-demo-package\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] }
    }
  ],
  webServer: [
    {
      command: 'dotnet run --project ../../src/Scada.Api/Scada.Api.csproj --no-launch-profile',
      url: 'http://127.0.0.1:5081/health',
      timeout: 60_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_URLS: 'http://127.0.0.1:5081',
        DOTNET_NOLOGO: 'true',
        DOTNET_CLI_TELEMETRY_OPTOUT: '1',
        Authentication__Enabled: 'true',
        Authentication__Jwt__Issuer: E2E_AUTH_ISSUER,
        Authentication__Jwt__Audience: E2E_AUTH_AUDIENCE,
        Authentication__Jwt__SigningKey: E2E_AUTH_SIGNING_KEY,
        Authentication__Local__Enabled: 'true',
        Authentication__Local__SecureCookie: 'false',
        Authentication__Local__Bootstrap__Username: 'c11-eee-package-developer',
        Authentication__Local__Bootstrap__DisplayName: 'C11 EEE Package Developer',
        Authentication__Local__Bootstrap__Password: 'C11-eee-package-password-123!',
        Authentication__Local__Bootstrap__Roles__0: 'developer',
        EngineeringRuntime__ProjectKey: 'eee-demo',
        Historian__Provider: 'timescaledb',
        HistoricalQuery__Enabled: 'true',
        HistoricalQuery__CursorKeyBase64: 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA='
      }
    },
    {
      command: 'npm run dev -- --host 127.0.0.1 --port 5174',
      url: 'http://127.0.0.1:5174',
      timeout: 60_000,
      reuseExistingServer: false,
      env: {
        SCADA_API_PROXY: 'http://127.0.0.1:5081'
      }
    }
  ]
});
