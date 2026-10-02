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
const canvasCss = readFileSync(
  join(process.cwd(), 'src/engineering/visual-editor/canvas/visual-editor-canvas.css'),
  'utf8'
);

test('authored background is projected into the established Canvas surface rather than a second canvas', () => {
  expect(inspectorSource).toContain("closest('.visual-editor-canvas-enhanced')");
  expect(inspectorSource).toContain("querySelector<HTMLElement>('.visual-editor-canvas__surface')");
  expect(inspectorSource).toContain('createPortal(');
  expect(inspectorSource).toContain('visual-editor-canvas__authored-background');
});

test('canvas keeps the grid on the established surface and projects authored background below visual objects', () => {
  expect(canvasCss).toContain('.visual-editor-canvas__surface.has-grid');
  expect(canvasCss).toContain('background-image:');
  expect(inspectorCss).toContain('.visual-editor-canvas__authored-background');
  expect(inspectorCss).toContain('z-index: 0');
  expect(inspectorCss).toContain('.visual-editor-canvas__viewport { z-index: 1; }');
});

test('authored background follows logical canvas pan and zoom variables', () => {
  expect(inspectorCss).toContain('left: var(--visual-editor-grid-pan-x)');
  expect(inspectorCss).toContain('top: var(--visual-editor-grid-pan-y)');
  expect(inspectorCss).toContain('width: calc(var(--visual-editor-grid-size) * var(--visual-editor-background-grid-width,600))');
  expect(inspectorCss).toContain('height: calc(var(--visual-editor-grid-size) * var(--visual-editor-background-grid-height,400))');
});
