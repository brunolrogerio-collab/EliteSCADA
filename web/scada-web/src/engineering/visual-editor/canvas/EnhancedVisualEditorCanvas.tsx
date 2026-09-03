import React, {
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent as ReactKeyboardEvent,
  type PointerEvent as ReactPointerEvent
} from 'react';
import { isVisualElementEffectivelyAuthoringLocked } from '../visualEditorAuthoringModel';
import type { VisualEditorCanvasContractProps, VisualEditorPoint } from '../visualEditorContracts';
import {
  resolveVisualEditorKeyboardCommand,
  type VisualEditorKeyboardCommand
} from '../visualEditorKeyboardModel';
import { VisualEditorCanvas as LegacyVisualEditorCanvas } from './VisualEditorCanvas';
import { VisualEditorOutliner } from './VisualEditorOutliner';
import {
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

export type EnhancedVisualEditorCanvasProps = VisualEditorCanvasContractProps & Readonly<{
  /** Optional session-level command sink. Legacy mutation shortcuts remain intact when omitted. */
  onKeyboardCommand?: (command: VisualEditorKeyboardCommand) => void;
}>;

type MarqueeDraft = Readonly<{
  pointerId: number;
  startLogical: VisualEditorPoint;
  endLogical: VisualEditorPoint;
  startLocal: VisualEditorPoint;
  endLocal: VisualEditorPoint;
  selectionMode: ReturnType<typeof selectionModeFromModifiers>;
}>;

/**
 * C07 interaction wrapper around the established Canvas renderer. It adds
 * logical marquee selection, hierarchy Outliner and authoring-lock interception
 * without duplicating canonical rendering or geometry mutation code.
 */
export function VisualEditorCanvas(props: EnhancedVisualEditorCanvasProps) {
  const wrapperRef = useRef<HTMLDivElement | null>(null);
  const marqueeSurfaceRef = useRef<HTMLElement | null>(null);
  const [marquee, setMarquee] = useState<MarqueeDraft | null>(null);

  const beginCapture = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (props.polygonToolActive || event.button !== 0 || event.altKey) return;
    const target = event.target instanceof HTMLElement ? event.target : null;
    if (!target) return;

    const objectNode = target.closest<HTMLElement>('[data-canvas-object-id]');
    if (objectNode) {
      const objectId = objectNode.dataset.canvasObjectId;
      if (objectId && isVisualElementEffectivelyAuthoringLocked(props.screen, objectId)) {
        event.preventDefault();
        event.stopPropagation();
        props.onUiIntent({
          kind: 'selection.change',
          objectIds: [objectId],
          mode: selectionModeFromModifiers(event)
        });
      }
      return;
    }

    if (target.closest('[data-canvas-resize-handle],[data-canvas-rotate-handle],[data-polygon-vertex-index]')) return;
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
    if (!marquee || event.pointerId !== marquee.pointerId) return;
    const surface = marqueeSurfaceRef.current;
    if (!surface) return;
    event.preventDefault();
    event.stopPropagation();
    setMarquee(Object.freeze({
      ...marquee,
      endLogical: clientToLogical(event.clientX, event.clientY, surface, props.viewport),
      endLocal: clientToWrapper(event.clientX, event.clientY, wrapperRef.current)
    }));
  };

  const finishCapture = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (!marquee || event.pointerId !== marquee.pointerId) return;
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
  };

  const cancelCapture = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (!marquee || event.pointerId !== marquee.pointerId) return;
    event.preventDefault();
    event.stopPropagation();
    marqueeSurfaceRef.current = null;
    setMarquee(null);
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

  const marqueeStyle = marquee ? marqueeOverlayStyle(marquee.startLocal, marquee.endLocal) : undefined;
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
    <LegacyVisualEditorCanvas {...props} />
    <VisualEditorOutliner
      screen={props.screen}
      selectedObjectIds={props.selectedObjectIds}
      onSelection={(objectId, mode) => props.onUiIntent({
        kind: 'selection.change',
        objectIds: [objectId],
        mode
      })}
    />
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
