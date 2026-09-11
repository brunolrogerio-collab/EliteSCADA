import React, { useLayoutEffect, useRef, useState } from 'react';
import {
  calculateRuntimeLogicalTransform,
  type RuntimeLogicalSize
} from './runtimeLogicalCanvas';

export function RuntimeLogicalViewport({
  designSize,
  children
}: Readonly<{
  designSize: RuntimeLogicalSize;
  children: React.ReactNode;
}>) {
  const viewportRef = useRef<HTMLDivElement>(null);
  const [viewport, setViewport] = useState({ width: 0, height: 0 });

  useLayoutEffect(() => {
    const element = viewportRef.current;
    if (!element) return;
    const measure = () => setViewport({ width: element.clientWidth, height: element.clientHeight });
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  const transform = calculateRuntimeLogicalTransform(
    viewport.width,
    viewport.height,
    designSize.width,
    designSize.height
  );

  return <div
    ref={viewportRef}
    className="runtime-logical-viewport"
    data-testid="runtime-logical-viewport"
    data-design-width={designSize.width}
    data-design-height={designSize.height}
    data-runtime-scale={transform.scale}
  >
    <div
      className="runtime-logical-stage"
      data-testid="runtime-logical-stage"
      style={{
        width: designSize.width,
        height: designSize.height,
        left: transform.offsetX,
        top: transform.offsetY,
        transform: `scale(${transform.scale})`
      }}
    >
      {children}
    </div>
  </div>;
}
