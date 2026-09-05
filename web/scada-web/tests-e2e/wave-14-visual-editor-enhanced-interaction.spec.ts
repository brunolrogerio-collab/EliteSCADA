import { expect, test } from '@playwright/test';
import type { ScreenEngineering } from '../src/engineering/types';
import {
  rootVisualEditorObjectIds,
  visualEditorKeyboardCommandMutatesSelection,
  visualEditorMarqueeModeForDrag
} from '../src/engineering/visual-editor/canvas/canvasEnhancedInteractionModel';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';

test('marquee direction follows CAD contain/intersect convention', () => {
  expect(visualEditorMarqueeModeForDrag({ x: 10, y: 10 }, { x: 100, y: 80 })).toBe('contain');
  expect(visualEditorMarqueeModeForDrag({ x: 100, y: 10 }, { x: 10, y: 80 })).toBe('intersect');
});

test('root Select All stays outside encapsulated group internals', () => {
  const screen: ScreenEngineering = {
    key: 'screen',
    name: 'Screen',
    elements: [{
      id: 'group',
      key: 'group',
      type: BUILTIN_VISUAL_OBJECT_TYPES.group,
      properties: {},
      children: [{
        id: 'child',
        key: 'child',
        type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
        properties: {}
      }]
    }, {
      id: 'peer',
      key: 'peer',
      type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
      properties: {}
    }]
  };

  expect(rootVisualEditorObjectIds(screen)).toEqual(['group', 'peer']);
});

test('lock interception only blocks commands that mutate the current selection', () => {
  expect(visualEditorKeyboardCommandMutatesSelection({ kind: 'delete' })).toBe(true);
  expect(visualEditorKeyboardCommandMutatesSelection({ kind: 'duplicate' })).toBe(true);
  expect(visualEditorKeyboardCommandMutatesSelection({ kind: 'nudge', deltaX: 1, deltaY: 0 })).toBe(true);
  expect(visualEditorKeyboardCommandMutatesSelection({ kind: 'copy' })).toBe(false);
  expect(visualEditorKeyboardCommandMutatesSelection({ kind: 'undo' })).toBe(false);
  expect(visualEditorKeyboardCommandMutatesSelection({ kind: 'selectAll' })).toBe(false);
});
