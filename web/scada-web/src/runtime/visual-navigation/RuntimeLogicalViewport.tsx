import React, { useLayoutEffect, useRef, useState } from 'react';
import {
  calculateRuntimeLogicalTransform,
  type RuntimeLogicalSize
} from './runtimeLogicalCanvas';

export function RuntimeLogicalViewport({
  designSize,
  mobileOrientation = 'landscape',
  children
}: Readonly<{
  designSize: RuntimeLogicalSize;
  mobileOrientation?: 'landscape' | 'portrait';
  children: React.ReactNode;
}>) {
  const viewportRef = useRef<HTMLDivElement>(null);
  const [viewport, setViewport] = useState({ width: 0, height: 0, coarsePointer: false });
  const [zoom, setZoom] = useState(1);

  useLayoutEffect(() => {
    const element = viewportRef.current;
    if (!element) return;
    const coarsePointer = window.matchMedia('(pointer: coarse)');
    const measure = () => setViewport({ width: element.clientWidth, height: element.clientHeight, coarsePointer: coarsePointer.matches });
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    coarsePointer.addEventListener('change', measure);
    return () => { observer.disconnect(); coarsePointer.removeEventListener('change', measure); };
  }, []);

  const rotateToLandscape = mobileOrientation === 'landscape' && viewport.coarsePointer && viewport.height > viewport.width;
  const transform = rotateToLandscape
    ? {
        scale: Math.min(viewport.width / designSize.height, viewport.height / designSize.width),
        offsetX: viewport.width / 2,
        offsetY: viewport.height / 2
      }
    : calculateRuntimeLogicalTransform(viewport.width, viewport.height, designSize.width, designSize.height);

  return <div
    ref={viewportRef}
    className="runtime-logical-viewport"
    data-testid="runtime-logical-viewport"
    data-design-width={designSize.width}
    data-design-height={designSize.height}
    data-runtime-scale={transform.scale}
    data-mobile-orientation={mobileOrientation}
    data-mobile-rotated={rotateToLandscape || undefined}
    style={{ overflow: zoom > 1 ? 'auto' : undefined }}
  >
    {viewport.coarsePointer && <div style={{ position: 'sticky', top: 0, left: 0, zIndex: 10, display: 'flex', gap: 4 }} role="toolbar" aria-label="Zoom">
      <button type="button" aria-label="Zoom −" onClick={() => setZoom(value => Math.max(1, value - .25))}>−</button>
      <button type="button" aria-label="Zoom reset" onClick={() => setZoom(1)}>{Math.round(zoom * 100)}%</button>
      <button type="button" aria-label="Zoom +" onClick={() => setZoom(value => Math.min(4, value + .25))}>+</button>
    </div>}
    <div
      className="runtime-logical-stage"
      data-testid="runtime-logical-stage"
      style={{
        width: designSize.width,
        height: designSize.height,
        left: transform.offsetX,
        top: transform.offsetY,
        transform: rotateToLandscape
          ? `translate(-50%, -50%) rotate(90deg) scale(${transform.scale * zoom})`
          : `scale(${transform.scale * zoom})`,
        transformOrigin: rotateToLandscape ? 'center' : '0 0'
      }}
    >
      {children}
    </div>
  </div>;
}
