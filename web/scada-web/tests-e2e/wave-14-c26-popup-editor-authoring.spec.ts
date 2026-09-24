import { randomUUID } from 'node:crypto';
import { expect, test, type Locator, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

type ExportedPackage = {
  popups?: Array<Record<string, unknown>>;
  [key: string]: unknown;
};

test('C26.9 mounted Popup editor exposes bounds in the canonical single-canvas authoring surface', async ({ page, request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as ExportedPackage;

  const popupId = randomUUID();
  const rectangleId = randomUUID();
  const suffix = Date.now();
  const popupKey = `c26-popup-authoring-${suffix}`;
  const rectangleKey = `c26-popup-rect-${suffix}`;
  const seededPackage = structuredClone(originalPackage);
  seededPackage.popups = [
    ...(seededPackage.popups ?? []),
    {
      id: popupId,
      key: popupKey,
      name: 'C26 Popup Authoring',
      templateKey: null,
      properties: { backgroundColor: '#101820' },
      context: {},
      metadata: {},
      x: 1700,
      y: 1000,
      elements: [{
        id: rectangleId,
        key: rectangleKey,
        type: 'core.rectangle',
        properties: { x: 20, y: 30, width: 180, height: 100, zIndex: 1, fillColor: '#245F99' },
        metadata: {}
      }]
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
    await page.locator('.eng-nav').getByRole('button', { name: /Popups/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: popupKey }).click();

    const workspace = page.getByTestId('popup-visual-editor-workspace');
    const canvas = workspace.getByTestId('visual-editor-canvas');
    const outliner = workspace.getByTestId('visual-editor-outliner');
    const inspector = workspace.getByTestId('visual-property-inspector');
    const boundary = workspace.getByTestId('visual-editor-logical-boundary');
    const canonicalLayer = workspace.getByTestId('visual-editor-canonical-layer');
    const authoredBackground = canvas.locator('.visual-editor-canvas__authored-background');
    const toolbar = workspace.getByTestId('visual-editor-authoring-toolbar');
    const paste = toolbar.getByRole('button', { name: 'Colar', exact: true });

    await expect(boundary).toBeVisible();
    await expect(boundary).toHaveAttribute('data-logical-width', '200');
    await expect(boundary).toHaveAttribute('data-logical-height', '130');
    await expect(boundary).toContainText('limites lógicos: 200 × 130');
    await expect(workspace.getByTestId('popup-authoring-bounds')).toContainText('200 × 130');
    await expect(paste).toBeDisabled();

    await expect(canvas).toHaveAttribute('data-renderer', 'canonical-single-surface');
    await expect(canonicalLayer).toBeVisible();
    await expect(canonicalLayer).toHaveCSS('width', '200px');
    await expect(canonicalLayer).toHaveCSS('height', '130px');
    await expect(authoredBackground).toHaveCSS('background-color', 'rgb(16, 24, 32)');
    await expect(workspace.getByTestId('popup-authoring-bounds')).toContainText('X 1700, Y 950');
    await expect(workspace.getByTestId('popup-runtime-composition-preview')).toHaveCount(0);

    const rectangle = canvasObject(page, rectangleId);
    await outlinerEntry(outliner, rectangleKey).click();
    await expect(rectangle).toHaveClass(/is-selected/);
    await expect(inspector).toContainText(rectangleKey);

    const widthInput = inspector.getByRole('spinbutton', { name: 'Width', exact: true });
    await expect(widthInput).toHaveValue('180');
    await widthInput.fill('260');
    await widthInput.press('Enter');
    await expect.poll(() => inlineNumber(rectangle, 'width')).toBe(260);
    await expect(boundary).toHaveAttribute('data-logical-width', '280');
    await expect(canonicalLayer).toHaveCSS('width', '280px');
    await expect(workspace.getByTestId('popup-authoring-bounds')).toContainText('X 1640');

    const leftBeforeMove = await inlineNumber(rectangle, 'left');
    const objectBox = await rectangle.boundingBox();
    expect(objectBox).not.toBeNull();
    await page.mouse.move(objectBox!.x + objectBox!.width / 2, objectBox!.y + objectBox!.height / 2);
    await page.mouse.down();
    await page.mouse.move(objectBox!.x + objectBox!.width / 2 + 40, objectBox!.y + objectBox!.height / 2 + 20, { steps: 4 });
    await page.mouse.up();
    await expect.poll(() => inlineNumber(rectangle, 'left')).toBeGreaterThan(leftBeforeMove);
    const widthAfterMove = Number(await boundary.getAttribute('data-logical-width'));
    expect(widthAfterMove).toBeGreaterThan(280);
    await expect(canonicalLayer).toHaveCSS('width', `${widthAfterMove}px`);

    const objectWidthBeforeResize = await inlineNumber(rectangle, 'width');
    const resizeHandle = rectangle.locator('[data-canvas-resize-handle="southEast"]');
    const handleBox = await resizeHandle.boundingBox();
    expect(handleBox).not.toBeNull();
    await page.mouse.move(handleBox!.x + handleBox!.width / 2, handleBox!.y + handleBox!.height / 2);
    await page.mouse.down();
    await page.mouse.move(handleBox!.x + handleBox!.width / 2 + 40, handleBox!.y + handleBox!.height / 2 + 20, { steps: 4 });
    await page.mouse.up();
    await expect.poll(() => inlineNumber(rectangle, 'width')).toBeGreaterThan(objectWidthBeforeResize);
    const widthAfterResize = Number(await boundary.getAttribute('data-logical-width'));
    const heightAfterResize = Number(await boundary.getAttribute('data-logical-height'));
    expect(widthAfterResize).toBeGreaterThan(widthAfterMove);
    expect(heightAfterResize).toBeGreaterThan(130);
    await expect(canonicalLayer).toHaveCSS('width', `${widthAfterResize}px`);
    await expect(canonicalLayer).toHaveCSS('height', `${heightAfterResize}px`);
    await expect(workspace.getByTestId('popup-authoring-bounds')).toContainText(`X ${1920 - widthAfterResize}, Y ${1080 - heightAfterResize}`);

    const canonicalMetrics = await canonicalLayer.evaluate(element => {
      const layer = element as HTMLElement;
      const renderer = layer.querySelector<HTMLElement>('.visual-editor-renderer-stage')!;
      return {
        layerWidth: layer.clientWidth,
        layerHeight: layer.clientHeight,
        rendererMargin: getComputedStyle(renderer).margin,
        rendererOverflow: getComputedStyle(renderer).overflow
      };
    });
    expect(canonicalMetrics.layerWidth).toBe(widthAfterResize);
    expect(canonicalMetrics.layerHeight).toBe(heightAfterResize);
    expect(canonicalMetrics.rendererMargin).toBe('0px');
    expect(canonicalMetrics.rendererOverflow).toBe('hidden');

    await workspace.getByTestId('popup-visual-editor-preview').click();
    await expect(workspace.getByText('Candidato válido', { exact: true })).toBeVisible();
    await expect(workspace.getByTestId('popup-visual-editor-apply')).toBeEnabled();
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

function canvasObject(page: Page, id: string): Locator {
  return page.locator(`[data-canvas-object-id="${id}"]`).first();
}

function outlinerEntry(outliner: Locator, key: string): Locator {
  return outliner.locator('.visual-editor-outliner__select').filter({ hasText: key }).first();
}

async function inlineNumber(locator: Locator, property: 'left' | 'top' | 'width' | 'height'): Promise<number> {
  return await locator.evaluate((element, key) => {
    const raw = (element as HTMLElement).style[key as 'left'];
    return Number.parseFloat(raw || '0');
  }, property);
}
