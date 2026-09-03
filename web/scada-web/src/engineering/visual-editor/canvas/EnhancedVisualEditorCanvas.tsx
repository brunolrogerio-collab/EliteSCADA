import React, {
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent as ReactKeyboardEvent,
  type PointerEvent as ReactPointerEvent
} from 'react';
import {
  isVisualElementEffectivelyAuthoringLocked,
  type VisualEditorAuthoringOperation
} from '../visualEditorAuthoringModel';
import type {
  VisualEditorCanvasContractProps,
  VisualEditorMutationIntent,
  VisualEditorPoint
} from '../visualEditorContracts';
import {
  resolveVisualEditorKeyboardCommand,
  type VisualEditorKeyboardCommand
} from '../visualEditorKeyboardModel';
import { VisualEditorAuthoringToolbar } from './VisualEditorAuthoringToolbar';
import { VisualEditorCanvas as LegacyVisualEditorCanvas } from './VisualEditorCanvas';
import { VisualEditorOutliner } from './VisualEditorOutliner';
import {
  DEFAULT_CANVAS_GRID_SIZE,
  clientDeltaToCanvas,
  collapseHierarchySelection,
  nextSelection,
  normalizeSelection,
  normalizeViewport,
  selectionModeFromModifiers
} from './canvasInteractionModel';
import {
  normalizeVisualEditorMarquee,
  resolveVisualEditorMarqueeSelection
} from './visualEditorSelectionModel';
import {
  rootVisualEditorObjectIds,
  visualEditorKeyboardCommandMutatesSelection,
  visualEditorMarqueeModeForDrag
} from './canvasEnhancedInteractionModel';
import {
  resolveVisualEditorMoveGuides,
  type VisualEditorMoveGuideResult
} from './visualEditorSmartGuidesModel';

export type EnhancedVisualEditorCanvasProps = VisualEditorCanvasContractProps & Readonly<{
  /** Optional session-level command sink. Legacy mutation shortcuts remain intact when omitted. */
  onKeyboardCommand?: (command: VisualEditorKeyboardCommand) => void;
  onAuthoringOperation?: (operation: VisualEditorAuthoringOperation) => void;
  canUndo?: boolean;
  canRedo?: boolean;
}>;

type MarqueeDraft = Readonly<{
  pointerId: number;
  startLogical: VisualEditorPoint;
  endLogical: VisualEditorPoint;
  startLocal: VisualEditorPoint;
  endLocal: VisualEditorPoint;
  selectionMode: ReturnType<typeof selectionModeFromModifiers>;
}>;

type GuideDrag = Readonly<{
  pointerId: number;
  startClient: VisualEditorPoint;
  objectIds: readonly string[];
}>;

/**
 * C07 interaction wrapper around the established Canvas renderer. It adds
 * logical marquee selection, hierarchy Outliner, smart alignment guides and
 * authoring-lock interception without duplicating canonical rendering or
 * geometry mutation code.
 */
