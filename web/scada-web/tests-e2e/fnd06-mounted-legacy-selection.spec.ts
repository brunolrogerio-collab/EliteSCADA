import { randomUUID } from 'node:crypto';
import { expect, test, type Locator, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

type VisualElement = Record<string, unknown> & {
  id: string;
  key: string;
  type: string;
  properties: Record<string, unknown>;
};

type ExportedPackage = Record<string, unknown> & {
  screens?: VisualView[];
  popups?: VisualView[];
};

type VisualView = Record<string, unknown> & {
  id: string;
  key: string;
  elements?: VisualElement[];
};

test('FND-06 mounted Screen selection contains known legacy objects and preserves authored data', async ({ page, request }) => {
  const original = await exportPackage(request);
  const suffix = Date.now();
  const screenKey = 'demo.overview';
  const tank = legacy('tank', `fnd06-tank-${suffix}`, 20, {
    equipmentPath: 'Plant/Tank-01', levelScale: 100, label: 'Legacy tank authored label'
  });
  const value = legacy('value', `fnd06-value-${suffix}`, 150, { format: '0.00 bar', sourcePath: 'Plant/Pressure' });
  const dynamo = legacy('dynamo', `fnd06-dynamo-${suffix}`, 280, { dynamoIdentity: 'pump-legacy', equipmentPath: 'Plant/Pump-01' });
  const status = legacy('status', `fnd06-status-${suffix}`, 410, { statusCode: 'fault', label: 'Pump status' });
  const unknown = legacy('vendor.unknown-x', `fnd06-unknown-${suffix}`, 540, { vendorState: 'opaque' });
  const seeded: ExportedPackage = structuredClone(original);
  seeded.screens = appendLegacyElements(
    seeded.screens,
    screenKey,
    [tank, value, dynamo, status, unknown],
    'Screen');

  try {
    await applyPackage(request, seeded);
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: screenKey }).click();

    const workspace = page.getByTestId('visual-editor-workspace');
    const outliner = workspace.getByTestId('visual-editor-outliner');
    const inspector = workspace.getByTestId('visual-property-inspector');
    await expect(workspace).toBeVisible();

    for (const [element, route] of [[tank, 'canvas'], [value, 'outliner'], [dynamo, 'canvas'], [status, 'outliner']] as const) {
      if (route === 'canvas') await canvasObject(page, element.id).click();
      else await outlinerEntry(outliner, element.key).click();
      await expect(workspace).toBeVisible();
      await expect(inspector).toContainText(element.key);
      await expect(inspector.getByRole('status')).toContainText(`Compatibility mode for persisted legacy type: ${element.type}`);
      await expect(workspace.getByTestId('visual-dynamic-property-editor')).toBeVisible();
      await expect(workspace.getByTestId('visual-binding-editor')).toBeVisible();
    }

    await canvasObject(page, tank.id).click();
    const width = inspector.getByRole('spinbutton', { name: 'Width', exact: true });
    await width.fill('123');
    await width.press('Enter');
    await expect(canvasObject(page, tank.id)).toHaveCSS('width', '123px');
    // label is not part of the bounded compatibility schema. Its presence after
    // the shared-surface edit proves the mounted draft did not discard authored
    // legacy-specific data while changing width.
    await expect(workspace.getByTestId('visual-editor-canonical-renderer')
      .getByText('Legacy tank authored label', { exact: true })).toBeVisible();

    const serverPackage = await exportPackage(request);
    const serverTank = (serverPackage.screens?.find(item => item.key === screenKey)?.elements as VisualElement[])
      .find(item => item.key === tank.key);
    expect(serverTank?.properties.width).toBe(100);
    expect(serverTank?.properties.equipmentPath).toBe('Plant/Tank-01');
    expect(serverTank?.properties.levelScale).toBe(100);

    await outlinerEntry(outliner, unknown.key).click();
    await expect(workspace).toBeVisible();
    await expect(inspector.getByRole('alert')).toContainText('not registered for property editing');
    await expect(workspace.getByTestId('visual-dynamic-property-editor').getByRole('alert')).toContainText('Unknown built-in visual object type');
    await expect(workspace.getByTestId('visual-binding-editor')).toContainText("Visual object type 'vendor.unknown-x' is not a registered");
  } finally {
    await restoreAndAssertCanonicalViews(request, original);
  }
});

