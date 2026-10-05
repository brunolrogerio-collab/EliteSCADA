import { defineConfig, devices } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  createE2eJwt,
  E2E_AUTH_AUDIENCE,
  E2E_AUTH_ISSUER,
  E2E_AUTH_SIGNING_KEY
} from './tests-e2e/jwt';

const developerToken = createE2eJwt('e2e-developer', ['developer'], 'E2E Developer');
const e2eProtectedMaterialStore = join(tmpdir(), `elitescada-e2e-protected-${randomUUID()}`);
const e2eProtectedMaterialKey = randomBytes(32).toString('base64');
process.env.ELITESCADA_E2E_PROTECTED_MATERIAL_STORE = e2eProtectedMaterialStore;

export default defineConfig({
  globalTeardown: './tests-e2e/cleanup-protected-material.ts',
  testDir: './tests-e2e',
  workers: 1,
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
      name: 'chromium-local-auth',
      testMatch: /local-auth\.spec\.ts/,
      retries: 0,
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'chromium',
      testIgnore: /local-auth\.spec\.ts/,
      dependencies: ['chromium-local-auth'],
      use: { ...devices['Desktop Chrome'] }
    }
  ],
  webServer: [
    {
      command: 'dotnet run --project ../../src/Scada.Api/Scada.Api.csproj --no-launch-profile',
      url: 'http://127.0.0.1:5080/health',
      timeout: 60_000,
      reuseExistingServer: false,
      stdout: 'pipe',
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
        ELITESCADA_PROTECTED_MATERIAL_KEY: e2eProtectedMaterialKey,
        ProtectedMaterial__Store__Path: e2eProtectedMaterialStore,
        EngineeringRuntime__ProjectKey: 'e2e-wave03',
      }
    },
    {
      command: 'npm run dev -- --host 127.0.0.1',
      url: 'http://127.0.0.1:5173',
      timeout: 60_000,
      // E2E must use Vite's same-origin proxy.  A direct API origin prevents
      // the first-run endpoint from persisting its Strict HttpOnly cookie.
      reuseExistingServer: false
    }
  ]
});
