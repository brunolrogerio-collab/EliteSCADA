import { expect, test } from '@playwright/test';
import { resolveVisualEditorKeyboardCommand } from '../src/engineering/visual-editor/visualEditorKeyboardModel';

test('maps platform-primary editing shortcuts without stealing editable fields', () => {
  expect(resolveVisualEditorKeyboardCommand({ key: 'c', ctrlKey: true })).toEqual({ kind: 'copy' });
  expect(resolveVisualEditorKeyboardCommand({ key: 'v', metaKey: true })).toEqual({ kind: 'paste' });
  expect(resolveVisualEditorKeyboardCommand({ key: 'd', ctrlKey: true })).toEqual({ kind: 'duplicate' });
  expect(resolveVisualEditorKeyboardCommand({ key: 'a', ctrlKey: true })).toEqual({ kind: 'selectAll' });
  expect(resolveVisualEditorKeyboardCommand({ key: 'c', ctrlKey: true, targetIsEditable: true })).toBeNull();
});

test('maps undo redo and grouping deterministically', () => {
  expect(resolveVisualEditorKeyboardCommand({ key: 'z', ctrlKey: true })).toEqual({ kind: 'undo' });
  expect(resolveVisualEditorKeyboardCommand({ key: 'z', metaKey: true, shiftKey: true })).toEqual({ kind: 'redo' });
  expect(resolveVisualEditorKeyboardCommand({ key: 'y', ctrlKey: true })).toEqual({ kind: 'redo' });
  expect(resolveVisualEditorKeyboardCommand({ key: 'g', ctrlKey: true })).toEqual({ kind: 'group' });
  expect(resolveVisualEditorKeyboardCommand({ key: 'g', ctrlKey: true, shiftKey: true })).toEqual({ kind: 'ungroup' });
});

test('Delete and Backspace resolve to the same authoring command', () => {
  expect(resolveVisualEditorKeyboardCommand({ key: 'Delete' })).toEqual({ kind: 'delete' });
  expect(resolveVisualEditorKeyboardCommand({ key: 'Backspace' })).toEqual({ kind: 'delete' });
});

test('arrow nudge uses logical fine and coarse deltas', () => {
  expect(resolveVisualEditorKeyboardCommand({ key: 'ArrowLeft' })).toEqual({ kind: 'nudge', deltaX: -1, deltaY: 0 });
  expect(resolveVisualEditorKeyboardCommand({ key: 'ArrowDown', shiftKey: true })).toEqual({ kind: 'nudge', deltaX: 0, deltaY: 10 });
  expect(resolveVisualEditorKeyboardCommand({ key: 'ArrowRight', fineNudge: 0.5 })).toEqual({ kind: 'nudge', deltaX: 0.5, deltaY: 0 });
  expect(resolveVisualEditorKeyboardCommand({ key: 'ArrowUp', shiftKey: true, coarseNudge: 25 })).toEqual({ kind: 'nudge', deltaX: 0, deltaY: -25 });
});

test('Alt-modified and unrelated commands are ignored', () => {
  expect(resolveVisualEditorKeyboardCommand({ key: 'ArrowLeft', altKey: true })).toBeNull();
  expect(resolveVisualEditorKeyboardCommand({ key: 'q', ctrlKey: true })).toBeNull();
  expect(resolveVisualEditorKeyboardCommand({ key: 'F5' })).toBeNull();
});
