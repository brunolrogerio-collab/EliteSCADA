import { expect, test } from '@playwright/test';
import { helpBodyBlocks } from '../src/help/helpNavigation';
import { installCurrentHelpCatalog } from './helpTestSupport';

test.use({ locale: 'pt-BR' });

test('a beginner can find a property without knowing an internal topic ID', async ({ page }) => {
  await installCurrentHelpCatalog(page);
  await page.goto('/help');
  await expect(page.getByRole('heading', { name: 'Encontre a orientação certa' })).toBeVisible();
  const nav = page.getByRole('navigation', { name: 'Tópicos', exact: true });
  await expect(nav.getByText('getting-started.startup-authentication', { exact: true })).toHaveCount(0);
  const search = page.getByRole('searchbox');
  await search.fill('casas decimais');
  await expect(nav.getByRole('link', { name: 'Entrada numérica', exact: true })).toBeVisible();
  await nav.getByRole('link', { name: 'Entrada numérica', exact: true }).click();
  const article = page.locator('[data-help-topic="visual.object.core.numericinput"]');
  await expect(article.getByRole('table')).toBeVisible();
  await expect(article.getByRole('rowheader', { name: /Mostrar Aplicar/ })).toBeVisible();
  await expect(article.getByText('showApplyButton', { exact: true })).not.toBeVisible();
  await article.getByRole('rowheader', { name: /Mostrar Aplicar/ }).getByText('Chave para Scripts').click();
  await expect(article.getByText('showApplyButton', { exact: true })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Neste artigo' })).toBeVisible();
});

test('real catalog renders separated steps, a real illustration and searchable code in all locales', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto('/help?topic=visual.first-screen');
  await expect(page.locator('article ol.help-page__steps > li')).toHaveCount(7);
  const figure = page.getByRole('img', { name: /Tela real da demonstração EEE/ });
  await expect(figure).toBeVisible();
  await expect.poll(() => figure.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  await page.screenshot({ path: testInfo.outputPath('help-first-screen-desktop.png'), fullPage: true });
  await page.getByRole('searchbox').fill('visual_property_clear');
  await expect(page.getByRole('navigation', { name: 'Tópicos', exact: true }).getByRole('link', { name: 'Client Visual: interação e aparência com exemplos', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Limpar busca' }).click();
  await page.getByLabel('Idioma', { exact: true }).selectOption('en');
  await expect(page.getByRole('heading', { name: 'Your first screen: from TAG to visible value' })).toBeVisible();
  await page.getByLabel('Language', { exact: true }).selectOption('es');
  await expect(page.getByRole('heading', { name: 'Su primera pantalla: del TAG al valor visible' })).toBeVisible();
});

test('property tables remain usable on a narrow viewport and empty search is recoverable', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/help?topic=visual.object.core.trend');
  await expect(page.getByRole('heading', { name: 'Gráfico de tendência', exact: true })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Campos deste objeto' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole('region', { name: 'Campos deste objeto' }).locator('tbody tr').first().scrollIntoViewIfNeeded();
  await page.screenshot({ path: testInfo.outputPath('help-object-mobile.png') });
  await page.getByRole('searchbox').fill('no-such-help-field-87123');
  await expect(page.getByText('Nenhum tópico corresponde à busca.', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: 'Limpar busca' }).click();
  await expect(page.getByText('Nenhum tópico corresponde à busca.', { exact: false })).toHaveCount(0);
});

test('numbered prose keeps decimal values inside the same instruction', () => {
  expect(helpBodyBlocks('1. Use 3.6 como fator. 2. Confira o valor.')).toEqual([
    { ordered: true, lines: ['Use 3.6 como fator.', 'Confira o valor.'] }
  ]);
});
