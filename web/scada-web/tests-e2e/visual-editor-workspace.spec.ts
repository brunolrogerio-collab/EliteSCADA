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
    await assetInput.setInputFiles({
      name: assetFileName,
      mimeType: 'image/png',
      buffer: ONE_PIXEL_PNG
    });

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

    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Rota', exact: true })).toHaveValue(originalScreen!.route ?? '');

    await page.locator('[data-object-type="core.image"]').click();
    const imageObject = page.locator('[data-canvas-object-type="core.image"]').last();
    await expect(imageObject).toBeVisible();
    await imageObject.click();

    const assetPicker = page.getByTestId('visual-editor-image-asset-picker').getByRole('combobox');
    await expect(assetPicker).toBeVisible();
    await assetPicker.selectOption(importedAssetId);

    const widthInput = page.getByLabel('width');
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
      const screen = model.screens?.find(candidate =>
        (originalScreen!.id && candidate.id === originalScreen!.id) || candidate.key === originalScreen!.key);
      const image = flatten(screen?.elements ?? []).find(element =>
        element.type === 'core.image' && readAssetId(element) === importedAssetId);
      if (!screen || !image || screen.route !== nextRoute) return null;
      return { screen, image };
    }).not.toBeNull().then(async () => {
      const response = await request.get('/api/engineering/export/json');
      const model = await response.json() as ExportedPackage;
      const screen = model.screens!.find(candidate =>
        (originalScreen!.id && candidate.id === originalScreen!.id) || candidate.key === originalScreen!.key)!;
      const image = flatten(screen.elements ?? []).find(element =>
        element.type === 'core.image' && readAssetId(element) === importedAssetId)!;
      return { screen, image };
    });

    expect(persisted.image.properties?.width).toBe(180);
    expect(persisted.image.properties?.x).toBe(10);
    expect(persisted.image.bindings).toContainEqual(expect.objectContaining({
      key: bindingProperty,
      kind: 'tag',
      target: bindingTag!.path
    }));
    expect(JSON.stringify(persisted.screen)).not.toContain('selectedObjectIds');
    expect(JSON.stringify(persisted.screen)).not.toContain('viewport');
    expect(JSON.stringify(persisted.screen)).not.toContain('hoveredObjectId');

    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Rota', exact: true })).toHaveValue(nextRoute);
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);

    const persistedImageObject = page.locator('[data-canvas-object-type="core.image"]').last();
    await persistedImageObject.click();
    await expect(page.getByTestId('visual-editor-image-asset-picker').getByRole('combobox')).toHaveValue(importedAssetId);

    const workspaceAfterResponse = await request.get('/api/engineering/workspace');
    expect(workspaceAfterResponse.ok()).toBeTruthy();
    const workspaceAfter = await workspaceAfterResponse.json() as { changeVersion: number };
    expect(workspaceAfter.changeVersion).toBeGreaterThan(workspaceBefore.changeVersion);
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});

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
