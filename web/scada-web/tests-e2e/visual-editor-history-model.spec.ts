import { expect, test } from '@playwright/test';
import {
  canRedoVisualEditorHistory,
  canUndoVisualEditorHistory,
  commitVisualEditorHistory,
  createVisualEditorHistory,
  endVisualEditorHistoryGesture,
  redoVisualEditorHistory,
  undoVisualEditorHistory
} from '../src/engineering/visual-editor/visualEditorHistoryModel';

test('undo and redo traverse canonical draft snapshots', () => {
  let history = createVisualEditorHistory({ value: 0 });
  history = commitVisualEditorHistory(history, { value: 1 });
  history = commitVisualEditorHistory(history, { value: 2 });

  expect(canUndoVisualEditorHistory(history)).toBe(true);
  history = undoVisualEditorHistory(history);
  expect(history.present).toEqual({ value: 1 });
  history = undoVisualEditorHistory(history);
  expect(history.present).toEqual({ value: 0 });
  expect(canUndoVisualEditorHistory(history)).toBe(false);

  history = redoVisualEditorHistory(history);
  expect(history.present).toEqual({ value: 1 });
  history = redoVisualEditorHistory(history);
  expect(history.present).toEqual({ value: 2 });
  expect(canRedoVisualEditorHistory(history)).toBe(false);
});

test('a pointer gesture coalesces repeated draft updates into one undo step', () => {
  let history = createVisualEditorHistory({ x: 0 });
  history = commitVisualEditorHistory(history, { x: 10 }, { coalesceKey: 'move:object-1' });
  history = commitVisualEditorHistory(history, { x: 20 }, { coalesceKey: 'move:object-1' });
  history = commitVisualEditorHistory(history, { x: 30 }, { coalesceKey: 'move:object-1' });
  history = endVisualEditorHistoryGesture(history);

  expect(history.past).toHaveLength(1);
  expect(undoVisualEditorHistory(history).present).toEqual({ x: 0 });
});

test('ending a gesture makes the next operation a separate undo step', () => {
  let history = createVisualEditorHistory({ x: 0, width: 10 });
  history = commitVisualEditorHistory(history, { x: 25, width: 10 }, { coalesceKey: 'move:a' });
  history = endVisualEditorHistoryGesture(history);
  history = commitVisualEditorHistory(history, { x: 25, width: 50 }, { coalesceKey: 'resize:a' });
  history = endVisualEditorHistoryGesture(history);

  history = undoVisualEditorHistory(history);
  expect(history.present).toEqual({ x: 25, width: 10 });
  history = undoVisualEditorHistory(history);
  expect(history.present).toEqual({ x: 0, width: 10 });
});

test('committing after undo clears redo history', () => {
  let history = createVisualEditorHistory({ value: 0 });
  history = commitVisualEditorHistory(history, { value: 1 });
  history = commitVisualEditorHistory(history, { value: 2 });
  history = undoVisualEditorHistory(history);
  expect(canRedoVisualEditorHistory(history)).toBe(true);

  history = commitVisualEditorHistory(history, { value: 9 });
  expect(history.present).toEqual({ value: 9 });
  expect(canRedoVisualEditorHistory(history)).toBe(false);
});

test('history limit trims only old draft snapshots', () => {
  let history = createVisualEditorHistory({ value: 0 }, 2);
  history = commitVisualEditorHistory(history, { value: 1 });
  history = commitVisualEditorHistory(history, { value: 2 });
  history = commitVisualEditorHistory(history, { value: 3 });

  expect(history.past).toEqual([{ value: 1 }, { value: 2 }]);
  history = undoVisualEditorHistory(history);
  expect(history.present).toEqual({ value: 2 });
  history = undoVisualEditorHistory(history);
  expect(history.present).toEqual({ value: 1 });
});

test('history snapshots are isolated from later object mutation', () => {
  const initial = { nested: { value: 1 } };
  let history = createVisualEditorHistory(initial);
  initial.nested.value = 99;
  expect(history.present).toEqual({ nested: { value: 1 } });

  const next = { nested: { value: 2 } };
  history = commitVisualEditorHistory(history, next);
  next.nested.value = 77;
  expect(history.present).toEqual({ nested: { value: 2 } });
});
