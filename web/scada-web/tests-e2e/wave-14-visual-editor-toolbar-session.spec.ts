import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import {
  applyVisualEditorSessionKeyboardCommand,
  createVisualEditorSession,
  currentVisualEditorSessionScreen,
  withVisualEditorSessionSelection
} from '../src/engineering/visual-editor/visualEditorSessionModel';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';

function rectangle(id: string, x: number, y: number): VisualElementEngineering {
  return {
    id,
    key: id,
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    properties: { x, y, width: 20, height: 20 }
  };
}

function screen(elements: readonly VisualElementEngineering[]): ScreenEngineering {
  return { key: 'screen', name: 'Screen', elements: [...elements], properties: {}, context: {}, metadata: {} };
}

function selectedSession(draft: ScreenEngineering, ids: readonly string[]) {
  return withVisualEditorSessionSelection(createVisualEditorSession(draft), ids);
}

function element(draft: ScreenEngineering, id: string): VisualElementEngineering {
  const found = draft.elements?.find(item => item.id === id);
  if (!found) throw new Error(`Missing element ${id}`);
  return found;
}

test('toolbar align command mutates through session history and undo restores draft', () => {
  let session = selectedSession(screen([
    rectangle('a', 10, 10),
    rectangle('b', 30, 25)
  ]), ['a', 'b']);

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'align', operation: 'left' });
  expect(element(currentVisualEditorSessionScreen(session), 'b').properties?.x).toBe(10);

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'undo' });
  expect(element(currentVisualEditorSessionScreen(session), 'b').properties?.x).toBe(30);
});

test('toolbar size command uses first selected object as deterministic reference', () => {
  const draft = screen([
    { ...rectangle('reference', 0, 0), properties: { x: 0, y: 0, width: 40, height: 50 } },
    { ...rectangle('target', 60, 0), properties: { x: 60, y: 0, width: 10, height: 15 } }
  ]);
  let session = selectedSession(draft, ['reference', 'target']);
  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'size', operation: 'sameSize' });
  expect(element(currentVisualEditorSessionScreen(session), 'target').properties).toMatchObject({ width: 40, height: 50 });
});

test('toolbar lock command persists authoring lock and blocks subsequent align', () => {
  let session = selectedSession(screen([
    rectangle('a', 10, 10),
    rectangle('b', 30, 20)
  ]), ['a', 'b']);

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'lock', locked: true });
  expect(element(currentVisualEditorSessionScreen(session), 'a').metadata?.['engineering.authoring.locked']).toBe('true');
  expect(() => applyVisualEditorSessionKeyboardCommand(session, { kind: 'align', operation: 'top' }))
    .toThrow(/locked for Engineering authoring/);
});

test('toolbar group command selects created group and undo restores original selection objects', () => {
  let session = selectedSession(screen([rectangle('a', 0, 0), rectangle('b', 30, 0)]), ['a', 'b']);
  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'group' });
  expect(session.selectedObjectIds).toHaveLength(1);
  expect(currentVisualEditorSessionScreen(session).elements?.[0].type).toBe(BUILTIN_VISUAL_OBJECT_TYPES.group);

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'undo' });
  expect(currentVisualEditorSessionScreen(session).elements?.map(item => item.id)).toEqual(['a', 'b']);
});
