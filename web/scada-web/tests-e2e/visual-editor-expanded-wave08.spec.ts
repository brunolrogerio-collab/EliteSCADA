import { expect, test } from '@playwright/test';

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
  [key: string]: unknown;
};

test('Wave 08 creates a closed free polygon and a dynamic text binding through canonical Engineering', async ({ page, request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
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

    const palette = page.getByTestId('visual-object-palette');
    await palette.locator('[data-object-type="core.polygon"]').click();
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

    await palette.locator('[data-object-type="core.text"]').click();
    const textObject = page.locator('[data-canvas-object-type="core.text"]').last();
    await expect(textObject).toBeVisible();
    await textObject.click();

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
      const response = await request.get('/api/engineering/export/json');
      if (!response.ok()) return null;
      const exported = await response.json() as EngineeringPackage;
      const persistedScreen = exported.screens?.find(candidate => candidate.key === screen!.key);
      const elements = flatten(persistedScreen?.elements ?? []);
      const savedPolygon = elements.find(element => element.type === 'core.polygon');
      const savedText = elements.find(element =>
        element.type === 'core.text' && element.bindings?.some(item => item.key === 'text' && item.target === tag!.path));
      return savedPolygon && savedText ? { savedPolygon, savedText } : null;
    }).not.toBeNull().then(async () => {
      const response = await request.get('/api/engineering/export/json');
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

    await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();
    await expect(page.locator('[data-canvas-object-type="core.polygon"]')).not.toHaveCount(0);
    await expect(page.locator('.visual-editor-object-error')).toHaveCount(0);
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: original
    });
    expect(restore.ok()).toBeTruthy();
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
