import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { resolveRuntimeLogicalSize } from '../src/runtime/visual-navigation/runtimeLogicalCanvas';
import { resolvePopupLogicalBounds, resolvePopupLogicalPosition } from '../src/runtime/visual-navigation/runtimePopupPosition';
import { buildEeeDemoPackage, EEE_HMI } from './c11-eee-demo-hmi';

const projectKey = 'e2e-wave11';

test('C26 Runtime composes the canonical EEE Popup from authored bounds without moving the Screen', async ({ page, request }) => {
  const original = await loadWorking(request);

  try {
    const candidate = buildEeeDemoPackage(original);
    const authoredPopup = candidate.popups.find((popup: any) => popup.key === EEE_HMI.popups.p01.key);
    expect(authoredPopup).toBeTruthy();

    const authoredBounds = resolvePopupLogicalBounds(authoredPopup);
    const authoredPosition = resolvePopupLogicalPosition(authoredPopup, resolveRuntimeLogicalSize(), authoredBounds);
    expect(authoredBounds.width).toBeGreaterThan(0);
    expect(authoredBounds.height).toBeGreaterThan(0);

    await previewAndApply(request, candidate, 'C26 Popup composition');
    const saved = await savePublishActivate(request, 'EliteSCADA — C26 Popup composition regression');

    await page.setViewportSize({ width: 1280, height: 720 });
    await page.goto('/');
    await expect(page.getByTestId('runtime-engineering-application'))
      .toHaveAttribute('data-runtime-revision', String(saved.revision));
    await expect(page.getByTestId('runtime-visual-navigator'))
      .toHaveAttribute('data-active-screen-key', EEE_HMI.screens.overview.key);

    const screen = page.locator('.runtime-visual-screen');
    await expect(screen).toBeVisible();
    const screenBefore = await layoutSnapshot(screen);

    await page.getByRole('button', { name: 'DETALHES P01' }).click();

    const popupLayer = page.locator('.runtime-visual-popup-layer');
    await expect(popupLayer).toHaveAttribute('data-popup-count', '1');
    const popup = page.locator('.runtime-visual-popup').first();
    await expect(popup).toBeVisible();
    await expect(popup).toHaveAttribute('data-popup-key', EEE_HMI.popups.p01.key);
    await expect(popup).toHaveAttribute('data-popup-logical-x', String(authoredPosition.x));
    await expect(popup).toHaveAttribute('data-popup-logical-y', String(authoredPosition.y));
    await expect(popup).toHaveAttribute('data-popup-logical-width', String(authoredBounds.width));
    await expect(popup).toHaveAttribute('data-popup-logical-height', String(authoredBounds.height));
    await expect(popup.getByRole('button', { name: 'PARAR' })).toBeVisible();
    await expect(popup.getByRole('button', { name: 'FECHAR' })).toBeVisible();

    const content = popup.locator('.runtime-visual-popup-content');
    const renderer = content.locator(':scope > .runtime-visual-definition > .visual-editor-renderer-stage');
    await expect(renderer).toBeVisible();
    const popupMetrics = await renderer.evaluate(element => {
      const node = element as HTMLElement;
      const style = getComputedStyle(node);
      return {
        clientWidth: node.clientWidth,
        clientHeight: node.clientHeight,
        scrollWidth: node.scrollWidth,
        scrollHeight: node.scrollHeight,
        scrollLeft: node.scrollLeft,
        scrollTop: node.scrollTop,
        marginTop: style.marginTop,
        marginRight: style.marginRight,
        marginBottom: style.marginBottom,
        marginLeft: style.marginLeft,
        minHeight: style.minHeight,
        overflowX: style.overflowX,
        overflowY: style.overflowY,
        backgroundImage: style.backgroundImage,
        boxShadow: style.boxShadow
      };
    });
    expect(popupMetrics.clientWidth).toBe(authoredBounds.width);
    expect(popupMetrics.clientHeight).toBe(authoredBounds.height);
    expect(popupMetrics.scrollLeft).toBe(0);
    expect(popupMetrics.scrollTop).toBe(0);
    expect(popupMetrics.scrollWidth).toBeLessThanOrEqual(popupMetrics.clientWidth + 1);
    expect(popupMetrics.scrollHeight).toBeLessThanOrEqual(popupMetrics.clientHeight + 1);
    expect(popupMetrics.marginTop).toBe('0px');
    expect(popupMetrics.marginRight).toBe('0px');
    expect(popupMetrics.marginBottom).toBe('0px');
    expect(popupMetrics.marginLeft).toBe('0px');
    expect(popupMetrics.minHeight).toBe('0px');
    expect(popupMetrics.overflowX).toBe('hidden');
    expect(popupMetrics.overflowY).toBe('hidden');
    expect(popupMetrics.backgroundImage).toBe('none');
    expect(popupMetrics.boxShadow).toBe('none');

    expect(await layoutSnapshot(screen)).toEqual(screenBefore);
    await assertNoDocumentOverflow(page);

    await popup.getByRole('button', { name: 'FECHAR' }).click();
    await expect(popupLayer).toHaveAttribute('data-popup-count', '0');
    expect(await layoutSnapshot(screen)).toEqual(screenBefore);
    await assertNoDocumentOverflow(page);
  } finally {
    await previewAndApply(request, original, 'C26 Popup cleanup');
    await savePublishActivate(request, 'Wave 11 E2E — restored after C26 Popup regression');
  }
});

