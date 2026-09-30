import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const activeProjection = (branding: unknown, visualAssets: unknown[] = []) => ({
  mode: 'engineering',
  projectKey: 'branding-proof',
  projectName: 'Branding Proof',
  revision: 7,
  activatedAtUtc: '2026-09-29T22:00:00Z',
  package: {
    schema: 'scada.engineering',
    schemaVersion: 21,
    screens: [],
    popups: [],
    dynamos: [],
    scripts: [],
    scriptVisualEventReferences: [],
    visualAssets,
    branding
  }
});

test('global shell mounts Active TEXT branding across dark, light and responsive layouts', async ({ page }) => {
  await page.route('**/api/runtime/application', route => route.fulfill({
    json: activeProjection({ mode: 'text', text: 'North Plant', subtitle: 'Operations' })
  }));
  await page.setViewportSize({ width: 1280, height: 760 });
  await page.goto('/engineering');

  const brand = page.locator('.app-brand');
  await expect(brand).toHaveAttribute('data-branding-mode', 'text');
  await expect(brand).toContainText('North Plant');
  await expect(brand).toContainText('Operations');
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', /dark|light/);

  await page.getByRole('combobox', { name: /Tema|Theme/ }).selectOption('light');
  await expect(page.locator('html')).toHaveAttribute('data-app-theme', 'light');
  await expect(brand).toBeVisible();

  await page.setViewportSize({ width: 700, height: 760 });
  await expect(brand).toBeVisible();
  const box = await brand.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.x).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width).toBeLessThanOrEqual(700);
});

test('NONE collapses the brand slot and invalid IMAGE falls back with an explicit diagnostic', async ({ page }) => {
  await page.route('**/api/runtime/application', route => route.fulfill({
    json: activeProjection({ mode: 'none' })
  }));
  await page.goto('/engineering');
  await expect(page.locator('.app-brand')).toHaveCount(0);
  await expect(page.locator('.app-bar')).toHaveClass(/app-bar--branding-none/);

  await page.unroute('**/api/runtime/application');
  await page.route('**/api/runtime/application', route => route.fulfill({
    json: activeProjection({ mode: 'image', visualAssetId: '0a86490c-2364-4e21-8a69-5cb332f77559' })
  }));
  await page.reload();

  const brand = page.locator('.app-brand');
  await expect(brand).toHaveAttribute('data-branding-mode', 'default');
  await expect(brand).toContainText('EliteSCADA');
  await expect(brand.getByRole('status')).toContainText(/missing.*Active application/i);
});

test('Working Branding preview is mounted separately from Active shell authority', async ({ page }) => {
  await page.route('**/api/runtime/application', route => route.fulfill({
    json: activeProjection({ mode: 'text', text: 'ACTIVE BRAND' })
  }));
  await page.goto('/engineering/branding');

  await expect(page.locator('.app-brand')).toContainText('ACTIVE BRAND');
  const editor = page.getByTestId('branding-editor');
  await expect(editor).toBeVisible();
  await editor.getByRole('combobox', { name: /Mode/i }).selectOption('text');
  await editor.getByLabel('Application text').fill('WORKING BRAND');

  await expect(page.getByTestId('branding-working-preview')).toContainText('WORKING BRAND');
  await expect(page.locator('.app-brand')).toContainText('ACTIVE BRAND');
  await expect(page.locator('.app-brand')).not.toContainText('WORKING BRAND');
});
