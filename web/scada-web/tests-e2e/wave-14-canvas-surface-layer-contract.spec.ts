import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const inspectorSource = readFileSync(
  join(process.cwd(), 'src/engineering/visual-editor/canvas/VisualDefinitionSurfaceInspector.tsx'),
  'utf8'
);
const inspectorCss = readFileSync(
  join(process.cwd(), 'src/engineering/visual-editor/canvas/VisualDefinitionSurfaceInspector.css'),
  'utf8'
);

test('authored background is projected into the established Canvas surface rather than a second canvas', () => {
  expect(inspectorSource).toContain("closest('.visual-editor-canvas-enhanced')");
  expect(inspectorSource).toContain("querySelector<HTMLElement>('.visual-editor-canvas__surface')");
  expect(inspectorSource).toContain('createPortal(');
  expect(inspectorSource).toContain('visual-editor-canvas__authored-background');
});

test('canvas layer order keeps authored background below grid and visual objects', () => {
  expect(inspectorCss).toContain('.visual-editor-canvas__authored-background');
  expect(inspectorCss).toContain('z-index: 0');
  expect(inspectorCss).toContain('.visual-editor-canvas__surface.has-grid::after');
  expect(inspectorCss).toContain('z-index: 1');
  expect(inspectorCss).toContain('.visual-editor-canvas__viewport { z-index: 2; }');
});

test('authored background follows logical canvas pan and zoom variables', () => {
  expect(inspectorCss).toContain('left: var(--visual-editor-grid-pan-x)');
  expect(inspectorCss).toContain('top: var(--visual-editor-grid-pan-y)');
  expect(inspectorCss).toContain('width: calc(var(--visual-editor-grid-size) * 600)');
  expect(inspectorCss).toContain('height: calc(var(--visual-editor-grid-size) * 400)');
});
