import { expect, test } from '@playwright/test';
import { createE2eJwt } from './jwt';

const baseURL = 'http://127.0.0.1:5173';

test.use({ locale: 'pt-BR' });

test('TAG Monitor is an Engineering diagnostic while its facts remain Active Runtime data', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('.runtime-tag-inspector')).toHaveCount(0);

  await page.goto('/engineering');
  const tagMonitor = page.getByRole('button', { name: /TAG Monitor/ });
  await expect(tagMonitor).toBeVisible();
  await tagMonitor.click();

  await expect(page).toHaveURL(/\/engineering\/diagnostics\/tag-monitor$/);
  await expect(page.getByTestId('engineering-tag-monitor')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'TAG Monitor', level: 1 })).toBeVisible();
  // The shared Engineering header intentionally hides eyebrow text to keep
  // section headers compact; the diagnostic identity is carried by the h1.
  await expect(page.getByText('Engenharia / Diagnósticos', { exact: true })).toBeAttached();

  const context = page.getByTestId('tag-monitor-context');
  await expect(context.getByText('Contexto Engineering', { exact: true })).toBeVisible();
  await expect(context.getByText('Revisão Working', { exact: true })).toBeVisible();
  await expect(context.getByText('Fonte observada', { exact: true })).toBeVisible();
  await expect(context.getByText('Active Runtime', { exact: true })).toBeVisible();
  // The explicit local-auth fixture publishes an Engineering application; it is
  // deliberately not the historical simulation fallback.
  await expect(context.getByText(/E2E Explicit Demo Fixture|E2E C01 First Project|e2e-wave03/).first()).toBeVisible();
  await expect(page.getByTestId('tag-monitor-runtime-boundary')).toContainText(/Working e Active|Working x Active/);

  const inspector = page.locator('.runtime-tag-inspector');
  await expect(inspector).toBeVisible();
  await expect(inspector.getByRole('heading', { name: 'Inspector de TAGs' })).toBeVisible();
  await expect(inspector.getByText('Realtime conectado', { exact: true })).toBeVisible({ timeout: 15_000 });

  await inspector.getByLabel('Buscar TAGs').fill('pressure');
  await expect(inspector.locator('.runtime-tag-row')).toHaveCount(1);
  await expect(inspector.locator('.runtime-tag-row').first()).toContainText('Demo.Discharge.Pressure');

  await inspector.getByLabel('Buscar TAGs').fill('');
  await inspector.getByRole('option').filter({ hasText: 'Demo.Tank01.Level' }).click();
  const terms = inspector.getByRole('term');
  await expect(terms.filter({ hasText: 'Qualidade' }).first()).toBeVisible();
  await expect(terms.filter({ hasText: 'Timestamp EliteSCADA' }).first()).toBeVisible();
  await expect(terms.filter({ hasText: 'Timestamp da origem' }).first()).toBeVisible();
  await expect(terms.filter({ hasText: 'Timestamp do servidor' }).first()).toBeVisible();
  await expect(terms.filter({ hasText: 'Tipo' }).first()).toBeVisible();
  await expect(terms.filter({ hasText: 'Unidade' }).first()).toBeVisible();
  await expect(terms.filter({ hasText: 'Origem / Data Source' }).first()).toBeVisible();
  await expect(terms.filter({ hasText: 'Acesso' }).first()).toBeVisible();
  await expect(terms.filter({ hasText: 'ID estável' }).first()).toBeVisible();
  await expect(inspector.getByRole('heading', { name: 'Histórico recente' })).toBeVisible();

  await expect(inspector.getByRole('button', { name: /gravar|escrever|write/i })).toHaveCount(0);
});

test('TAG Monitor windows large Runtime lists instead of mounting every TAG', async ({ page }) => {
  const now = new Date().toISOString();
  const tags = Array.from({ length: 5_000 }, (_, index) => {
    const suffix = String(index + 1).padStart(5, '0');
    const id = `capacity-tag-${suffix}`;
    return {
      id,
      name: `Tag${suffix}`,
      path: `Capacity.Tag${suffix}`,
      dataType: 'number',
      engineeringUnit: 'units',
      description: '',
      readOnly: true,
      current: { tagId: id, value: index + 1, timestamp: now, quality: 'good', source: 'capacity-fixture' }
    };
  });

  await page.route('**/api/tags', route => route.fulfill({ json: tags }));
  await page.route('**/api/tags/by-path/**', async route => {
    const path = decodeURIComponent(new URL(route.request().url()).pathname.split('/').pop() ?? '');
    const tag = tags.find(item => item.path === path) ?? tags[0];
    await route.fulfill({ json: { tag } });
  });
  await page.route('**/api/history/**', route => route.fulfill({ json: [] }));

  await page.goto('/engineering/diagnostics/tag-monitor');
  const inspector = page.locator('.runtime-tag-inspector');
  await expect(inspector.locator('.runtime-tag-summary-item').first().getByText('5000', { exact: true })).toBeVisible();
  const tagList = inspector.locator('.runtime-tag-list');
  expect(await tagList.getByRole('option').count()).toBeLessThan(40);
  await tagList.evaluate(element => { element.scrollTop = element.scrollHeight; });
  await expect(tagList.getByRole('option').filter({ hasText: 'Capacity.Tag05000' })).toBeVisible();
  expect(await tagList.getByRole('option').count()).toBeLessThan(40);

  await inspector.getByLabel('Buscar TAGs').fill('Tag05000');
  await expect(tagList.getByRole('option')).toHaveCount(1);
  await expect(tagList.getByRole('option')).toContainText('Capacity.Tag05000');
});

test('operator-only cannot obtain Engineering TAG Monitor through its direct URL', async ({ browser }) => {
  const operatorToken = createE2eJwt('c06-operator', ['operator'], 'C06 Operator');
  const context = await browser.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${operatorToken}` }
  });

  try {
    const page = await context.newPage();
    await page.goto('/engineering/diagnostics/tag-monitor');

    await expect(page.getByRole('alert')).toContainText('Você não possui permissão para acessar esta área.');
    await expect(page.getByRole('button', { name: /TAG Monitor/ })).toHaveCount(0);
    await expect(page.getByTestId('engineering-tag-monitor')).toHaveCount(0);
    await expect(page.locator('.runtime-tag-inspector')).toHaveCount(0);

    const engineering = await context.request.get('/api/engineering/workspace');
    expect(engineering.status()).toBe(403);

    // Runtime read authority remains independent: operator TAG reads are still valid,
    // but they do not grant the Engineering Diagnostics product surface.
    const tags = await context.request.get('/api/tags');
    expect(tags.ok()).toBeTruthy();
  } finally {
    await context.close();
  }
});
