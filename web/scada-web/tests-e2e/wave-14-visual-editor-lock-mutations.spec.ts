import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import {
  deleteVisualEditorElements,
  nudgeVisualEditorElements,
  pasteVisualEditorElements,
  type VisualEditorClipboardPayload
} from '../src/engineering/visual-editor/visualEditorClipboardModel';
import { VISUAL_EDITOR_AUTHORING_LOCK_METADATA_KEY } from '../src/engineering/visual-editor/visualEditorAuthoringModel';

function lockedElement(
  id: string,
  key: string,
  children?: VisualElementEngineering[]
): VisualElementEngineering {
  return {
    id,
    key,
    type: children ? 'core.group' : 'core.rectangle',
    metadata: { [VISUAL_EDITOR_AUTHORING_LOCK_METADATA_KEY]: 'true' },
    properties: { x: 10, y: 20, width: 20, height: 20 },
    children
  };
}

function screen(elements: VisualElementEngineering[]): ScreenEngineering {
  return { id: 'screen-1', key: 'main', name: 'Main', elements };
}

test('Delete and arrow nudge fail closed for a directly locked object', () => {
  const source = screen([lockedElement('locked', 'Locked')]);

  expect(() => deleteVisualEditorElements(source, ['locked'])).toThrow(/locked for Engineering authoring/);
  expect(() => nudgeVisualEditorElements(source, ['locked'], 1, 0)).toThrow(/locked for Engineering authoring/);
  expect(source.elements?.[0]?.properties).toMatchObject({ x: 10, y: 20 });
});

test('inherited group lock blocks keyboard mutations of children', () => {
  const source = screen([
    lockedElement('group', 'Group', [
      {
        id: 'child',
        key: 'Child',
        type: 'core.rectangle',
        properties: { x: 5, y: 6, width: 10, height: 10 }
      }
    ])
  ]);

  expect(() => nudgeVisualEditorElements(source, ['child'], 10, 0)).toThrow(/locked for Engineering authoring/);
  expect(() => deleteVisualEditorElements(source, ['child'])).toThrow(/locked for Engineering authoring/);
});

test('paste cannot mutate the child collection of a locked group', () => {
  const source = screen([lockedElement('group', 'Group', [])]);
  const payload: VisualEditorClipboardPayload = Object.freeze({
    sourceParentId: null,
    elements: Object.freeze([
      {
        id: 'source',
        key: 'Source',
        type: 'core.rectangle',
        properties: { x: 0, y: 0, width: 10, height: 10 }
      }
    ])
  });

  expect(() => pasteVisualEditorElements(source, payload, 'group', {
    createObjectId: () => 'copy'
  })).toThrow(/locked for Engineering authoring/);
});
