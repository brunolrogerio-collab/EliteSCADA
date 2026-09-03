import type { ScreenEngineering, VisualElementEngineering } from '../types';
import {
  applyVisualEditorAuthoringOperation,
  assertVisualElementsAuthoringEditable,
  type VisualEditorAuthoringOperation
} from './visualEditorAuthoringModel';
import {
  copyVisualEditorElements,
  deleteVisualEditorElements,
  duplicateVisualEditorElements,
  nudgeVisualEditorElements,
  pasteVisualEditorElements,
  type VisualEditorClipboardPayload
} from './visualEditorClipboardModel';
import { updateScreenElement } from './visualEditorCanonicalModel';
import {
  removeDynamoPublicParameterValue,
  setDynamoPublicParameterValue
} from './dynamo/dynamoPublicInterfaceModel';
import {
  canRedoVisualEditorHistory,
  canUndoVisualEditorHistory,
  commitVisualEditorHistory,
  createVisualEditorHistory,
  endVisualEditorHistoryGesture,
  redoVisualEditorHistory,
  undoVisualEditorHistory,
  type VisualEditorHistoryState
} from './visualEditorHistoryModel';
import type { VisualEditorKeyboardCommand } from './visualEditorKeyboardModel';
import { applyVisualDefinitionSurfacePatch } from './visualDefinitionSurfaceModel';
import {
  applyVisualEditorZOrderOperation,
  type VisualEditorZOrderOperation
} from './visualEditorZOrderModel';

export type VisualEditorSessionState = Readonly<{
  history: VisualEditorHistoryState<ScreenEngineering>;
  selectedObjectIds: readonly string[];
  clipboard: VisualEditorClipboardPayload | null;
}>;

export type VisualEditorSessionCommitOptions = Readonly<{
  selectedObjectIds?: readonly string[];
  coalesceKey?: string | null;
}>;

/**
 * Wave 14 C07 authoring-session coordinator. Canonical Engineering Screen data
 * lives in history.present; selection and clipboard stay transient and CAS/package
 * versions never enter this state.
 */
export function createVisualEditorSession(
  screen: ScreenEngineering,
  historyLimit = 100
): VisualEditorSessionState {
  return Object.freeze({
    history: createVisualEditorHistory(screen, historyLimit),
    selectedObjectIds: Object.freeze([]),
    clipboard: null
  });
}

export function currentVisualEditorSessionScreen(
  state: VisualEditorSessionState
): ScreenEngineering {
  return state.history.present;
}

export function canUndoVisualEditorSession(state: VisualEditorSessionState): boolean {
  return canUndoVisualEditorHistory(state.history);
}

export function canRedoVisualEditorSession(state: VisualEditorSessionState): boolean {
  return canRedoVisualEditorHistory(state.history);
}

export function withVisualEditorSessionSelection(
  state: VisualEditorSessionState,
  objectIds: readonly string[]
): VisualEditorSessionState {
  return Object.freeze({
    ...state,
    selectedObjectIds: sanitizeSelection(state.history.present, objectIds)
  });
}

export function commitVisualEditorSessionDraft(
  state: VisualEditorSessionState,
  nextScreen: ScreenEngineering,
  options: VisualEditorSessionCommitOptions = {}
): VisualEditorSessionState {
  const history = commitVisualEditorHistory(state.history, nextScreen, {
    coalesceKey: options.coalesceKey ?? null
  });
  return Object.freeze({
    ...state,
    history,
    selectedObjectIds: sanitizeSelection(
      history.present,
      options.selectedObjectIds ?? state.selectedObjectIds
    )
  });
}

export function endVisualEditorSessionGesture(
  state: VisualEditorSessionState
): VisualEditorSessionState {
  return Object.freeze({ ...state, history: endVisualEditorHistoryGesture(state.history) });
}

export function applyVisualEditorSessionAuthoringOperation(
  state: VisualEditorSessionState,
  operation: VisualEditorAuthoringOperation
): VisualEditorSessionState {
  const before = state.history.present;
  const ungroupChildren = operation.kind === 'ungroup'
    ? selectedGroupChildIds(before, operation.objectIds)
    : Object.freeze([] as string[]);
  const nextScreen = applyVisualEditorAuthoringOperation(before, operation);

  let nextSelection = state.selectedObjectIds;
  if (operation.kind === 'group') {
    const beforeIds = collectObjectIds(before.elements ?? []);
    const created = [...collectObjectIds(nextScreen.elements ?? [])].filter(id => !beforeIds.has(id));
    nextSelection = created.length === 1 ? Object.freeze(created) : sanitizeSelection(nextScreen, state.selectedObjectIds);
  } else if (operation.kind === 'ungroup') {
    nextSelection = sanitizeSelection(nextScreen, ungroupChildren);
  }

  return commitVisualEditorSessionDraft(state, nextScreen, { selectedObjectIds: nextSelection });
}

export function applyVisualEditorSessionZOrder(
  state: VisualEditorSessionState,
  operation: VisualEditorZOrderOperation
): VisualEditorSessionState {
  if (state.selectedObjectIds.length === 0) return state;
  return commitVisualEditorSessionDraft(
    state,
    applyVisualEditorZOrderOperation(state.history.present, state.selectedObjectIds, operation)
  );
}

