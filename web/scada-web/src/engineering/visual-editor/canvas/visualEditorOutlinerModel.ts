import type { ScreenEngineering, VisualElementEngineering } from '../../types';
import {
  isVisualElementAuthoringLocked,
  isVisualElementEffectivelyAuthoringLocked
} from '../visualEditorAuthoringModel';

export type VisualEditorOutlinerNode = Readonly<{
  objectId: string;
  key: string;
  type: string;
  dynamoKey: string | null;
  directLocked: boolean;
  effectiveLocked: boolean;
  children: readonly VisualEditorOutlinerNode[];
}>;

export function buildVisualEditorOutliner(
  screen: ScreenEngineering
): readonly VisualEditorOutlinerNode[] {
  return Object.freeze((screen.elements ?? []).flatMap(element => {
    const node = buildNode(screen, element);
    return node ? [node] : [];
  }));
}

export function countVisualEditorOutlinerNodes(
  nodes: readonly VisualEditorOutlinerNode[]
): number {
  return nodes.reduce((sum, node) => sum + 1 + countVisualEditorOutlinerNodes(node.children), 0);
}

function buildNode(
  screen: ScreenEngineering,
  element: VisualElementEngineering
): VisualEditorOutlinerNode | null {
  const objectId = element.id?.trim();
  if (!objectId) return null;
  return Object.freeze({
    objectId,
    key: element.key,
    type: element.type,
    dynamoKey: element.dynamoKey?.trim() || null,
    directLocked: isVisualElementAuthoringLocked(element),
    effectiveLocked: isVisualElementEffectivelyAuthoringLocked(screen, objectId),
    children: Object.freeze((element.children ?? []).flatMap(child => {
      const node = buildNode(screen, child);
      return node ? [node] : [];
    }))
  });
}
