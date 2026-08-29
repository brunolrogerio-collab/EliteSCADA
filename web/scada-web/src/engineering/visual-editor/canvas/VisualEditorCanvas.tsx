import React, { useMemo, useRef, useState } from 'react';
import type {
  CSSProperties,
  KeyboardEvent as ReactKeyboardEvent,
  PointerEvent as ReactPointerEvent,
  WheelEvent as ReactWheelEvent
} from 'react';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../../visual-runtime';
import type {
  VisualEditorBounds,
  VisualEditorCanvasContractProps,
  VisualEditorMutationIntent,
  VisualEditorPoint,
  VisualEditorViewport
} from '../visualEditorContracts';
import { polygonBounds, polygonPointsAttribute, readPolygonPoints } from '../polygonGeometry';
import {
  DEFAULT_CANVAS_GRID_SIZE,
  clientDeltaToCanvas,
  collapseHierarchySelection,
  hasMeaningfulDelta,
  nextSelection,
  normalizeSelection,
  normalizeViewport,
  panViewport,
  projectCanvasElements,
  resizeBounds,
  rotationDeltaDegrees,
  selectionModeFromModifiers,
  snapPoint,
  snapScalar,
  zoomViewport,
  type CanvasElementProjection,
  type CanvasResizeHandle
} from './canvasInteractionModel';
import './visual-editor-canvas.css';

type MoveInteraction = Readonly<{
  kind: 'move'; pointerId: number; startClient: VisualEditorPoint; objectIds: readonly string[]; delta: VisualEditorPoint;
}>;
type ResizeInteraction = Readonly<{
  kind: 'resize'; pointerId: number; startClient: VisualEditorPoint; objectId: string; handle: CanvasResizeHandle; startBounds: VisualEditorBounds; bounds: VisualEditorBounds;
}>;
type RotateInteraction = Readonly<{
  kind: 'rotate'; pointerId: number; startClient: VisualEditorPoint; centerClient: VisualEditorPoint; objectIds: readonly string[]; deltaDegrees: number;
}>;
type PanInteraction = Readonly<{
  kind: 'pan'; pointerId: number; startClient: VisualEditorPoint; startViewport: VisualEditorViewport; viewport: VisualEditorViewport;
}>;
type PolygonVertexInteraction = Readonly<{
  kind: 'polygon-vertex';
  pointerId: number;
  startClient: VisualEditorPoint;
  objectId: string;
  vertexIndex: number;
  startPoints: readonly VisualEditorPoint[];
  points: readonly VisualEditorPoint[];
  pointScaleX: number;
  pointScaleY: number;
}>;
type CanvasInteraction = MoveInteraction | ResizeInteraction | RotateInteraction | PanInteraction | PolygonVertexInteraction | null;

const CANVAS_CONTENT_WIDTH = 6000;
const CANVAS_CONTENT_HEIGHT = 4000;