async function layoutSnapshot(locator: ReturnType<Page['locator']>) {
  return await locator.evaluate(element => {
    const node = element as HTMLElement;
    const rect = node.getBoundingClientRect();
    return {
      clientWidth: node.clientWidth,
      clientHeight: node.clientHeight,
      scrollWidth: node.scrollWidth,
      scrollHeight: node.scrollHeight,
      scrollLeft: node.scrollLeft,
      scrollTop: node.scrollTop,
      x: rect.x,
      y: rect.y,
      width: rect.width,
      height: rect.height
    };
  });
}

async function assertNoDocumentOverflow(page: Page) {
  const overflow = await page.evaluate(() => ({
    width: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    height: document.documentElement.scrollHeight - document.documentElement.clientHeight
  }));
  expect(overflow.width).toBeLessThanOrEqual(1);
  expect(overflow.height).toBeLessThanOrEqual(1);
}

async function previewAndApply(request: APIRequestContext, candidate: any, label: string) {
  const workspace = await loadWorkspace(request);
  const previewResponse = await request.post('/api/engineering/import/json/preview', { data: candidate });
  expect(previewResponse.ok(), `${label} preview failed: HTTP ${previewResponse.status()} ${await previewResponse.text()}`).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number; items?: any[] };
  expect(preview.canApply, `${label} preview issues: ${JSON.stringify(preview.items ?? [], null, 2)}`).toBe(true);
  expect(preview.errorCount).toBe(0);

  const afterPreview = await loadWorkspace(request);
  expect(afterPreview.changeVersion).toBe(workspace.changeVersion);
  const applyResponse = await request.post('/api/engineering/import/json/apply', {
    headers: { 'x-elitescada-workspace-version': String(afterPreview.changeVersion) },
    data: candidate
  });
  expect(applyResponse.ok(), `${label} Apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`).toBeTruthy();
}

async function savePublishActivate(request: APIRequestContext, projectName: string): Promise<{ revision: number }> {
  const save = await request.post(`/api/engineering/persistence/${projectKey}/save`, { data: { projectName } });
  expect(save.ok(), `Save failed: HTTP ${save.status()} ${await save.text()}`).toBeTruthy();
  const saved = await save.json() as { revision: number };
  const publish = await request.post(`/api/engineering/persistence/${projectKey}/revisions/${saved.revision}/publish`, { data: {} });
  expect(publish.ok(), `Publish failed: HTTP ${publish.status()} ${await publish.text()}`).toBeTruthy();
  const activate = await request.post(`/api/engineering/persistence/${projectKey}/published/activate`, { data: {} });
  expect(activate.ok(), `Activate failed: HTTP ${activate.status()} ${await activate.text()}`).toBeTruthy();
  return saved;
}

async function loadWorking(request: APIRequestContext): Promise<any> {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function loadWorkspace(request: APIRequestContext): Promise<{ changeVersion: number }> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}