export function VisualEditorCanvas(props: EnhancedVisualEditorCanvasProps) {
  const wrapperRef = useRef<HTMLDivElement | null>(null);
  const marqueeSurfaceRef = useRef<HTMLElement | null>(null);
  const [marquee, setMarquee] = useState<MarqueeDraft | null>(null);
  const [guideDrag, setGuideDrag] = useState<GuideDrag | null>(null);
  const [guidePreview, setGuidePreview] = useState<VisualEditorMoveGuideResult | null>(null);

  const beginCapture = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (props.polygonToolActive || event.button !== 0 || event.altKey) return;
    const target = event.target instanceof HTMLElement ? event.target : null;
    if (!target) return;

    const manipulationHandle = target.closest('[data-canvas-resize-handle],[data-canvas-rotate-handle],[data-polygon-vertex-index]');
    const objectNode = target.closest<HTMLElement>('[data-canvas-object-id]');
    if (objectNode) {
      const objectId = objectNode.dataset.canvasObjectId;
      if (!objectId) return;
      if (isVisualElementEffectivelyAuthoringLocked(props.screen, objectId)) {
        event.preventDefault();
        event.stopPropagation();
        props.onUiIntent({
          kind: 'selection.change',
          objectIds: [objectId],
          mode: selectionModeFromModifiers(event)
        });
        return;
      }
      if (manipulationHandle) return;

      const current = normalizeSelection(props.selectedObjectIds);
      const mode = selectionModeFromModifiers(event);
      const currentSet = new Set(current);
      const preserveExistingSelection = mode === 'replace' && currentSet.has(objectId);
      const requestedSelection = preserveExistingSelection
        ? current
        : nextSelection(current, objectId, mode);
      const dragObjectIds = collapseHierarchySelection(props.screen.elements ?? [], requestedSelection);
      if (dragObjectIds.some(id => isVisualElementEffectivelyAuthoringLocked(props.screen, id))) {
        event.preventDefault();
        event.stopPropagation();
        return;
      }
      setGuideDrag(Object.freeze({
        pointerId: event.pointerId,
        startClient: Object.freeze({ x: event.clientX, y: event.clientY }),
        objectIds: dragObjectIds
      }));
      setGuidePreview(null);
      return;
    }

    const surface = target.closest<HTMLElement>('.visual-editor-canvas__surface');
    if (!surface) return;

    const logical = clientToLogical(event.clientX, event.clientY, surface, props.viewport);
    const local = clientToWrapper(event.clientX, event.clientY, wrapperRef.current);
    marqueeSurfaceRef.current = surface;
    surface.setPointerCapture?.(event.pointerId);
    setMarquee(Object.freeze({
      pointerId: event.pointerId,
      startLogical: logical,
      endLogical: logical,
      startLocal: local,
      endLocal: local,
      selectionMode: selectionModeFromModifiers(event)
    }));
    event.preventDefault();
    event.stopPropagation();
  };

  const moveCapture = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (marquee && event.pointerId === marquee.pointerId) {
      const surface = marqueeSurfaceRef.current;
      if (!surface) return;
      event.preventDefault();
      event.stopPropagation();
      setMarquee(Object.freeze({
        ...marquee,
        endLogical: clientToLogical(event.clientX, event.clientY, surface, props.viewport),
        endLocal: clientToWrapper(event.clientX, event.clientY, wrapperRef.current)
      }));
      return;
    }

    if (!guideDrag || event.pointerId !== guideDrag.pointerId) return;
    const snapEnabled = wrapperRef.current
      ?.querySelector<HTMLButtonElement>('[data-testid="canvas-snap-toggle"]')
      ?.getAttribute('aria-pressed') === 'true';
    const delta = clientDeltaToCanvas(
      { x: event.clientX - guideDrag.startClient.x, y: event.clientY - guideDrag.startClient.y },
      props.viewport,
      snapEnabled,
      DEFAULT_CANVAS_GRID_SIZE
    );
    setGuidePreview(resolveVisualEditorMoveGuides(props.screen, guideDrag.objectIds, delta));
  };

  const finishCapture = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (marquee && event.pointerId === marquee.pointerId) {
      event.preventDefault();
      event.stopPropagation();
      const surface = marqueeSurfaceRef.current;
      if (surface?.hasPointerCapture?.(event.pointerId)) surface.releasePointerCapture(event.pointerId);
      marqueeSurfaceRef.current = null;

      const clientDistance = Math.hypot(
        marquee.endLocal.x - marquee.startLocal.x,
        marquee.endLocal.y - marquee.startLocal.y
      );
      if (clientDistance < 3) {
        if (marquee.selectionMode === 'replace') {
          props.onUiIntent({ kind: 'selection.change', objectIds: [], mode: 'replace' });
        }
        setMarquee(null);
        return;
      }

      const rect = normalizeVisualEditorMarquee(marquee.startLogical, marquee.endLogical);
      const mode = visualEditorMarqueeModeForDrag(marquee.startLogical, marquee.endLogical);
      const objectIds = resolveVisualEditorMarqueeSelection(props.screen.elements ?? [], rect, mode);
      props.onUiIntent({
        kind: 'selection.change',
        objectIds,
        mode: marquee.selectionMode
      });
      setMarquee(null);
      return;
    }

    if (guideDrag && event.pointerId === guideDrag.pointerId) {
      queueMicrotask(() => {
        setGuideDrag(null);
        setGuidePreview(null);
      });
    }
  };

  const cancelCapture = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (marquee && event.pointerId === marquee.pointerId) {
      event.preventDefault();
      event.stopPropagation();
      marqueeSurfaceRef.current = null;
      setMarquee(null);
    }
    if (guideDrag && event.pointerId === guideDrag.pointerId) {
      setGuideDrag(null);
      setGuidePreview(null);
    }
  };

  const keyCapture = (event: ReactKeyboardEvent<HTMLDivElement>): void => {
    const target = event.target instanceof HTMLElement ? event.target : null;
    const targetIsEditable = Boolean(target?.closest('input,textarea,select,[contenteditable="true"]'));
    const command = resolveVisualEditorKeyboardCommand({
      key: event.key,
      ctrlKey: event.ctrlKey,
      metaKey: event.metaKey,
      shiftKey: event.shiftKey,
      altKey: event.altKey,
      targetIsEditable
    });
    if (!command) return;

    if (command.kind === 'selectAll') {
      event.preventDefault();
      event.stopPropagation();
      props.onUiIntent({
        kind: 'selection.change',
        objectIds: rootVisualEditorObjectIds(props.screen),
        mode: 'replace'
      });
      return;
    }

    const selectionLocked = props.selectedObjectIds.some(objectId =>
      isVisualElementEffectivelyAuthoringLocked(props.screen, objectId));
    if (selectionLocked && visualEditorKeyboardCommandMutatesSelection(command)) {
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (props.onKeyboardCommand) {
      event.preventDefault();
      event.stopPropagation();
      props.onKeyboardCommand(command);
    }
  };

  const handleMutationIntent = (intent: VisualEditorMutationIntent): void => {
    if (intent.kind === 'object.move' && guideDrag && sameObjectSet(intent.objectIds, guideDrag.objectIds)) {
      const guided = resolveVisualEditorMoveGuides(props.screen, intent.objectIds, intent.delta);
      props.onMutationIntent(Object.freeze({ ...intent, delta: guided.delta }));
      setGuideDrag(null);
      setGuidePreview(null);
      return;
    }
    props.onMutationIntent(intent);
  };

  const marqueeStyle = marquee ? marqueeOverlayStyle(marquee.startLocal, marquee.endLocal) : undefined;
  const verticalGuideStyle = guidePreview?.verticalGuide
    ? smartGuideOverlayStyle('vertical', guidePreview.verticalGuide.position, wrapperRef.current, props.viewport)
    : undefined;
  const horizontalGuideStyle = guidePreview?.horizontalGuide
    ? smartGuideOverlayStyle('horizontal', guidePreview.horizontalGuide.position, wrapperRef.current, props.viewport)
    : undefined;

  return <div
    ref={wrapperRef}
    className="visual-editor-canvas-enhanced"
    style={{ position: 'relative' }}
    onPointerDownCapture={beginCapture}
    onPointerMoveCapture={moveCapture}
    onPointerUpCapture={finishCapture}
    onPointerCancelCapture={cancelCapture}
    onKeyDownCapture={keyCapture}
  >
    <VisualEditorAuthoringToolbar
      screen={props.screen}
      selectedObjectIds={props.selectedObjectIds}
      onOperation={props.onAuthoringOperation}
      onKeyboardCommand={props.onKeyboardCommand}
      canUndo={props.canUndo}
      canRedo={props.canRedo}
    />
    <LegacyVisualEditorCanvas {...props} onMutationIntent={handleMutationIntent} />
    <VisualEditorOutliner
      screen={props.screen}
      selectedObjectIds={props.selectedObjectIds}
      onSelection={(objectId, mode) => props.onUiIntent({
        kind: 'selection.change',
        objectIds: [objectId],
        mode
      })}
    />
    {verticalGuideStyle ? <div
      className="visual-editor-smart-guide is-vertical"
      data-testid="visual-editor-smart-guide-vertical"
      aria-hidden="true"
      style={verticalGuideStyle}
    /> : null}
    {horizontalGuideStyle ? <div
      className="visual-editor-smart-guide is-horizontal"
      data-testid="visual-editor-smart-guide-horizontal"
      aria-hidden="true"
      style={horizontalGuideStyle}
    /> : null}
    {marquee ? <div
      data-testid="visual-editor-marquee"
      data-marquee-mode={visualEditorMarqueeModeForDrag(marquee.startLogical, marquee.endLogical)}
      aria-hidden="true"
      style={marqueeStyle}
    /> : null}
  </div>;
}

