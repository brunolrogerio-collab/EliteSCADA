import { useMemo, useRef, useState } from 'react';
import type {
  CSSProperties,
  KeyboardEvent as ReactKeyboardEvent,
  PointerEvent as ReactPointerEvent,
  WheelEvent as ReactWheelEvent
} from 'react';
import type {
  VisualEditorBounds,
  VisualEditorCanvasContractProps,
  VisualEditorMutationIntent,
  VisualEditorPoint,
  VisualEditorViewport
} from '../visualEditorContracts';
import {
  DEFAULT_CANVAS_GRID_SIZE,
  clientDeltaToCanvas,
  hasMeaningfulDelta,
  nextSelection,
  normalizeSelection,
  normalizeViewport,
  panViewport,
  projectCanvasElements,
  resizeBounds,
  rotationDeltaDegrees,
  selectionModeFromModifiers,
  snapScalar,
  zoomViewport,
  type CanvasElementProjection,
  type CanvasResizeHandle
} from './canvasInteractionModel';
import './visual-editor-canvas.css';

type MoveInteraction = Readonly<{
  kind: 'move';
  pointerId: number;
  startClient: VisualEditorPoint;
  objectIds: readonly string[];
  delta: VisualEditorPoint;
}>;

type ResizeInteraction = Readonly<{
  kind: 'resize';
  pointerId: number;
  startClient: VisualEditorPoint;
  objectId: string;
  handle: CanvasResizeHandle;
  startBounds: VisualEditorBounds;
  bounds: VisualEditorBounds;
}>;

type RotateInteraction = Readonly<{
  kind: 'rotate';
  pointerId: number;
  startClient: VisualEditorPoint;
  centerClient: VisualEditorPoint;
  objectIds: readonly string[];
  deltaDegrees: number;
}>;

type PanInteraction = Readonly<{
  kind: 'pan';
  pointerId: number;
  startClient: VisualEditorPoint;
  startViewport: VisualEditorViewport;
  viewport: VisualEditorViewport;
}>;

type CanvasInteraction = MoveInteraction | ResizeInteraction | RotateInteraction | PanInteraction | null;

const CANVAS_CONTENT_WIDTH = 6000;
const CANVAS_CONTENT_HEIGHT = 4000;

