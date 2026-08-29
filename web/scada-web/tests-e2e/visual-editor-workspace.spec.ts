import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

type ExportedVisualElement = {
  id?: string | null;
  key: string;
  type: string;
  bindings?: Array<{ key: string; kind: string; target: string }> | null;
  properties?: Record<string, unknown> | null;
  children?: ExportedVisualElement[] | null;
  propertyExpressions?: Array<{
    propertyKey: string;
    expression: { text: string; resultType: string; dependencies?: Array<{ symbol: string; kind: string; tagReference: { tagId: string } }> | null };
  }> | null;
  booleanConditions?: Array<{ propertyKey: string; kind: string; minimum?: number | null; maximum?: number | null }> | null;
  analogFill?: { inputMinimum: number; inputMaximum: number; fillColor: string; direction?: string; source: { kind: string; tagReference?: { tagId: string } | null } } | null;
};

type ExportedPackage = {
  screens?: Array<{
    id?: string;
    key: string;
    name: string;
    route?: string | null;
    elements?: ExportedVisualElement[] | null;
    [key: string]: unknown;
  }>;
  tags?: Array<{
    id?: string;
    name: string;
    path: string;
    dataType: string;
  }>;
  [key: string]: unknown;
};

const ONE_PIXEL_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64'
);

test('Wave 08 composes Canvas, palette, properties, project-source binding, image asset and canonical save/reopen', async ({ page, request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;
  const originalScreen = originalPackage.screens?.[0];
  expect(originalScreen).toBeTruthy();

  const bindingTag = originalPackage.tags?.find(tag => tag.dataType.toLowerCase() === 'boolean')
    ?? originalPackage.tags?.find(tag => ['int16', 'int32', 'int64', 'float', 'double'].includes(tag.dataType.toLowerCase()));
  expect(bindingTag, 'seeded demo must expose at least one Boolean or numeric TAG for Wave 08 binding acceptance').toBeTruthy();
  const bindingProperty = bindingTag!.dataType.toLowerCase() === 'boolean' ? 'visible' : 'x';

  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceResponse.json() as { changeVersion: number };
  const nextRoute = `/wave-08-screen-${Date.now()}`;
  const assetFileName = `wave-08-image-${Date.now()}.png`;

  try {
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
    await expect(page.getByTestId('visual-editor-canvas')).toBeVisible();
    await expect(page.getByTestId('visual-object-palette')).toBeVisible();
    await expect(page.getByTestId('visual-property-inspector')).toBeVisible();
    await expect(page.getByTestId('visual-editor-canonical-renderer')).toBeVisible();
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);

    const screenList = page.locator('.visual-editor-screen-list');
    await screenList.getByRole('button').filter({ hasText: originalScreen!.key }).click();

    const route = page.getByRole('textbox', { name: 'Rota', exact: true });
    await expect(route).toHaveValue(originalScreen!.route ?? '');

    const assetInput = page.locator('.visual-editor-file-import input[type="file"]');
    await expect(assetInput).toBeEnabled();
    await assetInput.setInputFiles({ name: assetFileName, mimeType: 'image/png', buffer: ONE_PIXEL_PNG });

    const importedAssetId = await expect.poll(async () => {
      const response = await request.get('/api/engineering/visual-assets');
      if (!response.ok()) return null;
      const assets = await response.json() as Array<{ id?: string | null; originalFileName: string }>;
      return assets.find(asset => asset.originalFileName === assetFileName)?.id ?? null;
    }).not.toBeNull().then(async () => {
      const response = await request.get('/api/engineering/visual-assets');
      const assets = await response.json() as Array<{ id?: string | null; originalFileName: string }>;
      return assets.find(asset => asset.originalFileName === assetFileName)!.id!;
    });

    await page.locator('[data-object-type="core.image"]').click();
    const imageObject = page.locator('[data-canvas-object-type="core.image"]').last();
    await expect(imageObject).toBeVisible();
    await imageObject.click();

    const assetPicker = page.getByTestId('visual-editor-image-asset-picker').getByRole('combobox');
    await expect(assetPicker).toBeVisible();
    await assetPicker.selectOption(importedAssetId);

    const widthInput = page
      .getByTestId('visual-property-inspector')
      .getByRole('spinbutton', { name: 'width', exact: true });
    await widthInput.fill('180');
    await widthInput.press('Enter');

    const canvasSurface = page.locator('.visual-editor-canvas__surface');
    await canvasSurface.focus();
    await canvasSurface.press('ArrowRight');

    const bindingEditor = page.getByTestId('visual-binding-editor');
    await expect(bindingEditor).toBeVisible();
    await bindingEditor.getByLabel('Propriedade visual').selectOption(bindingProperty);
    const sourceSelect = bindingEditor.getByLabel('Fonte do projeto');
    const sourceOption = sourceSelect.locator('option').filter({ hasText: bindingTag!.path }).first();
    const sourceLabel = await sourceOption.textContent();
    expect(sourceLabel).toBeTruthy();
    await sourceSelect.selectOption({ label: sourceLabel! });

    await bindingEditor.getByRole('button', { name: 'Procurar referências do projeto' }).click();
    await expect(bindingEditor.getByTestId('project-reference-browser')).toBeVisible();
    await expect(bindingEditor.getByTestId('project-reference-browser').locator('details')).not.toHaveCount(0);
    await bindingEditor.getByRole('button', { name: 'Procurar referências do projeto' }).click();
    await bindingEditor.getByRole('button', { name: 'Aplicar binding' }).click();

    await route.fill(nextRoute);
    const apply = page.getByTestId('visual-editor-apply');
    await expect(apply).toBeDisabled();
    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    await expect(apply).toBeEnabled();
    page.once('dialog', dialog => dialog.accept());
    await apply.click();

    const persisted = await expect.poll(async () => {
      const response = await request.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const model = await response.json() as ExportedPackage;
      const persistedScreen = model.screens?.find(candidate =>
        (originalScreen!.id && candidate.id === originalScreen!.id) || candidate.key === originalScreen!.key);
      const image = flatten(persistedScreen?.elements ?? []).find(element =>
        element.type === 'core.image' && readAssetId(element) === importedAssetId);
      if (!persistedScreen || !image || persistedScreen.route !== nextRoute) return null;
      return { screen: persistedScreen, image };
    }).not.toBeNull().then(async () => {
      const response = await request.get('/api/engineering/export/json');
      const model = await response.json() as ExportedPackage;
      const persistedScreen = model.screens!.find(candidate =>
        (originalScreen!.id && candidate.id === originalScreen!.id) || candidate.key === originalScreen!.key)!;
      const image = flatten(persistedScreen.elements ?? []).find(element =>
        element.type === 'core.image' && readAssetId(element) === importedAssetId)!;
      return { screen: persistedScreen, image };
    });

    expect(persisted.image.properties?.width).toBe(180);
    expect(persisted.image.properties?.x).toBe(10);
    expect(persisted.image.bindings).toContainEqual(expect.objectContaining({ key: bindingProperty, kind: 'tag', target: bindingTag!.path }));
    expect(JSON.stringify(persisted.screen)).not.toContain('selectedObjectIds');
    expect(JSON.stringify(persisted.screen)).not.toContain('viewport');
    expect(JSON.stringify(persisted.screen)).not.toContain('hoveredObjectId');

    const workspaceAfterResponse = await request.get('/api/engineering/workspace');
    expect(workspaceAfterResponse.ok()).toBeTruthy();
    const workspaceAfter = await workspaceAfterResponse.json() as { changeVersion: number };
    expect(workspaceAfter.changeVersion).toBeGreaterThan(workspaceBefore.changeVersion);
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});

test('FOLLOW-B mounted editor persists expression, Boolean Condition and Analog Fill through Preview/Apply', async ({ page, request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;
  const originalScreen = originalPackage.screens?.[0];
  expect(originalScreen).toBeTruthy();
  const numericTag = originalPackage.tags?.find(tag =>
    Boolean(tag.id) && ['int16', 'int32', 'int64', 'float', 'double'].includes(tag.dataType.toLowerCase()));
  expect(numericTag, 'seeded demo must expose a stable-ID numeric TAG for FOLLOW-B acceptance').toBeTruthy();

  try {
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: originalScreen!.key }).click();

    await page.locator('[data-object-type="core.rectangle"]').click();
    const rectangle = page.locator('[data-canvas-object-type="core.rectangle"]').last();
    await expect(rectangle).toBeVisible();
    await rectangle.click();

    const dynamic = page.getByTestId('visual-dynamic-property-editor');
    await expect(dynamic).toBeVisible();

    await dynamic.getByLabel('Visual property').selectOption('visible');
    await dynamic.getByLabel('Source mode').selectOption('BooleanCondition');
    const conditionPanel = dynamic.locator('.dynamic-property-editor__panel');
    await conditionPanel.getByLabel('Condition preset').selectOption('NumericInterval');
    await selectSourceByPath(conditionPanel.getByLabel('Canonical source'), numericTag!.path);
    await conditionPanel.getByRole('spinbutton', { name: 'Minimum', exact: true }).fill('20');
    await conditionPanel.getByRole('spinbutton', { name: 'Maximum', exact: true }).fill('80');
    await conditionPanel.getByRole('button', { name: 'Apply condition' }).click();

    await dynamic.getByLabel('Visual property').selectOption('x');
    await dynamic.getByLabel('Source mode').selectOption('Expression');
    const expressionPanel = dynamic.locator('.dynamic-property-editor__panel');
    await selectSourceByPath(expressionPanel.locator('select').first(), numericTag!.path);
    await expressionPanel.getByRole('button', { name: 'Insert source' }).click();
    await expect(expressionPanel.getByRole('textbox', { name: 'Expression' })).not.toHaveValue('');
    await expressionPanel.getByRole('button', { name: 'Apply expression' }).click();

    const analog = dynamic.locator('.dynamic-property-editor__analog-fill');
    await analog.getByLabel('Enabled').check();
    await selectSourceByPath(analog.getByLabel('Canonical source'), numericTag!.path);
    await analog.getByLabel('Input minimum').fill('0');
    await analog.getByLabel('Input maximum').fill('100');
    await analog.getByLabel('Fill color').fill('#12AB34');
    await analog.getByLabel('Direction').selectOption('LeftToRight');
    await analog.getByRole('button', { name: 'Apply Analog Fill' }).click();

    const apply = page.getByTestId('visual-editor-apply');
    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    await expect(apply).toBeEnabled();
    page.once('dialog', dialog => dialog.accept());
    await apply.click();

    const persisted = await expect.poll(async () => {
      const response = await request.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const model = await response.json() as ExportedPackage;
      const persistedScreen = model.screens?.find(candidate =>
        (originalScreen!.id && candidate.id === originalScreen!.id) || candidate.key === originalScreen!.key);
      return flatten(persistedScreen?.elements ?? []).find(element =>
        element.type === 'core.rectangle' &&
        element.propertyExpressions?.some(item => item.propertyKey === 'x') &&
        element.booleanConditions?.some(item => item.propertyKey === 'visible') &&
        Boolean(element.analogFill)) ?? null;
    }).not.toBeNull().then(async () => {
      const response = await request.get('/api/engineering/export/json');
      const model = await response.json() as ExportedPackage;
      const persistedScreen = model.screens!.find(candidate =>
        (originalScreen!.id && candidate.id === originalScreen!.id) || candidate.key === originalScreen!.key)!;
      return flatten(persistedScreen.elements ?? []).find(element =>
        element.type === 'core.rectangle' && element.propertyExpressions?.some(item => item.propertyKey === 'x') &&
        element.booleanConditions?.some(item => item.propertyKey === 'visible') && Boolean(element.analogFill))!;
    });

    expect(persisted.booleanConditions?.[0]).toMatchObject({
      propertyKey: 'visible', kind: 'numericInterval', minimum: 20, maximum: 80
    });
    expect(persisted.propertyExpressions?.find(item => item.propertyKey === 'x')?.expression).toMatchObject({ resultType: 'number' });
    expect(persisted.propertyExpressions?.find(item => item.propertyKey === 'x')?.expression.dependencies?.[0].tagReference.tagId).toBe(numericTag!.id);
    expect(persisted.analogFill).toMatchObject({
      inputMinimum: 0, inputMaximum: 100, fillColor: '#12AB34', direction: 'leftToRight',
      source: { kind: 'tag', tagReference: { tagId: numericTag!.id } }
    });

    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});

async function selectSourceByPath(select: import('@playwright/test').Locator, path: string): Promise<void> {
  const option = select.locator(`option[value="${path.replaceAll('"', '\\"')}"]`);
  await expect(option, `expected canonical source option for ${path}`).toHaveCount(1);
  await select.selectOption(path);
}

function flatten(elements: readonly ExportedVisualElement[]): ExportedVisualElement[] {
  const result: ExportedVisualElement[] = [];
  for (const element of elements) {
    result.push(element);
    result.push(...flatten(element.children ?? []));
  }
  return result;
}

function readAssetId(element: ExportedVisualElement): string | null {
  const value = element.properties?.assetRef;
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return null;
  const assetId = (value as { assetId?: unknown }).assetId;
  return typeof assetId === 'string' ? assetId : null;
}