import { expect, test, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });
const sourceId = '15000000-0000-0000-0000-000000000495';
const base = `/api/runtime/media-sources/${sourceId}`;

async function mount(page: Page) {
  const responses: Record<string, unknown> = {
    '/api/auth/config': { authenticationEnabled: true, localLoginEnabled: true, initialAdministratorRequired: false, passwordPolicy: { minimumLength: 8, maximumLength: 1024 } },
    '/api/auth/me': { subjectId: 'media-reviewer', username: 'media-reviewer', displayName: 'Media Reviewer', roles: ['developer'], identityProvider: 'local' },
    '/api/auth/local-session': { authenticated: true, username: 'media-reviewer' },
    '/api/auth/effective-capabilities': { authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 }, authenticationEnabled: true, runtime: ['View', 'TrendUse', 'SystemAdmin'], workspace: ['EngineeringView', 'EngineeringModify', 'SystemAdmin'] },
    '/api/engineering/persistence/status': { enabled: true, hasProjects: true },
    '/api/runtime/application': {
      mode: 'engineering', projectKey: 'synthetic-media-review', projectName: 'Synthetic media review', revision: 1,
      package: { schema: 'scada.engineering', schemaVersion: 16, startupScreenId: '15000000-0000-0000-0000-000000000496',
        screens: [{ id: '15000000-0000-0000-0000-000000000496', key: 'media.home', name: 'Media Home', elements: [{ id: sourceId, key: 'synthetic-camera', type: 'core.videoPlayer', properties: { x: 100, y: 100, width: 960, height: 540, mediaSourceId: sourceId, mediaAutoplay: true, mediaMuted: true, mediaControls: true } }] }],
        scripts: [], scriptVisualEventReferences: [], visualAssets: [], dynamos: [], popups: [] }
    }
  };
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.startsWith(base)) return route.fallback();
    await route.fulfill({ json: responses[path] ?? [] });
  });
}

test('Runtime media exposes credential failure without an endless reconnect loop', async ({ page }) => {
  await mount(page);
  let contentRequests = 0;
  await page.route(`**${base}/**`, async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/info')) return route.fulfill({ json: { protocol: 'http', state: 'ready' } });
    if (path.endsWith('/content')) contentRequests++;
    await route.fulfill({ status: 502, json: { state: 'auth' } });
  });
  await page.goto('/');
  const player = page.locator('[data-media-state]');
  await expect(player).toHaveAttribute('data-media-state', 'auth');
  await expect(player.getByRole('status')).toContainText('Credencial');
  const before = contentRequests;
  await page.waitForTimeout(1500);
  expect(contentRequests).toBe(before);
  await player.getByRole('button', { name: 'Reconnect' }).click();
  await expect.poll(() => contentRequests).toBeGreaterThan(before);
});

test('Runtime HLS player decodes synthetic RTSP gateway media and supports pause', async ({ page, request }) => {
  test.skip(!process.env.ELITESCADA_MEDIA_LAB_HLS, 'Opt-in synthetic RTSP/HLS lab, not a physical camera.');
  test.setTimeout(45_000);
  await mount(page);
  const root = new URL(process.env.ELITESCADA_MEDIA_LAB_HLS!);
  await page.route(`**${base}/**`, async route => {
    const requested = new URL(route.request().url());
    if (requested.pathname.endsWith('/info')) return route.fulfill({ json: { protocol: 'hls', state: 'ready' } });
    if (requested.pathname.endsWith('/probe')) return route.fulfill({ json: { state: 'ready' } });
    const upstream = requested.pathname.endsWith('/content') ? root : new URL(requested.searchParams.get('resource')!);
    if (upstream.origin !== root.origin) throw new Error('Lab resource escaped the fixed gateway origin.');
    const response = await request.get(upstream.href);
    const contentType = response.headers()['content-type'] ?? 'application/octet-stream';
    if (contentType.includes('mpegurl')) {
      const rewrite = (value: string) => `${base}/resource?resource=${encodeURIComponent(new URL(value, upstream).href)}`;
      const body = (await response.text()).split('\n').map(line => line.startsWith('#')
        ? line.replace(/URI="([^"]+)"/g, (_match, value) => `URI="${rewrite(value)}"`)
        : line.trim() ? rewrite(line.trim()) : line).join('\n');
      await route.fulfill({ status: response.status(), contentType, body });
    } else await route.fulfill({ status: response.status(), contentType, body: await response.body() });
  });
  await page.goto('/');
  const player = page.locator('[data-media-state]');
  await expect(player).toHaveAttribute('data-media-state', 'live', { timeout: 30_000 });
  const video = player.locator('video');
  await expect.poll(() => video.evaluate(element => (element as HTMLVideoElement).currentTime)).toBeGreaterThan(0.5);
  expect(await video.evaluate(element => (element as HTMLVideoElement).videoWidth)).toBeGreaterThan(0);
  await video.evaluate(element => (element as HTMLVideoElement).pause());
  expect(await video.evaluate(element => (element as HTMLVideoElement).paused)).toBeTruthy();
});
