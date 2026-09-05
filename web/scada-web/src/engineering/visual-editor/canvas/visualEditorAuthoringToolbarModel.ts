import type { ScreenEngineering, VisualElementEngineering } from '../../types';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../../visual-runtime';
import {
  isVisualElementAuthoringLocked,
  isVisualElementEffectivelyAuthoringLocked
} from '../visualEditorAuthoringModel';

export type VisualEditorAuthoringToolbarState = Readonly<{
  selectionCount: number;
  selectedObjectIds: readonly string[];
  referenceObjectId: string | null;
  sameParent: boolean;
  hasEffectiveLock: boolean;
  canAlign: boolean;
  canDistribute: boolean;
  canSize: boolean;
  canGroup: boolean;
  canUngroup: boolean;
  canToggleLock: boolean;
  nextLockedValue: boolean;
}>;

export function buildVisualEditorAuthoringToolbarState(
  screen: ScreenEngineering,
  objectIds: readonly string[]
): VisualEditorAuthoringToolbarState {
  const ids = [...new Set(objectIds.map(id => id.trim()).filter(Boolean))];
  const located = ids
    .map(id => locate(screen.elements ?? [], id, null))
    .filter((value): value is Located => value !== null);
  const resolvedIds = located.map(item => item.element.id!).filter(Boolean);
  const sameParent = located.length > 0 && located.every(item => item.parentId === located[0].parentId);
  const hasEffectiveLock = resolvedIds.some(id => isVisualElementEffectivelyAuthoringLocked(screen, id));
  const allDirectLocked = located.length > 0 && located.every(item => isVisualElementAuthoringLocked(item.element));
  const hasInheritedOnlyLock = located.some(item =>
    !isVisualElementAuthoringLocked(item.element)
    && isVisualElementEffectivelyAuthoringLocked(screen, item.element.id!));
  const editableSiblingSelection = sameParent && !hasEffectiveLock;
  const canUngroup = editableSiblingSelection
    && located.length > 0
    && located.every(item => item.element.type === BUILTIN_VISUAL_OBJECT_TYPES.group && !item.element.dynamoKey);

  return Object.freeze({
    selectionCount: located.length,
    selectedObjectIds: Object.freeze(resolvedIds),
    referenceObjectId: resolvedIds[0] ?? null,
    sameParent,
    hasEffectiveLock,
    canAlign: editableSiblingSelection && located.length >= 2,
    canDistribute: editableSiblingSelection && located.length >= 3,
    canSize: editableSiblingSelection && located.length >= 2,
    canGroup: editableSiblingSelection && located.length >= 2,
    canUngroup,
    canToggleLock: located.length > 0 && !hasInheritedOnlyLock,
    nextLockedValue: !allDirectLocked
  });
}

type Located = Readonly<{
  element: VisualElementEngineering;
  parentId: string | null;
}>;

function locate(
  elements: readonly VisualElementEngineering[],
  objectId: string,
  parentId: string | null
): Located | null {
  for (const element of elements) {
    if (element.id === objectId) return Object.freeze({ element, parentId });
    const nested = locate(element.children ?? [], objectId, element.id ?? parentId);
    if (nested) return nested;
  }
  return null;
}
