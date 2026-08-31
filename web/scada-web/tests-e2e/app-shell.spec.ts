import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('primary shell keeps Runtime, Engineering, Audit and Licensing navigation coherent', async ({ page }) => {
  await page.goto('/');

  let navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  let context = page.locator('.app-context');
  await expect(page.getByRole('link', { name: 'EliteSCADA Runtime' })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Runtime/ })).toHaveAttribute('aria-current', 'page');
  await expect(context).toContainText('Área atual');
  await expect(context).toContainText('Runtime');
  await expect(page.getByRole('region', { name: 'Visão operacional' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Central de alarmes' })).toBeVisible();
  await expect(page.getByText('Trend básico', { exact: true })).toBeVisible();

  let runtimeViews = page.getByRole('navigation', { name: 'Runtime views' });
  await expect(runtimeViews.getByRole('link', { name: 'Visão geral' })).toHaveAttribute('aria-current', 'page');
  await runtimeViews.getByRole('link', { name: 'Histórico' }).click();
  await expect(page).toHaveURL(/\/runtime\/history$/);
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  runtimeViews = page.getByRole('navigation', { name: 'Runtime views' });
  await expect(navigation.getByRole('link', { name: /Runtime/ })).toHaveAttribute('aria-current', 'page');
  await expect(runtimeViews.getByRole('link', { name: 'Histórico' })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId('historical-data-browser-runtime')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Visão operacional' })).toHaveCount(0);

  await runtimeViews.getByRole('link', { name: 'Visão geral' }).click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole('region', { name: 'Visão operacional' })).toBeVisible();

  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await navigation.getByRole('link', { name: /Engineering/ }).click();
  await expect(page).toHaveURL(/\/engineering$/);
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  context = page.locator('.app-context');
  await expect(navigation.getByRole('link', { name: /Engineering/ })).toHaveAttribute('aria-current', 'page');
  await expect(context).toContainText('Engineering');
  await expect(page.getByText(/Gerenciamento do projeto|Project Management/, { exact: true })).toBeVisible();

  const engineeringNavigation = page.locator('.eng-nav');
  await engineeringNavigation.getByRole('button', { name: /Scripts/ }).click();
  await expect(page.getByRole('heading', { name: 'Scripts de Engenharia' })).toBeVisible();
  await expect(page.getByText('Editor sem autoridade de execução', { exact: true })).toBeVisible();

  await engineeringNavigation.getByRole('button', { name: /TAGs/ }).click();
  const entityBrowser = page.locator('.engineering-entity-browser');
  await expect(entityBrowser.getByRole('searchbox')).toBeVisible();
  await expect(entityBrowser.getByRole('listbox', { name: /TAGs: lista de entidades/ })).toBeVisible();

  await engineeringNavigation.getByRole('button', { name: /Segurança|Security/ }).click();
  await expect(page.getByTestId('user-administration')).toBeVisible();

  await navigation.getByRole('link', { name: /Auditoria/ }).click();
  await expect(page).toHaveURL(/\/audit$/);
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  context = page.locator('.app-context');
  await expect(navigation.getByRole('link', { name: /Auditoria/ })).toHaveAttribute('aria-current', 'page');
  await expect(context).toContainText('Auditoria');

  await navigation.getByRole('link', { name: /Licenciamento/ }).click();
  await expect(page).toHaveURL(/\/licensing$/);
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  context = page.locator('.app-context');
  await expect(navigation.getByRole('link', { name: /Licenciamento/ })).toHaveAttribute('aria-current', 'page');
  await expect(context).toContainText('Licenciamento');
  await expect(page.getByRole('heading', { name: 'Licenciamento' })).toBeVisible();
  await expect(page.getByText('Demo', { exact: true }).first()).toBeVisible();
  await expect(page.getByText('200', { exact: true }).first()).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'Solicitação da máquina' })).toHaveValue(/^ESREQ1\./);
});

test('primary shell follows the stored Engineering locale', async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem('elitescada.engineering.locale', 'en'));
  await page.goto('/engineering');

  const navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  const context = page.locator('.app-context');
  await expect(navigation.getByRole('link', { name: /Audit/ })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Licensing/ })).toBeVisible();
  await expect(page.getByText('Industrial platform', { exact: true })).toBeVisible();
  await expect(context).toContainText('Current area');
  await expect(context).toContainText('Engineering');
  await expect(page.getByText('Project Management', { exact: true })).toBeVisible();
});