export function VisualEditorCanvas({
  screen,
  selectedObjectIds,
  viewport,
  onUiIntent,
  onMutationIntent,
  polygonToolActive = false,
  onPolygonToolCancel
}: VisualEditorCanvasContractProps) {
  const surfaceRef = useRef<HTMLDivElement | null>(null);
  const [gridEnabled, setGridEnabled] = useState(true);
  const [snapEnabled, setSnapEnabled] = useState(true);
  const [hoveredObjectId, setHoveredObjectId] = useState<string | null>(null);
  const [interaction, setInteraction] = useState<CanvasInteraction>(null);
  const [polygonDraftPoints, setPolygonDraftPoints] = useState<readonly VisualEditorPoint[]>(Object.freeze([]));
  const [polygonHoverPoint, setPolygonHoverPoint] = useState<VisualEditorPoint | null>(null);
  const [selectedVertex, setSelectedVertex] = useState<Readonly<{ objectId: string; index: number }> | null>(null);

  const projectedElements = useMemo(() => projectCanvasElements(screen), [screen]);
  const selection = useMemo(() => normalizeSelection(selectedObjectIds), [selectedObjectIds]);
  const effectiveViewport = interaction?.kind === 'pan' ? interaction.viewport : normalizeViewport(viewport);
  const selectionSet = useMemo(() => new Set(selection), [selection]);
  const selectedProjection = selection.length === 1 ? findProjection(projectedElements, selection[0]) : null;
  const selectedPolygonPoints = selectedProjection?.element.type === BUILTIN_VISUAL_OBJECT_TYPES.polygon
    ? readPolygonPoints(selectedProjection.element)
    : Object.freeze([]);

  const emitSelection = (objectIds: readonly string[], mode: 'replace' | 'add' | 'toggle'): void => {
    onUiIntent({ kind: 'selection.change', objectIds, mode });
  };

  const emitMutationForSelection = (intent: VisualEditorMutationIntent): void => {
    if (selection.length === 0) return;
    onMutationIntent(intent);
  };

  const cancelPolygon = (): void => {
    setPolygonDraftPoints(Object.freeze([]));
    setPolygonHoverPoint(null);
    onPolygonToolCancel?.();
  };

  const finishPolygon = (): void => {
    if (polygonDraftPoints.length < 3) return;
    try {
      onMutationIntent({ kind: 'polygon.create', points: polygonDraftPoints });
      cancelPolygon();
    } catch {
      // Coordinator reducer will surface the canonical validation error.
    }
  };

  const appendPolygonPoint = (point: VisualEditorPoint): void => {
    const nextPoint = snapEnabled ? snapPoint(point, DEFAULT_CANVAS_GRID_SIZE) : point;
    setPolygonDraftPoints(current => {
      const last = current[current.length - 1];
      if (last && last.x === nextPoint.x && last.y === nextPoint.y) return current;
      return Object.freeze([...current, Object.freeze(nextPoint)]);
    });
  };

  const beginMove = (event: ReactPointerEvent<HTMLElement>, projection: CanvasElementProjection): void => {
    if (polygonToolActive || event.button !== 0 || projection.objectId === null) return;
    event.stopPropagation();
    const mode = selectionModeFromModifiers(event);
    const preserveExistingSelection = mode === 'replace' && selectionSet.has(projection.objectId);
    const requestedSelection = preserveExistingSelection ? selection : nextSelection(selection, projection.objectId, mode);
    const dragObjectIds = collapseHierarchySelection(screen.elements ?? [], requestedSelection);
    if (!preserveExistingSelection) emitSelection([projection.objectId], mode);
    event.currentTarget.setPointerCapture(event.pointerId);
    setInteraction({ kind: 'move', pointerId: event.pointerId, startClient: pointFromPointer(event), objectIds: dragObjectIds, delta: Object.freeze({ x: 0, y: 0 }) });
  };

  const beginResize = (event: ReactPointerEvent<HTMLButtonElement>, projection: CanvasElementProjection, handle: CanvasResizeHandle): void => {
    if (polygonToolActive || event.button !== 0 || projection.objectId === null || selection.length !== 1) return;
    event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    const startBounds = Object.freeze({ x: projection.geometry.x, y: projection.geometry.y, width: projection.geometry.width, height: projection.geometry.height });
    setInteraction({ kind: 'resize', pointerId: event.pointerId, startClient: pointFromPointer(event), objectId: projection.objectId, handle, startBounds, bounds: startBounds });
  };

  const beginRotate = (event: ReactPointerEvent<HTMLButtonElement>, projection: CanvasElementProjection): void => {
    if (polygonToolActive || event.button !== 0 || projection.objectId === null) return;
    event.stopPropagation();
    const objectNode = event.currentTarget.closest<HTMLElement>('[data-canvas-object-id]');
    if (!objectNode) return;
    const rect = objectNode.getBoundingClientRect();
    const centerClient = Object.freeze({ x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 });
    const localSelection = selectionSet.has(projection.objectId) ? selection : Object.freeze([projection.objectId]);
    if (!selectionSet.has(projection.objectId)) emitSelection([projection.objectId], 'replace');
    event.currentTarget.setPointerCapture(event.pointerId);
    setInteraction({ kind: 'rotate', pointerId: event.pointerId, startClient: pointFromPointer(event), centerClient, objectIds: localSelection, deltaDegrees: 0 });
  };

  const beginPolygonVertex = (
    event: ReactPointerEvent<HTMLButtonElement>,
    projection: CanvasElementProjection,
    vertexIndex: number,
    points: readonly VisualEditorPoint[]
  ): void => {
    if (event.button !== 0 || !projection.objectId || points.length < 3) return;
    event.stopPropagation();
    const bounds = polygonBounds(points);
    const pointScaleX = projection.geometry.width / Math.max(bounds.width, 1);
    const pointScaleY = projection.geometry.height / Math.max(bounds.height, 1);
    setSelectedVertex(Object.freeze({ objectId: projection.objectId, index: vertexIndex }));
    event.currentTarget.setPointerCapture(event.pointerId);
    setInteraction({
      kind: 'polygon-vertex', pointerId: event.pointerId, startClient: pointFromPointer(event), objectId: projection.objectId,
      vertexIndex, startPoints: points, points, pointScaleX, pointScaleY
    });
  };

  const handleSurfacePointerDown = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (polygonToolActive && event.button === 0) {
      event.preventDefault();
      appendPolygonPoint(clientPointToCanvas(event, effectiveViewport, surfaceRef.current));
      emitSelection([], 'replace');
      return;
    }
    if (event.target !== event.currentTarget) return;
    if (event.button === 1 || (event.button === 0 && event.altKey)) {
      event.preventDefault();
      event.currentTarget.setPointerCapture(event.pointerId);
      const startViewport = normalizeViewport(viewport);
      setInteraction({ kind: 'pan', pointerId: event.pointerId, startClient: pointFromPointer(event), startViewport, viewport: startViewport });
      return;
    }
    if (event.button === 0) emitSelection([], 'replace');
  };

  const handlePointerMove = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (polygonToolActive && !interaction) {
      const point = clientPointToCanvas(event, effectiveViewport, surfaceRef.current);
      setPolygonHoverPoint(snapEnabled ? snapPoint(point, DEFAULT_CANVAS_GRID_SIZE) : point);
    }
    if (!interaction || event.pointerId !== interaction.pointerId) return;
    const current = pointFromPointer(event);
    if (interaction.kind === 'pan') {
      setInteraction({ ...interaction, viewport: panViewport(interaction.startViewport, subtractPoints(current, interaction.startClient)) });
      return;
    }
    if (interaction.kind === 'rotate') {
      let deltaDegrees = rotationDeltaDegrees(interaction.centerClient, interaction.startClient, current);
      if (event.shiftKey) deltaDegrees = snapScalar(deltaDegrees, 15);
      setInteraction({ ...interaction, deltaDegrees });
      return;
    }
    const deltaClient = subtractPoints(current, interaction.startClient);
    const delta = clientDeltaToCanvas(deltaClient, effectiveViewport, snapEnabled, DEFAULT_CANVAS_GRID_SIZE);
    if (interaction.kind === 'move') {
      setInteraction({ ...interaction, delta });
      return;
    }
    if (interaction.kind === 'resize') {
      setInteraction({ ...interaction, bounds: resizeBounds(interaction.startBounds, delta, interaction.handle) });
      return;
    }
    const pointDelta = Object.freeze({
      x: delta.x / Math.max(interaction.pointScaleX, Number.EPSILON),
      y: delta.y / Math.max(interaction.pointScaleY, Number.EPSILON)
    });
    setInteraction({
      ...interaction,
      points: Object.freeze(interaction.startPoints.map((point, index) => index === interaction.vertexIndex
        ? Object.freeze({ x: point.x + pointDelta.x, y: point.y + pointDelta.y })
        : point))
    });
  };

  const finishInteraction = (event: ReactPointerEvent<HTMLDivElement>): void => {
    if (!interaction || event.pointerId !== interaction.pointerId) return;
    switch (interaction.kind) {
      case 'move':
        if (interaction.objectIds.length > 0 && hasMeaningfulDelta(interaction.delta)) onMutationIntent({ kind: 'object.move', objectIds: interaction.objectIds, delta: interaction.delta });
        break;
      case 'resize':
        if (!sameBounds(interaction.startBounds, interaction.bounds)) onMutationIntent({ kind: 'object.resize', objectId: interaction.objectId, bounds: interaction.bounds });
        break;
      case 'rotate':
        if (interaction.objectIds.length > 0 && Math.abs(interaction.deltaDegrees) > Number.EPSILON) onMutationIntent({ kind: 'object.rotate', objectIds: interaction.objectIds, deltaDegrees: interaction.deltaDegrees });
        break;
      case 'pan':
        if (!sameViewport(interaction.startViewport, interaction.viewport)) onUiIntent({ kind: 'viewport.change', viewport: interaction.viewport });
        break;
      case 'polygon-vertex':
        if (!pointsEqual(interaction.startPoints, interaction.points)) onMutationIntent({ kind: 'polygon.points.set', objectId: interaction.objectId, points: interaction.points });
        break;
    }
    setInteraction(null);
  };

  const handleWheel = (event: ReactWheelEvent<HTMLDivElement>): void => {
    event.preventDefault();
    if (event.ctrlKey || event.metaKey) {
      onUiIntent({ kind: 'viewport.change', viewport: zoomViewport(viewport, event.deltaY < 0 ? 1.1 : 1 / 1.1) });
      return;
    }
    onUiIntent({ kind: 'viewport.change', viewport: panViewport(viewport, { x: -event.deltaX, y: -event.deltaY }) });
  };

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>): void => {
    if (event.key === 'Escape') {
      setInteraction(null);
      if (polygonToolActive) cancelPolygon();
      else emitSelection([], 'replace');
      return;
    }
    if (polygonToolActive) {
      if (event.key === 'Enter' && polygonDraftPoints.length >= 3) {
        event.preventDefault();
        finishPolygon();
      }
      return;
    }
    if (selection.length === 0) return;
    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault(); onMutationIntent({ kind: 'object.delete', objectIds: selection }); return;
    }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'd') {
      event.preventDefault(); onMutationIntent({ kind: 'object.duplicate', objectIds: selection }); return;
    }
    const distance = (snapEnabled ? DEFAULT_CANVAS_GRID_SIZE : 1) * (event.shiftKey ? 5 : 1);
    const arrowDelta: VisualEditorPoint | null = event.key === 'ArrowLeft' ? { x: -distance, y: 0 }
      : event.key === 'ArrowRight' ? { x: distance, y: 0 }
      : event.key === 'ArrowUp' ? { x: 0, y: -distance }
      : event.key === 'ArrowDown' ? { x: 0, y: distance } : null;
    if (arrowDelta) { event.preventDefault(); onMutationIntent({ kind: 'object.move', objectIds: selection, delta: arrowDelta }); }
  };

  const addPolygonVertex = (): void => {
    if (!selectedProjection?.objectId || selectedProjection.element.type !== BUILTIN_VISUAL_OBJECT_TYPES.polygon || selectedPolygonPoints.length < 3) return;
    const index = selectedVertex?.objectId === selectedProjection.objectId ? selectedVertex.index : selectedPolygonPoints.length - 1;
    const nextIndex = (index + 1) % selectedPolygonPoints.length;
    const current = selectedPolygonPoints[index];
    const next = selectedPolygonPoints[nextIndex];
    const midpoint = Object.freeze({ x: (current.x + next.x) / 2, y: (current.y + next.y) / 2 });
    const points = [...selectedPolygonPoints];
    points.splice(index + 1, 0, midpoint);
    onMutationIntent({ kind: 'polygon.points.set', objectId: selectedProjection.objectId, points });
    setSelectedVertex(Object.freeze({ objectId: selectedProjection.objectId, index: index + 1 }));
  };

  const removePolygonVertex = (): void => {
    if (!selectedProjection?.objectId || selectedPolygonPoints.length <= 3 || selectedVertex?.objectId !== selectedProjection.objectId) return;
    const points = selectedPolygonPoints.filter((_, index) => index !== selectedVertex.index);
    onMutationIntent({ kind: 'polygon.points.set', objectId: selectedProjection.objectId, points });
    setSelectedVertex(null);
  };

  const viewportStyle = {
    transform: `translate(${effectiveViewport.panX}px, ${effectiveViewport.panY}px) scale(${effectiveViewport.zoom})`,
    transformOrigin: '0 0', width: `${CANVAS_CONTENT_WIDTH}px`, height: `${CANVAS_CONTENT_HEIGHT}px`
  } satisfies CSSProperties;
  const surfaceStyle = {
    '--visual-editor-grid-size': `${DEFAULT_CANVAS_GRID_SIZE * effectiveViewport.zoom}px`,
    '--visual-editor-grid-pan-x': `${effectiveViewport.panX}px`, '--visual-editor-grid-pan-y': `${effectiveViewport.panY}px`
  } as CSSProperties;

  const renderProjection = (projection: CanvasElementProjection, ancestorMovesWithSelection: boolean): React.ReactNode => {
    const objectId = projection.objectId;
    const selected = objectId !== null && selectionSet.has(objectId);
    const hovered = objectId !== null && hoveredObjectId === objectId;
    const moveTargeted = interaction?.kind === 'move' && objectId !== null && interaction.objectIds.includes(objectId);
    const applyMovePreview = moveTargeted && !ancestorMovesWithSelection;
    const resizeTargeted = interaction?.kind === 'resize' && objectId === interaction.objectId;
    const rotateTargeted = interaction?.kind === 'rotate' && objectId !== null && interaction.objectIds.includes(objectId);
    const geometry = resizeTargeted ? { ...projection.geometry, ...interaction.bounds } : projection.geometry;
    const previewX = geometry.x + (applyMovePreview && interaction?.kind === 'move' ? interaction.delta.x : 0);
    const previewY = geometry.y + (applyMovePreview && interaction?.kind === 'move' ? interaction.delta.y : 0);
    const previewRotation = geometry.rotation + (rotateTargeted && interaction?.kind === 'rotate' ? interaction.deltaDegrees : 0);
    const style = {
      left: `${previewX}px`, top: `${previewY}px`, width: `${Math.max(geometry.width, 1)}px`, height: `${Math.max(geometry.height, 1)}px`,
      zIndex: geometry.zIndex, display: geometry.visible ? undefined : 'none', transform: `rotate(${previewRotation}deg) scale(${geometry.scaleX}, ${geometry.scaleY})`, transformOrigin: 'center'
    } satisfies CSSProperties;
    const childAncestorMoving = ancestorMovesWithSelection || moveTargeted;
    const polygonPoints = projection.element.type === BUILTIN_VISUAL_OBJECT_TYPES.polygon
      ? (interaction?.kind === 'polygon-vertex' && interaction.objectId === objectId ? interaction.points : readPolygonPoints(projection.element))
      : Object.freeze([]);
    const pointBounds = polygonBounds(polygonPoints);
    const pointScaleX = geometry.width / Math.max(pointBounds.width, 1);
    const pointScaleY = geometry.height / Math.max(pointBounds.height, 1);

    return <div
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
      {projection.element.type === BUILTIN_VISUAL_OBJECT_TYPES.polygon && polygonPoints.length >= 3 ? <svg className="visual-editor-canvas__polygon-shape" viewBox={`0 0 ${Math.max(pointBounds.width, 1)} ${Math.max(pointBounds.height, 1)}`} preserveAspectRatio="none" aria-hidden="true">
        <polygon points={polygonPointsAttribute(polygonPoints.map(point => ({ x: point.x - pointBounds.minX, y: point.y - pointBounds.minY })))} />
      </svg> : null}
      <span className="visual-editor-canvas__object-label" aria-hidden="true">{projection.element.key}</span>

      {selected && objectId !== null ? <div className="visual-editor-canvas__adorners" aria-hidden="true">
        {selection.length === 1 ? <>{(['northWest', 'northEast', 'southEast', 'southWest'] as const).map(handle => <button key={handle} type="button" tabIndex={-1} className={`visual-editor-canvas__resize-handle handle-${handle}`} data-canvas-resize-handle={handle} onPointerDown={event => beginResize(event, projection, handle)} aria-label={`Resize ${handle}`} />)}</> : null}
        <button type="button" tabIndex={-1} className="visual-editor-canvas__rotate-handle" data-canvas-rotate-handle="true" onPointerDown={event => beginRotate(event, projection)} aria-label="Rotate selection" />
      </div> : null}

      {selected && objectId !== null && projection.element.type === BUILTIN_VISUAL_OBJECT_TYPES.polygon ? polygonPoints.map((point, index) => <button
        key={`vertex-${index}`}
        type="button"
        className={`visual-editor-canvas__polygon-vertex${selectedVertex?.objectId === objectId && selectedVertex.index === index ? ' is-selected' : ''}`}
        style={{ left: `${(point.x - pointBounds.minX) * pointScaleX}px`, top: `${(point.y - pointBounds.minY) * pointScaleY}px` }}
        aria-label={`Polygon vertex ${index + 1}`}
        data-polygon-vertex-index={index}
        onPointerDown={event => beginPolygonVertex(event, projection, index, polygonPoints)}
      />) : null}

      {projection.children.map(child => renderProjection(child, childAncestorMoving))}
    </div>;
  };

  const draftPreviewPoints = polygonToolActive && polygonHoverPoint
    ? [...polygonDraftPoints, polygonHoverPoint]
    : polygonDraftPoints;

  return <section className={`visual-editor-canvas${polygonToolActive ? ' is-polygon-tool' : ''}`} data-testid="visual-editor-canvas">
    <div className="visual-editor-canvas__toolbar" role="toolbar" aria-label="Canvas controls">
      <button type="button" onClick={() => onUiIntent({ kind: 'viewport.change', viewport: zoomViewport(viewport, 1 / 1.2) })} aria-label="Zoom out">−</button>
      <button type="button" onClick={() => onUiIntent({ kind: 'viewport.change', viewport: { zoom: 1, panX: 0, panY: 0 } })} aria-label="Reset viewport">100%</button>
      <button type="button" onClick={() => onUiIntent({ kind: 'viewport.change', viewport: zoomViewport(viewport, 1.2) })} aria-label="Zoom in">+</button>
      <button type="button" aria-pressed={gridEnabled} onClick={() => setGridEnabled(value => !value)} data-testid="canvas-grid-toggle">Grid</button>
      <button type="button" aria-pressed={snapEnabled} onClick={() => setSnapEnabled(value => !value)} data-testid="canvas-snap-toggle">Snap</button>
      {polygonToolActive ? <>
        <span className="visual-editor-canvas__polygon-status">Polygon · {polygonDraftPoints.length} points</span>
        <button type="button" disabled={polygonDraftPoints.length < 3} onClick={finishPolygon} data-testid="polygon-finish">Finish polygon</button>
        <button type="button" onClick={cancelPolygon}>Cancel polygon</button>
      </> : null}
      {!polygonToolActive && selectedProjection?.element.type === BUILTIN_VISUAL_OBJECT_TYPES.polygon ? <>
        <button type="button" onClick={addPolygonVertex}>+ Vertex</button>
        <button type="button" disabled={!selectedVertex || selectedPolygonPoints.length <= 3} onClick={removePolygonVertex}>− Vertex</button>
      </> : null}
      <span className="visual-editor-canvas__toolbar-spacer" />
      <button type="button" disabled={selection.length === 0 || polygonToolActive} onClick={() => emitMutationForSelection({ kind: 'object.duplicate', objectIds: selection })}>Duplicate</button>
      <button type="button" disabled={selection.length === 0 || polygonToolActive} onClick={() => emitMutationForSelection({ kind: 'object.delete', objectIds: selection })}>Delete</button>
      <button type="button" disabled={selection.length === 0 || polygonToolActive} onClick={() => emitMutationForSelection({ kind: 'object.zOrder', objectIds: selection, operation: 'sendToBack' })} aria-label="Send to back">⇤</button>
      <button type="button" disabled={selection.length === 0 || polygonToolActive} onClick={() => emitMutationForSelection({ kind: 'object.zOrder', objectIds: selection, operation: 'sendBackward' })} aria-label="Send backward">←</button>
      <button type="button" disabled={selection.length === 0 || polygonToolActive} onClick={() => emitMutationForSelection({ kind: 'object.zOrder', objectIds: selection, operation: 'bringForward' })} aria-label="Bring forward">→</button>
      <button type="button" disabled={selection.length === 0 || polygonToolActive} onClick={() => emitMutationForSelection({ kind: 'object.zOrder', objectIds: selection, operation: 'bringToFront' })} aria-label="Bring to front">⇥</button>
    </div>

    <div ref={surfaceRef} className={`visual-editor-canvas__surface${gridEnabled ? ' has-grid' : ''}`} style={surfaceStyle} tabIndex={0} role="application" aria-label={`Visual editor canvas for ${screen.name}`} onPointerDown={handleSurfacePointerDown} onPointerMove={handlePointerMove} onPointerUp={finishInteraction} onPointerCancel={() => setInteraction(null)} onDoubleClick={() => { if (polygonToolActive && polygonDraftPoints.length >= 3) finishPolygon(); }} onWheel={handleWheel} onKeyDown={handleKeyDown}>
      <div className="visual-editor-canvas__viewport" style={viewportStyle}>
        {projectedElements.map(projection => renderProjection(projection, false))}
        {polygonToolActive && draftPreviewPoints.length > 0 ? <svg className="visual-editor-canvas__polygon-draft" width={CANVAS_CONTENT_WIDTH} height={CANVAS_CONTENT_HEIGHT} aria-hidden="true">
          <polyline points={polygonPointsAttribute(draftPreviewPoints)} />
          {polygonDraftPoints.map((point, index) => <circle key={index} cx={point.x} cy={point.y} r={4} />)}
        </svg> : null}
      </div>
    </div>

    <footer className="visual-editor-canvas__status" aria-live="polite">
      <span>{Math.round(effectiveViewport.zoom * 100)}%</span><span>{selection.length} selected</span>{interaction?.kind ? <span>{interaction.kind}</span> : null}{polygonToolActive ? <span>polygon drawing</span> : null}
    </footer>
  </section>;
}

