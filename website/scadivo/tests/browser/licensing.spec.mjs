import { test, expect } from '@playwright/test';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { resolve, sep } from 'node:path';

// UI fixtures use synthetic data only. Backend authorization/transactions are covered
// by PortalIntegrationTests against a disposable PostgreSQL database.
const root = fileURLToPath(new URL('../../portal/wwwroot/', import.meta.url));
let server, baseUrl;
test.beforeAll(async () => {
  server = createServer(async (request, response) => {
    const path = decodeURIComponent(new URL(request.url, 'http://localhost').pathname).replace(/^\/portal\/?/, '');
    const target = resolve(root, path || 'index.html');
    if (!target.startsWith(resolve(root) + sep) && target !== resolve(root, 'index.html')) {
      response.writeHead(404).end(); return;
    }
    try {
      const body = await readFile(target);
      response.setHeader('Content-Type', target.endsWith('.js') ? 'application/javascript' : target.endsWith('.css') ? 'text/css' : target.endsWith('.svg') ? 'image/svg+xml' : 'text/html');
      response.end(body);
    } catch { response.writeHead(404).end(); }
  });
  await new Promise(done => server.listen(0, '127.0.0.1', done));
  baseUrl = `http://127.0.0.1:${server.address().port}`;
});
test.afterAll(async () => { await new Promise(done => server.close(done)); });

async function fixture(page, { owner = false, locale = 'en', role = 'Member', term = 'perpetual' } = {}) {
  const orgId = '11111111-1111-4111-8111-111111111111';
  const applicationId = '22222222-2222-4222-8222-222222222222';
  const license = {
    id: '33333333-3333-4333-8333-333333333333', publicCode: 'SCV-TEST-ONLY-1234',
    organizationId: orgId, organizationName: 'Synthetic Engineering School', applicationId,
    applicationName: 'Building automation laboratory', tier: 'Tags500', interactiveSeats: 4,
    viewOnlySeats: 8, haRuntime: true, assetProcessIntelligence: false, term,
    entitlementExpiresAtUtc: term === 'perpetual' ? null : '2026-11-25T10:00:00Z', maxActivations: 2, activationCount: 1, activeActivations: 1, status: 'active'
  };
  const activation = {
    id: '44444444-4444-4444-8444-444444444444', licenseId: license.id,
    licenseCode: license.publicCode, applicationName: license.applicationName,
    organizationName: license.organizationName, status: 'active', userName: 'portal.user',
    createdAtUtc: '2026-10-25T10:00:00Z', lastRenewedAtUtc: null
  };
  const calls = [];
  const applications = [{ id: applicationId, name: license.applicationName }];
  const members = [{ id: 'account-1', userName: 'portal.user', displayName: 'Portal User', role }];
  await page.route('**/portal/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace('/portal/api', '');
    const method = request.method();
    if (method !== 'GET') calls.push({ path, method, body: request.postDataJSON(), csrf: request.headers()['x-csrf-token'] });
    let result;
    if (path === '/session') result = { authenticated: true, csrfToken: 'synthetic-csrf', emailDeliveryConfigured: true, publicRegistrationEnabled: true, user: { id: 'account-1', userName: 'portal.user', displayName: 'Portal User', owner, profileComplete: true, locale } };
    else if (path === '/licensing/status') result = { onlineActivationAvailable: false };
    else if (path === '/licensing/organizations') result = [{ id: orgId, name: license.organizationName, role: owner ? 'PortalOwner' : role }];
    else if (path === '/admin/licensing/organizations') result = [{ id: orgId, name: license.organizationName }];
    else if (path.endsWith('/applications') && method === 'GET') result = applications;
    else if (path.endsWith('/applications') && method === 'POST') {
      const body = request.postDataJSON();
      result = { id: '55555555-5555-4555-8555-555555555555', name: body.name };
      applications.push(result);
    }
    else if (path.endsWith('/members') && method === 'GET') result = members;
    else if (path.endsWith('/members') && method === 'POST') {
      const body = request.postDataJSON();
      result = { id: 'new-member', userName: body.userIdentifier, displayName: body.userIdentifier, role: body.role };
      members.push(result);
    }
    else if (path === '/licensing/licenses' || path === '/admin/licensing/licenses' && method === 'GET') result = { asOfUtc: '2026-10-25T12:00:00Z', licenses: [license] };
    else if (path === '/licensing/activations') result = { asOfUtc: '2026-10-25T12:00:00Z', activations: [activation] };
    else if (path.endsWith(`/licenses/${license.id}/activations`)) result = [activation];
    else if (path === '/admin/overview') result = { accounts: 1, openTickets: 0, pendingInvitations: 0, emailDeliveryConfigured: true, publicRegistrationEnabled: true, queuedMessages: 0, sentMessages: 0, failedMessages: 0 };
    else if (path === '/admin/accounts') result = [];
    else if (method === 'DELETE' || path.endsWith('/revoke')) {
      activation.status = 'revoked';
      if (path === `/admin/licensing/licenses/${license.id}/revoke`) license.status = 'revoked';
      result = { revoked: true };
    }
    else if (method === 'POST' && path === '/admin/licensing/licenses') result = { ...license, ...request.postDataJSON() };
    else result = {};
    await route.fulfill({ json: result });
  });
  const browserErrors = [];
  page.on('pageerror', error => browserErrors.push(error.message));
  await page.goto(`${baseUrl}/portal/#licenses`);
  await expect(page.locator('.license-card')).toHaveCount(1);
  return { calls, browserErrors, license, activation, orgId, applicationId };
}

