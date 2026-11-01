import { expect, test } from '@playwright/test';
import { randomUUID } from 'node:crypto';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

type ExportedVisualElement = {
  id?: string | null;
  key: string;
  type: string;
  bindings?: Array<{ key: string; kind: string; target: string; direction?: string | null; metadata?: Record<string, string> | null }> | null;
  properties?: Record<string, unknown> | null;
  children?: ExportedVisualElement[] | null;
  propertyExpressions?: Array<{
    propertyKey: string;
    expression: { text: string; resultType: string; dependencies?: Array<{ symbol: string; kind: string; tagReference: { tagId: string } }> | null };
  }> | null;
  booleanConditions?: Array<{ propertyKey: string; kind: string; minimum?: number | null; maximum?: number | null }> | null;
  analogFill?: { inputMinimum: number; inputMaximum: number; fillColor: string; direction?: string; source: { kind: string; tagReference?: { tagId: string } | null } } | null;
  propertyMaps?: Array<{
    propertyKey: string;
    source: { kind: string; tagReference?: { tagId: string } | null };
    rules: Array<{ minimum?: number | null; maximum?: number | null; value: unknown }>;
    fallback?: unknown;
  }> | null;
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
  popups?: Array<{
    id?: string;
    key: string;
    name: string;
    elements?: ExportedVisualElement[] | null;
    [key: string]: unknown;
  }>;
  tags?: Array<{
    id?: string;
    name: string;
    path: string;
    dataType: string;
    source?: string | null;
    dataSourceId?: string | null;
    readOnly?: boolean;
    engineeringUnit?: string | null;
    initialValue?: { dataType: string; value: unknown } | null;
  }>;
  dataSources?: Array<{
    id?: string;
    key: string;
    name: string;
    driver: string;
    enabled?: boolean;
    settings?: Record<string, string> | null;
  }>;
  startupScreenId?: string | null;
  [key: string]: unknown;
};

const ONE_PIXEL_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64'
);

test('W15 R2 editor mounts shared side surfaces, canonical Dynamo preview and contextual productivity commands', async ({ page, request }, testInfo) => {
  const workspaceBeforeResponse = await request.get('/api/engineering/workspace');
  expect(workspaceBeforeResponse.ok()).toBeTruthy();
  const workspaceBefore = await workspaceBeforeResponse.json() as { changeVersion: number; isDirty: boolean };

  await page.goto('/engineering');
  await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();

  const workspace = page.getByTestId('visual-editor-workspace');
  const authoringSidebar = page.getByTestId('visual-editor-authoring-sidebar');
  const selectionInspector = page.getByTestId('visual-editor-selection-inspector');

  await expect(workspace).toBeVisible();
  await expect(page.locator('.visual-editor-canvas-enhanced__canvas').getByTestId('visual-editor-canonical-renderer')).toBeVisible();
  await expect(authoringSidebar).toBeVisible();
  await expect(authoringSidebar.getByTestId('visual-editor-outliner')).toBeVisible();
  await expect(authoringSidebar.getByTestId('visual-object-palette')).toHaveCount(0);
  await expect(page.locator('.visual-editor-canvas-enhanced__canvas').getByTestId('visual-editor-outliner')).toHaveCount(0);
  await expect(page.locator('[data-insert-object-type="core.image"]')).toBeVisible();

  await authoringSidebar.getByTestId('visual-editor-side-tab-library').click();
  await expect(authoringSidebar.getByTestId('visual-editor-outliner')).toBeHidden();
  const dynamoLibrary = page.getByTestId('visual-dynamo-library');
  await expect(dynamoLibrary).toBeVisible();
  await expect(dynamoLibrary.getByTestId('dynamo-library-canonical-thumbnail').first()).toBeVisible();
  const motor = dynamoLibrary.locator('[data-dynamo-key="motor.tefc"]');
  await expect(motor).toBeVisible();
  await motor.click();
  const canonicalDynamoPreview = dynamoLibrary.getByTestId('dynamo-library-canonical-preview');
  await expect(canonicalDynamoPreview).toBeVisible();
  await expect(canonicalDynamoPreview.locator('[data-object-id]')).toHaveCount(6);
  await expect(canonicalDynamoPreview.locator('.visual-editor-object-error')).toHaveCount(0);
  await testInfo.attach('w15-visual-quality-dynamo-library-preview', {
    body: await dynamoLibrary.screenshot(),
    contentType: 'image/png'
  });

  const workspaceAfterPreviewResponse = await request.get('/api/engineering/workspace');
  expect(workspaceAfterPreviewResponse.ok()).toBeTruthy();
  const workspaceAfterPreview = await workspaceAfterPreviewResponse.json() as { changeVersion: number; isDirty: boolean };
  expect(workspaceAfterPreview.changeVersion).toBe(workspaceBefore.changeVersion);
  expect(workspaceAfterPreview.isDirty).toBe(workspaceBefore.isDirty);

  await expect(selectionInspector.getByTestId('visual-editor-inspector-tab-properties')).toBeVisible();
  await expect(selectionInspector.getByTestId('visual-editor-inspector-tab-dynamics')).toBeVisible();
  await expect(selectionInspector.getByTestId('visual-editor-inspector-tab-events')).toBeVisible();

  await authoringSidebar.getByTestId('visual-editor-side-tab-structure').click();
  await expect(authoringSidebar.getByTestId('visual-editor-outliner')).toBeVisible();
  const paletteSeparator = workspace.getByRole('separator', { name: /estrutura e dínamos/i });
  const paletteWidth = Number(await paletteSeparator.getAttribute('aria-valuenow'));
  await paletteSeparator.focus();
  await page.keyboard.press('ArrowRight');
  await expect(paletteSeparator).toHaveAttribute('aria-valuenow', String(paletteWidth + 12));

  const paletteToggle = page.getByTestId('visual-editor-palette-toggle');
  await paletteToggle.click();
  await expect(authoringSidebar).toBeHidden();
  await expect(paletteToggle).toBeVisible();
  await paletteToggle.click();
  await expect(authoringSidebar).toBeVisible();

  const canvasObject = page.locator('[data-canvas-object-id]').first();
  await expect(canvasObject).toBeVisible();
  await canvasObject.click({ button: 'right' });

  const contextMenu = page.getByTestId('visual-editor-context-menu');
  await expect(contextMenu).toBeVisible();
  await expect(contextMenu.getByRole('menuitem', { name: 'Renomear', exact: true })).toBeVisible();
  await expect(contextMenu.getByRole('menuitem', { name: 'Duplicar', exact: true })).toBeVisible();
  await expect(contextMenu.getByRole('menuitem', { name: 'Propriedades', exact: true })).toBeVisible();

  await page.keyboard.press('Escape');
  await authoringSidebar.getByTestId('visual-editor-side-tab-library').click();
  await dynamoLibrary.locator('[data-dynamo-key="motor.tefc"]').click();
  await dynamoLibrary.locator('.visual-dynamo-library__add').click();
  const instanceInspector = selectionInspector.getByTestId('dynamo-instance-inspector');
  await expect(instanceInspector).toBeVisible();
  await expect(instanceInspector.locator('.visual-editor-dynamo-parameter').first()).toBeVisible();
  await expect(instanceInspector.getByText('motor.tefc', { exact: true })).toBeVisible();
  const inspectorLayout = await instanceInspector.evaluate(element => {
    const root = element.getBoundingClientRect();
    const summary = element.querySelector('summary')!.getBoundingClientRect();
    const body = element.querySelector('.visual-editor-dynamo-inspector__body')!.getBoundingClientRect();
    const properties = element.parentElement!.querySelector('[data-testid="visual-property-inspector"]')!.getBoundingClientRect();
    return { rootHeight: root.height, summaryHeight: summary.height, bodyHeight: body.height, rootBottom: root.bottom, propertiesTop: properties.top };
  });
  expect(inspectorLayout.rootHeight).toBeGreaterThan(inspectorLayout.summaryHeight + 100);
  expect(inspectorLayout.bodyHeight).toBeLessThan(400);
  expect(inspectorLayout.rootBottom).toBeLessThanOrEqual(inspectorLayout.propertiesTop);
  await expect(page.locator('.visual-editor-canvas-enhanced__canvas .visual-editor-svg-symbol').last()).toBeVisible();
});

