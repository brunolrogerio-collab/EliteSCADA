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
  >
    <div
      className="runtime-logical-stage"
      data-testid="runtime-logical-stage"
      style={{
        width: designSize.width,
        height: designSize.height,
        left: transform.offsetX,
        top: transform.offsetY,
        transform: rotateToLandscape
          ? `translate(-50%, -50%) rotate(90deg) scale(${transform.scale})`
          : `scale(${transform.scale})`,
        transformOrigin: rotateToLandscape ? 'center' : '0 0'
      }}
    >
      {children}
    </div>
  </div>;
}
