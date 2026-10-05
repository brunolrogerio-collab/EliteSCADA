import { expect, request as apiRequest, test } from '@playwright/test';
import { createE2eJwt } from './jwt';
import { createReportDraft } from '../src/engineering/reports/reportDesignerModel';
import type { ReportEngineeringDto } from '../src/engineering/reports/reportContracts';

test.use({ locale: 'pt-BR' });
test.describe.configure({ mode: 'serial' });

type VisualElement = {
  id?: string | null;
  key: string;
  type: string;
  bindings?: Array<{
    key: string;
    kind: string;
    target: string;
    metadata?: Record<string, string> | null;
  }> | null;
  properties?: Record<string, unknown> | null;
  children?: VisualElement[] | null;
};

type EngineeringPackage = {
  screens?: Array<{
    id?: string | null;
    key: string;
    name: string;
    route?: string | null;
    elements?: VisualElement[] | null;
  }>;
  tags?: Array<{ name: string; path: string; dataType: string; engineeringUnit?: string | null }>;
  reports?: ReportEngineeringDto[];
  [key: string]: unknown;
};

test('Wave 08 creates a closed free polygon and a dynamic text binding through canonical Engineering', async ({ page }) => {
  test.setTimeout(90_000);
  const api = await apiRequest.newContext({
    baseURL: 'http://127.0.0.1:5080',
    extraHTTPHeaders: {
      Authorization: `Bearer ${createE2eJwt('e2e-developer', ['developer'], 'E2E Developer')}`
    }
  });
  const originalResponse = await api.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const original = await originalResponse.json() as EngineeringPackage;
  const screen = original.screens?.[0];
  const tag = original.tags?.find(candidate => candidate.path?.trim());
  expect(screen).toBeTruthy();
  expect(tag).toBeTruthy();

  try {
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /^Telas\b/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: screen!.key }).click();

    const palette = page.getByTestId('visual-editor-authoring-toolbar');
    await palette.locator('[data-insert-object-type="core.polygon"]').click();
    const surface = page.locator('.visual-editor-canvas__surface');
    await expect(surface).toBeVisible();

    await surface.click({ position: { x: 80, y: 80 } });
    await surface.click({ position: { x: 220, y: 90 } });
    await surface.click({ position: { x: 190, y: 210 } });
    await surface.click({ position: { x: 100, y: 190 } });
    await surface.focus();
    await surface.press('Enter');

    const polygon = page.locator('[data-canvas-object-type="core.polygon"]').last();
    await expect(polygon).toBeVisible();
    await polygon.click();
    await expect(page.locator('.visual-editor-canvas__polygon-vertex')).toHaveCount(4);

    await palette.locator('[data-insert-object-type="core.text"]').click();
    const textObject = page.locator('[data-canvas-object-type="core.text"]').last();
    await expect(textObject).toBeVisible();
    await textObject.click();

    await page.getByTestId('visual-editor-inspector-tab-dynamics').click();
    const binding = page.getByTestId('visual-binding-editor');
    await binding.getByLabel('Propriedade visual').selectOption('text');
    const source = binding.getByLabel('Fonte do projeto');
    const option = source.locator('option').filter({ hasText: tag!.path }).first();
    const optionText = await option.textContent();
    expect(optionText).toBeTruthy();
    await source.selectOption({ label: optionText! });
    await binding.getByRole('button', { name: 'Aplicar binding', exact: true }).click();
    await expect(binding.getByTestId('visual-binding-current')).toContainText(tag!.path);

    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    page.once('dialog', dialog => dialog.accept());
    await page.getByTestId('visual-editor-apply').click();

    const persisted = await expect.poll(async () => {
      const response = await api.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const exported = await response.json() as EngineeringPackage;
      const persistedScreen = exported.screens?.find(candidate => candidate.key === screen!.key);
      const elements = flatten(persistedScreen?.elements ?? []);
      const savedPolygon = elements.find(element => element.type === 'core.polygon');
      const savedText = elements.find(element =>
        element.type === 'core.text' && element.bindings?.some(item => item.key === 'text' && item.target === tag!.path));
      return savedPolygon && savedText ? { savedPolygon, savedText } : null;
    }, { timeout: 30_000 }).not.toBeNull().then(async () => {
      const response = await api.get('/api/engineering/export/json');
      const exported = await response.json() as EngineeringPackage;
      const persistedScreen = exported.screens!.find(candidate => candidate.key === screen!.key)!;
      const elements = flatten(persistedScreen.elements ?? []);
      return {
        savedPolygon: elements.find(element => element.type === 'core.polygon')!,
        savedText: elements.find(element =>
          element.type === 'core.text' && element.bindings?.some(item => item.key === 'text' && item.target === tag!.path))!
      };
    });

    const points = persisted.savedPolygon.properties?.points;
    expect(Array.isArray(points)).toBeTruthy();
    expect(points).toHaveLength(4);
    expect(points?.[0]).not.toEqual(points?.[3]);
    expect(persisted.savedText.bindings).toContainEqual(expect.objectContaining({
      key: 'text',
      kind: 'tag',
      target: tag!.path,
      metadata: expect.objectContaining({
        presentationMode: 'scalar-text',
        sourceDataType: tag!.dataType
      })
    }));

    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible({ timeout: 30_000 });
    await expect(page.locator('[data-canvas-object-type="core.polygon"]')).not.toHaveCount(0);
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);
  } finally {
    const restore = await api.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: original
    });
    expect(restore.ok()).toBeTruthy();
    await api.dispose();
  }
});

