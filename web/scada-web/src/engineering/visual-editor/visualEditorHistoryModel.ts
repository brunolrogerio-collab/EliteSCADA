export type VisualEditorHistoryState<T> = Readonly<{
  past: readonly T[];
  present: T;
  future: readonly T[];
  activeCoalesceKey: string | null;
  limit: number;
}>;

export type VisualEditorHistoryCommitOptions = Readonly<{
  /**
   * Repeated commits with the same key collapse into one undo step until the
   * gesture is ended. Intended for pointer move/resize/rotate streams.
   */
  coalesceKey?: string | null;
}>;

export function createVisualEditorHistory<T>(
  initialDraft: T,
  limit = 100
): VisualEditorHistoryState<T> {
  if (!Number.isInteger(limit) || limit < 1) {
    throw new Error('Visual editor history limit must be a positive integer.');
  }
  return Object.freeze({
    past: Object.freeze([]),
    present: cloneDraft(initialDraft),
    future: Object.freeze([]),
    activeCoalesceKey: null,
    limit
  });
}

export function commitVisualEditorHistory<T>(
  state: VisualEditorHistoryState<T>,
  nextDraft: T,
  options: VisualEditorHistoryCommitOptions = {}
): VisualEditorHistoryState<T> {
  const requestedKey = normalizeCoalesceKey(options.coalesceKey);
  const isContinuation = requestedKey !== null && requestedKey === state.activeCoalesceKey;
  const past = isContinuation
    ? [...state.past]
    : trimPast([...state.past, cloneDraft(state.present)], state.limit);

  return Object.freeze({
    past: Object.freeze(past),
    present: cloneDraft(nextDraft),
    future: Object.freeze([]),
    activeCoalesceKey: requestedKey,
    limit: state.limit
  });
}

/** Ends a pointer/keyboard gesture without creating another history entry. */
export function endVisualEditorHistoryGesture<T>(
  state: VisualEditorHistoryState<T>
): VisualEditorHistoryState<T> {
  if (state.activeCoalesceKey === null) return state;
  return Object.freeze({ ...state, activeCoalesceKey: null });
}

export function undoVisualEditorHistory<T>(
  state: VisualEditorHistoryState<T>
): VisualEditorHistoryState<T> {
  if (state.past.length === 0) return endVisualEditorHistoryGesture(state);
  const previous = state.past[state.past.length - 1];
  return Object.freeze({
    past: Object.freeze(state.past.slice(0, -1).map(cloneDraft)),
    present: cloneDraft(previous),
    future: Object.freeze([cloneDraft(state.present), ...state.future.map(cloneDraft)]),
    activeCoalesceKey: null,
    limit: state.limit
  });
}

export function redoVisualEditorHistory<T>(
  state: VisualEditorHistoryState<T>
): VisualEditorHistoryState<T> {
  if (state.future.length === 0) return endVisualEditorHistoryGesture(state);
  const [next, ...remaining] = state.future;
  return Object.freeze({
    past: Object.freeze(trimPast([...state.past.map(cloneDraft), cloneDraft(state.present)], state.limit)),
    present: cloneDraft(next),
    future: Object.freeze(remaining.map(cloneDraft)),
    activeCoalesceKey: null,
    limit: state.limit
  });
}

export function canUndoVisualEditorHistory<T>(state: VisualEditorHistoryState<T>): boolean {
  return state.past.length > 0;
}

export function canRedoVisualEditorHistory<T>(state: VisualEditorHistoryState<T>): boolean {
  return state.future.length > 0;
}

/**
 * History owns Engineering draft snapshots only. CAS/package version tokens stay
 * in the persistence layer and therefore cannot be rewound by Undo/Redo.
 */
function cloneDraft<T>(value: T): T {
  if (value === null || value === undefined || typeof value !== 'object') return value;
  return structuredClone(value);
}

function trimPast<T>(values: T[], limit: number): T[] {
  if (values.length <= limit) return values;
  return values.slice(values.length - limit);
}

function normalizeCoalesceKey(value: string | null | undefined): string | null {
  if (value === null || value === undefined) return null;
  const normalized = value.trim();
  return normalized || null;
}
