import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
test.use({ locale: 'pt-BR' });

test('all 26 real catalog definitions mount their canonical artwork across five fixed stages', async ({ page }, testInfo) => {
  test.skip(!process.env.ELITESCADA_DYNAMO_REVIEW_FIXTURE, 'Generate the canonical review artifact with tools/DynamoReviewFixture.');
  const fixture = JSON.parse(readFileSync(process.env.ELITESCADA_DYNAMO_REVIEW_FIXTURE!, 'utf8'));
  expect(fixture.dynamos).toHaveLength(26);
  await page.route('**/review/catalog', route => route.fulfill({ json: fixture }));
  await page.route('**/review/artwork/*', route => {
    const id = new URL(route.request().url()).pathname.split('/').pop();
    const artwork = fixture.artwork.find((item: any) => item.asset.id === id);
    expect(artwork).toBeTruthy();
    return route.fulfill({ contentType: 'image/svg+xml', body: Buffer.from(artwork.content, 'base64') });
  });
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.setViewportSize({ width: 1600, height: 1200 });
  await page.goto('/tests-e2e/harness/authoring-preview.html?gallery');
  const renderer = page.getByTestId('visual-editor-canonical-renderer');
  await expect(renderer.locator('[data-dynamo-key]')).toHaveCount(26);
  for (const stage of [0,1,2,3,4]) {
    await page.getByLabel('Gallery state').selectOption(String(stage));
    await expect(renderer.locator('[data-dynamo-key]')).toHaveCount(26);
    await expect(renderer.locator('.visual-editor-svg-symbol svg')).toHaveCount(20);
    await expect(renderer.locator('.visual-editor-svg-symbol').first()).toHaveCSS('background-color', 'rgba(0, 0, 0, 0)');
    await expect(renderer.locator('.visual-editor-svg-symbol').first()).toHaveCSS('border-top-width', '0px');
    const expectedColor = ['#46535c', '#16a34a', '#eab308', '#dc2626', '#1687c9'][stage];
    await expect(renderer.locator('[data-dynamo-key="indicator.lamp.round"] [data-elitescada-slot="state"]').first()).toHaveAttribute('fill', new RegExp(`^${expectedColor}$`, 'i'));
    await page.screenshot({ path: testInfo.outputPath(`catalog-stage-${stage}.png`), fullPage: true });
  }
  expect(errors).toEqual([]);
});

test('factory SVG library filters categories and copies sanitized artwork through normal workspace CAS', async ({ page }) => {
  let catalogReads = 0;
  let imported = false;
  await page.route('**/api/engineering/static-artwork', async route => {
    catalogReads++;
    await route.fulfill({ json: { reviewRequired: true, entries: [
      { id: 'motor-001', key: 'motor', name: 'Motor original', category: 'Motors', style: '3D', status: 'draft', tags: ['motor'] },
      { id: 'valve-001', key: 'valve', name: 'Válvula original', category: 'Valves', style: '2D', status: 'draft', tags: ['valve'] }
    ] } });
  });
  await page.route('**/api/engineering/static-artwork/*/content', route => route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="40" fill="#82929a"/></svg>' }));
  await page.route('**/api/engineering/visual-assets/import?**', async route => {
    expect(route.request().method()).toBe('POST');
    expect(route.request().headers()['x-elitescada-workspace-version']).toBe('7');
    expect(new URL(route.request().url()).searchParams.get('key')).toBe('factory.motor-001');
    expect(route.request().postData()).toContain('<svg');
    imported = true;
    await route.fulfill({ json: { asset: { key: 'factory.motor-001' }, issues: [] } });
  });
  await page.goto('/tests-e2e/harness/authoring-preview.html');
  expect(catalogReads).toBe(0);
  const library = page.getByTestId('factory-artwork-library');
  await library.locator('summary').click();
  await expect(library.locator('summary')).toContainText('2');
  await library.getByLabel('Factory SVG category').selectOption('Motors');
  await expect(library.locator('article')).toHaveCount(1);
  await expect.poll(() => library.locator('img').evaluate(element => (element as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  await library.getByRole('button', { name: 'Copiar para o projeto' }).click();
  await expect.poll(() => imported).toBeTruthy();
  await expect(library.getByRole('button', { name: 'No projeto' })).toBeDisabled();
  await expect(page.getByTestId('refreshes')).toHaveText('1');
  await expect(library.getByRole('status')).toContainText('copiado');
});

test('Dynamo script wizard creates typed output via syntax validation Preview CAS Apply and explicit binding', async ({ page }) => {
  let loads = 0;
  let previewPackage: Record<string, any> | null = null;
  await page.route('**/api/engineering/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/engineering/workspace') return route.fulfill({ json: { changeVersion: 7, isDirty: true } });
    if (path === '/api/engineering/scripts') { loads++; return route.fulfill({ json: [] }); }
    if (path === '/api/engineering/script-visual-event-references') return route.fulfill({ json: [] });
    if (path.endsWith('/python/validate')) {
      const source = route.request().postDataJSON().source;
      expect(source).toContain('client_memory_write');
      expect(source).toContain('sample["quality"]');
      expect(source).toContain('15000000-0000-0000-0000-000000000010');
      return route.fulfill({ json: { diagnostics: [] } });
    }
    if (path.endsWith('/preview')) {
      previewPackage = route.request().postDataJSON();
      return route.fulfill({ json: { canApply: true, items: [], errorCount: 0 } });
    }
    if (path.endsWith('/apply')) {
      expect(route.request().headers()['x-elitescada-workspace-version']).toBe('7');
      expect(route.request().postDataJSON()).toEqual(previewPackage);
      return route.fulfill({ json: { created: 1, issues: [] } });
    }
    return route.fulfill({ json: [] });
  });
  await page.goto('/tests-e2e/harness/authoring-preview.html');
  expect(loads).toBe(0);
  const wizard = page.getByTestId('dynamo-script-wizard');
  await wizard.locator('summary').click();
  await expect.poll(() => loads).toBe(1);
  await wizard.getByRole('combobox', { name: 'Saída', exact: true }).selectOption('15000000-0000-0000-0000-000000000011');
  await expect(wizard.getByRole('combobox', { name: 'Saída', exact: true }).locator('option')).toHaveCount(2);
  await wizard.getByLabel('Source TAG').selectOption('15000000-0000-0000-0000-000000000010');
  await wizard.getByRole('button', { name: 'Validar script' }).click();
  await expect(wizard.getByRole('button', { name: 'Aplicar script' })).toBeEnabled();
  const captured = previewPackage as unknown as { scripts: Array<{ scope: string }>; scriptVisualEventReferences: Array<{ eventKind: string; timerIntervalMs: number }> };
  expect(captured.scripts[0].scope).toBe('clientVisual');
  expect(captured.scriptVisualEventReferences[0]).toMatchObject({ eventKind: 'timer', timerIntervalMs: 1000 });
  await wizard.getByRole('button', { name: 'Aplicar script' }).click();
  await expect(page.getByTestId('refreshes')).toHaveText('1');
  await expect(wizard.getByRole('status')).toContainText('Script aplicado');
  await wizard.getByRole('button', { name: 'Vincular saída ao parâmetro' }).click();
  const binding = JSON.parse(await page.getByTestId('output-binding').innerText());
  expect(binding).toMatchObject({ kind: 'ValueSource', key: 'runningSignal', valueSource: { kind: 'ClientMemory', valueType: 'Boolean', tagReference: { tagId: '15000000-0000-0000-0000-000000000011' } } });
});
