import { expect, test } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const outputDir = join(process.cwd(), 'artifacts', 'dynamo-artwork-preview');

test.use({
  locale: 'pt-BR',
  viewport: { width: 1920, height: 1440 },
  deviceScaleFactor: 2
});

test.setTimeout(240_000);

function safeFileName(value: string): string {
  return value.replace(/[^a-zA-Z0-9._-]+/g, '-');
}

function baseFamilyKey(key: string): string {
  return key.replace(/\.front-3d$|\.high-performance$/, '');
}

test('mounted Dynamo Library renders and captures all 72 canonical previews', async ({ page }) => {
  mkdirSync(outputDir, { recursive: true });

  await page.goto('/engineering');
  await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
  await expect(page.getByTestId('visual-editor-workspace')).toBeVisible({ timeout: 20_000 });

  await page.getByRole('tab', { name: 'Biblioteca' }).click();
  const library = page.getByTestId('visual-dynamo-library');
  await expect(library).toBeVisible();

  const cards = library.locator('[data-dynamo-key]');
  await expect(cards).toHaveCount(72);

  const manifest: Array<{
    index: number;
    key: string;
    familyKey: string;
    style: string;
    name: string;
    previewFile: string;
  }> = [];

  // Product grid evidence: remove only the scroll cap in the test DOM so all
  // real product cards are visible in one evidence capture. Rendering itself
  // remains the production CanonicalVisualPreview/CanonicalVisualRenderer.
  await page.addStyleTag({
    content: '.visual-dynamo-library__grid{max-height:none!important;overflow:visible!important;}'
  });
  await library.screenshot({ path: join(outputDir, '00-library-72-cards.png'), animations: 'disabled' });

  for (let index = 0; index < 72; index += 1) {
    const card = cards.nth(index);
    const key = await card.getAttribute('data-dynamo-key');
    const style = await card.getAttribute('data-dynamo-style');
    if (!key || !style) throw new Error(`Dynamo card ${index} is missing key/style evidence attributes.`);

    const name = (await card.locator('strong').first().innerText()).trim();
    await card.click();

    const preview = library.getByTestId('dynamo-library-canonical-preview');
    await expect(preview).toBeVisible();
    await expect(preview.locator('.visual-editor-object-error')).toHaveCount(0);
    await expect(preview.locator('.visual-editor-renderer-stage')).toBeVisible();

    const fileName = `${String(index + 1).padStart(2, '0')}-${safeFileName(baseFamilyKey(key))}-${safeFileName(style)}.png`;
    await preview.screenshot({
      path: join(outputDir, fileName),
      animations: 'disabled'
    });

    manifest.push({
      index: index + 1,
      key,
      familyKey: baseFamilyKey(key),
      style,
      name,
      previewFile: fileName
    });
  }

  const familyCounts = new Map<string, Set<string>>();
  for (const item of manifest) {
    const styles = familyCounts.get(item.familyKey) ?? new Set<string>();
    styles.add(item.style);
    familyCounts.set(item.familyKey, styles);
  }

  expect(familyCounts.size).toBe(24);
  for (const [familyKey, styles] of familyCounts) {
    expect(styles, `Expected three visual styles for ${familyKey}`).toEqual(new Set([
      'detailed-2d',
      'dimensional-front',
      'high-performance'
    ]));
  }

  writeFileSync(
    join(outputDir, 'manifest.json'),
    JSON.stringify({
      generatedFrom: 'mounted EliteSCADA Dynamo Library',
      expectedDefinitions: 72,
      expectedFamilies: 24,
      expectedStylesPerFamily: 3,
      definitions: manifest
    }, null, 2),
    'utf8'
  );
});
