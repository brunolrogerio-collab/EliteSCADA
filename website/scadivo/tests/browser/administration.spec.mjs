import { test, expect } from '@playwright/test';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { resolve, sep } from 'node:path';

const root = fileURLToPath(new URL('../../portal/wwwroot/', import.meta.url));
let server, baseUrl;
test.beforeAll(async () => {
  server = createServer(async (request, response) => {
    const path = decodeURIComponent(new URL(request.url, 'http://localhost').pathname).replace(/^\/portal\/?/, '');
    const target = resolve(root, path || 'index.html');
    if (!target.startsWith(resolve(root) + sep) && target !== resolve(root, 'index.html')) { response.writeHead(404).end(); return; }
    try {
      response.setHeader('Content-Type', target.endsWith('.js') ? 'application/javascript' : target.endsWith('.css') ? 'text/css' : target.endsWith('.svg') ? 'image/svg+xml' : 'text/html');
      response.end(await readFile(target));
    } catch { response.writeHead(404).end(); }
  });
  await new Promise(done => server.listen(0, '127.0.0.1', done));
  baseUrl = `http://127.0.0.1:${server.address().port}`;
});
test.afterAll(async () => { await new Promise(done => server.close(done)); });

async function fixture(page, { locale = 'en', administrator = true } = {}) {
  const calls = [], browserErrors = [], releases = [];
  const accounts = [
    { id: 'owner', userName: 'portal.owner', displayName: 'Portal Owner', owner: true, administrator: true, emailConfirmed: true, invitationAccepted: true, createdAtUtc: '2026-10-25T10:00:00Z' },
    { id: 'staff', userName: 'portal.staff', displayName: 'Portal Staff', owner: false, administrator: false, emailConfirmed: true, invitationAccepted: true, createdAtUtc: '2026-10-25T10:00:00Z' },
    { id: 'pending', userName: 'portal.pending', displayName: 'Pending User', owner: false, administrator: false, emailConfirmed: false, invitationAccepted: true, createdAtUtc: '2026-10-25T10:00:00Z' }
  ];
  page.on('pageerror', error => browserErrors.push(error.message));
  await page.route('**/portal/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace('/portal/api', '');
    const method = request.method();
    const body = method === 'GET' ? null : request.postDataJSON();
    if (method !== 'GET') calls.push({ path, method, body, csrf: request.headers()['x-csrf-token'] });
    let result = {};
    if (path === '/session') result = { authenticated: true, csrfToken: 'synthetic-csrf', emailDeliveryConfigured: true, user: { id: 'signed-in-staff', userName: 'scadivo.staff', displayName: 'SCADIVO Staff', owner: false, administrator, locale, profileComplete: true } };
    else if (path === '/admin/overview') result = { accounts: accounts.length, openTickets: 0, pendingInvitations: 1, publicRegistrationEnabled: true, emailDeliveryConfigured: true, queuedMessages: 0, sentMessages: 0, failedMessages: 0 };
    else if (path === '/admin/accounts') result = accounts;
    else if (path.endsWith('/administration')) {
      const account = accounts.find(item => path === `/admin/accounts/${item.id}/administration`);
      account.administrator = body.administrator; result = account;
    }
    else if (path === '/admin/downloads' && method === 'GET') result = releases;
    else if (path.startsWith('/admin/downloads') && method !== 'GET') {
      let release = releases.find(item => path === `/admin/downloads/${item.id}`);
      if (!release) { release = { id: 'synthetic-release', createdAtUtc: '2026-10-25T10:00:00Z' }; releases.push(release); }
      Object.assign(release, body); result = release;
    }
    else if (path === '/downloads') result = releases.filter(item => item.published);
    await route.fulfill({ json: result });
  });
  await page.goto(`${baseUrl}/portal/#admin`);
  return { calls, browserErrors, accounts, releases };
}

