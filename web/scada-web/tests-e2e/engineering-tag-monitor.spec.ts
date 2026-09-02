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
  await expect(page.getByText('Engineering / Diagnostics', { exact: true })).toBeVisible();

  const context = page.getByTestId('tag-monitor-context');
  await expect(context.getByText('Contexto Engineering', { exact: true })).toBeVisible();
  await expect(context.getByText('Revisão Working', { exact: true })).toBeVisible();
  await expect(context.getByText('Fonte observada', { exact: true })).toBeVisible();
  await expect(context.getByText('Active Runtime', { exact: true })).toBeVisible();
  await expect(context.getByText('Simulação / demo', { exact: true })).toBeVisible();
  await expect(page.getByTestId('tag-monitor-runtime-boundary')).toContainText('Working é contexto de engenharia');

  const inspector = page.locator('.runtime-tag-inspector');
  await expect(inspector).toBeVisible();
  await expect(inspector.getByRole('heading', { name: 'Inspector de TAGs' })).toBeVisible();
  await expect(inspector.getByText('Runtime / TAGs', { exact: true })).toBeVisible();
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

test('operator-only cannot obtain Engineering TAG Monitor through its direct URL', async ({ browser }) => {
  const operatorToken = createE2eJwt('c06-operator', ['operator'], 'C06 Operator');
  const context = await browser.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${operatorToken}` }
  });

  try {
    const page = await context.newPage();
    await page.goto('/engineering/diagnostics/tag-monitor');

    await expect(page.getByText('403 Forbidden', { exact: true })).toBeVisible();
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
