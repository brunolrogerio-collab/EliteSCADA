import { expect, request as playwrightRequest, test } from '@playwright/test';
import { createE2eJwt } from './jwt';
import {
  annotateReadinessIssue,
  E2E_BASE_URL,
  loginLocalDeveloper,
  openAnonymousContext,
  setProductLocale
} from './wave-03-readiness-helpers';

const localeExpectations = [
  {
    locale: 'pt-BR' as const,
    subtitle: 'Plataforma industrial',
    currentArea: 'Área atual',
    operations: 'Visão operacional',
    alarms: 'Central de alarmes',
    audit: 'Auditoria'
  },
  {
    locale: 'en' as const,
    subtitle: 'Industrial platform',
    currentArea: 'Current area',
    operations: 'Operational overview',
    alarms: 'Alarm center',
    audit: 'Audit'
  },
  {
    locale: 'es' as const,
    subtitle: 'Plataforma industrial',
    currentArea: 'Área actual',
    operations: 'Vista operacional',
    alarms: 'Centro de alarmas',
    audit: 'Auditoría'
  }
];

test.use({ locale: 'pt-BR' });

test('Wave 03 readiness: local session survives Runtime -> Engineering -> Audit navigation and logout', async ({ browser }) => {
  const context = await openAnonymousContext(browser);
  const page = await context.newPage();

  try {
    await loginLocalDeveloper(page);

    const navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
    await expect(navigation.getByRole('link', { name: /Runtime/ })).toHaveAttribute('aria-current', 'page');
    await expect(page.getByRole('region', { name: 'Visão operacional' })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Central de alarmes' })).toBeVisible();

    const account = page.locator('.user-session-menu');
    await expect(account).toBeVisible();
    await expect(account.locator('summary')).toHaveAttribute('aria-label', /Conta: Local Developer/);

    await navigation.getByRole('link', { name: /Engineering/ }).click();
    await expect(page).toHaveURL(/\/engineering$/);
    await expect(page.locator('.eng-shell')).toBeVisible();
    await expect(page.locator('.user-session-menu')).toBeVisible();

    await page.getByRole('navigation', { name: 'EliteSCADA' }).getByRole('link', { name: /Auditoria/ }).click();
    await expect(page).toHaveURL(/\/audit$/);
    await expect(page.getByRole('heading', { name: 'Auditoria' })).toBeVisible();
    await expect(page.locator('.user-session-menu')).toBeVisible();

    await page.locator('.user-session-menu summary').click();
    await page.getByRole('button', { name: 'Sair' }).click();
    await expect(page.locator('.auth-card')).toBeVisible();
    await expect.poll(async () => page.evaluate(async () => (await fetch('/api/auth/me')).status)).toBe(401);
  } finally {
    await context.close();
  }
});

test('Wave 03 readiness: Runtime stays operational while TAG/history diagnostics live in Engineering', async ({ page, request }) => {
  await page.goto('/');

  await expect(page.getByRole('region', { name: 'Visão operacional' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Central de alarmes' })).toBeVisible();
  await expect(page.locator('.runtime-tag-inspector')).toHaveCount(0);
  await expect(page.getByText(/ONLINE · 7 TAGs/)).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText('Reservatório TK01')).toBeVisible();

  const tagsResponse = await request.get('/api/tags');
  expect(tagsResponse.ok()).toBeTruthy();
  const tags = await tagsResponse.json() as Array<{ id: string; path: string; readOnly: boolean }>;
  expect(tags.length).toBeGreaterThan(0);

  const currentTag = tags.find(tag => tag.path === 'Demo.P01.Current');
  expect(currentTag).toBeTruthy();
  expect(currentTag!.readOnly).toBeTruthy();

  const currentResponse = await request.get('/api/tags/current');
  expect(currentResponse.ok()).toBeTruthy();

  const historyResponse = await request.get(`/api/history/${currentTag!.id}?limit=5`);
  expect(historyResponse.ok()).toBeTruthy();
  const history = await historyResponse.json() as Array<{ timestamp: string; quality: unknown }>;
  expect(Array.isArray(history)).toBeTruthy();
  expect(history.length).toBeGreaterThan(0);
  expect(history[0].timestamp).toBeTruthy();
  expect(history[0]).toHaveProperty('quality');
  expect(history[0].quality).not.toBeNull();

  await page.goto('/engineering/diagnostics/tag-monitor');
  const tagMonitor = page.getByTestId('engineering-tag-monitor');
  await expect(tagMonitor).toBeVisible();
  const inspector = tagMonitor.getByRole('region', { name: 'Inspector de TAGs' });
  await expect(inspector).toBeVisible();
  await expect(inspector.getByRole('listbox', { name: 'Inspector de TAGs' }).getByText(currentTag!.path, { exact: true })).toBeVisible({ timeout: 15_000 });

  // This acceptance harness is deliberately read-only. Process writes are covered by separate authority tests.
  const writeRequests = await page.evaluate(() => performance.getEntriesByType('resource')
    .map(entry => entry.name)
    .filter(name => /\/api\/tags\/[^/]+\/write(?:\?|$)/.test(name)));
  expect(writeRequests).toEqual([]);
});

test('Wave 03 readiness: Engineering exposes the configured domains, Gateway, diagnostics and lifecycle baseline', async ({ page, request }, testInfo) => {
  await page.goto('/engineering');
  await expect(page.locator('.eng-shell')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Visão geral do projeto' })).toBeVisible();

  const lifecycle = page.locator('.eng-lifecycle-workspace');
  await expect(lifecycle).toBeVisible();
  await expect(lifecycle).toContainText('Working');
  await expect(lifecycle).toContainText('Published');
  await expect(lifecycle).toContainText('Active');

  const engineeringNavigation = page.locator('.eng-nav');

  await engineeringNavigation.getByRole('button', { name: /Data Sources/ }).click();
  await expect(page.getByRole('heading', { name: 'Editor estruturado de Data Sources' })).toBeVisible();
  await expect(page.locator('.engineering-entity-browser').getByRole('searchbox')).toBeVisible();
  await expect(page.getByTestId('gateway-engineering-panel')).toBeVisible();

  await engineeringNavigation.getByRole('button', { name: /TAGs/ }).click();
  await expect(page.getByRole('heading', { name: 'Editor estruturado de TAGs' })).toBeVisible();
  await expect(page.locator('.engineering-entity-browser').getByRole('searchbox')).toBeVisible();

  const memoryResponse = await request.get('/api/internal-memory/client/definitions');
  expect(memoryResponse.ok()).toBeTruthy();
  const memoryDefinitions = await memoryResponse.json() as unknown[];
  if (memoryDefinitions.length === 0) {
    annotateReadinessIssue(
      testInfo,
      'TEST GAP',
      'Internal Memory acceptance fixture',
      'The WaveBase demo has no Client Memory definitions, so the default cross-product journey cannot exercise the Memory settings UI. Dedicated internal-memory.spec.ts covers the product contract; a later integrated demo fixture should add a canonical memory entity.'
    );
  }

  await engineeringNavigation.getByRole('button', { name: /Alarmes/ }).click();
  await expect(page.getByRole('heading', { name: 'Editor estruturado de Alarmes' })).toBeVisible();

  await engineeringNavigation.getByRole('button', { name: /Diagnósticos/ }).click();
  await expect(page.getByRole('heading', { name: 'Comunicação ativa' })).toBeVisible();

  const persistenceStatusResponse = await request.get('/api/engineering/persistence/status');
  expect(persistenceStatusResponse.ok()).toBeTruthy();
  const persistenceStatus = await persistenceStatusResponse.json() as {
    enabled: boolean;
    configuredProjectKey?: string | null;
  };

  expect(persistenceStatus.enabled).toBeTruthy();
  expect(persistenceStatus.configuredProjectKey).toBeTruthy();
  const lifecycleResponse = await request.get(`/api/engineering/persistence/${encodeURIComponent(persistenceStatus.configuredProjectKey!)}/lifecycle`);
  expect(lifecycleResponse.ok()).toBeTruthy();
});

test('Wave 03 readiness: Audit and user administration remain backend-authorized', async ({ request }) => {
  expect((await request.get('/api/audit?limit=1')).status()).toBe(200);
  expect((await request.get('/api/audit/diagnostics')).status()).toBe(200);
  expect((await request.get('/api/auth/users')).status()).toBe(200);

  const operator = await playwrightRequest.newContext({
    baseURL: E2E_BASE_URL,
    extraHTTPHeaders: {
      Authorization: `Bearer ${createE2eJwt('wave03-operator', ['operator'], 'Wave 03 Operator')}`
    }
  });
  const anonymous = await playwrightRequest.newContext({
    baseURL: E2E_BASE_URL,
    extraHTTPHeaders: { Authorization: '' }
  });

  try {
    expect((await operator.get('/api/audit?limit=1')).status()).toBe(403);
    expect((await operator.get('/api/audit/diagnostics')).status()).toBe(403);
    expect((await operator.get('/api/auth/users')).status()).toBe(403);

    expect((await anonymous.get('/api/audit?limit=1')).status()).toBe(401);
    expect((await anonymous.get('/api/audit/diagnostics')).status()).toBe(401);
    expect((await anonymous.get('/api/auth/users')).status()).toBe(401);
  } finally {
    await operator.dispose();
    await anonymous.dispose();
  }
});

for (const expected of localeExpectations) {
  test(`Wave 03 readiness: Runtime, Engineering, Audit and session states follow ${expected.locale}`, async ({ page }) => {
    await setProductLocale(page, expected.locale);
    await page.goto('/');

    const runtimeNavigation = page.getByRole('navigation', { name: 'EliteSCADA' });
    await expect(page.getByText(expected.subtitle, { exact: true })).toBeVisible();
    await expect(page.locator('.app-context')).toContainText(expected.currentArea);
    await expect(page.getByRole('region', { name: expected.operations })).toBeVisible();
    await expect(page.getByRole('region', { name: expected.alarms })).toBeVisible();
    await expect(page.locator('.user-session-menu')).toBeVisible();

    const engineeringLink = runtimeNavigation.getByRole('link', { name: /Engineering/ });
    await expect(engineeringLink).toHaveAttribute('href', '/engineering');
    await engineeringLink.click();
    await expect(page).toHaveURL(/\/engineering$/);
    await expect(page.locator('.eng-shell')).toBeVisible();
    await expect(page.locator('#engineering-locale')).toHaveValue(expected.locale);

    const engineeringNavigation = page.getByRole('navigation', { name: 'EliteSCADA' });
    const auditLink = engineeringNavigation.getByRole('link', { name: new RegExp(expected.audit) });
    await expect(auditLink).toHaveAttribute('href', '/audit');
    await auditLink.click();
    await expect(page).toHaveURL(/\/audit$/);
    await expect(page.getByRole('heading', { name: expected.audit, exact: true })).toBeVisible();
  });
}
