import { expect, test } from '@playwright/test';
import type { ScreenEngineering } from '../src/engineering/types';
import {
  applyVisualEditorSessionKeyboardCommand,
  createVisualEditorSession,
  currentVisualEditorSessionScreen
} from '../src/engineering/visual-editor/visualEditorSessionModel';
import {
  VISUAL_DEFINITION_SURFACE_KEYS,
  readVisualDefinitionSurfaceConfig
} from '../src/engineering/visual-editor/visualDefinitionSurfaceModel';

function screen(): ScreenEngineering {
  return {
    key: 'screen.main',
    name: 'Main',
    route: '/main',
    properties: { retained: 'yes' },
    context: {},
    metadata: {},
    elements: []
  };
}

test('surface authoring participates in Screen session undo and redo without replacing unrelated properties', () => {
  let session = createVisualEditorSession(screen());

  session = applyVisualEditorSessionKeyboardCommand(session, {
    kind: 'surface.set',
    patch: {
      backgroundColor: '#203040',
      backgroundImageAssetId: 'asset-process-map',
      backgroundImageFit: 'contain'
    }
  });

  let current = currentVisualEditorSessionScreen(session);
  expect(current.properties.retained).toBe('yes');
  expect(readVisualDefinitionSurfaceConfig(current.properties)).toEqual({
    backgroundColor: '#203040',
    backgroundImageAssetId: 'asset-process-map',
    backgroundImageFit: 'contain'
  });

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'undo' });
  current = currentVisualEditorSessionScreen(session);
  expect(current.properties.retained).toBe('yes');
  expect(current.properties[VISUAL_DEFINITION_SURFACE_KEYS.backgroundColor]).toBeUndefined();
  expect(current.properties[VISUAL_DEFINITION_SURFACE_KEYS.backgroundImageAssetId]).toBeUndefined();

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'redo' });
  current = currentVisualEditorSessionScreen(session);
  expect(readVisualDefinitionSurfaceConfig(current.properties)).toEqual({
    backgroundColor: '#203040',
    backgroundImageAssetId: 'asset-process-map',
    backgroundImageFit: 'contain'
  });
});

test('surface reset removes only C07 canonical surface keys', () => {
  let session = createVisualEditorSession({
    ...screen(),
    properties: {
      retained: 'yes',
      [VISUAL_DEFINITION_SURFACE_KEYS.backgroundColor]: '#112233',
      [VISUAL_DEFINITION_SURFACE_KEYS.backgroundImageAssetId]: 'asset-old',
      [VISUAL_DEFINITION_SURFACE_KEYS.backgroundImageFit]: 'cover'
    }
  });

  session = applyVisualEditorSessionKeyboardCommand(session, {
    kind: 'surface.set',
    patch: {
      backgroundColor: null,
      backgroundImageAssetId: null,
      backgroundImageFit: null
    }
  });

  const current = currentVisualEditorSessionScreen(session);
  expect(current.properties).toEqual({ retained: 'yes' });
});