test('FND-06 mounted Popup selection contains legacy value and status without poisoning the editor', async ({ page, request }) => {
  const original = await exportPackage(request);
  const suffix = Date.now();
  const popupKey = 'popup.pump.standard';
  const value = legacy('value', `fnd06-popup-value-${suffix}`, 20, { sourcePath: 'Plant/PopupPressure', precision: 3 });
  const status = legacy('status', `fnd06-popup-status-${suffix}`, 160, { statusCode: 'fault', alarmClass: 'critical' });
  const seeded: ExportedPackage = structuredClone(original);
  seeded.popups = appendLegacyElements(seeded.popups, popupKey, [value, status], 'Popup');

  try {
    await applyPackage(request, seeded);
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Popups/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: popupKey }).click();

    const workspace = page.getByTestId('popup-visual-editor-workspace');
    const outliner = workspace.getByTestId('visual-editor-outliner');
    const inspector = workspace.getByTestId('visual-property-inspector');
    for (const [element, route] of [[value, 'canvas'], [status, 'outliner']] as const) {
      if (route === 'canvas') await canvasObject(page, element.id).click();
      else await outlinerEntry(outliner, element.key).click();
      await expect(workspace).toBeVisible();
      await expect(inspector).toContainText(element.key);
      await expect(inspector.getByRole('status')).toContainText(`Compatibility mode for persisted legacy type: ${element.type}`);
      await expect(workspace.getByTestId('visual-dynamic-property-editor')).toBeVisible();
      await expect(workspace.getByTestId('visual-binding-editor')).toBeVisible();
    }
  } finally {
    await restoreAndAssertCanonicalViews(request, original);
  }
});

function appendLegacyElements(
  views: VisualView[] | undefined,
  key: string,
  legacyElements: VisualElement[],
  kind: 'Screen' | 'Popup'
): VisualView[] {
  if (!views?.some(view => view.key === key))
    throw new Error(`Canonical ${kind} '${key}' is required for the FND-06 fixture.`);

  return views.map(view => view.key === key
    ? { ...view, elements: [...(view.elements ?? []), ...legacyElements] }
    : view);
}

async function restoreAndAssertCanonicalViews(
  request: import('@playwright/test').APIRequestContext,
  original: ExportedPackage
): Promise<void> {
  await applyPackage(request, original);
  const restored = await exportPackage(request);

  expect(restored.screens).toEqual(original.screens);
  expect(restored.popups).toEqual(original.popups);
  expect([
    ...(restored.screens ?? []),
    ...(restored.popups ?? [])
  ].some(view => view.key.startsWith('fnd06-'))).toBeFalsy();
}

function legacy(type: string, key: string, x: number, legacyFields: Record<string, unknown>): VisualElement {
  return {
    id: randomUUID(), key, type,
    properties: { x, y: 40, width: 100, height: 60, zIndex: 1, ...legacyFields },
    metadata: {}
  };
}

function canvasObject(page: Page, id: string): Locator {
  return page.locator(`[data-canvas-object-id="${id}"]`).first();
}

function outlinerEntry(outliner: Locator, key: string): Locator {
  return outliner.locator('.visual-editor-outliner__select').filter({ hasText: key }).first();
}

async function exportPackage(request: import('@playwright/test').APIRequestContext): Promise<ExportedPackage> {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok(), await response.text()).toBeTruthy();
  return await response.json() as ExportedPackage;
}

async function applyPackage(request: import('@playwright/test').APIRequestContext, value: ExportedPackage): Promise<void> {
  const response = await request.post('/api/engineering/import/json/apply', {
    headers: { 'content-type': 'application/json; charset=utf-8' }, data: value
  });
  expect(response.ok(), await response.text()).toBeTruthy();
}