export function VisualEditorCanvas({
  screen,
  selectedObjectIds,
  viewport,
  onUiIntent,
  onMutationIntent
}: VisualEditorCanvasContractProps) {
  const surfaceRef = useRef<HTMLDivElement | null>(null);
  const [gridEnabled, setGridEnabled] = useState(true);
  const [snapEnabled, setSnapEnabled] = useState(true);
  const [hoveredObjectId, setHoveredObjectId] = useState<string | null>(null);
  const [interaction, setInteraction] = useState<CanvasInteraction>(null);

  const projectedElements = useMemo(() => projectCanvasElements(screen), [screen]);
  const selection = useMemo(() => normalizeSelection(selectedObjectIds), [selectedObjectIds]);
  const effectiveViewport = interaction?.kind === 'pan'
    ? interaction.viewport
    : normalizeViewport(viewport);
  const selectionSet = useMemo(() => new Set(selection), [selection]);

  const emitSelection = (
    objectIds: readonly string[],
    mode: 'replace' | 'add' | 'toggle'
  ): void => {
    onUiIntent({ kind: 'selection.change', objectIds, mode });
  };

  const emitMutationForSelection = (intent: VisualEditorMutationIntent): void => {
    if (selection.length === 0) return;
    onMutationIntent(intent);
  };

  const beginMove = (
    event: ReactPointerEvent<HTMLElement>,
    projection: CanvasElementProjection
  ): void => {
    if (event.button !== 0 || projection.objectId === null) return;
    event.stopPropagation();

    const mode = selectionModeFromModifiers(event);
    const preserveExistingSelection = mode === 'replace' && selectionSet.has(projection.objectId);
    const dragObjectIds = preserveExistingSelection
      ? selection
      : nextSelection(selection, projection.objectId, mode);
    if (!preserveExistingSelection) {
      emitSelection([projection.objectId], mode);
    }

    event.currentTarget.setPointerCapture(event.pointerId);
    setInteraction({
      kind: 'move',
      pointerId: event.pointerId,
      startClient: pointFromPointer(event),
      objectIds: dragObjectIds,
      delta: Object.freeze({ x: 0, y: 0 })
    });
  };

  const beginResize = (
    event: ReactPointerEvent<HTMLButtonElement>,
    projection: CanvasElementProjection,
    handle: CanvasResizeHandle
  ): void => {
    if (event.button !== 0 || projection.objectId === null || selection.length !== 1) return;
    event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    const startBounds = Object.freeze({
      x: projection.geometry.x,
      y: projection.geometry.y,
      width: projection.geometry.width,
      height: projection.geometry.height
    });
    setInteraction({
      kind: 'resize',
      pointerId: event.pointerId,
      startClient: pointFromPointer(event),
      objectId: projection.objectId,
      handle,
      startBounds,
      bounds: startBounds
    });
  };

  const beginRotate = (
    event: ReactPointerEvent<HTMLButtonElement>,
    projection: CanvasElementProjection
  ): void => {
    if (event.button !== 0 || projection.objectId === null) return;
    event.stopPropagation();
    const objectNode = event.currentTarget.closest<HTMLElement>('[data-canvas-object-id]');
    if (!objectNode) return;
    const rect = objectNode.getBoundingClientRect();
    const centerClient = Object.freeze({
      x: rect.left + rect.width / 2,
      y: rect.top + rect.height / 2
    });
    const localSelection = selectionSet.has(projection.objectId)
      ? selection
      : Object.freeze([projection.objectId]);
    if (!selectionSet.has(projection.objectId)) emitSelection([projection.objectId], 'replace');

    event.currentTarget.setPointerCapture(event.pointerId);
    setInteraction({
      kind: 'rotate',
      pointerId: event.pointerId,
      startClient: pointFromPointer(event),
      centerClient,
      objectIds: localSelection,
      deltaDegrees: 0
    });
  };

  const handleSurfacePointerDown = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (event.target !== event.currentTarget) return;

    if (event.button === 1 || (event.button === 0 && event.altKey)) {
      event.preventDefault();
      event.currentTarget.setPointerCapture(event.pointerId);
      const startViewport = normalizeViewport(viewport);
      setInteraction({
        kind: 'pan',
        pointerId: event.pointerId,
        startClient: pointFromPointer(event),
        startViewport,
        viewport: startViewport
      });
      return;
    }

    if (event.button === 0) emitSelection([], 'replace');
  };

  const handlePointerMove = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (!interaction || event.pointerId !== interaction.pointerId) return;
    const current = pointFromPointer(event);

    if (interaction.kind === 'pan') {
      const deltaClient = subtractPoints(current, interaction.startClient);
      setInteraction({
        ...interaction,
        viewport: panViewport(interaction.startViewport, deltaClient)
      });
      return;
    }

    if (interaction.kind === 'rotate') {
      let deltaDegrees = rotationDeltaDegrees(
        interaction.centerClient,
        interaction.startClient,
        current
      );
      if (event.shiftKey) deltaDegrees = snapScalar(deltaDegrees, 15);
      setInteraction({ ...interaction, deltaDegrees });
      return;
    }

    const deltaClient = subtractPoints(current, interaction.startClient);
    const delta = clientDeltaToCanvas(
      deltaClient,
      effectiveViewport,
      snapEnabled,
      DEFAULT_CANVAS_GRID_SIZE
    );

    if (interaction.kind === 'move') {
      setInteraction({ ...interaction, delta });
      return;
    }

    setInteraction({
      ...interaction,
      bounds: resizeBounds(interaction.startBounds, delta, interaction.handle)
    });
  };

  const finishInteraction = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (!interaction || event.pointerId !== interaction.pointerId) return;

    switch (interaction.kind) {
      case 'move':
        if (interaction.objectIds.length > 0 && hasMeaningfulDelta(interaction.delta)) {
          onMutationIntent({
            kind: 'object.move',
            objectIds: interaction.objectIds,
            delta: interaction.delta
          });
        }
        break;
      case 'resize':
        if (!sameBounds(interaction.startBounds, interaction.bounds)) {
          onMutationIntent({
            kind: 'object.resize',
            objectId: interaction.objectId,
            bounds: interaction.bounds
          });
        }
        break;
      case 'rotate':
        if (interaction.objectIds.length > 0 && Math.abs(interaction.deltaDegrees) > Number.EPSILON) {
          onMutationIntent({
            kind: 'object.rotate',
            objectIds: interaction.objectIds,
            deltaDegrees: interaction.deltaDegrees
          });
        }
        break;
      case 'pan':
        if (!sameViewport(interaction.startViewport, interaction.viewport)) {
          onUiIntent({ kind: 'viewport.change', viewport: interaction.viewport });
        }
        break;
    }

    setInteraction(null);
  };

  const handleWheel = (event: ReactWheelEvent<HTMLDivElement>): void => {
    event.preventDefault();
    if (event.ctrlKey || event.metaKey) {
      const factor = event.deltaY < 0 ? 1.1 : 1 / 1.1;
      onUiIntent({ kind: 'viewport.change', viewport: zoomViewport(viewport, factor) });
      return;
    }
    onUiIntent({
      kind: 'viewport.change',
      viewport: panViewport(viewport, { x: -event.deltaX, y: -event.deltaY })
    });
  };

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>): void => {
    if (event.key === 'Escape') {
      setInteraction(null);
      emitSelection([], 'replace');
      return;
    }

    if (selection.length === 0) return;

    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      onMutationIntent({ kind: 'object.delete', objectIds: selection });
      return;
    }

    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'd') {
      event.preventDefault();
      onMutationIntent({ kind: 'object.duplicate', objectIds: selection });
      return;
    }

    const step = snapEnabled ? DEFAULT_CANVAS_GRID_SIZE : 1;
    const multiplier = event.shiftKey ? 5 : 1;
    const distance = step * multiplier;
    const arrowDelta: VisualEditorPoint | null = event.key === 'ArrowLeft'
      ? { x: -distance, y: 0 }
      : event.key === 'ArrowRight'
        ? { x: distance, y: 0 }
        : event.key === 'ArrowUp'
          ? { x: 0, y: -distance }
          : event.key === 'ArrowDown'
            ? { x: 0, y: distance }
            : null;
    if (arrowDelta) {
      event.preventDefault();
      onMutationIntent({ kind: 'object.move', objectIds: selection, delta: arrowDelta });
    }
  };

  const viewportStyle = {
    transform: `translate(${effectiveViewport.panX}px, ${effectiveViewport.panY}px) scale(${effectiveViewport.zoom})`,
    transformOrigin: '0 0',
    width: `${CANVAS_CONTENT_WIDTH}px`,
    height: `${CANVAS_CONTENT_HEIGHT}px`
  } satisfies CSSProperties;

  const surfaceStyle = {
    '--visual-editor-grid-size': `${DEFAULT_CANVAS_GRID_SIZE * effectiveViewport.zoom}px`,
    '--visual-editor-grid-pan-x': `${effectiveViewport.panX}px`,
    '--visual-editor-grid-pan-y': `${effectiveViewport.panY}px`
  } as CSSProperties;

  const renderProjection = (
    projection: CanvasElementProjection,
    ancestorMovesWithSelection: boolean
  ): React.ReactNode => {
    const objectId = projection.objectId;
    const selected = objectId !== null && selectionSet.has(objectId);
    const hovered = objectId !== null && hoveredObjectId === objectId;
    const moveTargeted = interaction?.kind === 'move' &&
      objectId !== null &&
      interaction.objectIds.includes(objectId);
    const applyMovePreview = moveTargeted && !ancestorMovesWithSelection;
    const resizeTargeted = interaction?.kind === 'resize' && objectId === interaction.objectId;
    const rotateTargeted = interaction?.kind === 'rotate' &&
      objectId !== null &&
      interaction.objectIds.includes(objectId);

    const geometry = resizeTargeted
      ? { ...projection.geometry, ...interaction.bounds }
      : projection.geometry;
    const previewX = geometry.x + (applyMovePreview && interaction?.kind === 'move' ? interaction.delta.x : 0);
    const previewY = geometry.y + (applyMovePreview && interaction?.kind === 'move' ? interaction.delta.y : 0);
    const previewRotation = geometry.rotation + (rotateTargeted && interaction?.kind === 'rotate'
      ? interaction.deltaDegrees
      : 0);

    const style = {
      left: `${previewX}px`,
      top: `${previewY}px`,
      width: `${Math.max(geometry.width, 1)}px`,
      height: `${Math.max(geometry.height, 1)}px`,
      zIndex: geometry.zIndex,
      display: geometry.visible ? undefined : 'none',
      transform: `rotate(${previewRotation}deg) scale(${geometry.scaleX}, ${geometry.scaleY})`,
      transformOrigin: 'center'
    } satisfies CSSProperties;

    const childAncestorMoving = ancestorMovesWithSelection || moveTargeted;

    return (
      <div
        key={objectId ?? `${projection.element.type}:${projection.element.key}`}
        className={`visual-editor-canvas__object${selected ? ' is-selected' : ''}${hovered ? ' is-hovered' : ''}${projection.identityIssue ? ' has-identity-issue' : ''}`}
        style={style}
        data-canvas-object-id={objectId ?? undefined}
        data-canvas-object-key={projection.element.key}
        data-canvas-object-type={projection.element.type}
        data-canvas-identity-issue={projection.identityIssue ?? undefined}
        onPointerDown={event => beginMove(event, projection)}
        onPointerEnter={() => setHoveredObjectId(objectId)}
        onPointerLeave={() => setHoveredObjectId(current => current === objectId ? null : current)}
      >
        <span className="visual-editor-canvas__object-label" aria-hidden="true">
          {projection.element.key}
        </span>

        {selected && objectId !== null ? (
          <div className="visual-editor-canvas__adorners" aria-hidden="true">
            {selection.length === 1 ? (
              <>
                {(['northWest', 'northEast', 'southEast', 'southWest'] as const).map(handle => (
                  <button
                    key={handle}
                    type="button"
                    tabIndex={-1}
                    className={`visual-editor-canvas__resize-handle handle-${handle}`}
                    data-canvas-resize-handle={handle}
                    onPointerDown={event => beginResize(event, projection, handle)}
                    aria-label={`Resize ${handle}`}
                  />
                ))}
              </>
            ) : null}
            <button
              type="button"
              tabIndex={-1}
              className="visual-editor-canvas__rotate-handle"
              data-canvas-rotate-handle="true"
              onPointerDown={event => beginRotate(event, projection)}
              aria-label="Rotate selection"
            />
          </div>
        ) : null}

        {projection.children.map(child => renderProjection(child, childAncestorMoving))}
      </div>
    );
  };

  return (
    <section className="visual-editor-canvas" data-testid="visual-editor-canvas">
      <div className="visual-editor-canvas__toolbar" role="toolbar" aria-label="Canvas controls">
        <button
          type="button"
          onClick={() => onUiIntent({ kind: 'viewport.change', viewport: zoomViewport(viewport, 1 / 1.2) })}
          aria-label="Zoom out"
        >−</button>
        <button
          type="button"
          onClick={() => onUiIntent({ kind: 'viewport.change', viewport: { zoom: 1, panX: 0, panY: 0 } })}
          aria-label="Reset viewport"
        >100%</button>
        <button
          type="button"
          onClick={() => onUiIntent({ kind: 'viewport.change', viewport: zoomViewport(viewport, 1.2) })}
          aria-label="Zoom in"
        >+</button>
        <button
          type="button"
          aria-pressed={gridEnabled}
          onClick={() => setGridEnabled(value => !value)}
          data-testid="canvas-grid-toggle"
        >Grid</button>
        <button
          type="button"
          aria-pressed={snapEnabled}
          onClick={() => setSnapEnabled(value => !value)}
          data-testid="canvas-snap-toggle"
        >Snap</button>
        <span className="visual-editor-canvas__toolbar-spacer" />
        <button
          type="button"
          disabled={selection.length === 0}
          onClick={() => emitMutationForSelection({ kind: 'object.duplicate', objectIds: selection })}
        >Duplicate</button>
        <button
          type="button"
          disabled={selection.length === 0}
          onClick={() => emitMutationForSelection({ kind: 'object.delete', objectIds: selection })}
        >Delete</button>
        <button
          type="button"
          disabled={selection.length === 0}
          onClick={() => emitMutationForSelection({ kind: 'object.zOrder', objectIds: selection, operation: 'sendToBack' })}
          aria-label="Send to back"
        >⇤</button>
        <button
          type="button"
          disabled={selection.length === 0}
          onClick={() => emitMutationForSelection({ kind: 'object.zOrder', objectIds: selection, operation: 'sendBackward' })}
          aria-label="Send backward"
        >←</button>
        <button
          type="button"
          disabled={selection.length === 0}
          onClick={() => emitMutationForSelection({ kind: 'object.zOrder', objectIds: selection, operation: 'bringForward' })}
          aria-label="Bring forward"
        >→</button>
        <button
          type="button"
          disabled={selection.length === 0}
          onClick={() => emitMutationForSelection({ kind: 'object.zOrder', objectIds: selection, operation: 'bringToFront' })}
          aria-label="Bring to front"
        >⇥</button>
      </div>

      <div
        ref={surfaceRef}
        className={`visual-editor-canvas__surface${gridEnabled ? ' has-grid' : ''}`}
        style={surfaceStyle}
        tabIndex={0}
        role="application"
        aria-label={`Visual editor canvas for ${screen.name}`}
        onPointerDown={handleSurfacePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={finishInteraction}
        onPointerCancel={() => setInteraction(null)}
        onWheel={handleWheel}
        onKeyDown={handleKeyDown}
      >
        <div className="visual-editor-canvas__viewport" style={viewportStyle}>
          {projectedElements.map(projection => renderProjection(projection, false))}
        </div>
      </div>

      <footer className="visual-editor-canvas__status" aria-live="polite">
        <span>{Math.round(effectiveViewport.zoom * 100)}%</span>
        <span>{selection.length} selected</span>
        {interaction?.kind ? <span>{interaction.kind}</span> : null}
      </footer>
    </section>
  );
}

function pointFromPointer(event: ReactPointerEvent<HTMLElement>): VisualEditorPoint {
  return Object.freeze({ x: event.clientX, y: event.clientY });
}

function subtractPoints(current: VisualEditorPoint, start: VisualEditorPoint): VisualEditorPoint {
  return Object.freeze({ x: current.x - start.x, y: current.y - start.y });
}

function sameBounds(left: VisualEditorBounds, right: VisualEditorBounds): boolean {
  return left.x === right.x &&
    left.y === right.y &&
    left.width === right.width &&
    left.height === right.height;
}

function sameViewport(left: VisualEditorViewport, right: VisualEditorViewport): boolean {
  return left.zoom === right.zoom && left.panX === right.panX && left.panY === right.panY;
}
