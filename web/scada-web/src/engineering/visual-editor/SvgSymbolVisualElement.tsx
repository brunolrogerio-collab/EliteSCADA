import React from 'react';
import type { VisualElementEngineering } from '../types';
import { VISUAL_PROPERTY_KEYS, type VisualPropertyValue } from '../../visual-runtime';
import {
  normalizeSvgColor,
  readSvgPaintOverrides,
  svgSemanticDynamicPropertyKey,
  svgSymbolPropertyIsDriven
} from './svgSymbolModel';

const markupCache = new Map<string, Promise<string>>();

export function SvgSymbolVisualElement({
  element,
  values,
  assetUrl
}: {
  element: VisualElementEngineering;
  values: Readonly<Record<string, VisualPropertyValue>>;
  assetUrl: string;
}) {
  const [markup, setMarkup] = React.useState<string | null>(null);
  React.useEffect(() => {
    let cancelled = false;
    const request = markupCache.get(assetUrl) ?? fetch(assetUrl, { credentials: 'same-origin' }).then(async response => {
      if (!response.ok) throw new Error(`SVG asset request failed with HTTP ${response.status}.`);
      return response.text();
    });
    markupCache.set(assetUrl, request);
    void request.then(source => {
      if (!cancelled) setMarkup(applySvgInstancePaint(source, element, values));
    }).catch(() => {
      if (!cancelled) setMarkup(null);
    });
    return () => { cancelled = true; };
  }, [assetUrl, element, values]);

  if (!markup) return null;
  return <span
    className="visual-editor-svg-symbol__markup"
    aria-hidden="true"
    style={{ display: 'block', width: '100%', height: '100%', pointerEvents: 'none' }}
    dangerouslySetInnerHTML={{ __html: markup }}
  />;
}

export function applySvgInstancePaint(
  source: string,
  element: VisualElementEngineering,
  values: Readonly<Record<string, VisualPropertyValue>>
): string {
  const document = new DOMParser().parseFromString(source, 'image/svg+xml');
  const root = document.documentElement;
  if (root.localName !== 'svg' || root.namespaceURI !== 'http://www.w3.org/2000/svg' ||
      document.querySelector('parsererror,script,foreignObject,iframe,object,embed')) {
    throw new Error('SVG asset content is not an accepted static SVG.');
  }

  for (const node of Array.from(root.querySelectorAll('*'))) {
    for (const attribute of Array.from(node.attributes)) {
      if (attribute.name.toLowerCase().startsWith('on')) throw new Error('SVG active content is not allowed.');
      if ((attribute.localName === 'href') && !attribute.value.trim().startsWith('#'))
        throw new Error('SVG external references are not allowed.');
    }
  }

  const overrides = readSvgPaintOverrides(element);
  const fillDriven = svgSymbolPropertyIsDriven(element, VISUAL_PROPERTY_KEYS.fillColor);
  const strokeDriven = svgSymbolPropertyIsDriven(element, VISUAL_PROPERTY_KEYS.strokeColor);
  const widthDriven = svgSymbolPropertyIsDriven(element, VISUAL_PROPERTY_KEYS.strokeWidth);
  const globalFill = fillDriven ? normalizeSvgColor(String(values[VISUAL_PROPERTY_KEYS.fillColor] ?? '')) : null;
  const globalStroke = strokeDriven ? normalizeSvgColor(String(values[VISUAL_PROPERTY_KEYS.strokeColor] ?? '')) : null;
  const globalWidth = widthDriven ? Number(values[VISUAL_PROPERTY_KEYS.strokeWidth]) : null;

  for (const node of Array.from(root.querySelectorAll<SVGElement>('*'))) {
    const slot = semanticSlot(node);
    const slotOverride = slot ? overrides.slots?.[slot] : undefined;
    const sourceFill = presentationValue(node, 'fill');
    const sourceStroke = presentationValue(node, 'stroke');

    const normalizedFill = normalizeSvgColor(sourceFill);
    const normalizedStroke = normalizeSvgColor(sourceStroke);
    const dynamicFill = slot
      ? normalizeSvgColor(String(values[svgSemanticDynamicPropertyKey(slot, 'fill')] ?? ''))
      : null;
    const dynamicStroke = slot
      ? normalizeSvgColor(String(values[svgSemanticDynamicPropertyKey(slot, 'stroke')] ?? ''))
      : null;
    const dynamicStrokeWidthValue = slot
      ? values[svgSemanticDynamicPropertyKey(slot, 'strokeWidth')]
      : undefined;
    const dynamicStrokeWidth = typeof dynamicStrokeWidthValue === 'number' &&
      Number.isFinite(dynamicStrokeWidthValue) &&
      dynamicStrokeWidthValue >= 0
      ? dynamicStrokeWidthValue
      : null;
    const fill = dynamicFill ?? slotOverride?.fill ?? (normalizedFill ? overrides.palette?.[normalizedFill] : undefined) ?? globalFill;
    const stroke = dynamicStroke ?? slotOverride?.stroke ?? (normalizedStroke ? overrides.palette?.[normalizedStroke] : undefined) ?? globalStroke;
    const strokeWidth = dynamicStrokeWidth ?? slotOverride?.strokeWidth ?? (globalWidth !== null && Number.isFinite(globalWidth) ? globalWidth : null);
    const explicitFillBlocksOverride = sourceFill?.toLowerCase() === 'none' || sourceFill?.toLowerCase().startsWith('url(');
    const explicitStrokeBlocksOverride = sourceStroke?.toLowerCase().startsWith('url(');

    if (fill && !explicitFillBlocksOverride && isFillCapable(node))
      setPresentationValue(node, 'fill', fill);
    if (stroke && !explicitStrokeBlocksOverride && isStrokeCapable(node))
      setPresentationValue(node, 'stroke', stroke);
    if (strokeWidth !== null && !explicitStrokeBlocksOverride && (sourceStroke?.toLowerCase() !== 'none' || Boolean(stroke)))
      setPresentationValue(node, 'stroke-width', String(strokeWidth));
  }

  root.setAttribute('width', '100%');
  root.setAttribute('height', '100%');
  root.style.display = 'block';
  return new XMLSerializer().serializeToString(root);
}

function semanticSlot(node: Element): string | null {
  for (let current: Element | null = node; current; current = current.parentElement) {
    const slot = current.getAttribute('data-elitescada-slot')?.trim();
    if (slot) return slot;
  }
  return null;
}

function isFillCapable(node: SVGElement): boolean {
  return /^(path|rect|circle|ellipse|polygon|polyline|text|tspan|use)$/i.test(node.localName);
}

function isStrokeCapable(node: SVGElement): boolean {
  return /^(path|rect|circle|ellipse|line|polygon|polyline|text|tspan|use)$/i.test(node.localName);
}

function presentationValue(node: SVGElement, property: string): string | null {
  const inline = node.style.getPropertyValue(property).trim();
  if (inline) return inline;
  return node.getAttribute(property)?.trim() || null;
}

function setPresentationValue(node: SVGElement, property: string, value: string) {
  if (node.style.getPropertyValue(property)) node.style.setProperty(property, value);
  else node.setAttribute(property, value);
}
