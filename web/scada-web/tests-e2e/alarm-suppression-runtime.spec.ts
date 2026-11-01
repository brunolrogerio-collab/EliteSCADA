import { expect, test, type Page, type Route } from '@playwright/test';
import type { RuntimeAlarmCenterItem, RuntimeAlarmSuppressionCause, RuntimeAlarmSuppressionSnapshot } from '../src/runtime/alarmCenterTypes';
import { selectUnacknowledgedAlarmSounds } from '../src/runtime/alarmSounds';

const harnessPath = '/tests-e2e/alarm-suppression-harness.html';
const alarmId = '11111111-1111-1111-1111-111111111111';
const locationId = '22222222-2222-2222-2222-222222222222';

function alarm(suppressed: boolean, causes: RuntimeAlarmSuppressionCause[] = []): RuntimeAlarmCenterItem {
  return {
    definitionId: alarmId,
    name: 'Pressão alta da bomba',
    tagId: '33333333-3333-3333-3333-333333333333',
    type: 'High',
    priority: 'high',
    state: 'Active',
    lastTransition: '2026-10-31T10:00:00Z',
    lastValue: 12.5,
    area: 'Authority.Process',
    message: 'Pressão acima do limite',
    activatedAt: '2026-10-31T10:00:00Z',
    locationId,
    locationPath: 'Planta / Casa de bombas',
    isSuppressed: suppressed,
    suppressionCauses: causes
  };
}

function snapshot(suppressed: boolean): RuntimeAlarmSuppressionSnapshot {
  const causes: RuntimeAlarmSuppressionCause[] = suppressed ? [{
    targetKind: 'location',
    targetId: locationId,
    targetName: 'Casa de bombas',
    targetPath: 'Planta / Casa de bombas',
    actor: 'Operador',
    reasonCode: 'maintenance',
    appliedAtUtc: '2026-10-31T10:00:00Z',
    expiresAtUtc: '2026-10-31T14:00:00Z'
  }] : [];
  return {
    projectKey: 'e2e-project',
    engineeringRevision: 3,
    suppressionRevision: suppressed ? 1 : 0,
    alarms: [{
      definitionId: alarmId,
      name: 'Pressão alta da bomba',
      area: 'Authority.Process',
      locationId,
      locationPath: 'Planta / Casa de bombas',
      isSuppressed: suppressed,
      suppressionCauses: causes
    }],
    locations: [{
      id: locationId,
      parentLocationId: null,
      name: 'Casa de bombas',
      path: 'Planta / Casa de bombas',
      alarmCount: 1,
      suppressedAlarmCount: suppressed ? 1 : 0
    }]
  };
}

async function fulfillJson(route: Route, body: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}

async function installApi(page: Page) {
  let suppressed = false;
  let putBody: unknown;
  let deletePath = '';
  await page.route('**/api/alarms**', async route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/alarms/suppressions' && route.request().method() === 'GET')
      return fulfillJson(route, snapshot(suppressed));
    if (url.pathname === '/api/alarms/definitions')
      return fulfillJson(route, [{ id: alarmId, name: 'Pressão alta da bomba', tagId: '33333333-3333-3333-3333-333333333333', type: 'High', priority: 'high', area: 'Authority.Process', soundProfile: 'short' }]);
    if (url.pathname === '/api/alarms' && route.request().method() === 'GET')
      return fulfillJson(route, [alarm(suppressed, snapshot(suppressed).alarms[0].suppressionCauses)]);
    if (url.pathname.startsWith('/api/alarms/suppressions/') && route.request().method() === 'PUT') {
      putBody = route.request().postDataJSON();
      suppressed = true;
      return fulfillJson(route, { changed: true, affectedAlarmCount: 1 });
    }
    if (url.pathname.startsWith('/api/alarms/suppressions/') && route.request().method() === 'DELETE') {
      deletePath = url.pathname;
      suppressed = false;
      return fulfillJson(route, { changed: true, affectedAlarmCount: 1 });
    }
    return fulfillJson(route, { error: `Unexpected request: ${route.request().method()} ${url.pathname}` }, 404);
  });
  return { getPutBody: () => putBody, getDeletePath: () => deletePath };
}

test('Runtime applies and releases a location suppression while showing the active alarm and inherited cause', async ({ page }) => {
  const api = await installApi(page);
  await page.goto(harnessPath);
  await expect(page.getByRole('heading', { name: 'Central de alarmes' })).toBeVisible();
  await expect(page.getByRole('button', { name: /Pressão alta da bomba/ })).toBeVisible();

  await page.getByRole('combobox', { name: 'Aplicar a' }).selectOption('location');
  await page.getByRole('combobox', { name: 'Área ou subárea' }).selectOption(locationId);
  await page.getByRole('combobox', { name: 'Motivo' }).selectOption('maintenance');
  await page.getByRole('combobox', { name: 'Duração' }).selectOption('4-hours');
  await page.getByRole('button', { name: 'Suprimir anúncio' }).click();

  await expect.poll(() => api.getPutBody()).toEqual({
    reasonCode: 'maintenance',
    expiresAtUtc: expect.any(String)
  });
  expect(api.getPutBody()).toMatchObject({ reasonCode: 'maintenance' });
  await expect(page.locator('.runtime-alarm-suppressed-detail')).toContainText('Supressão ativa · Ativo');
  await expect(page.locator('.runtime-alarm-suppressed-detail')).toContainText('Esta supressão vem de uma área ou subárea acima.');
  await expect(page.getByLabel('Detalhes').getByText('Ativo', { exact: true })).toBeVisible();

  await page.locator('.runtime-alarm-suppressed-detail').getByRole('button', { name: 'Liberar supressão' }).click();
  await expect.poll(() => api.getDeletePath()).toBe(`/api/alarms/suppressions/location/${locationId}`);
  await expect(page.locator('.runtime-alarm-suppressed-detail')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Suprimir anúncio' })).toBeVisible();
});

test('suppressed active alarms are excluded from sound without changing their alarm state', () => {
  const suppressed = alarm(true, snapshot(true).alarms[0].suppressionCauses);
  const sounds = selectUnacknowledgedAlarmSounds([suppressed], [{
    id: alarmId,
    name: suppressed.name,
    tagId: suppressed.tagId,
    type: suppressed.type,
    priority: suppressed.priority,
    soundProfile: 'short'
  }]);

  expect(suppressed.state).toBe('Active');
  expect(suppressed.isSuppressed).toBe(true);
  expect(sounds).toEqual([]);
});