test('Wave 08 composes Canvas, palette, properties, project-source binding, image asset and canonical save/reopen', async ({ page, request }) => {
  page.setDefaultTimeout(5_000);
  // CI runners can need longer to navigate into the full Engineering shell;
  // keep the short interaction budget while giving page navigation its own bound.
  page.setDefaultNavigationTimeout(20_000);

  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;
  const originalScreen = originalPackage.screens?.find(candidate => candidate.id === originalPackage.startupScreenId)
    ?? originalPackage.screens?.[0];
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
    await expect(page.getByTestId('visual-object-palette')).toHaveCount(0);
    await expect(page.getByTestId('visual-editor-authoring-toolbar')).toBeVisible();
    await expect(page.getByTestId('visual-property-inspector')).toBeVisible();
    await expect(page.getByTestId('visual-editor-canvas').getByTestId('visual-editor-canonical-renderer')).toBeVisible();
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);

    const screenList = page.locator('.visual-editor-screen-list');
    await screenList.getByRole('button').filter({ hasText: originalScreen!.key }).click();

    const route = page.getByRole('textbox', { name: 'Endereço da tela', exact: true });
    await expect(route).toHaveValue(originalScreen!.route ?? '');

    await page.locator('[data-insert-object-type="core.image"]').click();
    const imageObject = page.locator('[data-canvas-object-type="core.image"]').last();
    await expect(imageObject).toBeVisible();
    await imageObject.click();

    const assetBrowser = page.getByTestId('visual-editor-image-asset-picker');
    const importImageButton = assetBrowser.getByRole('button', { name: 'Importar asset do projeto…' });
    await expect(importImageButton).toBeEnabled();
    const fileChooserPromise = page.waitForEvent('filechooser');
    await importImageButton.click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({ name: assetFileName, mimeType: 'image/png', buffer: ONE_PIXEL_PNG });

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

    const assetPicker = assetBrowser.getByRole('combobox');
    await expect(assetPicker).toBeVisible();
    await expect(assetPicker).toHaveValue(importedAssetId);

    const widthInput = page
      .getByTestId('visual-property-inspector')
      .getByRole('spinbutton', { name: 'Width', exact: true });
    await widthInput.fill('180');
    await widthInput.press('Enter');

    const canvasSurface = page.locator('.visual-editor-canvas__surface');
    await canvasSurface.focus();
    await canvasSurface.press('ArrowRight');

    const bindingEditor = page.getByTestId('visual-binding-editor');

    await page.getByTestId('visual-editor-inspector-tab-dynamics').click();
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
    expect(persisted.image.properties?.x).toBe(1);
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

    await page.locator('[data-insert-object-type="core.rectangle"]').click();
    const rectangle = page.locator('[data-canvas-object-type="core.rectangle"]').last();
    await expect(rectangle).toBeVisible();
    await rectangle.click();

    await page.getByTestId('visual-editor-inspector-tab-dynamics').click();
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

    await dynamic.getByLabel('Visual property').selectOption('fillColor');
    await dynamic.getByLabel('Source mode').selectOption('RangeMap');
    const rangeMap = dynamic.getByTestId('visual-range-property-map');
    await selectSourceByPath(rangeMap.getByLabel('Canonical source'), numericTag!.path);
    await rangeMap.getByRole('spinbutton', { name: 'Minimum', exact: true }).fill('0');
    await rangeMap.getByRole('spinbutton', { name: 'Maximum', exact: true }).fill('60');
    await rangeMap.getByLabel('Mapped color').fill('#008800');
    await rangeMap.getByLabel('Fallback (optional)').fill('#CC0000');
    await rangeMap.getByRole('button', { name: 'Apply range map' }).click();

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
        Boolean(element.analogFill) &&
        element.propertyMaps?.some(item => item.propertyKey === 'fillColor')) ?? null;
    }).not.toBeNull().then(async () => {
      const response = await request.get('/api/engineering/export/json');
      const model = await response.json() as ExportedPackage;
      const persistedScreen = model.screens!.find(candidate =>
        (originalScreen!.id && candidate.id === originalScreen!.id) || candidate.key === originalScreen!.key)!;
      return flatten(persistedScreen.elements ?? []).find(element =>
        element.type === 'core.rectangle' && element.propertyExpressions?.some(item => item.propertyKey === 'x') &&
        element.booleanConditions?.some(item => item.propertyKey === 'visible') && Boolean(element.analogFill) &&
        element.propertyMaps?.some(item => item.propertyKey === 'fillColor'))!;
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
    expect(persisted.propertyMaps?.find(item => item.propertyKey === 'fillColor')).toMatchObject({
      propertyKey: 'fillColor',
      source: { kind: 'tag', tagReference: { tagId: numericTag!.id } },
      rules: [{ minimum: 0, maximum: 60, value: '#008800' }],
      fallback: '#CC0000'
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

test('W15 Dynamic Text and Numeric Input are mounted, persisted and Design mode never writes', async ({ page, request }, testInfo) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;
  const originalScreen = originalPackage.screens?.[0];
  expect(originalScreen).toBeTruthy();
  const numericTag = originalPackage.tags?.find(tag =>
    Boolean(tag.id) && tag.readOnly === false &&
    ['int16', 'int32', 'int64', 'float', 'double'].includes(tag.dataType.toLowerCase()));
  expect(numericTag, 'seeded demo must expose one writable numeric TAG for Numeric Input acceptance').toBeTruthy();

  try {
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: originalScreen!.key }).click();
    await page.getByTestId('visual-editor-inspector-tab-dynamics').click();

    const palette = page.getByTestId('visual-editor-authoring-toolbar');
    const bindingEditor = page.getByTestId('visual-binding-editor');

    await palette.locator('[data-insert-object-type="core.text"]').click();
    const textObject = page.locator('[data-canvas-object-type="core.text"]').last();
    await textObject.click();
    const textId = await textObject.getAttribute('data-canvas-object-id');
    expect(textId).toBeTruthy();

    await bindingEditor.getByLabel('Propriedade visual').selectOption('text');
    const textSource = bindingEditor.getByLabel('Fonte do projeto');
    await selectBindingSourceByPath(textSource, numericTag!.path);
    const format = bindingEditor.getByTestId('visual-dynamic-text-format');
    await format.getByRole('spinbutton', { name: 'Decimal places' }).fill('2');
    await format.getByRole('textbox', { name: 'Unit' }).fill('bar');
    await format.getByRole('textbox', { name: 'Prefix' }).fill('SP ');
    await bindingEditor.getByRole('button', { name: 'Aplicar binding' }).click();

    await palette.locator('[data-insert-object-type="core.numericInput"]').click();
    const numericCanvas = page.locator('[data-canvas-object-type="core.numericInput"]').last();
    await numericCanvas.click();
    const numericId = await numericCanvas.getAttribute('data-canvas-object-id');
    expect(numericId).toBeTruthy();

    await page.getByTestId('visual-editor-inspector-tab-properties').click();
    await bindingEditor.getByLabel('Valor numérico').selectOption('value');
    await selectBindingSourceByPath(bindingEditor.getByLabel('TAG / variável'), numericTag!.path);
    await bindingEditor.getByRole('button', { name: 'Aplicar binding' }).click();

    const numericRendered = page.getByTestId('visual-editor-canonical-layer').locator('[data-object-id="' + numericId + '"]');
    await expect(numericRendered).toHaveAttribute('data-numeric-input-state', 'design');
    const designInput = numericRendered.locator('input[type="number"]');
    await expect(designInput).toHaveCount(1);
    await expect(designInput).toHaveAttribute('readonly', '');
    const designApplyButton = numericRendered.locator('.visual-editor-numeric-input__apply');
    if (await numericRendered.getAttribute('data-show-apply') === 'true') {
      await expect(designApplyButton).toBeDisabled();
    } else {
      await expect(designApplyButton).toHaveCount(0);
    }

    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    page.once('dialog', dialog => dialog.accept());
    await applyAndAwaitCanonicalCompletion(page, 'visual-editor-apply');
    await page.reload();

    const reopenedText = page.getByTestId('visual-editor-canonical-layer').locator('[data-object-id="' + textId + '"]');
    const reopenedNumeric = page.getByTestId('visual-editor-canonical-layer').locator('[data-object-id="' + numericId + '"]');
    await expect(reopenedText).toBeVisible();
    await expect(reopenedNumeric).toHaveAttribute('data-numeric-input-state', 'design');
    await testInfo.attach('dynamic-text-numeric-input-save-reopen', {
      body: await page.screenshot({ fullPage: true }),
      contentType: 'image/png'
    });

    const persistedResponse = await request.get('/api/engineering/export/json');
    const persisted = await persistedResponse.json() as ExportedPackage;
    const screenAfter = persisted.screens?.find(item => item.id === originalScreen!.id || item.key === originalScreen!.key);
    const savedText = flatten(screenAfter?.elements ?? []).find(item => item.id === textId);
    const savedNumeric = flatten(screenAfter?.elements ?? []).find(item => item.id === numericId);
    expect(savedText?.bindings?.[0]).toMatchObject({
      key: 'text', target: numericTag!.path,
      metadata: { presentationMode: 'scalar-text', decimalPlaces: '2', engineeringUnit: 'bar', prefix: 'SP ' }
    });
    expect(savedNumeric).toMatchObject({ type: 'core.numericInput' });
    expect(savedNumeric?.bindings?.find(binding => binding.key === 'value')).toMatchObject({
      target: numericTag!.path, direction: 'readWrite'
    });
  } finally {
    try {
      const restore = await request.post('/api/engineering/import/json/apply', {
        headers: { 'content-type': 'application/json; charset=utf-8' }, data: originalPackage
      });
      if (!restore.ok() && testInfo.errors.length === 0) expect(restore.ok()).toBeTruthy();
    } catch (restoreError) {
      // Keep the first failure visible if Playwright has already closed the
      // request context due to a test timeout or another earlier assertion.
      if (testInfo.status === 'passed' && testInfo.errors.length === 0) throw restoreError;
    }
  }
});

test('W15 first-user flow configures rectangle and Text through canonical WYSIWYG in Screen and Popup', async ({ page, request }, testInfo) => {
  // This exercises two complete authoring/save/reopen cycles against the full
  // canonical package; keep it above Playwright's short 30-second default.
  test.setTimeout(90_000);
  const apiBaseUrl = process.env.ELITESCADA_E2E_API_BASE_URL ?? 'http://127.0.0.1:5080';
  const originalResponse = await page.request.get(`${apiBaseUrl}/api/engineering/export/json`);
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;
  const originalScreen = originalPackage.screens?.[0] ?? {
    id: randomUUID(), key: 'e2e-wysiwyg-screen', name: 'E2E WYSIWYG Screen',
    route: '/e2e-wysiwyg', elements: []
  };
  const originalPopup = originalPackage.popups?.[0] ?? {
    id: randomUUID(), key: 'e2e-wysiwyg-popup', name: 'E2E WYSIWYG Popup', elements: []
  };

  async function exercise(kind: 'screen' | 'popup') {
    const root = kind === 'screen' ? page.getByTestId('visual-editor-workspace') : page.getByTestId('popup-visual-editor-workspace');
    const palette = root.getByTestId('visual-editor-authoring-toolbar');
    const inspector = root.getByTestId('visual-property-inspector');

    async function renameObject(key: string) {
      const identity = inspector.getByTestId('visual-property-identity');
      if (await identity.getAttribute('open') === null) {
        await identity.locator('summary').click();
      }
      const keyInput = inspector.getByTestId('visual-property-identity-key');
      await keyInput.fill(key);
      await keyInput.press('Enter');
    }

    await palette.locator('[data-insert-object-type="core.rectangle"]').click();
    const rectangle = root.locator('[data-canvas-object-type="core.rectangle"]').last();
    await rectangle.click();
    const rectangleId = await rectangle.getAttribute('data-canvas-object-id');
    expect(rectangleId).toBeTruthy();
    await expect(inspector.getByTestId('visual-property-identity-id')).toHaveText(rectangleId!);
    await expect(root.getByTestId('visual-editor-outliner').locator('[role="treeitem"][aria-selected="true"]')).toHaveCount(1);

    await inspector.locator('[data-property-key="fillColor"] input[type="color"]').fill('#123456');
    await inspector.locator('[data-property-key="strokeColor"] input[type="color"]').fill('#654321');
    const strokeWidth = inspector.locator('[data-property-key="strokeWidth"] input');
    await strokeWidth.fill('5');
    await strokeWidth.press('Enter');

    const canonicalRectangle = root.getByTestId('visual-editor-canonical-layer').locator('[data-object-id="' + rectangleId + '"]');
    await expect.poll(() => canonicalRectangle.evaluate(element => ({
      fill: getComputedStyle(element).backgroundColor,
      stroke: getComputedStyle(element).borderTopColor,
      width: getComputedStyle(element).borderTopWidth
    }))).toEqual({ fill: 'rgb(18, 52, 86)', stroke: 'rgb(101, 67, 33)', width: '5px' });

    const renamedRect = 'rect-' + kind + '-first-user';
    await renameObject(renamedRect);
    await expect(rectangle).toHaveAttribute('data-canvas-object-id', rectangleId!);
    await expect(rectangle).toHaveAttribute('data-canvas-object-key', renamedRect);

    await palette.locator('[data-insert-object-type="core.text"]').click();
    const textObject = root.locator('[data-canvas-object-type="core.text"]').last();
    await textObject.click();
    const textId = await textObject.getAttribute('data-canvas-object-id');
    expect(textId).toBeTruthy();
    const literal = 'W15 ' + kind.toUpperCase() + ' FIRST USER';
    const literalInput = inspector.locator('[data-property-key="text"] input');
    await literalInput.fill(literal);
    await literalInput.press('Enter');
    const textKey = 'text-' + kind + '-first-user';
    await renameObject(textKey);

    const canonicalText = root.getByTestId('visual-editor-canonical-layer').locator('[data-object-id="' + textId + '"]');
    await expect(canonicalText).toContainText(literal);
    await expect(root.getByTestId('visual-editor-outliner').locator('[role="treeitem"][aria-selected="true"]')).toContainText(textKey);

    const surface = root.locator('.visual-editor-canvas__surface');
    await surface.focus();
    await surface.press('Control+z');
    await expect(textObject).not.toHaveAttribute('data-canvas-object-key', textKey);
    await expect(textObject).toHaveAttribute('data-canvas-object-id', textId!);
    await surface.press('Control+Shift+z');
    await expect(textObject).toHaveAttribute('data-canvas-object-key', textKey);

    return { rectangleId: rectangleId!, textId: textId!, literal, textKey };
  }

  try {
    if (!originalPackage.screens?.length || !originalPackage.popups?.length) {
      const seeded = await page.request.post(`${apiBaseUrl}/api/engineering/import/json/apply`, {
        headers: { 'content-type': 'application/json; charset=utf-8' },
        data: {
          ...originalPackage,
          screens: originalPackage.screens?.length ? originalPackage.screens : [originalScreen],
          popups: originalPackage.popups?.length ? originalPackage.popups : [originalPopup]
        }
      });
      expect(seeded.ok(), `could not prepare isolated Screen/Popup fixture (HTTP ${seeded.status()})`).toBeTruthy();
    }

    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: originalScreen!.key }).click();
    const screenProof = await exercise('screen');

    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    await expect(page.getByTestId('visual-editor-apply')).toBeEnabled();
    page.once('dialog', dialog => dialog.accept());
    await applyAndAwaitCanonicalCompletion(page, 'visual-editor-apply');
    await page.reload();
    await expect(page.getByTestId('visual-editor-canonical-layer').locator('[data-object-id="' + screenProof.textId + '"]')).toContainText(screenProof.literal);
    await testInfo.attach('screen-first-user-save-reopen', {
      body: await page.screenshot({ fullPage: true }),
      contentType: 'image/png'
    });

    await page.locator('.eng-nav').getByRole('button', { name: /Popups/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: originalPopup!.key }).click();
    const popupProof = await exercise('popup');

    await page.getByTestId('popup-visual-editor-preview').click();
    await expect(page.getByTestId('popup-visual-editor-apply')).toBeEnabled();
    page.once('dialog', dialog => dialog.accept());
    await applyAndAwaitCanonicalCompletion(page, 'popup-visual-editor-apply');
    await page.reload();
    await expect(page.getByTestId('visual-editor-canonical-layer').locator('[data-object-id="' + popupProof.textId + '"]')).toContainText(popupProof.literal);
    await testInfo.attach('popup-first-user-save-reopen', {
      body: await page.screenshot({ fullPage: true }),
      contentType: 'image/png'
    });

    const persistedResponse = await page.request.get(`${apiBaseUrl}/api/engineering/export/json`);
    const persisted = await persistedResponse.json() as ExportedPackage;
    const screenAfter = persisted.screens?.find(item => item.id === originalScreen!.id || item.key === originalScreen!.key);
    const popupAfter = persisted.popups?.find(item => item.id === originalPopup!.id || item.key === originalPopup!.key);
    expect(flatten(screenAfter?.elements ?? []).find(item => item.id === screenProof.textId)).toMatchObject({
      id: screenProof.textId, key: screenProof.textKey, properties: { text: screenProof.literal }
    });
    expect(flatten(popupAfter?.elements ?? []).find(item => item.id === popupProof.textId)).toMatchObject({
      id: popupProof.textId, key: popupProof.textKey, properties: { text: popupProof.literal }
    });
  } finally {
    const restore = await page.request.post(`${apiBaseUrl}/api/engineering/import/json/apply`, {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});

test('W15 CheckBox and ComboBox can be authored, bound, published, and operated through the canonical Runtime', async ({ page, request }, testInfo) => {
  test.setTimeout(120_000);
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;
  const originalScreen = originalPackage.screens?.find(candidate => candidate.id === originalPackage.startupScreenId)
    ?? originalPackage.screens?.[0];
  expect(originalScreen, 'the fixture must provide a screen for canonical control authoring').toBeTruthy();

  const suffix = `${Date.now()}-${randomUUID().slice(0, 8)}`;
  const projectKeyResponse = await request.get('/api/engineering/workspace');
  expect(projectKeyResponse.ok()).toBeTruthy();
  const workspace = await projectKeyResponse.json() as { projectKey?: string | null };
  const projectKey = workspace.projectKey;
  expect(projectKey, 'the fixture must provide a project for Runtime activation').toBeTruthy();

  const memoryDataSourceId = randomUUID();
  const memoryDataSourceKey = `w15-controls-${suffix}`;
  const booleanTagId = randomUUID();
  const booleanTagPath = `E2E.W15.Controls.${suffix}.Enabled`;
  const stringTagId = randomUUID();
  const stringTagPath = `E2E.W15.Controls.${suffix}.Mode`;
  const seededPackage = structuredClone(originalPackage);
  seededPackage.dataSources = [...(seededPackage.dataSources ?? []), {
    id: memoryDataSourceId,
    key: memoryDataSourceKey,
    name: `Temporary visual controls ${suffix}`,
    driver: 'builtin.memory.server',
    enabled: true,
    settings: {}
  }];
  seededPackage.tags = [...(seededPackage.tags ?? []),
    {
      id: booleanTagId, name: 'Temporary Boolean state', path: booleanTagPath,
      dataType: 'boolean', source: memoryDataSourceKey, dataSourceId: memoryDataSourceId,
      readOnly: false, initialValue: { dataType: 'boolean', value: false }
    },
    {
      id: stringTagId, name: 'Temporary operator mode', path: stringTagPath,
      dataType: 'string', source: memoryDataSourceKey, dataSourceId: memoryDataSourceId,
      readOnly: false, initialValue: { dataType: 'string', value: 'AUTO' }
    }
  ];

  let seeded = false;
  let runtimeMayHaveChanged = false;
  try {
    const seed = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: seededPackage
    });
    expect(seed.ok(), `could not seed temporary control TAGs (HTTP ${seed.status()})`).toBeTruthy();
    seeded = true;

    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: originalScreen!.key }).click();
    const palette = page.getByTestId('visual-editor-authoring-toolbar');

    await palette.locator('[data-insert-object-type="core.checkBox"]').click();
    const checkBoxCanvas = page.locator('[data-canvas-object-type="core.checkBox"]').last();
    await checkBoxCanvas.click();
    const checkBoxId = await checkBoxCanvas.getAttribute('data-canvas-object-id');
    expect(checkBoxId).toBeTruthy();
    await page.locator('[data-property-key="text"] input').fill('E2E boolean control');
    await page.locator('[data-property-key="text"] input').press('Enter');
    const checkBoxWritable = page.locator('[data-property-key="interactionEnabled"] input[type="checkbox"]');
    await checkBoxWritable.check();
    await page.getByTestId('visual-editor-inspector-tab-dynamics').click();
    const checkBoxBinding = page.getByTestId('visual-binding-editor');
    await checkBoxBinding.getByLabel('Propriedade visual').selectOption('checked');
    await selectBindingSourceByPath(checkBoxBinding.getByLabel('Fonte do projeto'), booleanTagPath);
    await checkBoxBinding.getByRole('button', { name: 'Aplicar binding' }).click();
    await expect(checkBoxBinding.getByTestId('visual-binding-current')).toContainText(booleanTagPath);
    await page.getByTestId('visual-editor-inspector-tab-properties').click();
    const checkBoxConfig = page.getByTestId('visual-checkbox-configuration');
    await expect(checkBoxConfig.getByLabel('Uso')).toHaveValue('boolean');
    await checkBoxConfig.getByRole('button', { name: 'Aplicar configuração' }).click();

    await palette.locator('[data-insert-object-type="core.comboBox"]').click();
    const comboBoxCanvas = page.locator('[data-canvas-object-type="core.comboBox"]').last();
    await comboBoxCanvas.click();
    const comboBoxId = await comboBoxCanvas.getAttribute('data-canvas-object-id');
    expect(comboBoxId).toBeTruthy();
    await page.locator('[data-property-key="text"] input').fill('E2E mode selector');
    await page.locator('[data-property-key="text"] input').press('Enter');
    const comboBoxWritable = page.locator('[data-property-key="interactionEnabled"] input[type="checkbox"]');
    await comboBoxWritable.check();
    const comboBoxConfig = page.getByTestId('visual-combobox-configuration');
    await comboBoxConfig.getByLabel('Valor do TAG 1').fill('AUTO');
    await comboBoxConfig.getByLabel('Texto exibido 1').fill('Automático');
    await comboBoxConfig.getByRole('button', { name: 'Adicionar opção' }).click();
    await comboBoxConfig.getByLabel('Valor do TAG 2').fill('MANUAL');
    await comboBoxConfig.getByLabel('Texto exibido 2').fill('Manual');
    await comboBoxConfig.getByRole('button', { name: 'Aplicar configuração' }).click();

    await page.getByTestId('visual-editor-inspector-tab-dynamics').click();
    const comboBoxBinding = page.getByTestId('visual-binding-editor');
    await comboBoxBinding.getByLabel('Propriedade visual').selectOption('text');
    await selectBindingSourceByPath(comboBoxBinding.getByLabel('Fonte do projeto'), stringTagPath);
    await comboBoxBinding.getByRole('button', { name: 'Aplicar binding' }).click();
    await expect(comboBoxBinding.getByTestId('visual-binding-current')).toContainText(stringTagPath);

    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    page.once('dialog', dialog => dialog.accept());
    await applyAndAwaitCanonicalCompletion(page, 'visual-editor-apply');
    await page.reload();
    await expect(page.getByTestId('visual-editor-canonical-layer').locator(`[data-object-id="${checkBoxId}"]`)).toBeVisible();
    await expect(page.getByTestId('visual-editor-canonical-layer').locator(`[data-object-id="${comboBoxId}"]`)).toBeVisible();

    const persistedResponse = await request.get('/api/engineering/export/json');
    expect(persistedResponse.ok()).toBeTruthy();
    const persisted = await persistedResponse.json() as ExportedPackage;
    const persistedScreen = persisted.screens?.find(candidate => candidate.id === originalScreen!.id || candidate.key === originalScreen!.key);
    const controls = flatten(persistedScreen?.elements ?? []);
    expect(controls.find(element => element.id === checkBoxId)).toMatchObject({
      id: checkBoxId, type: 'core.checkBox', bindings: [expect.objectContaining({
        key: 'checked', kind: 'Tag', target: booleanTagPath, direction: 'readWrite'
      })], properties: expect.objectContaining({ text: 'E2E boolean control', interactionEnabled: true })
    });
    expect(controls.find(element => element.id === comboBoxId)).toMatchObject({
      id: comboBoxId, type: 'core.comboBox', bindings: [expect.objectContaining({
        key: 'text', kind: 'Tag', target: stringTagPath, direction: 'readWrite'
      })], properties: expect.objectContaining({
        text: 'E2E mode selector', interactionEnabled: true,
        comboBoxOptions: [{ value: 'AUTO', label: 'Automático' }, { value: 'MANUAL', label: 'Manual' }]
      })
    });

    runtimeMayHaveChanged = true;
    await savePublishActivate(request, projectKey!, 'visual-controls-790-e2e');
    await page.goto('/');
    const runtimeCheckBox = page.locator(`[data-object-id="${checkBoxId}"] input[type="checkbox"]`);
    const runtimeComboBox = page.locator(`[data-object-id="${comboBoxId}"] select`);
    await expect(runtimeCheckBox).toBeEnabled();
    await expect(runtimeCheckBox).not.toBeChecked();
    await expect(runtimeComboBox).toBeEnabled();
    await expect(runtimeComboBox).toHaveValue('AUTO');
    await runtimeCheckBox.check();
    await expect.poll(async () => {
      const response = await request.get(`/api/tags/by-path/${encodeURIComponent(booleanTagPath)}`);
      if (!response.ok()) return null;
      const body = await response.json() as { current?: { value?: unknown } | null };
      return body.current?.value ?? null;
    }).toBe(true);
    await runtimeComboBox.selectOption('MANUAL');
    await expect.poll(async () => {
      const response = await request.get(`/api/tags/by-path/${encodeURIComponent(stringTagPath)}`);
      if (!response.ok()) return null;
      const body = await response.json() as { current?: { value?: unknown } | null };
      return body.current?.value ?? null;
    }).toBe('MANUAL');
    await testInfo.attach('checkbox-combobox-active-runtime', {
      body: await page.getByTestId('runtime-engineering-canvas').screenshot(), contentType: 'image/png'
    });
  } finally {
    if (seeded) {
      const restore = await request.post('/api/engineering/import/json/apply', {
        headers: { 'content-type': 'application/json; charset=utf-8' }, data: originalPackage
      });
      expect(restore.ok(), `could not restore original Engineering package (HTTP ${restore.status()})`).toBeTruthy();
    }

    for (const tagId of [booleanTagId, stringTagId]) {
      const currentWorkspaceResponse = await request.get('/api/engineering/workspace');
      expect(currentWorkspaceResponse.ok()).toBeTruthy();
      const currentWorkspace = await currentWorkspaceResponse.json() as { changeVersion: number };
      const deletion = await request.delete(`/api/engineering/tags/${tagId}`, {
        headers: { 'x-elitescada-workspace-version': String(currentWorkspace.changeVersion) }
      });
      expect(deletion.ok() || deletion.status() === 404, `temporary TAG cleanup failed (HTTP ${deletion.status()})`).toBeTruthy();
    }

    const currentWorkspaceResponse = await request.get('/api/engineering/workspace');
    expect(currentWorkspaceResponse.ok()).toBeTruthy();
    const currentWorkspace = await currentWorkspaceResponse.json() as { changeVersion: number };
    const sourceDeletion = await request.delete(`/api/engineering/data-sources/${memoryDataSourceId}`, {
      headers: { 'x-elitescada-workspace-version': String(currentWorkspace.changeVersion) }
    });
    expect(sourceDeletion.ok() || sourceDeletion.status() === 404,
      `temporary memory Data Source cleanup failed (HTTP ${sourceDeletion.status()})`).toBeTruthy();

    if (runtimeMayHaveChanged) await savePublishActivate(request, projectKey!, 'visual-controls-790-e2e-restore');
  }
});

