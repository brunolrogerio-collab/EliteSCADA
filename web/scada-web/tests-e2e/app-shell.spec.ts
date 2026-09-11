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
  await expect(page.getByTestId('runtime-engineering-application').or(page.getByTestId('runtime-simulation-fallback'))).toBeVisible();
  await expect(page.locator('.eng-shell')).toHaveCount(0);
  await expect(page.locator('.runtime-tag-inspector')).toHaveCount(0);

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

test('Engineering visual workspace can reclaim constrained viewport without losing panels', async ({ page }) => {
  await page.setViewportSize({ width: 1024, height: 720 });
  await page.goto('/engineering');

  const engineeringNavigation = page.locator('.eng-nav');
  await engineeringNavigation.getByRole('button', { name: /Telas/ }).click();
  await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();

  const canvas = page.locator('.visual-editor-canvas-slot');
  const screens = page.locator('.visual-editor-screens');
  const palette = page.locator('.visual-editor-palette-slot');
  const properties = page.locator('.visual-editor-inspector-slot');
  const initialCanvas = await canvas.boundingBox();
  expect(initialCanvas).not.toBeNull();

  const navigationToggle = page.getByTestId('engineering-navigation-toggle');
  const screensToggle = page.getByTestId('visual-editor-screens-toggle');
  const paletteToggle = page.getByTestId('visual-editor-palette-toggle');
  const propertiesToggle = page.getByTestId('visual-editor-properties-toggle');

  await navigationToggle.click();
  await expect(navigationToggle).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.eng-sidebar')).toBeHidden();

  await screensToggle.click();
  await expect(screensToggle).toHaveAttribute('aria-pressed', 'true');
  await expect(screens).toBeHidden();

  await paletteToggle.click();
  await expect(paletteToggle).toHaveAttribute('aria-pressed', 'true');
  await expect(palette).toBeHidden();

  const reclaimedCanvas = await canvas.boundingBox();
  expect(reclaimedCanvas).not.toBeNull();
  expect(reclaimedCanvas!.width).toBeGreaterThan(initialCanvas!.width);

  await propertiesToggle.click();
  await expect(propertiesToggle).toHaveAttribute('aria-pressed', 'true');
  await expect(properties).toBeHidden();
  await propertiesToggle.click();
  await expect(propertiesToggle).toHaveAttribute('aria-pressed', 'false');
  await expect(properties).toBeVisible();

  await paletteToggle.click();
  await screensToggle.click();
  await navigationToggle.click();
  await expect(page.locator('.eng-sidebar')).toBeVisible();
  await expect(screens).toBeVisible();
  await expect(palette).toBeVisible();
  await expect(properties).toBeVisible();

  await expect.poll(() => page.locator('.visual-editor-screen-list').evaluate(element => getComputedStyle(element).overflowY)).toBe('auto');
  await expect.poll(() => palette.evaluate(element => getComputedStyle(element).overflowY)).toBe('auto');
  await expect.poll(() => properties.evaluate(element => getComputedStyle(element).overflowY)).toBe('auto');
});
