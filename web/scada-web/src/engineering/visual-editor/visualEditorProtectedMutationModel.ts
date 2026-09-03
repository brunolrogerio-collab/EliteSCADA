import type { ScreenEngineering } from '../types';
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
 * projected onto the deterministic collision-free stacking model.
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
    case 'object.duplicate':
      assertVisualElementsAuthoringEditable(screen, intent.objectIds);
      return duplicateVisualEditorElements(screen, intent.objectIds, {
        createObjectId: options.createObjectId,
        offsetX: options.duplicateOffset ?? 12,
        offsetY: options.duplicateOffset ?? 12
      }).screen;
    case 'object.delete':
      return deleteVisualEditorElements(screen, intent.objectIds).screen;
    case 'object.zOrder':
      return applyVisualEditorZOrderOperation(screen, intent.objectIds, mapZOrderOperation(intent.operation));
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