function clientToLogical(
  clientX: number,
  clientY: number,
  surface: HTMLElement,
  viewportInput: VisualEditorCanvasContractProps['viewport']
): VisualEditorPoint {
  const viewport = normalizeViewport(viewportInput);
  const rect = surface.getBoundingClientRect();
  return Object.freeze({
    x: (clientX - rect.left - viewport.panX) / viewport.zoom,
    y: (clientY - rect.top - viewport.panY) / viewport.zoom
  });
}

function clientToWrapper(
  clientX: number,
  clientY: number,
  wrapper: HTMLElement | null
): VisualEditorPoint {
  const rect = wrapper?.getBoundingClientRect();
  return Object.freeze({
    x: clientX - (rect?.left ?? 0),
    y: clientY - (rect?.top ?? 0)
  });
}

function marqueeOverlayStyle(start: VisualEditorPoint, end: VisualEditorPoint): CSSProperties {
  const left = Math.min(start.x, end.x);
  const top = Math.min(start.y, end.y);
  return {
    position: 'absolute',
    left,
    top,
    width: Math.abs(end.x - start.x),
    height: Math.abs(end.y - start.y),
    border: '1px dashed #245f99',
    background: 'rgba(36,95,153,.10)',
    pointerEvents: 'none',
    zIndex: 100000
  };
}

