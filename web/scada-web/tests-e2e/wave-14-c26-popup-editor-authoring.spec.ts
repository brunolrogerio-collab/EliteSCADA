import { randomUUID } from 'node:crypto';
import { expect, test, type Locator, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

type ExportedPackage = {
  popups?: Array<Record<string, unknown>>;
  [key: string]: unknown;
};

test('C26.9 mounted Popup editor exposes bounds and preserves Runtime composition semantics', async ({ page, request }) => {
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
    const runtimePreview = workspace.getByTestId('popup-runtime-composition-preview');
    const runtimeBox = workspace.getByTestId('popup-runtime-composition-box');
    const toolbar = workspace.getByTestId('visual-editor-authoring-toolbar');
    const paste = toolbar.getByRole('button', { name: 'Colar', exact: true });

    await expect(boundary).toBeVisible();
    await expect(boundary).toHaveAttribute('data-logical-width', '200');
    await expect(boundary).toHaveAttribute('data-logical-height', '130');
    await expect(boundary).toContainText('limites lógicos: 200 × 130');
    await expect(workspace.getByTestId('popup-authoring-bounds')).toContainText('200 × 130');
    await expect(paste).toBeDisabled();

    await expect(runtimePreview).toHaveAttribute('data-popup-logical-x', '1700');
    await expect(runtimePreview).toHaveAttribute('data-popup-logical-y', '950');
    await expect(runtimePreview).toHaveAttribute('data-popup-logical-width', '200');
    await expect(runtimePreview).toHaveAttribute('data-popup-logical-height', '130');
    await expect(runtimePreview.getByTestId('runtime-logical-viewport')).toHaveAttribute('data-design-width', '1920');
    await expect(runtimePreview.getByTestId('runtime-logical-viewport')).toHaveAttribute('data-design-height', '1080');
    await expect(runtimeBox).toHaveAttribute('data-popup-key', popupKey);
    await expect(runtimeBox.locator('.runtime-visual-popup-content')).toHaveCSS('background-color', 'rgb(16, 24, 32)');

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
    await expect(runtimePreview).toHaveAttribute('data-popup-logical-width', '280');
    await expect(runtimePreview).toHaveAttribute('data-popup-logical-x', '1640');

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
    await expect(runtimePreview).toHaveAttribute('data-popup-logical-width', String(widthAfterMove));

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
    await expect(runtimePreview).toHaveAttribute('data-popup-logical-width', String(widthAfterResize));
    await expect(runtimePreview).toHaveAttribute('data-popup-logical-height', String(heightAfterResize));

    const previewMetrics = await runtimeBox.evaluate(element => {
      const box = element as HTMLElement;
      const content = box.querySelector<HTMLElement>('.runtime-visual-popup-content')!;
      const renderer = box.querySelector<HTMLElement>('.visual-editor-renderer-stage')!;
      return {
        left: Number.parseFloat(box.style.left),
        top: Number.parseFloat(box.style.top),
        width: Number.parseFloat(box.style.width),
        contentWidth: content.clientWidth,
        contentHeight: content.clientHeight,
        rendererMargin: getComputedStyle(renderer).margin,
        rendererOverflow: getComputedStyle(renderer).overflow
      };
    });
    expect(previewMetrics.left).toBe(1920 - widthAfterResize);
    expect(previewMetrics.top).toBe(1080 - heightAfterResize);
    expect(previewMetrics.width).toBe(widthAfterResize);
    expect(previewMetrics.contentWidth).toBe(widthAfterResize);
    expect(previewMetrics.contentHeight).toBe(heightAfterResize);
    expect(previewMetrics.rendererMargin).toBe('0px');
    expect(previewMetrics.rendererOverflow).toBe('hidden');

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