export function applyVisualEditorSessionKeyboardCommand(
  state: VisualEditorSessionState,
  command: VisualEditorKeyboardCommand
): VisualEditorSessionState {
  switch (command.kind) {
    case 'undo': {
      const history = undoVisualEditorHistory(state.history);
      return Object.freeze({
        ...state,
        history,
        selectedObjectIds: sanitizeSelection(history.present, state.selectedObjectIds)
      });
    }
    case 'redo': {
      const history = redoVisualEditorHistory(state.history);
      return Object.freeze({
        ...state,
        history,
        selectedObjectIds: sanitizeSelection(history.present, state.selectedObjectIds)
      });
    }
    case 'copy':
      if (state.selectedObjectIds.length === 0) return state;
      return Object.freeze({
        ...state,
        clipboard: copyVisualEditorElements(state.history.present, state.selectedObjectIds)
      });
    case 'paste': {
      if (!state.clipboard) return state;
      const result = pasteVisualEditorElements(state.history.present, state.clipboard);
      return commitVisualEditorSessionDraft(state, result.screen, { selectedObjectIds: result.objectIds });
    }
    case 'duplicate': {
      if (state.selectedObjectIds.length === 0) return state;
      const result = duplicateVisualEditorElements(state.history.present, state.selectedObjectIds);
      return commitVisualEditorSessionDraft(state, result.screen, { selectedObjectIds: result.objectIds });
    }
    case 'delete': {
      if (state.selectedObjectIds.length === 0) return state;
      const result = deleteVisualEditorElements(state.history.present, state.selectedObjectIds);
      return commitVisualEditorSessionDraft(state, result.screen, { selectedObjectIds: result.objectIds });
    }
    case 'group':
      if (state.selectedObjectIds.length < 2) return state;
      return applyVisualEditorSessionAuthoringOperation(state, {
        kind: 'group',
        objectIds: state.selectedObjectIds
      });
    case 'ungroup':
      if (state.selectedObjectIds.length === 0) return state;
      return applyVisualEditorSessionAuthoringOperation(state, {
        kind: 'ungroup',
        objectIds: state.selectedObjectIds
      });
    case 'align':
      if (state.selectedObjectIds.length < 2) return state;
      return applyVisualEditorSessionAuthoringOperation(state, {
        kind: 'align',
        objectIds: state.selectedObjectIds,
        operation: command.operation
      });
    case 'distribute':
      if (state.selectedObjectIds.length < 3) return state;
      return applyVisualEditorSessionAuthoringOperation(state, {
        kind: 'distribute',
        objectIds: state.selectedObjectIds,
        operation: command.operation
      });
    case 'size':
      if (state.selectedObjectIds.length < 2) return state;
      return applyVisualEditorSessionAuthoringOperation(state, {
        kind: 'size',
        objectIds: state.selectedObjectIds,
        referenceObjectId: state.selectedObjectIds[0],
        operation: command.operation
      });
    case 'lock':
      if (state.selectedObjectIds.length === 0) return state;
      return applyVisualEditorSessionAuthoringOperation(state, {
        kind: 'lock',
        objectIds: state.selectedObjectIds,
        locked: command.locked
      });
    case 'surface.set':
      return commitVisualEditorSessionDraft(
        state,
        applyVisualDefinitionSurfacePatch(state.history.present, command.patch)
      );
    case 'dynamoParameter.set': {
      assertVisualElementsAuthoringEditable(state.history.present, [command.objectId]);
      const nextScreen = updateScreenElement(state.history.present, command.objectId, instance =>
        setDynamoPublicParameterValue(instance, command.definition, command.value));
      return commitVisualEditorSessionDraft(state, nextScreen);
    }
    case 'dynamoParameter.remove': {
      assertVisualElementsAuthoringEditable(state.history.present, [command.objectId]);
      const nextScreen = updateScreenElement(state.history.present, command.objectId, instance =>
        removeDynamoPublicParameterValue(instance, command.definition, command.parameterKey));
      return commitVisualEditorSessionDraft(state, nextScreen);
    }
    case 'selectAll':
      return Object.freeze({
        ...state,
        selectedObjectIds: Object.freeze(
          (state.history.present.elements ?? []).flatMap(element => element.id ? [element.id] : [])
        )
      });
    case 'nudge': {
      if (state.selectedObjectIds.length === 0) return state;
      const result = nudgeVisualEditorElements(
        state.history.present,
        state.selectedObjectIds,
        command.deltaX,
        command.deltaY
      );
      return commitVisualEditorSessionDraft(state, result.screen, {
        selectedObjectIds: result.objectIds,
        coalesceKey: 'keyboard:nudge'
      });
    }
  }
}

function sanitizeSelection(
  screen: ScreenEngineering,
  objectIds: readonly string[]
): readonly string[] {
  const existing = collectObjectIds(screen.elements ?? []);
  return Object.freeze([...new Set(objectIds.filter(id => existing.has(id)))]);
}

function collectObjectIds(
  elements: readonly VisualElementEngineering[],
  target = new Set<string>()
): Set<string> {
  for (const element of elements) {
    if (element.id?.trim()) target.add(element.id);
    if (element.children?.length) collectObjectIds(element.children, target);
  }
  return target;
}

function selectedGroupChildIds(
  screen: ScreenEngineering,
  objectIds: readonly string[]
): readonly string[] {
  const wanted = new Set(objectIds);
  const result: string[] = [];
  const visit = (elements: readonly VisualElementEngineering[]) => {
    for (const element of elements) {
      if (element.id && wanted.has(element.id)) {
        for (const child of element.children ?? []) {
          if (child.id) result.push(child.id);
        }
      }
      if (element.children?.length) visit(element.children);
    }
  };
  visit(screen.elements ?? []);
  return Object.freeze(result);
}