test('W15 measurement widgets retain bindings and display configuration through editor preview, canonical save/load and Runtime', async ({ page, request }, testInfo) => {
  test.setTimeout(90_000);
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;
  const staleMeasurementWidgets = (originalPackage.screens ?? []).reduce(
    (total, screen) => total + removeMeasurementTestWidgets(screen.elements ?? []),
    0
  );
  if (staleMeasurementWidgets > 0) {
    const cleanup = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: originalPackage
    });
    expect(cleanup.ok(), `could not remove ${staleMeasurementWidgets} stale test widgets (HTTP ${cleanup.status()})`).toBeTruthy();
  }
  const originalScreen = originalPackage.screens?.find(candidate => candidate.id === originalPackage.startupScreenId)
    ?? originalPackage.screens?.[0];
  const numericTag = originalPackage.tags?.find(tag => ['int16', 'int32', 'int64', 'float', 'double'].includes(tag.dataType.toLowerCase()));
  expect(originalScreen, 'the fixture must contain a Screen for the persisted visual contract').toBeTruthy();
  expect(numericTag, 'the fixture must expose a numeric TAG for the measurement bindings').toBeTruthy();

  const widgetTypes = [
    'core.measurementDial', 'core.measurementRing', 'core.measurementBar',
    'core.measurementThermometer', 'core.measurementBands', 'core.measurementTarget'
  ];
  const widgets: ExportedVisualElement[] = widgetTypes.map((type, index) => ({
    id: randomUUID(),
    key: `w15-measurement-${index + 1}`,
    type,
    bindings: [{
      key: 'value', kind: 'tag', target: numericTag!.path, direction: 'input',
      metadata: { engineeringUnit: 'bar' }
    }],
    properties: {
      x: 20 + index * 230, y: 160, width: 210, height: 140,
      minimum: 0, maximum: 100, measurementUnit: 'bar', measurementTheme: 'high-contrast',
      measurementQualityVisible: true, measurementFreshnessVisible: true,
      measurementStaleAfterSeconds: 30,
      ...(type === 'core.measurementBands' ? { measurementBandOneEnd: 30, measurementBandTwoEnd: 70 } : {}),
      ...(type === 'core.measurementTarget' ? { measurementTarget: 65 } : {})
    }
  }));
  const seededPackage = structuredClone(originalPackage);
  const seededScreen = seededPackage.screens?.find(candidate =>
    (originalScreen!.id && candidate.id === originalScreen!.id) || candidate.key === originalScreen!.key);
  expect(seededScreen).toBeTruthy();
  seededScreen!.elements = [...(seededScreen!.elements ?? []), ...widgets];
  let projectKey: string | null = null;
  let restoreActiveRuntime = false;

  try {
    const seed = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: seededPackage
    });
    expect(seed.ok(), `could not seed canonical measurement model (HTTP ${seed.status()})`).toBeTruthy();
    const workspaceResponse = await request.get('/api/engineering/workspace');
    expect(workspaceResponse.ok()).toBeTruthy();
    const workspace = await workspaceResponse.json() as { projectKey?: string | null };
    projectKey = workspace.projectKey ?? null;
    expect(projectKey, 'the test fixture must have a project to activate for Runtime proof').toBeTruthy();
    restoreActiveRuntime = true;

    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: originalScreen!.key }).click();
    const editorWidgets = page.locator('[data-testid="visual-editor-canonical-layer"] [data-widget-type^="core.measurement"]');
    await expect(editorWidgets).toHaveCount(6);
    for (let index = 0; index < widgetTypes.length; index += 1) {
      await expect(editorWidgets.nth(index)).toHaveAttribute('data-widget-type', widgetTypes[index]);
      await expect(editorWidgets.nth(index)).toHaveAttribute('data-object-id', widgets[index].id!);
    }
    await testInfo.attach('measurement-widgets-editor-loaded-preview', {
      body: await page.getByTestId('visual-editor-canonical-layer').screenshot(),
      contentType: 'image/png'
    });

    // Move one authored object by a single logical pixel so the normal Preview -> Apply
    // lifecycle validates and saves the exact model now mounted by the editor.
    const firstEditorObject = page.locator(
      `.visual-editor-canvas__interaction-layer [data-canvas-object-type="${widgetTypes[0]}"][data-canvas-object-id="${widgets[0].id}"]`
    );
    await expect(firstEditorObject).toBeVisible();
    await firstEditorObject.click();
    const canvasSurface = page.locator('.visual-editor-canvas__surface');
    await canvasSurface.focus();
    await canvasSurface.press('ArrowRight');
    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    await expect(page.getByTestId('visual-editor-apply')).toBeEnabled();
    page.once('dialog', dialog => dialog.accept());
    await applyAndAwaitCanonicalCompletion(page, 'visual-editor-apply');
    await page.reload();
    await expect(page.getByTestId('visual-editor-canonical-layer')).toBeVisible();

    const persistedResponse = await request.get('/api/engineering/export/json');
    expect(persistedResponse.ok()).toBeTruthy();
    const persistedPackage = await persistedResponse.json() as ExportedPackage;
    const persistedScreen = persistedPackage.screens?.find(candidate =>
      (originalScreen!.id && candidate.id === originalScreen!.id) || candidate.key === originalScreen!.key);
    const persistedElements = flatten(persistedScreen?.elements ?? []);
    const persistedWidgets = widgets.map(widget => persistedElements.find(element => element.id === widget.id));
    expect(persistedWidgets.every(Boolean)).toBeTruthy();
    for (let index = 0; index < widgetTypes.length; index += 1) {
      expect(persistedWidgets[index]).toMatchObject({
        id: widgets[index].id, key: widgets[index].key, type: widgetTypes[index],
        bindings: [expect.objectContaining({ key: 'value', kind: 'tag', target: numericTag!.path })],
        properties: expect.objectContaining({
          minimum: 0, maximum: 100, measurementUnit: 'bar', measurementTheme: 'high-contrast',
          measurementQualityVisible: true, measurementFreshnessVisible: true,
          measurementStaleAfterSeconds: 30
        })
      });
    }
    expect(persistedWidgets[4]?.properties).toMatchObject({ measurementBandOneEnd: 30, measurementBandTwoEnd: 70 });
    expect(persistedWidgets[5]?.properties).toMatchObject({ measurementTarget: 65 });

    await savePublishActivate(request, projectKey!, 'measurement-widgets-e2e');
    await page.goto('/');
    const runtimeWidgets = page.locator('[data-widget-type^="core.measurement"]');
    await expect(runtimeWidgets).toHaveCount(6, { timeout: 20_000 });
    for (let index = 0; index < widgetTypes.length; index += 1) {
      await expect(runtimeWidgets.nth(index)).toHaveAttribute('data-widget-type', widgetTypes[index]);
      await expect(runtimeWidgets.nth(index)).toHaveAttribute('data-object-id', widgets[index].id!);
    }
    await expect(runtimeWidgets.nth(4)).toContainText('Limites das faixas: 30 bar · 70 bar');
    await expect(runtimeWidgets.nth(5)).toHaveAttribute('aria-label', /Meta: 65 bar/);
    await testInfo.attach('measurement-widgets-active-runtime-loaded-model', {
      body: await page.getByTestId('runtime-engineering-canvas').screenshot(),
      contentType: 'image/png'
    });

    const persistedJson = encodeURIComponent(JSON.stringify(persistedWidgets));
    for (const mode of ['preview', 'runtime'] as const) {
      await page.goto(`/tests-e2e/harness/measurement-visual-harness.html?mode=${mode}&elements=${persistedJson}`);
      const rendered = page.locator('[data-widget-type^="core.measurement"]');
      await expect(rendered).toHaveCount(6);
      for (let index = 0; index < widgetTypes.length; index += 1) {
        await expect(rendered.nth(index)).toHaveAttribute('data-widget-type', widgetTypes[index]);
        await expect(rendered.nth(index)).toHaveAttribute('data-object-id', widgets[index].id!);
        await expect(rendered.nth(index)).toHaveClass(/visual-measurement--high-contrast/);
        await expect(rendered.nth(index)).toContainText('bar');
        await expect(rendered.nth(index)).toContainText('Good');
      }
      await expect(rendered.nth(4).locator('.visual-measurement__band-track > span').nth(0)).toHaveAttribute('style', /width:\s*30%/);
      await expect(rendered.nth(4).locator('.visual-measurement__band-track > span').nth(1)).toHaveAttribute('style', /width:\s*40%/);
      await expect(rendered.nth(5)).toHaveAttribute('aria-label', /Target: 65 bar/);
      await testInfo.attach(`measurement-widgets-${mode}-loaded-canonical-model`, {
        body: await page.screenshot(), contentType: 'image/png'
      });
    }
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
    if (restoreActiveRuntime && projectKey) {
      await savePublishActivate(request, projectKey, 'measurement-widgets-e2e-restore');
    }
  }
});