test('member sees application and license identity, filters, and deactivates only their installation', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  const { calls, browserErrors, license } = await fixture(page);
  await expect(page.getByRole('heading', { name: license.applicationName }).first()).toBeVisible();
  await expect(page.getByLabel('License code', { exact: true })).toHaveValue(license.publicCode);
  await expect(page.getByRole('button', { name: 'Create entitlement' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Add application', exact: true })).toHaveCount(0);
  await page.screenshot({ path: test.info().outputPath('licensing-desktop.png') });
  await page.getByLabel('Find licenses').fill('does-not-exist');
  await expect(page.locator('.license-card')).toHaveCount(0);
  await page.getByLabel('Find licenses').fill('laboratory');
  await expect(page.locator('.license-card')).toHaveCount(1);
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Deactivate installation' }).click();
  await expect(page.getByRole('button', { name: 'Deactivate installation' })).toHaveCount(0);
  expect(calls).toContainEqual(expect.objectContaining({ method: 'DELETE', csrf: 'synthetic-csrf' }));
  expect(calls.some(call => call.path.startsWith('/admin/'))).toBe(false);
  expect(browserErrors).toEqual([]);
});

test('organization manager adds applications and members without issuing a license', async ({ page }) => {
  const { calls, browserErrors } = await fixture(page, { role: 'Owner' });
  await page.getByLabel('Application name', { exact: true }).fill('Campus water station');
  await page.getByRole('button', { name: 'Add application', exact: true }).click();
  await expect.poll(() => calls.filter(call => call.path.endsWith('/applications')).length).toBe(1);
  await expect(page.getByText('Campus water station', { exact: true })).toBeVisible();
  await page.getByLabel('Email or username of the confirmed account').fill('new.member');
  await page.getByLabel('Organization role').selectOption('Admin');
  await page.getByRole('button', { name: 'Add member', exact: true }).click();
  await expect.poll(() => calls.filter(call => call.path.endsWith('/members')).length).toBe(1);
  await expect(page.locator('.organization-member').filter({ hasText: 'new.member' })).toBeVisible();
  expect(calls.find(call => call.path.endsWith('/members')).body).toEqual({ userIdentifier: 'new.member', role: 'Admin' });
  expect(calls.some(call => call.path.startsWith('/admin/'))).toBe(false);
  expect(browserErrors).toEqual([]);
});

test('portal owner explicitly defines entitlements and cannot extend a perpetual license', async ({ page }) => {
  const { calls, browserErrors, applicationId } = await fixture(page, { owner: true });
  await page.getByRole('button', { name: 'Administration', exact: true }).click();
  await page.locator('.admin-subnav').getByRole('button', { name: 'Licenses', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'New entitlement', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Create entitlement' })).toBeDisabled();
  await page.getByLabel('Organization', { exact: true }).selectOption({ label: 'Synthetic Engineering School' });
  await page.getByLabel('Application', { exact: true }).selectOption(applicationId);
  await page.getByLabel('Tag capacity').selectOption('Tags1000');
  await page.getByLabel('Entitlement term', { exact: true }).selectOption('annual');
  await page.getByLabel('Total Interactive sessions').fill('3');
  await page.getByLabel('Total View Only sessions').fill('7');
  await expect(page.getByLabel('Activated installation limit')).toHaveValue('');
  await page.getByLabel('Activated installation limit').fill('2');
  await page.getByLabel('Include redundancy / HA Runtime').check();
  await page.getByRole('button', { name: 'Create entitlement' }).click();
  await expect.poll(() => calls.some(call => call.path === '/admin/licensing/licenses')).toBe(true);
  expect(calls.find(call => call.path === '/admin/licensing/licenses').body).toEqual({ applicationId, tier: 'Tags1000', term: 'annual', interactiveSeats: 3, viewOnlySeats: 7, maxActivations: 2, haRuntime: true, assetProcessIntelligence: false });
  await page.getByText('Manage license', { exact: true }).click();
  await expect(page.getByRole('button', { name: 'Extend license', exact: true })).toHaveCount(0);
  expect(browserErrors).toEqual([]);
});

for (const [locale, title, searchLabel] of [['pt-BR', 'Suas licenças', 'Buscar licenças'], ['en', 'Your licenses', 'Find licenses'], ['es', 'Tus licencias', 'Buscar licencias']]) {
  test(`licensing remains within a 360px viewport in ${locale}`, async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 800 });
    const { browserErrors } = await fixture(page, { locale });
    await expect(page.getByRole('heading', { name: title, exact: true })).toBeVisible();
    await expect(page.getByLabel(searchLabel)).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(browserErrors).toEqual([]);
    await page.screenshot({ path: test.info().outputPath(`licensing-mobile-${locale}.png`) });
  });
}

test('subscription extension and confirmed revocation target the selected license', async ({ page }) => {
  const { calls, browserErrors, license } = await fixture(page, { owner: true, term: 'monthly' });
  await page.getByRole('button', { name: 'Administration', exact: true }).click();
  await page.locator('.admin-subnav').getByRole('button', { name: 'Licenses', exact: true }).click();
  await page.getByText('Manage license', { exact: true }).click();
  await page.getByLabel('Extend entitlement', { exact: true }).selectOption('annual');
  await page.getByRole('button', { name: 'Extend license', exact: true }).click();
  await expect.poll(() => calls.some(call => call.path.endsWith('/extend'))).toBe(true);
  expect(calls.find(call => call.path.endsWith('/extend'))).toMatchObject({ path: `/admin/licensing/licenses/${license.id}/extend`, csrf: 'synthetic-csrf', body: { term: 'annual' } });
  await page.getByText('Manage license', { exact: true }).click();
  await page.getByText('Revoke license', { exact: true }).click();
  await page.getByLabel('Reason', { exact: true }).fill('Synthetic revocation test');
  page.once('dialog', dialog => dialog.dismiss());
  await page.getByRole('button', { name: 'Confirm revocation' }).click();
  expect(calls.some(call => call.path.endsWith('/revoke'))).toBe(false);
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Confirm revocation' }).click();
  await expect(page.locator('.license-card .status-revoked')).toBeVisible();
  expect(calls.find(call => call.path.endsWith('/revoke'))).toMatchObject({ path: `/admin/licensing/licenses/${license.id}/revoke`, csrf: 'synthetic-csrf', body: { reason: 'Synthetic revocation test' } });
  expect(browserErrors).toEqual([]);
});

for (const [availability, expected] of [
  [true, 'Online activation and renewal enabled.'],
  [false, 'Online activation and renewal disabled.'],
  [null, 'Online licensing availability could not be checked.']
]) {
  test(`administration reports runtime licensing availability ${availability}`, async ({ page }) => {
    const { browserErrors } = await fixture(page, { owner: true });
    await page.route('**/portal/api/licensing/status', route => availability === null
      ? route.fulfill({ status: 503, json: { error: 'unavailable' } })
      : route.fulfill({ json: { onlineActivationAvailable: availability } }));
    await page.getByRole('button', { name: 'Administration', exact: true }).click();
    await expect(page.getByText(expected, { exact: false })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Portal accounts', exact: true })).toBeVisible();
    await expect(page.getByText('Payments and online licensing are not active.', { exact: false })).toHaveCount(0);
    await page.screenshot({ path: test.info().outputPath(`admin-status-${availability}.png`) });
    expect(browserErrors).toEqual([]);
  });
}
