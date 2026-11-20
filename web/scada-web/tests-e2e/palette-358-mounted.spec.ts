import { expect, test } from '@playwright/test';
import { c07VisualEditorText } from '../src/engineering/visual-editor/c07VisualEditorI18n';

const labels = {
  'pt-BR': ['Retângulo', 'Elipse', 'Texto', 'Desenho', 'Exibição e controles', 'Biblioteca'],
  en: ['Rectangle', 'Ellipse', 'Text', 'Draw', 'Display and controls', 'Library'],
  es: ['Rectángulo', 'Elipse', 'Texto', 'Dibujo', 'Visualización y controles', 'Biblioteca']
} as const;

for (const locale of ['pt-BR', 'en', 'es'] as const) {
  for (const [width, height] of [[1366, 768], [1440, 900], [1920, 1080]]) {
    for (const editor of ['screens', 'popups']) {
      test(`${editor} palette · ${locale} · ${width}x${height}`, async ({ page }) => {
        await page.setViewportSize({ width, height });
        await page.addInitScript(language => localStorage.setItem('elitescada.engineering.locale', language), locale);
        await page.goto(`/engineering/${editor}`);
        const workspace = page.getByTestId(editor === 'screens' ? 'visual-editor-workspace' : 'popup-visual-editor-workspace');
        const toolbar = workspace.getByTestId('visual-editor-authoring-toolbar');
        const [rectangle, ellipse, text, draw, controls, library] = labels[locale];
        await expect(toolbar.getByRole('group', { name: draw, exact: true })).toBeVisible();
        await expect(toolbar.getByRole('group', { name: controls, exact: true })).toBeVisible();

        const buttons = toolbar.locator('button[data-insert-object-type]');
        const catalog = c07VisualEditorText(locale).palette;
        const rendered = await buttons.evaluateAll(elements => elements.map(element => [
          element.getAttribute('title'), element.getAttribute('aria-label')
        ]));
        expect(rendered.length).toBe(28);
        for (const [title, accessibleName] of rendered) {
          expect(title).toBe(accessibleName);
          expect(Object.values(catalog)).toContain(title);
        }

        const objects = workspace.locator('[data-canvas-object-id]');
        const initial = await objects.count();
        const shape = toolbar.getByRole('button', { name: rectangle, exact: true });
        await shape.focus();
        await page.keyboard.press('Tab');
        await expect(toolbar.getByRole('button', { name: ellipse, exact: true })).toBeFocused();
        await page.keyboard.press('Shift+Tab');
        await expect(shape).toBeFocused();
        await page.keyboard.press('Escape');
        await expect(objects).toHaveCount(initial);
        for (const [index, name] of [rectangle, ellipse, text].entries()) {
          await toolbar.getByRole('button', { name, exact: true }).focus();
          await page.keyboard.press(index === 1 ? 'Space' : 'Enter');
          await expect(objects).toHaveCount(initial + index + 1);
        }
        const tab = workspace.getByTestId('visual-editor-side-tab-library');
        await expect(tab).toHaveAccessibleName(library);
        await tab.focus();
        await page.keyboard.press('Enter');
        await expect(tab).toHaveAttribute('aria-selected', 'true');
        await expect(objects).toHaveCount(initial + 3);
      });
    }
  }
}
