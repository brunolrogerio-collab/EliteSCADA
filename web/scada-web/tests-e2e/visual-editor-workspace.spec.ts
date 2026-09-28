import { expect, test } from '@playwright/test';

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
    readOnly?: boolean;
    engineeringUnit?: string | null;
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
      .getByRole('spinbutton', { name: 'Width', exact: true });
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

    const palette = page.getByTestId('visual-object-palette');
    const bindingEditor = page.getByTestId('visual-binding-editor');

    await palette.locator('[data-object-type="core.text"]').click();
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

    await palette.locator('[data-object-type="core.numericInput"]').click();
    const numericCanvas = page.locator('[data-canvas-object-type="core.numericInput"]').last();
    await numericCanvas.click();
    const numericId = await numericCanvas.getAttribute('data-canvas-object-id');
    expect(numericId).toBeTruthy();

    await bindingEditor.getByLabel('Propriedade visual').selectOption('value');
    await selectBindingSourceByPath(bindingEditor.getByLabel('Fonte do projeto'), numericTag!.path);
    await bindingEditor.getByRole('button', { name: 'Aplicar binding' }).click();

    const numericRendered = page.getByTestId('visual-editor-canonical-layer').locator('[data-object-id="' + numericId + '"]');
    await expect(numericRendered).toHaveAttribute('data-numeric-input-state', 'design');
    const designInput = numericRendered.locator('input[type="number"]');
    await expect(designInput).toHaveCount(1);
    await expect(designInput).toHaveAttribute('readonly', '');
    await expect(numericRendered.locator('button').filter({ hasText: 'Apply' })).toBeDisabled();

    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    page.once('dialog', dialog => dialog.accept());
    await page.getByTestId('visual-editor-apply').click();
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
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});

test('W15 first-user flow configures rectangle and Text through canonical WYSIWYG in Screen and Popup', async ({ page, request }, testInfo) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;
  const originalScreen = originalPackage.screens?.[0];
  const originalPopup = originalPackage.popups?.[0];
  expect(originalScreen).toBeTruthy();
  expect(originalPopup).toBeTruthy();

  async function exercise(kind: 'screen' | 'popup') {
    const root = kind === 'screen' ? page.getByTestId('visual-editor-workspace') : page.getByTestId('popup-visual-editor-workspace');
    const palette = root.getByTestId('visual-object-palette');
    const inspector = root.getByTestId('visual-property-inspector');

    await palette.locator('[data-object-type="core.rectangle"]').click();
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
    await inspector.getByTestId('visual-property-identity-key').fill(renamedRect);
    await inspector.getByTestId('visual-property-identity-key').press('Enter');
    await expect(rectangle).toHaveAttribute('data-canvas-object-id', rectangleId!);
    await expect(rectangle).toHaveAttribute('data-canvas-object-key', renamedRect);

    await palette.locator('[data-object-type="core.text"]').click();
    const textObject = root.locator('[data-canvas-object-type="core.text"]').last();
    await textObject.click();
    const textId = await textObject.getAttribute('data-canvas-object-id');
    expect(textId).toBeTruthy();
    const literal = 'W15 ' + kind.toUpperCase() + ' FIRST USER';
    const literalInput = inspector.locator('[data-property-key="text"] input');
    await literalInput.fill(literal);
    await literalInput.press('Enter');
    const textKey = 'text-' + kind + '-first-user';
    await inspector.getByTestId('visual-property-identity-key').fill(textKey);
    await inspector.getByTestId('visual-property-identity-key').press('Enter');

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
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: originalScreen!.key }).click();
    const screenProof = await exercise('screen');

    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    await expect(page.getByTestId('visual-editor-apply')).toBeEnabled();
    page.once('dialog', dialog => dialog.accept());
    await page.getByTestId('visual-editor-apply').click();
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
    await page.getByTestId('popup-visual-editor-apply').click();
    await page.reload();
    await expect(page.getByTestId('visual-editor-canonical-layer').locator('[data-object-id="' + popupProof.textId + '"]')).toContainText(popupProof.literal);
    await testInfo.attach('popup-first-user-save-reopen', {
      body: await page.screenshot({ fullPage: true }),
      contentType: 'image/png'
    });

    const persistedResponse = await request.get('/api/engineering/export/json');
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
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});

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

function readAssetId(element: ExportedVisualElement): string | null {
  const value = element.properties?.assetRef;
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return null;
  const assetId = (value as { assetId?: unknown }).assetId;
  return typeof assetId === 'string' ? assetId : null;
}
