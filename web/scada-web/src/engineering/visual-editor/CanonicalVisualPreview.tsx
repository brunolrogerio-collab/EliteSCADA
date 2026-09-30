import React, { useMemo } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { DynamoEngineering, VisualElementEngineering } from '../types';
import { CanonicalVisualRenderer, type VisualAssetUrlResolver } from './CanonicalVisualRenderer';
import './CanonicalVisualPreview.css';

export type CanonicalVisualPreviewVariant = 'thumbnail' | 'detail';

export function CanonicalVisualPreview({
  elements, dynamoDefinitions, locale, width, height, emptyLabel, visualAssetUrl, variant = 'detail', testId
}: Readonly<{
  elements: readonly VisualElementEngineering[] | null | undefined;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  locale: EngineeringLocale;
  width?: number | null;
  height?: number | null;
  emptyLabel: string;
  visualAssetUrl?: VisualAssetUrlResolver;
  variant?: CanonicalVisualPreviewVariant;
  testId?: string;
}>) {
  const authored = useMemo(() => previewSize(elements, width, height), [elements, width, height]);
  const viewport = variant === 'thumbnail' ? { width: 66, height: 48 } : { width: 360, height: 220 };
  const scale = Math.min(viewport.width / authored.width, viewport.height / authored.height, variant === 'thumbnail' ? 1 : 2.25);
  return <div className={`canonical-visual-preview canonical-visual-preview--${variant}`}
    data-testid={testId ?? `canonical-visual-preview-${variant}`} data-preview-width={authored.width} data-preview-height={authored.height}>
    <div className="canonical-visual-preview__scaled" style={{ width: authored.width * scale, height: authored.height * scale }}>
      <div className="canonical-visual-preview__frame" style={{ width: authored.width, height: authored.height, transform: `scale(${scale})`, transformOrigin: 'top left' }}>
        <CanonicalVisualRenderer elements={elements} emptyLabel={emptyLabel} locale={locale} dynamoDefinitions={dynamoDefinitions}
          visualAssetUrl={visualAssetUrl} showTechnicalFallbackText={false} liveBindings={false} />
      </div>
    </div>
  </div>;
}

function previewSize(elements: readonly VisualElementEngineering[] | null | undefined, explicitWidth?: number | null, explicitHeight?: number | null) {
  const width = positive(explicitWidth), height = positive(explicitHeight);
  if (width && height) return Object.freeze({ width, height });
  let maxX = 1, maxY = 1;
  const visit = (element: VisualElementEngineering, offsetX = 0, offsetY = 0) => {
    const x = offsetX + numeric(element.properties?.x, 0), y = offsetY + numeric(element.properties?.y, 0);
    const elementWidth = Math.max(1, numeric(element.properties?.width, 80)), elementHeight = Math.max(1, numeric(element.properties?.height, 60));
    maxX = Math.max(maxX, x + elementWidth); maxY = Math.max(maxY, y + elementHeight);
    for (const child of element.children ?? []) visit(child, x, y);
  };
  for (const element of elements ?? []) visit(element);
  return Object.freeze({ width: width ?? Math.min(Math.max(maxX, 64), 1920), height: height ?? Math.min(Math.max(maxY, 48), 1080) });
}
function positive(value: number | null | undefined): number | null { return Number.isFinite(value) && Number(value) > 0 ? Number(value) : null; }
function numeric(value: unknown, fallback: number): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback; }