function smartGuideOverlayStyle(
  axis: 'vertical' | 'horizontal',
  position: number,
  wrapper: HTMLElement | null,
  viewportInput: VisualEditorCanvasContractProps['viewport']
): CSSProperties | undefined {
  if (!wrapper) return undefined;
  const surface = wrapper.querySelector<HTMLElement>('.visual-editor-canvas__surface');
  if (!surface) return undefined;
  const wrapperRect = wrapper.getBoundingClientRect();
  const surfaceRect = surface.getBoundingClientRect();
  const viewport = normalizeViewport(viewportInput);
  const base = {
    position: 'absolute',
    pointerEvents: 'none',
    zIndex: 99999,
    background: '#d946ef'
  } satisfies CSSProperties;
  if (axis === 'vertical') {
    return {
      ...base,
      left: surfaceRect.left - wrapperRect.left + viewport.panX + position * viewport.zoom,
      top: surfaceRect.top - wrapperRect.top,
      width: 1,
      height: surfaceRect.height
    };
  }
  return {
    ...base,
    left: surfaceRect.left - wrapperRect.left,
    top: surfaceRect.top - wrapperRect.top + viewport.panY + position * viewport.zoom,
    width: surfaceRect.width,
    height: 1
  };
}

function sameObjectSet(left: readonly string[], right: readonly string[]): boolean {
  if (left.length !== right.length) return false;
  const expected = new Set(right);
  return left.every(value => expected.has(value));
}
