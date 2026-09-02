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
  await expect(width.getByText('Width', { exact: true })).toBeVisible();
  await expect(width.locator('code')).toHaveText('width');
  await expect(width.getByRole('spinbutton', { name: 'width' })).toBeVisible();

  const visible = inspector.locator('[data-property-key="visible"]');
  await expect(visible).toHaveAttribute('data-editor-type', 'boolean');
  await expect(visible.getByRole('checkbox', { name: 'visible' })).toBeVisible();

  const horizontalFlip = inspector.locator('[data-property-key="horizontalFlip"]');
  await expect(horizontalFlip).toHaveAttribute('data-editor-type', 'boolean');
  await expect(horizontalFlip.getByText('Horizontal Flip', { exact: true })).toBeVisible();
  await horizontalFlip.getByRole('checkbox', { name: 'horizontalFlip' }).check();

  const verticalFlip = inspector.locator('[data-property-key="verticalFlip"]');
  await expect(verticalFlip).toHaveAttribute('data-editor-type', 'boolean');
  await verticalFlip.getByRole('checkbox', { name: 'verticalFlip' }).check();

  const tooltip = inspector.locator('[data-property-key="tooltip"]');
  await expect(tooltip).toHaveAttribute('data-editor-type', 'string');
  await tooltip.getByRole('textbox', { name: 'tooltip' }).fill('Pump station visual');
  await tooltip.getByRole('textbox', { name: 'tooltip' }).press('Enter');

  const strokeStyle = inspector.locator('[data-property-key="strokeStyle"]');
  await expect(strokeStyle).toHaveAttribute('data-editor-type', 'enum');
  await expect(strokeStyle).toHaveAttribute('data-editor-hint', 'stroke-style');
  const strokeSelector = strokeStyle.locator('[data-property-editor="stroke-style"]');
  await expect(strokeSelector.getByRole('radio')).toHaveCount(6);
  await expect(strokeSelector.getByRole('radio', { name: 'solid' })).toHaveAttribute('aria-checked', 'true');
  await strokeSelector.getByRole('radio', { name: 'none' }).click();
  await expect(strokeSelector.getByRole('radio', { name: 'none' })).toHaveAttribute('aria-checked', 'true');

  const fillColor = inspector.locator('[data-property-key="fillColor"]');
  await expect(fillColor).toHaveAttribute('data-editor-type', 'color');
  await expect(fillColor.locator('input[type="color"]')).toBeVisible();
  await expect(fillColor.locator('input[type="range"]')).toBeVisible();
  await expect(fillColor.getByRole('button', { name: 'Transparent' })).toBeVisible();
  const manualColor = fillColor.locator('.property-inspector__color-text');
  await manualColor.fill('rgba(17, 34, 51, 0.5)');
  await manualColor.press('Enter');
  await expect(manualColor).toHaveValue('#11223380');
  await expect(fillColor.getByRole('button', { name: 'Use default' })).toBeEnabled();
  await fillColor.getByRole('button', { name: 'Use default' }).click();

  await page.locator('[data-object-type="core.text"]').click();
  const textObject = page.locator('[data-canvas-object-type="core.text"]').last();
  await expect(textObject).toBeVisible();
  await textObject.click();

  const fontFamily = inspector.locator('[data-property-key="fontFamily"]');
  await expect(fontFamily).toHaveAttribute('data-editor-hint', 'font-family');
  const fontInput = fontFamily.getByRole('textbox', { name: 'fontFamily' });
  await expect(fontInput).toHaveAttribute('list', /-fonts$/);

  const underline = inspector.locator('[data-property-key="underline"]');
  await expect(underline).toHaveAttribute('data-editor-type', 'boolean');
  await expect(underline.getByRole('checkbox', { name: 'underline' })).toBeVisible();

  const textWrap = inspector.locator('[data-property-key="textWrap"]');
  await expect(textWrap).toHaveAttribute('data-editor-type', 'boolean');
  await expect(textWrap.getByRole('checkbox', { name: 'textWrap' })).toBeChecked();

  const lineHeight = inspector.locator('[data-property-key="lineHeight"]');
  await expect(lineHeight).toHaveAttribute('data-editor-type', 'number');
  await expect(lineHeight.getByRole('spinbutton', { name: 'lineHeight' })).toHaveValue('1.2');

  const textOverflow = inspector.locator('[data-property-key="textOverflow"]');
  await expect(textOverflow).toHaveAttribute('data-editor-type', 'enum');
  await expect(textOverflow.getByRole('combobox', { name: 'textOverflow' }).locator('option')).toHaveText(['clip', 'ellipsis']);

  await page.locator('[data-object-type="core.image"]').click();
  const imageObject = page.locator('[data-canvas-object-type="core.image"]').last();
  await expect(imageObject).toBeVisible();
  await imageObject.click();

  const assetRef = inspector.locator('[data-property-key="assetRef"]');
  await expect(assetRef).toHaveAttribute('data-editor-type', 'assetRef');
  await expect(assetRef).toHaveAttribute('data-editor-hint', 'project-asset');
  const assetBrowser = assetRef.getByTestId('visual-editor-image-asset-picker');
  await expect(assetBrowser.getByRole('combobox', { name: 'assetRef' })).toBeVisible();
  await expect(assetRef.locator('input[type="text"]')).toHaveCount(0);
});
