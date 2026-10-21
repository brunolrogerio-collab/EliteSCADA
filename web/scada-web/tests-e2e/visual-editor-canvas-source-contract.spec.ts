import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

function expectCanvasHasNoApiAuthority(canvas: string): void {
  const apiImports = [...canvas.matchAll(/import\s+\{([^}]+)\}\s+from ['"]([^'"]+)['"];?/g)]
    .filter(match => match[2].endsWith('/api'));
  expect(apiImports).toEqual([]);
  expect(canvas).toContain("import { visualAssetContentUrl } from '../../visualAssetContentUrl';");
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

  expectCanvasHasNoApiAuthority(canvas);
  expect(canvas).not.toContain('fetch(');
  expect(canvas).not.toContain('previewEngineering');
  expect(canvas).not.toContain('applyEngineering');
  expect(canvas).not.toContain('localStorage');
  expect(canvas).not.toContain('sessionStorage');
});

test('Canvas geometry projection uses the Engineering compatibility seam and public Visual Property Registry', async () => {
  const model = await source('../src/engineering/visual-editor/canvas/canvasInteractionModel.ts');

  expect(model).toContain('COMMON_VISUAL_PROPERTY_REGISTRY');
  expect(model).toContain('getVisualSchemaForEngineering');
  expect(model).not.toContain('getBuiltinVisualObjectSchema');
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
  expect(canvas).toContain('resolveVisualDefinitionSurfaceStyle(screen.properties, visualAssetContentUrl)');
  expect(canvas).not.toMatch(/\bscreen\.(?:metadata|properties)\s*(?:=|\.\w+\s*=)/);
  expect(canvas).not.toContain('selectedObjectIds.push');
});

test('screen authoring exposes a fit-to-runtime-size control without replacing the 1:1 reset', async () => {
  const canvas = await source('../src/engineering/visual-editor/canvas/VisualEditorCanvas.tsx');
  const screenWorkspace = await source('../src/engineering/visual-editor/VisualEditorWorkspaceLegacy.tsx');
  const runtimeCanvas = await source('../src/runtime/visual-navigation/runtimeLogicalCanvas.ts');

  expect(canvas).toContain('fitViewportToBounds');
  expect(canvas).toContain('data-testid="canvas-fit-screen"');
  expect(canvas).toContain('fitScreen:');
  expect(canvas).toContain('zoom: 1, panX: 0, panY: 0');
  expect(screenWorkspace).toContain('resolveRuntimeLogicalSize(screenOrientation)');
  expect(screenWorkspace).toContain('width: logicalScreenSize.width');
  expect(screenWorkspace).toContain('height: logicalScreenSize.height');
  expect(runtimeCanvas).toContain('DEFAULT_RUNTIME_DESIGN_WIDTH = 1920');
  expect(runtimeCanvas).toContain('DEFAULT_RUNTIME_DESIGN_HEIGHT = 1080');
});

test('dragging an already selected object preserves the existing multiselect set', async () => {
  const canvas = await source('../src/engineering/visual-editor/canvas/VisualEditorCanvas.tsx');

  expect(canvas).toContain("const preserveExistingSelection = mode === 'replace' && selectionSet.has(projection.objectId);");
  expect(canvas).toContain('const requestedSelection = preserveExistingSelection');
  expect(canvas).toContain('const dragObjectIds = collapseHierarchySelection(screen.elements ?? [], requestedSelection);');
  expect(canvas).toContain('if (!preserveExistingSelection)');
  expect(canvas).not.toContain("emitSelection([projection.objectId], mode);\n    const localSelection");
});


test('Screen and Popup authoring use one canonical-rendered primary canvas instead of stacked previews', async () => {
  const canvas = await source('../src/engineering/visual-editor/canvas/VisualEditorCanvas.tsx');
  const screenWorkspace = await source('../src/engineering/visual-editor/VisualEditorWorkspaceLegacy.tsx');
  const popupWorkspace = await source('../src/engineering/visual-editor/PopupVisualEditorWorkspaceImpl.tsx');

  expect(canvas).toContain("from '../CanonicalVisualRenderer'");
  expect(canvas).toContain('data-renderer="canonical-single-surface"');
  expect(canvas).toContain('data-testid="visual-editor-canonical-layer"');
  expect(canvas).toContain('projectTransientInteraction');
  expect(canvas).toContain('applyVisualEditorMutationIntent');
  expectCanvasHasNoApiAuthority(canvas);

  expect(screenWorkspace).not.toContain('visual-editor-canonical-preview-label');
  expect(screenWorkspace).not.toContain('<CanonicalVisualRenderer');
  expect(popupWorkspace).not.toContain('PopupRuntimeCompositionPreview');
  expect(popupWorkspace).not.toContain('popup-runtime-composition-preview');
  expect(popupWorkspace).not.toContain('popup-runtime-composition-box');
  expect(popupWorkspace).not.toContain('visual-editor-canonical-preview-label');
});
