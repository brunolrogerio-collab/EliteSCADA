import { randomUUID } from 'node:crypto';
import { expect, test, type Locator, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

type ExportedTag = {
  path: string;
  dataType: string;
};

type ExportedPackage = {
  screens?: Array<Record<string, unknown>>;
  tags?: ExportedTag[];
  [key: string]: unknown;
};

test('C26.8 mounted Screen editor exposes truthful controls and completes basic authoring', async ({ page, request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;
  const booleanTag = originalPackage.tags?.find(tag => tag.dataType.toLowerCase() === 'boolean');
  expect(booleanTag, 'seeded Engineering must expose a Boolean TAG for binding authoring').toBeTruthy();

  const screenId = randomUUID();
  const rectangleAId = randomUUID();
  const rectangleBId = randomUUID();
  const rectangleCId = randomUUID();
  const textId = randomUUID();
  const suffix = Date.now();
  const screenKey = `c26-screen-authoring-${suffix}`;
  const rectangleAKey = `c26-rect-a-${suffix}`;
  const rectangleBKey = `c26-rect-b-${suffix}`;
  const rectangleCKey = `c26-rect-c-${suffix}`;
  const textKey = `c26-text-${suffix}`;
  const seededPackage = structuredClone(originalPackage);
  seededPackage.screens = [
    ...(seededPackage.screens ?? []),
    {
      id: screenId,
      key: screenKey,
      name: 'C26 Screen Authoring',
      route: `/c26-screen-authoring-${suffix}`,
      properties: { canvasWidth: '800', canvasHeight: '600' },
      context: {},
      metadata: {},
      elements: [
        rectangle(rectangleAId, rectangleAKey, 30, 50, 1),
        rectangle(rectangleBId, rectangleBKey, 160, 140, 2),
        rectangle(rectangleCId, rectangleCKey, 290, 230, 3),
        {
          id: textId,
          key: textKey,
          type: 'core.text',
          properties: { x: 60, y: 310, width: 220, height: 60, zIndex: 4, text: 'Original C26 text' },
          metadata: {}
        }
      ]
    }
  ];

  let seeded = false;
  try {
    const seedResponse = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: seededPackage
    });
    expect(seedResponse.ok(), await seedResponse.text()).toBeTruthy();
    seeded = true;

    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: screenKey }).click();

    const toolbar = page.getByTestId('visual-editor-authoring-toolbar');
    const canvasToolbar = page.getByRole('toolbar', { name: 'Canvas controls' });
    const canvas = page.getByTestId('visual-editor-canvas');
    const canvasSurface = canvas.locator('.visual-editor-canvas__surface');
    const viewport = canvas.locator('.visual-editor-canvas__viewport');
    const outliner = page.getByTestId('visual-editor-outliner');
    const inspector = page.getByTestId('visual-property-inspector');
    const undo = toolbar.getByRole('button', { name: 'Desfazer', exact: true });
    const redo = toolbar.getByRole('button', { name: 'Refazer', exact: true });
    const copy = toolbar.getByRole('button', { name: 'Copiar', exact: true });
    const paste = toolbar.getByRole('button', { name: 'Colar', exact: true });

    await expect(undo).toBeDisabled();
    await expect(redo).toBeDisabled();
    await expect(copy).toBeDisabled();
    await expect(paste).toBeDisabled();

    const grid = canvasToolbar.getByTestId('canvas-grid-toggle');
    const snap = canvasToolbar.getByTestId('canvas-snap-toggle');
    await expect(grid).toHaveAttribute('aria-pressed', 'true');
    await grid.click();
    await expect(grid).toHaveAttribute('aria-pressed', 'false');
    await grid.click();
    await expect(grid).toHaveAttribute('aria-pressed', 'true');
    await expect(snap).toHaveAttribute('aria-pressed', 'true');
    await snap.click();
    await expect(snap).toHaveAttribute('aria-pressed', 'false');

    await expect(canvas.locator('.visual-editor-canvas__status')).toContainText('100%');
    await canvasToolbar.getByRole('button', { name: 'Zoom in' }).click();
    await expect(canvas.locator('.visual-editor-canvas__status')).toContainText('120%');
    await canvasToolbar.getByRole('button', { name: 'Reset viewport' }).click();
    await expect(canvas.locator('.visual-editor-canvas__status')).toContainText('100%');
    const transformBeforePan = await viewport.evaluate(element => (element as HTMLElement).style.transform);
    await canvasSurface.dispatchEvent('wheel', { deltaX: 24, deltaY: 16 });
    await expect.poll(() => viewport.evaluate(element => (element as HTMLElement).style.transform))
      .not.toBe(transformBeforePan);
    await canvasToolbar.getByRole('button', { name: 'Reset viewport' }).click();

    const rectangleA = canvasObject(page, rectangleAId);
    await outlinerEntry(outliner, rectangleAKey).click();
    await expect(rectangleA).toHaveClass(/is-selected/);
    await expect(inspector).toContainText(rectangleAKey);
    await expect(copy).toBeEnabled();

    const widthInput = inspector.getByRole('spinbutton', { name: 'Width', exact: true });
    await expect(widthInput).toHaveValue('80');
    await widthInput.fill('96');
    await widthInput.press('Enter');
    await expect.poll(() => inlineNumber(rectangleA, 'width')).toBe(96);
    await expect(undo).toBeEnabled();

    const leftBeforeMove = await inlineNumber(rectangleA, 'left');
    const objectBox = await rectangleA.boundingBox();
    expect(objectBox).not.toBeNull();
    await page.mouse.move(objectBox!.x + objectBox!.width / 2, objectBox!.y + objectBox!.height / 2);
    await page.mouse.down();
    await page.mouse.move(objectBox!.x + objectBox!.width / 2 + 47, objectBox!.y + objectBox!.height / 2 + 23, { steps: 4 });
    await page.mouse.up();
    await expect.poll(() => inlineNumber(rectangleA, 'left')).not.toBe(leftBeforeMove);

    const widthBeforeResize = await inlineNumber(rectangleA, 'width');
    const resizeHandle = rectangleA.locator('[data-canvas-resize-handle="southEast"]');
    const handleBox = await resizeHandle.boundingBox();
    expect(handleBox).not.toBeNull();
    await page.mouse.move(handleBox!.x + handleBox!.width / 2, handleBox!.y + handleBox!.height / 2);
    await page.mouse.down();
    await page.mouse.move(handleBox!.x + handleBox!.width / 2 + 31, handleBox!.y + handleBox!.height / 2 + 19, { steps: 4 });
    await page.mouse.up();
    const widthAfterResize = await expect.poll(() => inlineNumber(rectangleA, 'width'))
      .toBeGreaterThan(widthBeforeResize)
      .then(() => inlineNumber(rectangleA, 'width'));

    await undo.click();
    await expect.poll(() => inlineNumber(rectangleA, 'width')).toBe(widthBeforeResize);
    await expect(redo).toBeEnabled();
    await redo.click();
    await expect.poll(() => inlineNumber(rectangleA, 'width')).toBe(widthAfterResize);

    await outlinerEntry(outliner, textKey).click();
    const textInput = inspector.getByRole('textbox', { name: 'Text', exact: true });
    await textInput.fill('C26 edited text');
    await textInput.press('Enter');
    await expect(page.getByTestId('visual-editor-canonical-renderer').getByText('C26 edited text', { exact: true })).toBeVisible();

    await expect(paste).toBeDisabled();
    await copy.click();
    await expect(paste).toBeEnabled();
    const roots = canvas.locator('.visual-editor-canvas__viewport > .visual-editor-canvas__object');
    await expect(roots).toHaveCount(4);
    await paste.click();
    await expect(roots).toHaveCount(5);
    await undo.click();
    await expect(roots).toHaveCount(4);
    await redo.click();
    await expect(roots).toHaveCount(5);

    await outlinerEntry(outliner, rectangleAKey).click();
    await outlinerEntry(outliner, rectangleBKey).click({ modifiers: ['Shift'] });
    await outlinerEntry(outliner, rectangleCKey).click({ modifiers: ['Shift'] });
    const alignTop = toolbar.getByRole('button', { name: 'Alinhar ao topo', exact: true });
    const distributeCenters = toolbar.getByRole('button', { name: 'Distribuir centros horizontalmente', exact: true });
    await expect(alignTop).toBeEnabled();
    await expect(distributeCenters).toBeEnabled();
    await alignTop.click();
    const rectangleB = canvasObject(page, rectangleBId);
    const rectangleC = canvasObject(page, rectangleCId);
    await expect.poll(async () => new Set([
      await inlineNumber(rectangleA, 'top'),
      await inlineNumber(rectangleB, 'top'),
      await inlineNumber(rectangleC, 'top')
    ]).size).toBe(1);
    await distributeCenters.click();
    await expect.poll(async () => centerX(rectangleB))
      .toBeCloseTo(((await centerX(rectangleA)) + (await centerX(rectangleC))) / 2, 4);

    await toolbar.getByRole('button', { name: 'Agrupar', exact: true }).click();
    await expect(roots).toHaveCount(3);
    const group = roots.filter({ has: page.locator(`[data-canvas-object-id="${rectangleAId}"]`) });
    await expect(group).toHaveAttribute('data-canvas-object-type', 'core.group');

    await outlinerEntry(outliner, rectangleAKey).click();
    await outlinerEntry(outliner, textKey).click({ modifiers: ['Shift'] });
    await expect(copy).toBeDisabled();
    await expect(canvasToolbar.getByRole('button', { name: 'Duplicate', exact: true })).toBeDisabled();
    await expect(canvasToolbar.getByRole('button', { name: 'Delete', exact: true })).toBeDisabled();

    await outlinerEntry(outliner, 'group').click();
    await toolbar.getByRole('button', { name: 'Desagrupar', exact: true }).click();
    await expect(roots).toHaveCount(5);

    await outlinerEntry(outliner, rectangleAKey).click();
    await toolbar.getByRole('button', { name: 'Bloquear seleção', exact: true }).click();
    await expect(rectangleA).toHaveClass(/is-authoring-locked/);
    await expect(rectangleA.locator('[data-canvas-resize-handle]')).toHaveCount(0);
    await expect(canvasToolbar.getByRole('button', { name: 'Duplicate', exact: true })).toBeDisabled();
    await expect(canvasToolbar.getByRole('button', { name: 'Delete', exact: true })).toBeDisabled();
    await expect(canvasToolbar.getByRole('button', { name: 'Bring to front', exact: true })).toBeDisabled();
    await toolbar.getByRole('button', { name: 'Desbloquear seleção', exact: true }).click();
    await expect(rectangleA).not.toHaveClass(/is-authoring-locked/);
    await expect(rectangleA.locator('[data-canvas-resize-handle]')).toHaveCount(4);

    await canvasToolbar.getByRole('button', { name: 'Bring to front', exact: true }).click();
    await expect.poll(() => inlineNumber(rectangleA, 'zIndex')).toBeGreaterThan(await inlineNumber(rectangleC, 'zIndex'));

    const binding = page.getByTestId('visual-binding-editor');
    await binding.getByLabel('Propriedade visual').selectOption('visible');
    const source = binding.getByLabel('Fonte do projeto');
    const sourceOption = source.locator('option').filter({ hasText: booleanTag!.path }).first();
    const sourceValue = await sourceOption.getAttribute('value');
    expect(sourceValue).toBeTruthy();
    await source.selectOption(sourceValue!);
    await binding.getByRole('button', { name: 'Aplicar binding', exact: true }).click();
    await expect(binding.getByTestId('visual-binding-current')).toContainText(booleanTag!.path);

    await snap.click();
    await expect(snap).toHaveAttribute('aria-pressed', 'true');
    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    await expect(page.getByTestId('visual-editor-apply')).toBeEnabled();
  } finally {
    if (seeded) {
      const restore = await request.post('/api/engineering/import/json/apply', {
        headers: { 'content-type': 'application/json; charset=utf-8' },
        data: originalPackage
      });
      expect(restore.ok(), await restore.text()).toBeTruthy();
    }
  }
});

function rectangle(id: string, key: string, x: number, y: number, zIndex: number) {
  return {
    id,
    key,
    type: 'core.rectangle',
    properties: { x, y, width: 80, height: 60, zIndex, fillColor: '#245F99' },
    metadata: {}
  };
}

function canvasObject(page: Page, id: string): Locator {
  return page.locator(`[data-canvas-object-id="${id}"]`).first();
}

function outlinerEntry(outliner: Locator, key: string): Locator {
  return outliner.locator('.visual-editor-outliner__select').filter({ hasText: key }).first();
}

async function inlineNumber(locator: Locator, property: 'left' | 'top' | 'width' | 'height' | 'zIndex'): Promise<number> {
  return await locator.evaluate((element, key) => {
    const raw = (element as HTMLElement).style[key as 'left'];
    return Number.parseFloat(raw || '0');
  }, property);
}

async function centerX(locator: Locator): Promise<number> {
  return (await inlineNumber(locator, 'left')) + (await inlineNumber(locator, 'width')) / 2;
}
