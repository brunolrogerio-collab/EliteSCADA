import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('Runtime exposes an entry to the Engineering workspace', async ({ page }) => {
  await page.goto('/');

  const engineeringLink = page.getByRole('link', { name: 'Engineering' });
  await expect(engineeringLink).toBeVisible();
  await engineeringLink.click();

  await expect(page).toHaveURL(/\/engineering$/);
  await expect(page.getByText('EliteSCADA Engineering')).toBeVisible();
});

test('Engineering workspace renders the public model and switches locale without changing Engineering identifiers', async ({ page, request }) => {
  const engineeringResponse = await request.get('/api/engineering/export/json');
  expect(engineeringResponse.ok()).toBeTruthy();
  const engineering = await engineeringResponse.json() as {
    schema: string;
    schemaVersion: number;
    tags: Array<{ path: string }>;
  };

  await page.goto('/engineering');

  await expect(page.getByText('EliteSCADA Engineering')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Visão geral do projeto' })).toBeVisible();
  await expect(page.getByText(`${engineering.schema} v${engineering.schemaVersion}`)).toBeVisible();
  await expect(page.getByText(String(engineering.tags.length), { exact: true }).first()).toBeVisible();

  await page.getByRole('button', { name: /TAGs/ }).click();
  await expect(page.getByRole('heading', { name: 'TAGs' })).toBeVisible();
  await expect(page.getByText('Demo.P01.Frequency', { exact: true })).toBeVisible();
  await expect(page.getByText('Demo.Tank01.Level', { exact: true })).toBeVisible();

  const locale = page.getByLabel('Idioma');
  await locale.selectOption('en');
  await expect(page.getByText('Project', { exact: true })).toBeVisible();
  await expect(page.getByText('Overview', { exact: true })).toBeVisible();
  await expect(page.getByText('Demo.P01.Frequency', { exact: true })).toBeVisible();

  await page.getByLabel('Language').selectOption('es');
  await expect(page.getByText('Proyecto', { exact: true })).toBeVisible();
  await expect(page.getByText('Vista general', { exact: true })).toBeVisible();
  await expect(page.getByText('Demo.P01.Frequency', { exact: true })).toBeVisible();

  await page.reload();
  await expect(page.getByLabel('Idioma')).toHaveValue('es');
  await expect(page.getByText('Proyecto', { exact: true })).toBeVisible();
});

test('Engineering navigation exposes the current public engineering domains read-only', async ({ page }) => {
  await page.goto('/engineering');

  const sections = [
    { button: /Data Sources/, heading: 'Data Sources', expected: 'builtin.simulation' },
    { button: /Alarmes/, heading: 'Alarmes', expected: 'High discharge pressure' },
    { button: /Templates/, heading: 'Templates', expected: 'pump.standard' },
    { button: /Equipamentos/, heading: 'Equipamentos', expected: 'Demo.P01' },
    { button: /Dínamos/, heading: 'Dínamos', expected: 'dynamo.pump.standard' },
    { button: /Telas/, heading: 'Telas', expected: 'demo.overview' },
    { button: /Popups/, heading: 'Popups', expected: 'popup.pump.standard' },
    { button: /Segurança/, heading: 'Papéis e capacidades', expected: 'operator' }
  ];

  for (const section of sections) {
    await page.getByRole('button', { name: section.button }).click();
    await expect(page.getByRole('heading', { name: section.heading })).toBeVisible();
    await expect(page.getByText(section.expected, { exact: true }).first()).toBeVisible();
  }
});
