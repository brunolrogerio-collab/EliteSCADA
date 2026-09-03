import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import {
  applyVisualEditorSessionKeyboardCommand,
  applyVisualEditorSessionZOrder,
  canRedoVisualEditorSession,
  canUndoVisualEditorSession,
  createVisualEditorSession,
  currentVisualEditorSessionScreen,
  withVisualEditorSessionSelection
} from '../src/engineering/visual-editor/visualEditorSessionModel';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';

function rectangle(id: string, x: number, zIndex = 0): VisualElementEngineering {
  return {
    id,
    key: id,
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    properties: { x, y: 10, width: 20, height: 20, zIndex }
  };
}

function screen(elements: readonly VisualElementEngineering[]): ScreenEngineering {
  return {
    id: 'screen-id',
    key: 'screen',
    name: 'Screen',
    route: '/screen',
    elements: [...elements],
    properties: {},
    context: {},
    metadata: {}
  };
}

test('session groups selected siblings and undo restores the canonical draft', () => {
  let session = createVisualEditorSession(screen([
    rectangle('one', 10),
    rectangle('two', 60)
  ]));
  session = withVisualEditorSessionSelection(session, ['one', 'two']);
  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'group' });

  const grouped = currentVisualEditorSessionScreen(session);
  expect(grouped.elements).toHaveLength(1);
  expect(grouped.elements?.[0].type).toBe(BUILTIN_VISUAL_OBJECT_TYPES.group);
  expect(grouped.elements?.[0].children?.map(child => child.id)).toEqual(['one', 'two']);
  expect(session.selectedObjectIds).toEqual([grouped.elements?.[0].id]);
  expect(canUndoVisualEditorSession(session)).toBe(true);

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'undo' });
  expect(currentVisualEditorSessionScreen(session).elements?.map(element => element.id)).toEqual(['one', 'two']);
  expect(canRedoVisualEditorSession(session)).toBe(true);
});

test('session copy and paste keep clipboard transient while committing only the Screen draft', () => {
  let session = createVisualEditorSession(screen([rectangle('one', 10)]));
  session = withVisualEditorSessionSelection(session, ['one']);
  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'copy' });

  expect(canUndoVisualEditorSession(session)).toBe(false);
  expect(session.clipboard?.elements.map(element => element.id)).toEqual(['one']);

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'paste' });
  const pasted = currentVisualEditorSessionScreen(session);
  expect(pasted.elements).toHaveLength(2);
  expect(session.selectedObjectIds).toHaveLength(1);
  expect(session.selectedObjectIds[0]).not.toBe('one');
  expect(canUndoVisualEditorSession(session)).toBe(true);

  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'undo' });
  expect(currentVisualEditorSessionScreen(session).elements).toHaveLength(1);
  expect(session.clipboard?.elements.map(element => element.id)).toEqual(['one']);
});

test('session keyboard mutation respects canonical authoring locks', () => {
  const locked = {
    ...rectangle('locked', 10),
    metadata: { 'engineering.authoring.locked': 'true' }
  };
  let session = createVisualEditorSession(screen([locked]));
  session = withVisualEditorSessionSelection(session, ['locked']);

  expect(() => applyVisualEditorSessionKeyboardCommand(session, {
    kind: 'nudge', deltaX: 1, deltaY: 0
  })).toThrow(/locked for Engineering authoring/);
  expect(() => applyVisualEditorSessionKeyboardCommand(session, { kind: 'delete' }))
    .toThrow(/locked for Engineering authoring/);
});

test('session z-order uses deterministic collision-free stacking and preserves selection', () => {
  let session = createVisualEditorSession(screen([
    rectangle('back', 10, 5),
    rectangle('middle', 40, 5),
    rectangle('front', 70, 5)
  ]));
  session = withVisualEditorSessionSelection(session, ['back']);
  session = applyVisualEditorSessionZOrder(session, 'front');

  const elements = currentVisualEditorSessionScreen(session).elements ?? [];
  const z = Object.fromEntries(elements.map(element => [element.id!, element.properties?.zIndex]));
  expect(new Set(Object.values(z)).size).toBe(3);
  expect(Number(z.back)).toBeGreaterThan(Number(z.front));
  expect(session.selectedObjectIds).toEqual(['back']);
});

test('select all stays at the current root authoring level', () => {
  const group: VisualElementEngineering = {
    id: 'group',
    key: 'group',
    type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 0, y: 0, width: 100, height: 100 },
    children: [rectangle('child', 10)]
  };
  let session = createVisualEditorSession(screen([group, rectangle('peer', 120)]));
  session = applyVisualEditorSessionKeyboardCommand(session, { kind: 'selectAll' });

  expect(session.selectedObjectIds).toEqual(['group', 'peer']);
});