test('delegated administrator can save drafts, preserve all translated notes and publish or withdraw a download', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  const { calls, releases, browserErrors } = await fixture(page);
  await page.locator('.admin-subnav').getByRole('button', { name: 'Downloads', exact: true }).click();
  await page.getByLabel('Version', { exact: true }).fill('v1.0.0');
  await page.getByLabel('Platform', { exact: true }).selectOption('windows-x64');
  await page.getByLabel('Notes language', { exact: true }).selectOption('pt-BR');
  await page.getByLabel('Release notes', { exact: true }).fill('Notas em português');
  await page.getByLabel('Notes language', { exact: true }).selectOption('en');
  await page.getByLabel('Release notes', { exact: true }).fill('English release notes');
  await page.getByLabel('Notes language', { exact: true }).selectOption('es');
  await page.getByLabel('Release notes', { exact: true }).fill('Notas en español');
  await page.getByRole('button', { name: 'Save download', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Edit download', exact: true })).toBeVisible();
  await page.screenshot({ path: test.info().outputPath('downloads-administration.png'), fullPage: true });
  expect(releases[0]).toMatchObject({ published: false, officialUrl: '', sha256: '', notesPt: 'Notas em português', notesEn: 'English release notes', notesEs: 'Notas en español' });
  await page.getByLabel('Official file link', { exact: false }).fill('https://github.com/brunolrogerio-collab/EliteSCADA/releases/download/v1.0.0/scadivo.zip');
  await page.getByLabel('SHA-256', { exact: false }).fill('a'.repeat(64));
  await page.getByLabel('Published in the user catalog', { exact: true }).check();
  await page.getByRole('button', { name: 'Save download', exact: true }).click();
  await expect(page.getByText('Published', { exact: true })).toBeVisible();
  expect(releases[0].published).toBe(true);
  expect(calls.every(call => call.csrf === 'synthetic-csrf')).toBe(true);
  await page.locator('.portal-nav').getByRole('button', { name: 'Downloads', exact: true }).click();
  await expect(page.getByText('English release notes', { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Download file ↗' })).toHaveAttribute('href', '/portal/api/downloads/synthetic-release/file');
  await page.getByLabel('Platform', { exact: true }).selectOption('linux-x64');
  await expect(page.getByRole('link', { name: 'Download file ↗' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Administration', exact: true }).click();
  await page.getByLabel('Published in the user catalog', { exact: true }).uncheck();
  await page.getByRole('button', { name: 'Save download', exact: true }).click();
  await expect(page.getByText('Draft', { exact: true })).toBeVisible();
  expect(releases[0].published).toBe(false);
  expect(browserErrors).toEqual([]);
});

test('granting staff administration requires confirmation and protects the owner and unconfirmed accounts', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const { calls, browserErrors } = await fixture(page);
  const row = page.getByRole('row').filter({ hasText: 'portal.staff' });
  await expect(page.getByRole('row').filter({ hasText: 'portal.owner' }).getByRole('button', { name: 'Grant administration' })).toHaveCount(0);
  await expect(page.getByRole('row').filter({ hasText: 'portal.pending' }).getByRole('button', { name: 'Grant administration' })).toBeDisabled();
  await expect(page.getByRole('row').filter({ hasText: 'portal.pending' }).getByText('Awaiting confirmation')).toBeVisible();
  await page.screenshot({ path: test.info().outputPath('accounts-administration.png'), fullPage: true });
  page.once('dialog', dialog => dialog.dismiss());
  await row.getByRole('button', { name: 'Grant administration', exact: true }).click();
  expect(calls).toEqual([]);
  page.once('dialog', dialog => dialog.accept());
  await row.getByRole('button', { name: 'Grant administration', exact: true }).click();
  await expect(row.getByRole('button', { name: 'Remove administration', exact: true })).toBeVisible();
  expect(calls[0]).toMatchObject({ path: '/admin/accounts/staff/administration', method: 'PUT', csrf: 'synthetic-csrf', body: { administrator: true } });
  page.once('dialog', dialog => dialog.accept());
  await row.getByRole('button', { name: 'Remove administration', exact: true }).click();
  await expect(row.getByRole('button', { name: 'Grant administration', exact: true })).toBeVisible();
  expect(calls[1].body).toEqual({ administrator: false });
  expect(browserErrors).toEqual([]);
});

test('ordinary member does not receive the Portal administration navigation', async ({ page }) => {
  const { calls, browserErrors } = await fixture(page, { administrator: false });
  await expect(page.getByRole('heading', { name: 'Overview', exact: true })).toBeVisible();
  await expect(page.locator('.portal-nav').getByRole('button', { name: 'Administration', exact: true })).toHaveCount(0);
  expect(calls).toEqual([]); expect(browserErrors).toEqual([]);
});

for (const [locale, label, title] of [['pt-BR', 'Downloads', 'Novo download'], ['en', 'Downloads', 'New download'], ['es', 'Descargas', 'Nueva descarga']]) {
  test(`downloads editor fits 360 px in ${locale}`, async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 800 });
    const { browserErrors } = await fixture(page, { locale });
    await page.locator('.admin-subnav').getByRole('button', { name: label, exact: true }).click();
    await expect(page.getByRole('heading', { name: title, exact: true })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(browserErrors).toEqual([]);
  });
}
