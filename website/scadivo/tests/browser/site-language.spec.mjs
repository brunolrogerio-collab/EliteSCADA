import { readFile } from 'node:fs/promises';
import { expect, test } from '@playwright/test';

const site = new URL('../../site/', import.meta.url);
const deployedSite = process.env.SCADIVO_TEST_SITE_URL;
const baseUrl = deployedSite || 'http://scadivo.test';

async function visitWithLanguages(page, languages) {
  if (!deployedSite) {
    const files = {
      '/': { name: 'index.html', contentType: 'text/html' },
      '/site.js': { name: 'site.js', contentType: 'application/javascript' }
    };
    await page.route('http://scadivo.test/**', async route => {
      const file = files[new URL(route.request().url()).pathname];
      if (!file) return route.fulfill({ status: 404 });
      await route.fulfill({ contentType: file.contentType, body: await readFile(new URL(file.name, site)) });
    });
  }
  await page.addInitScript(preferred => {
    Object.defineProperty(navigator, 'languages', { get: () => preferred });
  }, languages);
  await page.goto(baseUrl);
}

for (const [languages, expected] of [
  [['pt-PT', 'es'], 'pt-BR'],
  [['de-DE', 'es-MX', 'en'], 'es'],
  [['en-GB', 'pt-BR'], 'en'],
  [['fr-FR', 'de-DE'], 'en']
]) {
  test(`site uses ${expected} for ${languages.join(', ')}`, async ({ page }) => {
    await visitWithLanguages(page, languages);
    await expect(page.locator('html')).toHaveAttribute('lang', expected);
    await expect(page.locator('#language-select')).toHaveValue(expected === 'pt-BR' ? 'pt' : expected);
    expect(await page.evaluate(() => localStorage.getItem('scadivo-site-language'))).toBeNull();
  });
}

test('the site remembers a deliberate language selection across visits', async ({ page }) => {
  await visitWithLanguages(page, ['en']);
  await page.locator('#language-select').selectOption('es');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('lang', 'es');
  await page.goto(`${baseUrl}/?lang=pt-BR`);
  await expect(page.locator('html')).toHaveAttribute('lang', 'pt-BR');
  await page.goto(`${baseUrl}/?lang=fr`);
  await expect(page.locator('html')).toHaveAttribute('lang', 'pt-BR');
});
