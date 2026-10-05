import { expect, test, type APIRequestContext } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('real categorized factory library copies and inserts SVGs in screen, popup and template without losing the draft', async ({ page, request }, testInfo) => {
  test.setTimeout(120_000);
  const original = await (await request.get('/api/engineering/export/json')).json();
  const catalog = await (await request.get('/api/engineering/static-artwork')).json();
  expect(catalog.entries).toHaveLength(2208);
  const motor = catalog.entries.find((entry: any) => entry.category === 'industrial/rotating/motors');
  const valve = catalog.entries.find((entry: any) => entry.category === 'industrial/process/valves');
  expect(motor).toBeTruthy(); expect(valve).toBeTruthy();
  page.on('dialog', dialog => void dialog.accept());
  try {
    await page.goto('/engineering/libraries');
    const browser = page.getByTestId('library-catalog-browser');
    await expect(browser).toBeVisible();
    await browser.getByTestId('library-catalog-kind-filter').selectOption('visual-asset');
    await browser.getByTestId('library-catalog-search').fill(motor.name);
    await browser.locator(`[data-category-id="${motor.category}"]`).click();
    await expect(browser.getByTestId('library-catalog-entry').first()).toBeVisible();
    await browser.locator(`[data-artwork-id="${motor.id}"]`).click();
    await expect.poll(() => browser.getByTestId('library-catalog-preview').locator('img').evaluate(node => (node as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
    await browser.getByTestId('library-catalog-copy-svg').click();
    await expect.poll(async () => (await (await request.get('/api/engineering/export/json')).json()).visualAssets.some((asset: any) => asset.key === `factory.${motor.id}`)).toBeTruthy();
    let working = await (await request.get('/api/engineering/export/json')).json();
    const copied = working.visualAssets.find((asset: any) => asset.key === `factory.${motor.id}`);
    expect(copied.metadata).toMatchObject({ categoryPath: motor.category, factoryArtworkId: motor.id, artworkReviewStatus: 'draft' });
    const countAfterCopy = working.visualAssets.length;

    for (const section of ['Telas', 'Popups', 'Templates']) {
      await page.locator('.eng-nav').getByRole('button', { name: new RegExp(`^${section}`) }).click();
      const editor = page.getByTestId(section === 'Popups' ? 'popup-visual-editor-workspace' : 'visual-editor-workspace');
      await expect(editor).toBeVisible();
      const name = editor.locator('.visual-editor-screen-form input').first();
      const draftName = `SVG ${section} draft preserved`;
      await name.fill(draftName);
      await editor.locator('[data-insert-object-type="core.rectangle"]').click();
      const rectangles = await editor.locator('[data-canvas-object-type="core.rectangle"]').count();
      const svgCount = await editor.locator('[data-canvas-object-type="core.svgSymbol"]').count();
      await editor.getByTestId('visual-editor-side-tab-library').click();
      const palette = editor.getByTestId('static-artwork-palette');
      await expect(palette.locator('header')).toContainText('2208');
      await palette.getByLabel('Categoria dos SVGs').selectOption(motor.category);
      await palette.getByLabel('Buscar SVG na biblioteca').fill(motor.name);
      await palette.locator(`[data-artwork-id="${motor.id}"]`).click();
      await expect(name).toHaveValue(draftName);
      await expect(editor.locator('[data-canvas-object-type="core.rectangle"]')).toHaveCount(rectangles);
      await expect(editor.locator('[data-canvas-object-type="core.svgSymbol"]')).toHaveCount(svgCount + 1);
      await expect(editor.locator('.visual-editor-object-error')).toHaveCount(0);
      working = await (await request.get('/api/engineering/export/json')).json();
      expect(working.visualAssets).toHaveLength(countAfterCopy); // same identity reused, not 3 copies
      const prefix = section === 'Popups' ? 'popup-visual-editor' : 'visual-editor';
      await editor.getByTestId(`${prefix}-preview`).click();
      await expect(editor.getByTestId(`${prefix}-apply`)).toBeEnabled();
      await editor.getByTestId(`${prefix}-apply`).click();
      await expect.poll(async () => {
        const model = await (await request.get('/api/engineering/export/json')).json();
        const collection = section === 'Popups' ? model.popups : section === 'Templates' ? model.templates : model.screens;
        return collection.some((item: any) => item.name === draftName);
      }).toBeTruthy();
      const saved = await (await request.get('/api/engineering/export/json')).json();
      const collection = section === 'Popups' ? saved.popups : section === 'Templates' ? saved.templates : saved.screens;
      const authored = collection.find((item: any) => item.name === draftName);
      expect(authored.elements.some((element: any) => element.type === 'core.svgSymbol' && JSON.stringify(element.properties.assetRef).includes(copied.id))).toBeTruthy();
      if (section === 'Telas') await testInfo.attach('screen-library-SVG-inserted', { body: await editor.screenshot(), contentType: 'image/png' });
    }

    const stale = await request.post(`/api/engineering/static-artwork/${valve.id}/import`, { headers: { 'x-elitescada-workspace-version': '0' } });
    expect(stale.status()).toBe(409);
    expect((await request.post('/api/engineering/static-artwork/missing/import', { headers: await version(request) })).status()).toBe(404);
  } finally {
    const restored = await request.post('/api/engineering/import/json/apply', { headers: await version(request), data: original });
    expect(restored.ok(), await restored.text()).toBeTruthy();
  }
});

async function version(request: APIRequestContext) {
  const workspace = await (await request.get('/api/engineering/workspace')).json();
  return { 'x-elitescada-workspace-version': String(workspace.changeVersion) };
}