function pointFromPointer(event: ReactPointerEvent<HTMLElement>): VisualEditorPoint { return Object.freeze({ x: event.clientX, y: event.clientY }); }
function clientPointToCanvas(event: ReactPointerEvent<HTMLElement>, viewport: VisualEditorViewport, surface: HTMLElement | null): VisualEditorPoint {
  const normalized = normalizeViewport(viewport);
  const rect = surface?.getBoundingClientRect();
  const localX = event.clientX - (rect?.left ?? 0) - normalized.panX;
  const localY = event.clientY - (rect?.top ?? 0) - normalized.panY;
  return Object.freeze({ x: localX / normalized.zoom, y: localY / normalized.zoom });
}
function subtractPoints(current: VisualEditorPoint, start: VisualEditorPoint): VisualEditorPoint { return Object.freeze({ x: current.x - start.x, y: current.y - start.y }); }
function sameBounds(left: VisualEditorBounds, right: VisualEditorBounds): boolean { return left.x === right.x && left.y === right.y && left.width === right.width && left.height === right.height; }
function sameViewport(left: VisualEditorViewport, right: VisualEditorViewport): boolean { return left.zoom === right.zoom && left.panX === right.panX && left.panY === right.panY; }
function pointsEqual(left: readonly VisualEditorPoint[], right: readonly VisualEditorPoint[]): boolean { return left.length === right.length && left.every((point, index) => point.x === right[index]?.x && point.y === right[index]?.y); }
function findProjection(projections: readonly CanvasElementProjection[], objectId: string): CanvasElementProjection | null {
  for (const projection of projections) {
    if (projection.objectId === objectId) return projection;
    const child = findProjection(projection.children, objectId);
    if (child) return child;
  }
  return null;
}
