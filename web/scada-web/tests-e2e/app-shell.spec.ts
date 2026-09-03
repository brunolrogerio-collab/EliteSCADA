import { expect, test, type Locator } from '@playwright/test';

test.use({ locale: 'pt-BR' });

async function expectCssToken(locator: Locator, property: string, token: string) {
  const colors = await locator.evaluate((element, args) => {
    const tokenValue = getComputedStyle(document.documentElement).getPropertyValue(args.token).trim();
    const probe = document.createElement('span');
    probe.style.color = tokenValue;
    document.body.append(probe);
    const expected = getComputedStyle(probe).color;
    probe.remove();
    return {
      actual: getComputedStyle(element).getPropertyValue(args.property),
      expected
    };
  }, { property, token });
  expect(colors.actual).toBe(colors.expected);
}

test('primary shell keeps authorized application navigation coherent without Engineering chrome inside Runtime', async ({ page }) => {
  await page.goto('/');

  let navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Runtime/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId('runtime-engineering-application').or(page.locator('main').filter({ hasText: /Runtime/ }).first())).toBeVisible();
  await expect(page.getByRole('region', { name: 'Visão operacional' })).toHaveCount(0);
  await expect(page.getByText('Trend básico', { exact: true })).toHaveCount(0);

  const theme = page.getByRole('combobox', { name: 'Tema' });
  await expect(theme).toBeVisible();
  await theme.selectOption('light');
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');

  const runtimeViews = page.getByRole('navigation', { name: 'Runtime views' });
  if (await runtimeViews.count()) {
    await runtimeViews.getByRole('link', { name: 'Histórico' }).click();
    await expect(page).toHaveURL(/\/runtime\/history$/);
    await expect(page.getByTestId('historical-data-browser-runtime')).toBeVisible();
    await expectCssToken(page.locator('.runtime-history-page'), 'background-color', '--app-bg');
  }

  await page.goto('/engineering');
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Engineering/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByText(/Gerenciamento do projeto|Project Management/, { exact: true })).toBeVisible();
  await expectCssToken(page.locator('.eng-shell'), 'background-color', '--app-bg');

  const engineeringNavigation = page.locator('.eng-nav');
  await engineeringNavigation.getByRole('button', { name: /Diagnósticos|Diagnostics/ }).click();
  await expect(page.getByText(/TAG Monitor/i)).toBeVisible();

  await page.goto('/audit');
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Auditoria/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('.audit-panel').first()).toBeVisible();
  await expectCssToken(page.locator('.audit-panel').first(), 'background-color', '--app-surface');

  await page.goto('/licensing');
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Licenciamento/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByRole('heading', { name: 'Licenciamento' })).toBeVisible();
  await expect(page.locator('.licensing-card').first()).toBeVisible();
  await expectCssToken(page.locator('.licensing-card').first(), 'background-color', '--app-surface');
});

test('shell uses shared locale, updates live from Engineering selector, and preserves personal theme independently', async ({ page }) => {
  await page.addInitScript(() => {
    window.localStorage.setItem('elitescada.engineering.locale', 'en');
    window.localStorage.setItem('elitescada.app.theme', 'dark');
  });
  await page.goto('/engineering');

  const navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Audit/ })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Licensing/ })).toBeVisible();
  await expect(page.getByText('Industrial platform', { exact: true })).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'dark');
  await expect(page.getByRole('combobox', { name: 'Theme' })).toHaveValue('dark');
  await expect(page.getByText('Project Management', { exact: true })).toBeVisible();

  const locale = page.locator('#engineering-locale');
  await locale.selectOption('es');
  await expect(page.getByText('Plataforma industrial', { exact: true })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Auditoría/ })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Licenciamiento/ })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Tema' })).toHaveValue('dark');

  await locale.selectOption('pt-BR');
  await expect(page.getByText('Plataforma industrial', { exact: true })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Auditoria/ })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Licenciamento/ })).toBeVisible();
});