test('Wave 15 authors a selectable report and closed Bezier with editable anchors', async ({ page }) => {
  test.setTimeout(90_000);
  const api = await apiRequest.newContext({
    baseURL: 'http://127.0.0.1:5080',
    extraHTTPHeaders: {
      Authorization: `Bearer ${createE2eJwt('e2e-developer', ['developer'], 'E2E Developer')}`
    }
  });
  const originalResponse = await api.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const original = await originalResponse.json() as EngineeringPackage;
  const screen = original.screens?.[0];
  expect(screen).toBeTruthy();
  const report = createReportDraft(original.reports ?? []);
  report.name = 'E2E Report Launcher';

  try {
    const seeded = await api.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: { ...original, reports: [...(original.reports ?? []), report] }
    });
    expect(seeded.ok()).toBeTruthy();
    await page.goto('/engineering');
    await page.locator('.eng-nav').getByRole('button', { name: /^Telas\b/ }).click();
    await page.locator('.visual-editor-screen-list').getByRole('button').filter({ hasText: screen!.key }).click();
    const palette = page.getByTestId('visual-editor-authoring-toolbar');
    const surface = page.locator('.visual-editor-canvas__surface');
    await palette.locator('[data-insert-object-type="core.reportLauncher"]').click();
    const launcher = page.locator('[data-canvas-object-type="core.reportLauncher"]').last();
    await launcher.click();
    const reportLink = page.getByTestId('visual-property-inspector').locator('[data-property-key="reportKey"]');
    await expect(reportLink.getByTestId('report-launcher-report-select')).toBeVisible();
    await reportLink.getByTestId('report-launcher-report-select').selectOption(report.key);

    await palette.locator('[data-insert-object-type="core.bezier"]').click();
    await surface.click({ position: { x: 100, y: 180 } });
    await surface.click({ position: { x: 230, y: 150 } });
    await surface.click({ position: { x: 200, y: 250 } });
    await surface.click({ position: { x: 130, y: 280 } });
    await page.getByTestId('bezier-finish').click();

    const curve = page.locator('[data-canvas-object-type="core.bezier"]').last();
    await expect(curve).toBeVisible();
    await curve.click();
    const anchors = page.locator('.visual-editor-canvas__bezier-point[data-bezier-point-kind="anchor"]');
    await expect(anchors).toHaveCount(4);
    await anchors.nth(1).click();
    const addAnchor = page.getByTestId('bezier-anchor-add');
    const removeAnchor = page.getByTestId('bezier-anchor-remove');
    await addAnchor.click();
    await expect(anchors).toHaveCount(5);
    await expect(removeAnchor).toBeEnabled();
    await removeAnchor.click();
    await expect(anchors).toHaveCount(4);
    await anchors.nth(1).click();
    await expect(removeAnchor).toBeEnabled();
    await removeAnchor.click();
    await expect(anchors).toHaveCount(3);
    await expect(removeAnchor).toBeDisabled();

    await page.getByTestId('visual-editor-preview').click();
    await expect(page.getByText('Candidato válido', { exact: true })).toBeVisible();
    page.once('dialog', dialog => dialog.accept());
    await page.getByTestId('visual-editor-apply').click();

    await expect.poll(async () => {
      const response = await api.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const exported = await response.json() as EngineeringPackage;
      const persistedScreen = exported.screens?.find(candidate => candidate.key === screen!.key);
      const savedCurve = flatten(persistedScreen?.elements ?? []).find(element => element.type === 'core.bezier');
      const savedLauncher = flatten(persistedScreen?.elements ?? []).find(element => element.type === 'core.reportLauncher');
      const path = savedCurve?.properties?.bezierPath;
      return typeof path === 'string' && savedLauncher?.properties?.reportKey === report.key ? { path, reportKey: savedLauncher.properties.reportKey } : null;
    }, { timeout: 30_000 }).toMatchObject({ reportKey: report.key, path: expect.stringMatching(/^M .*Z$/) });
    const persistedResponse = await api.get('/api/engineering/export/json');
    const persisted = await persistedResponse.json() as EngineeringPackage;
    const persistedElements = flatten(persisted.screens!.find(candidate => candidate.key === screen!.key)?.elements ?? []);
    const savedCurve = persistedElements.find(element => element.type === 'core.bezier');
    const savedLauncher = persistedElements.find(element => element.type === 'core.reportLauncher');
    expect(String(savedCurve?.properties?.bezierPath).match(/\bC\b/g)).toHaveLength(3);
    expect(savedLauncher?.properties?.reportKey).toBe(report.key);
  } finally {
    const restore = await api.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' }, data: original
    });
    expect(restore.ok()).toBeTruthy();
    await api.dispose();
  }
});

function flatten(elements: readonly VisualElement[]): VisualElement[] {
  const result: VisualElement[] = [];
  for (const element of elements) {
    result.push(element);
    result.push(...flatten(element.children ?? []));
  }
  return result;
}
