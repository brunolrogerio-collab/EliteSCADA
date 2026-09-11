import type { ScreenEngineering, VisualElementEngineering } from '../types';
import { applyVisualEditorAuthoringOperation, assertVisualElementsAuthoringEditable } from './visualEditorAuthoringModel';
import {
  deleteVisualEditorElements,
  duplicateVisualEditorElements
} from './visualEditorClipboardModel';
import {
  applyVisualEditorMutationIntent as applyLegacyVisualEditorMutationIntent,
  type VisualEditorMutationOptions
} from './visualEditorCanonicalModelLegacy';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import { applyVisualEditorZOrderOperation } from './visualEditorZOrderModel';

/**
 * C07 safety seam for legacy Canvas/Inspector mutation intents. All mutations of
 * existing objects respect authoring locks, while legacy z-order intents are
 * projected onto the deterministic collision-free stacking model whenever the
 * stacking context needs normalization or a multi-selection must move as a block.
 */
export function applyProtectedVisualEditorMutationIntent(
  screen: ScreenEngineering,
  intent: VisualEditorMutationIntent,
  options: VisualEditorMutationOptions = {}
): ScreenEngineering {
  switch (intent.kind) {
    case 'object.add':
      if (intent.parentObjectId) assertVisualElementsAuthoringEditable(screen, [intent.parentObjectId]);
      return applyLegacyVisualEditorMutationIntent(screen, intent, options);
    case 'dynamo.add':
      return applyLegacyVisualEditorMutationIntent(screen, intent, options);
    case 'object.move':
    case 'object.rotate':
    case 'property.set':
    case 'property.remove':
      assertVisualElementsAuthoringEditable(screen, intent.objectIds);
      return applyLegacyVisualEditorMutationIntent(screen, intent, options);
    case 'object.resize':
    case 'polygon.points.set':
    case 'binding.set':
    case 'binding.remove':
    case 'propertyExpression.set':
    case 'propertyExpression.remove':
    case 'booleanCondition.set':
    case 'booleanCondition.remove':
    case 'analogFill.set':
    case 'analogFill.remove':
      assertVisualElementsAuthoringEditable(screen, [intent.objectId]);
      return applyLegacyVisualEditorMutationIntent(screen, intent, options);
    case 'object.duplicate': {
      assertVisualElementsAuthoringEditable(screen, intent.objectIds);
      const rootObjectIds = collapseSelectedDescendants(screen.elements ?? [], intent.objectIds);
      return duplicateVisualEditorElements(screen, rootObjectIds, {
        createObjectId: options.createObjectId,
        offsetX: options.duplicateOffset ?? 12,
        offsetY: options.duplicateOffset ?? 12
      }).screen;
    }
    case 'object.delete':
      return deleteVisualEditorElements(screen, intent.objectIds).screen;
    case 'object.zOrder': {
      assertVisualElementsAuthoringEditable(screen, intent.objectIds);
      // Preserve the established single-object canonical contract when the
      // stacking context is already collision-free: only the selected object's
      // explicit zIndex is mutated. C07 normalization is required for ties and
      // for multi-selection stable-block moves.
      if (intent.objectIds.length === 1 && !hasZIndexCollision(screen.elements ?? [], intent.objectIds[0])) {
        return applyLegacyVisualEditorMutationIntent(screen, intent, options);
      }
      return applyVisualEditorZOrderOperation(screen, intent.objectIds, mapZOrderOperation(intent.operation));
    }
    case 'polygon.create':
      return applyLegacyVisualEditorMutationIntent(screen, intent, options);
  }
}

/** Authoring toolbar operations share the same protected canonical seam. */
export function applyProtectedVisualEditorAuthoringOperation(
  screen: ScreenEngineering,
  operation: Parameters<typeof applyVisualEditorAuthoringOperation>[1]
): ScreenEngineering {
  return applyVisualEditorAuthoringOperation(screen, operation);
}

function collapseSelectedDescendants(
  elements: readonly VisualElementEngineering[],
  objectIds: readonly string[]
): readonly string[] {
  const requested = new Set(objectIds.map(value => value.trim()).filter(Boolean));
  const roots: string[] = [];

  const visit = (items: readonly VisualElementEngineering[], ancestorSelected: boolean): void => {
    for (const element of items) {
      const objectId = element.id?.trim() ?? '';
      const selected = Boolean(objectId && requested.has(objectId));
      if (selected && !ancestorSelected) roots.push(objectId);
      if (element.children?.length) visit(element.children, ancestorSelected || selected);
    }
  };

  visit(elements, false);
  return Object.freeze(roots);
}

function hasZIndexCollision(
  elements: readonly VisualElementEngineering[],
  objectId: string
): boolean {
  const siblings = siblingElementsForObject(elements, objectId);
  if (!siblings) return false;
  const seen = new Set<number>();
  for (const sibling of siblings) {
    const raw = sibling.properties?.zIndex;
    const zIndex = typeof raw === 'number' && Number.isFinite(raw) ? raw : 0;
    if (seen.has(zIndex)) return true;
    seen.add(zIndex);
  }
  return false;
}

function siblingElementsForObject(
  elements: readonly VisualElementEngineering[],
  objectId: string
): readonly VisualElementEngineering[] | null {
  if (elements.some(element => element.id === objectId)) return elements;
  for (const element of elements) {
    if (!element.children?.length) continue;
    const nested = siblingElementsForObject(element.children, objectId);
    if (nested) return nested;
  }
  return null;
}

function mapZOrderOperation(
  operation: Extract<VisualEditorMutationIntent, { kind: 'object.zOrder' }>['operation']
): Parameters<typeof applyVisualEditorZOrderOperation>[2] {
  switch (operation) {
    case 'bringToFront': return 'front';
    case 'sendToBack': return 'back';
    case 'bringForward': return 'forward';
    case 'sendBackward': return 'backward';
  }
}
