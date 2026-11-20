import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { chromium } from 'playwright';

const forum = new URL(process.argv[2]);
const catalog = new URL('/categories', forum);
const apiUrl = new URL('/categories.json', forum);
const translations = JSON.parse(await readFile(
  new URL('../../deploy/community-homolog/category-localizations.json', import.meta.url), 'utf8'
));
const picker = '#scadivo-community-language[data-scadivo-bound=true]';
const plainText = value => value.replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim();

function translated(entry, lang) {
  return lang === 'pt-BR'
    ? { name: entry.sourceTitle, description: entry.sourceDescription }
    : entry.translations[lang];
}

async function verifyCatalog(page, lang, { mobile = false } = {}) {
  const rowsSelector = mobile ? '.category-list .category-list-item' : '.category-list tbody tr';
  const rows = page.locator(rowsSelector);
  await page.locator(picker).waitFor({ state: 'visible' });
  await rows.locator('h3').first().waitFor({ state: 'visible' });
  assert.equal(await page.locator('html').getAttribute('lang'), lang);
  assert.equal(await page.locator('#toggle-current-user').count(), 0, 'Test session must stay anonymous');
  assert.equal(await page.locator(picker).inputValue(), lang === 'pt-BR' ? 'pt_BR' : lang);

  const displayed = await rows.evaluateAll(items => items.map(item => {
    const heading = item.querySelector('h3');
    const link = heading?.querySelector('a') ?? item.querySelector('a[href*="/c/"]');
    const description = item.querySelector('.category-description');
    return {
      slug: link?.getAttribute('href')?.match(/\/c\/([^/]+)\//)?.[1],
      name: heading?.innerText.trim(),
      description: description?.innerText.trim().replace(/\s+/g, ' '),
      descriptionVisible: Boolean(description?.getClientRects().length)
    };
  }));
  for (const entry of translations) {
    const rendered = displayed.find(row => row.slug === entry.slug);
    assert.ok(rendered, entry.slug);
    const expected = translated(entry, lang);
    assert.equal(rendered.name, expected.name, entry.slug);
    if (!mobile) {
      assert.equal(rendered.descriptionVisible, true, entry.slug);
      assert.equal(rendered.description, plainText(expected.description), entry.slug);
    }
  }
}

const browser = await chromium.launch();
try {
  for (const [locale, lang] of [['pt-PT', 'pt-BR'], ['es-MX', 'es'], ['en-GB', 'en'], ['de-DE', 'en']]) {
    const context = await browser.newContext({ locale });
    const page = await context.newPage();
    const response = await page.goto(catalog.href, { waitUntil: 'load' });
    assert.equal(response.status(), 200);
    await verifyCatalog(page, lang);

    const responseJson = await context.request.get(apiUrl.href, { headers: { 'Accept-Language': locale } });
    assert.equal(responseJson.status(), 200);
    const { category_list: { categories } } = await responseJson.json();
    for (const entry of translations) {
      const category = categories.find(item => item.slug === entry.slug);
      const expected = translated(entry, lang);
      assert.equal(category.name, expected.name, entry.slug);
      assert.equal(category.description, expected.description, entry.slug);
    }
    console.log(`Browser ${locale} → ${lang}. All eight names and descriptions matched in the catalog and API.`);
    await context.close();
  }

  const explicitlySpanish = new URL(catalog);
  explicitlySpanish.searchParams.set('tl', 'es');
  for (let visit = 0; visit < 4; visit++) {
    const context = await browser.newContext({ locale: 'en-GB' });
    const page = await context.newPage();
    await page.goto(explicitlySpanish.href, { waitUntil: 'load' });
    await verifyCatalog(page, 'es');
    await page.goto(catalog.href, { waitUntil: 'load' });
    await verifyCatalog(page, 'es');
    await context.close();
  }
  console.log('The same manual selection persisted in four independent sessions.');

  for (const mobile of [false, true]) {
    const options = mobile ? { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } : {};
    const context = await browser.newContext({ locale: 'en-GB', ...options });
    const page = await context.newPage();
    await page.goto(catalog.href, { waitUntil: 'load' });
    await verifyCatalog(page, 'en', { mobile });
    for (const [option, lang] of [['es', 'es'], ['pt_BR', 'pt-BR'], ['en', 'en']]) {
      await Promise.all([
        page.waitForURL(url => url.searchParams.get('tl') === option, { waitUntil: 'load' }),
        page.locator(picker).selectOption(option)
      ]);
      await verifyCatalog(page, lang, { mobile });
      await page.goto(catalog.href, { waitUntil: 'load' });
      await page.reload({ waitUntil: 'load' });
      await verifyCatalog(page, lang, { mobile });
      console.log(`${mobile ? 'Mobile' : 'Desktop'} ${lang}. Manual preference persisted after revisit and reload.`);
    }
    await context.close();
  }
} finally {
  await browser.close();
}
