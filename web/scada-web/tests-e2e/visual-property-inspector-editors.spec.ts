import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('schema-driven Property Inspector mounts type-appropriate editors without private UI property state', async ({ page }) => {
  await page.goto('/engineering');
  await page.locator('.eng-nav').getByRole('button', { name: /Telas/ }).click();
  await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();

  const firstScreen = page.locator('.visual-editor-screen-list').getByRole('button').first();
  await firstScreen.click();

  await page.locator('[data-object-type="core.rectangle"]').click();
  const rectangle = page.locator('[data-canvas-object-type="core.rectangle"]').last();
  await expect(rectangle).toBeVisible();
  await rectangle.click();

  const inspector = page.getByTestId('visual-property-inspector');
  const width = inspector.locator('[data-property-key="width"]');
  await expect(width).toHaveAttribute('data-editor-type', 'number');
  await expect(width.getByRole('spinbutton')).toBeVisible();

  const visible = inspector.locator('[data-property-key="visible"]');
  await expect(visible).toHaveAttribute('data-editor-type', 'boolean');
  await expect(visible.getByRole('checkbox')).toBeVisible();

  const strokeStyle = inspector.locator('[data-property-key="strokeStyle"]');
  await expect(strokeStyle).toHaveAttribute('data-editor-type', 'enum');
  const strokeSelect = strokeStyle.getByRole('combobox');
  await expect(strokeSelect.locator('option')).toHaveText([
    'none', 'solid', 'dashed', 'dotted', 'dash-dot', 'dash-dot-dot'
  ]);
  await strokeSelect.selectOption('none');

  const fillColor = inspector.locator('[data-property-key="fillColor"]');
  await expect(fillColor).toHaveAttribute('data-editor-type', 'color');
  await expect(fillColor.locator('input[type="color"]')).toBeVisible();
  await expect(fillColor.locator('input[type="range"]')).toBeVisible();
  await expect(fillColor.getByRole('button', { name: 'Transparent' })).toBeVisible();
  const manualColor = fillColor.locator('.property-inspector__color-text');
  await manualColor.fill('#11223380');
  await manualColor.press('Enter');
  await expect(fillColor.getByRole('button', { name: 'Use default' })).toBeEnabled();
  await fillColor.getByRole('button', { name: 'Use default' }).click();

  await page.locator('[data-object-type="core.text"]').click();
  const textObject = page.locator('[data-canvas-object-type="core.text"]').last();
  await expect(textObject).toBeVisible();
  await textObject.click();

  const fontFamily = inspector.locator('[data-property-key="fontFamily"]');
  await expect(fontFamily).toHaveAttribute('data-editor-hint', 'font-family');
  const fontInput = fontFamily.getByRole('textbox');
  await expect(fontInput).toHaveAttribute('list', /-fonts$/);

  await page.locator('[data-object-type="core.image"]').click();
  const imageObject = page.locator('[data-canvas-object-type="core.image"]').last();
  await expect(imageObject).toBeVisible();
  await imageObject.click();

  const assetRef = inspector.locator('[data-property-key="assetRef"]');
  await expect(assetRef).toHaveAttribute('data-editor-type', 'assetRef');
  await expect(assetRef).toHaveAttribute('data-editor-hint', 'project-asset');
  const assetBrowser = assetRef.getByTestId('visual-editor-image-asset-picker');
  await expect(assetBrowser.getByRole('combobox')).toBeVisible();
  await expect(assetRef.locator('input[type="text"]')).toHaveCount(0);
});
