import { defineConfig, devices } from '@playwright/test';
import {
  createE2eJwt,
  E2E_AUTH_AUDIENCE,
  E2E_AUTH_ISSUER,
  E2E_AUTH_SIGNING_KEY
} from './tests-e2e/jwt';

const developerToken = createE2eJwt('wave11-developer', ['developer'], 'Wave 11 Developer');

export default defineConfig({
  testDir: './tests-wave11',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: 0,
  workers: 1,
  reporter: [['line'], ['html', { outputFolder: 'playwright-report-wave11', open: 'never' }]],
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
      name: 'chromium-wave11-c16-startup-bootstrap',
      testMatch: /c16-startup-bootstrap\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-lifecycle',
      testMatch: /(?:^|[\\/])active-runtime\.spec\.ts$/,
      dependencies: ['chromium-wave11-c16-startup-bootstrap'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c22-runtime-shell-viewport-fit',
      testMatch: /c22-runtime-shell-viewport-fit\.spec\.ts/,
      dependencies: ['chromium-wave11-lifecycle'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c17-memory',
      testMatch: /(?:c17-memory-lifecycle|c17-datasource-new-transition)\.spec\.ts/,
      dependencies: ['chromium-wave11-c22-runtime-shell-viewport-fit'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c15-trend',
      testMatch: /c15-trend-active-runtime\.spec\.ts/,
      dependencies: ['chromium-wave11-c17-memory'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c16-operational-runtime',
      testMatch: /c16-operational-runtime\.spec\.ts/,
      dependencies: ['chromium-wave11-c15-trend'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c18-browsers',
      testMatch: /c18-alarm-event-browsers-active-runtime\.spec\.ts/,
      dependencies: ['chromium-wave11-c16-operational-runtime'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c19-operational-events',
      testMatch: /c19-operational-event-script-bridge\.spec\.ts/,
      dependencies: ['chromium-wave11-c18-browsers'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c20-visual-dynamic-wire',
      testMatch: /c20-visual-dynamic-wire-contract\.spec\.ts/,
      dependencies: ['chromium-wave11-c19-operational-events'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c21-dynamo-tag-reference',
      testMatch: /c21-dynamo-tag-reference-runtime\.spec\.ts/,
      dependencies: ['chromium-wave11-c20-visual-dynamic-wire'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c11-eee-foundation',
      testMatch: /c11-eee-demo-foundation\.spec\.ts/,
      dependencies: ['chromium-wave11-c21-dynamo-tag-reference'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-c11-eee-hmi',
      testMatch: /c11-eee-demo-hmi\.spec\.ts/,
      dependencies: ['chromium-wave11-c11-eee-foundation'],
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium-wave11-owner-package',
      testMatch: /owner-test-artifact\.spec\.ts/,
      dependencies: ['chromium-wave11-c11-eee-hmi'],
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
        Authentication__Local__Bootstrap__Username: 'wave11-local-developer',
        Authentication__Local__Bootstrap__DisplayName: 'Wave 11 Local Developer',
        Authentication__Local__Bootstrap__Password: 'Wave11-local-password-123!',
        Authentication__Local__Bootstrap__Roles__0: 'developer',
        EngineeringRuntime__ProjectKey: 'e2e-wave11',
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