async function savePublishActivate(
  request: import('@playwright/test').APIRequestContext,
  projectKey: string,
  actor: string
): Promise<void> {
  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspace = await workspaceResponse.json() as { projectName?: string | null };
  const save = await request.post(`/api/engineering/persistence/${encodeURIComponent(projectKey)}/save`, {
    data: { projectName: workspace.projectName ?? projectKey, savedBy: actor }
  });
  expect(save.ok(), `could not save project revision for ${actor} (HTTP ${save.status()})`).toBeTruthy();
  const saved = await save.json() as { revision: number };
  expect(Number.isInteger(saved.revision)).toBeTruthy();

  const publish = await request.post(`/api/engineering/persistence/${encodeURIComponent(projectKey)}/revisions/${saved.revision}/publish`, {
    data: { publishedBy: actor }
  });
  expect(publish.ok(), `could not publish revision ${saved.revision} for ${actor} (HTTP ${publish.status()})`).toBeTruthy();

  const activate = await request.post(`/api/engineering/persistence/${encodeURIComponent(projectKey)}/published/activate`, {
    data: { activatedBy: actor }
  });
  expect(activate.ok(), `could not activate revision ${saved.revision} for ${actor} (HTTP ${activate.status()})`).toBeTruthy();
}

async function applyAndAwaitCanonicalCompletion(
  page: import('@playwright/test').Page,
  applyTestId: 'visual-editor-apply' | 'popup-visual-editor-apply'
): Promise<void> {
  const apply = page.getByTestId(applyTestId);
  const responsePromise = page.waitForResponse(response =>
    response.url().includes('/api/engineering/import/json/apply') &&
    response.request().method() === 'POST'
  );
  await apply.click();
  const response = await responsePromise;
  expect(response.ok(), `canonical Engineering Apply failed with HTTP ${response.status()}`).toBeTruthy();

  // applyDraft awaits onApplied() before clearing preview/candidate. Waiting for
  // the mounted preview state to disappear proves the real Apply lifecycle has
  // completed before a reload can abort or race the authoritative refresh.
  await expect(page.getByText('Candidato válido', { exact: true })).toHaveCount(0);
}

async function selectBindingSourceByPath(select: import('@playwright/test').Locator, path: string): Promise<void> {
  const option = select.locator('option').filter({ hasText: path });
  await expect(option, `expected one visible canonical binding source for ${path}`).toHaveCount(1);
  const label = await option.textContent();
  expect(label).toBeTruthy();
  await select.selectOption({ label: label! });
}

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

function removeMeasurementTestWidgets(elements: ExportedVisualElement[]): number {
  let removed = 0;
  for (let index = elements.length - 1; index >= 0; index -= 1) {
    const element = elements[index];
    if (element.key?.startsWith('w15-measurement-')) {
      elements.splice(index, 1);
      removed += 1;
      continue;
    }
    removed += removeMeasurementTestWidgets(element.children ?? []);
  }
  return removed;
}

function readAssetId(element: ExportedVisualElement): string | null {
  const value = element.properties?.assetRef;
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return null;
  const assetId = (value as { assetId?: unknown }).assetId;
  return typeof assetId === 'string' ? assetId : null;
}
