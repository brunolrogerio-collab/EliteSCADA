import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

test('Canvas emits shared UI and mutation intents without persistence or API authority', async () => {
  const canvas = await source('../src/engineering/visual-editor/canvas/VisualEditorCanvas.tsx');

  expect(canvas).toContain("from '../visualEditorContracts'");
  expect(canvas).toContain("kind: 'selection.change'");
  expect(canvas).toContain("kind: 'viewport.change'");
  expect(canvas).toContain("kind: 'object.move'");
  expect(canvas).toContain("kind: 'object.resize'");
  expect(canvas).toContain("kind: 'object.rotate'");
  expect(canvas).toContain("kind: 'object.duplicate'");
  expect(canvas).toContain("kind: 'object.delete'");
  expect(canvas).toContain("kind: 'object.zOrder'");

  expect(canvas).not.toMatch(/from ['\"][^'\"]*\/api['\"]/);
  expect(canvas).not.toContain('fetch(');
  expect(canvas).not.toContain('previewEngineering');
  expect(canvas).not.toContain('applyEngineering');
  expect(canvas).not.toContain('localStorage');
  expect(canvas).not.toContain('sessionStorage');
});

test('Canvas geometry projection consumes the public Visual Property Registry instead of duplicating defaults', async () => {
  const model = await source('../src/engineering/visual-editor/canvas/canvasInteractionModel.ts');

  expect(model).toContain('COMMON_VISUAL_PROPERTY_REGISTRY');
  expect(model).toContain('getBuiltinVisualObjectSchema');
  expect(model).toContain('VISUAL_PROPERTY_KEYS');
  expect(model).not.toMatch(/const\s+(?:X|Y|WIDTH|HEIGHT|ROTATION|Z_INDEX)_DEFAULT/i);
  expect(model).not.toContain('element.properties =');
  expect(model).not.toContain('screen.elements =');
});

test('Canvas keeps viewport selection hover and adornment state out of canonical Screen data', async () => {
  const canvas = await source('../src/engineering/visual-editor/canvas/VisualEditorCanvas.tsx');
  const contracts = await source('../src/engineering/visual-editor/visualEditorContracts.ts');

  expect(contracts).toContain("kind: 'selection.change'");
  expect(contracts).toContain("kind: 'viewport.change'");
  expect(canvas).toContain('useState(true)');
  expect(canvas).toContain('setHoveredObjectId');
  expect(canvas).toContain('setInteraction');
  expect(canvas).not.toContain('screen.metadata');
  expect(canvas).not.toContain('screen.properties');
  expect(canvas).not.toContain('selectedObjectIds.push');
});
