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
  expect(inspectorSource).toContain("querySelector<HTMLElement>('.visual-editor-canvas__canonical-layer')");
  expect(inspectorSource).toContain('createPortal(');
  expect(inspectorSource).toContain('visual-editor-canvas__authored-background');
});

test('canvas keeps the grid on the established surface and projects authored background below visual objects', () => {
  expect(canvasCss).toContain('.visual-editor-canvas__canonical-layer.has-grid');
  expect(canvasCss).toContain('background-image:');
  expect(inspectorCss).toContain('.visual-editor-canvas__authored-background');
  expect(inspectorCss).toContain('z-index: 0');
  expect(canvasCss).toContain('z-index: 1;');
  expect(canvasCss).toContain('z-index: 2;');
});

test('authored background shares the canonical layer dimensions and viewport transform', async () => {
  const canvasSource = await import('node:fs/promises').then(({ readFile }) => readFile(
    new URL('../src/engineering/visual-editor/canvas/VisualEditorCanvas.tsx', import.meta.url),
    'utf8'
  ));
  expect(canvasSource).toContain('transform: `translate(${effectiveViewport.panX}px, ${effectiveViewport.panY}px) scale(${effectiveViewport.zoom})`');
  expect(canvasSource).toContain('width: logicalBoundary?.width ?? CANVAS_CONTENT_WIDTH');
  expect(canvasSource).toContain('height: logicalBoundary?.height ?? CANVAS_CONTENT_HEIGHT');
  expect(inspectorCss).toContain('inset: 0; width: 100%; height: 100%;');
});
