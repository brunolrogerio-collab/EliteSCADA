import { expect, test } from '@playwright/test';
import { c07VisualEditorText } from '../src/engineering/visual-editor/c07VisualEditorI18n';

test.use({ locale: 'pt-BR' });

test('C07 visual authoring surfaces follow live pt-BR, en and es locale changes', async ({ page }) => {
  await page.goto('/engineering');
  await page.getByRole('button', { name: /Telas/ }).click();
  await expect(page.getByTestId('visual-editor-workspace')).toBeVisible();

  const toolbar = page.getByTestId('visual-editor-authoring-toolbar');
  const outliner = page.getByTestId('visual-editor-outliner');
  const surface = page.getByTestId('visual-definition-surface-inspector');
  const library = page.getByTestId('visual-dynamo-library');

  await expect(toolbar.getByRole('button', { name: 'Desfazer' })).toBeVisible();
  await expect(outliner.getByText('Estrutura', { exact: true })).toBeVisible();
  await page.getByRole('tab', { name: 'Assets' }).click();
  await expect(surface.getByText('Fundo', { exact: true })).toBeVisible();
  await surface.locator('summary').click();
  await expect(surface.getByLabel('Usar')).toHaveValue('theme');
  await expect(surface.locator('select').first().locator('option')).toHaveCount(3);
  await page.getByRole('tab', { name: 'Biblioteca' }).click();
  await expect(library.getByText('Biblioteca de dínamos', { exact: true })).toBeVisible();

  await page.getByLabel('Idioma').selectOption('en');
  await expect(toolbar.getByRole('button', { name: 'Undo' })).toBeVisible();
  await page.getByRole('tab', { name: 'Structure' }).click();
  await expect(outliner.getByText('Outliner', { exact: true })).toBeVisible();
  await page.getByRole('tab', { name: 'Assets' }).click();
  await expect(surface.getByText('Background', { exact: true })).toBeVisible();
  await page.getByRole('tab', { name: 'Library' }).click();
  await expect(library.getByText('Dynamo library', { exact: true })).toBeVisible();

  await page.getByLabel('Language').selectOption('es');
  await expect(toolbar.getByRole('button', { name: 'Deshacer' })).toBeVisible();
  await page.getByRole('tab', { name: 'Estructura' }).click();
  await expect(outliner.getByText('Estructura', { exact: true })).toBeVisible();
  await page.getByRole('tab', { name: 'Assets' }).click();
  await expect(surface.getByText('Fondo', { exact: true })).toBeVisible();
  await page.getByRole('tab', { name: 'Biblioteca' }).click();
  await expect(library.getByText('Biblioteca de dínamos', { exact: true })).toBeVisible();
});

test('C07 Runtime Dynamo semantic and error labels are localized without changing state keys', () => {
  expect(c07VisualEditorText('pt-BR').surface.mode.theme).toBe('Tema atual (claro/escuro)');
  expect(c07VisualEditorText('en').surface.mode.theme).toBe('Current theme (light/dark)');
  expect(c07VisualEditorText('es').surface.mode.theme).toBe('Tema actual (claro/oscuro)');
  expect(c07VisualEditorText('pt-BR').runtimeState).toMatchObject({
    fault: 'FALHA', active: 'ATIVO', transition: 'TRANSIÇÃO', dynamoError: 'ERRO DE DÍNAMO'
  });
  expect(c07VisualEditorText('en').runtimeState).toMatchObject({
    fault: 'FAULT', active: 'ACTIVE', transition: 'TRANSITION', dynamoError: 'DYNAMO ERROR'
  });
  expect(c07VisualEditorText('es').runtimeState).toMatchObject({
    fault: 'FALLA', active: 'ACTIVO', transition: 'TRANSICIÓN', dynamoError: 'ERROR DE DÍNAMO'
  });
});
