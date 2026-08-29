import React, { type CSSProperties } from 'react';
import { visualAssetContentUrl } from '../api';
import type { VisualElementEngineering, VisualEngineeringPropertyValue } from '../types';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  decodeVisualEngineeringProperties,
  getBuiltinVisualObjectSchema,
  VISUAL_PROPERTY_KEYS,
  type VisualPropertyValue
} from '../../visual-runtime';
import { polygonBounds, polygonPointsAttribute, readPolygonPoints } from './polygonGeometry';

export type CanonicalVisualRendererProps = {
  elements: readonly VisualElementEngineering[] | null | undefined;
  emptyLabel: string;
};

const builtinVisualTypes = new Set<string>(Object.values(BUILTIN_VISUAL_OBJECT_TYPES));

export function CanonicalVisualRenderer({ elements, emptyLabel }: CanonicalVisualRendererProps) {
  const rootElements = elements ?? [];
  if (rootElements.length === 0) return <div className="visual-editor-renderer-empty">{emptyLabel}</div>;

  return <div className="visual-editor-renderer-stage" data-testid="visual-editor-canonical-renderer">
    {rootElements.map((element, index) => <CanonicalElement key={element.id ?? `${element.key}-${index}`} element={element} />)}
  </div>;
}

function CanonicalElement({ element }: { element: VisualElementEngineering }) {
  if (!builtinVisualTypes.has(element.type)) return <LegacyCompatibilityElement element={element} />;

  try {
    const schema = getBuiltinVisualObjectSchema(element.type);
    const values: Readonly<Record<string, VisualPropertyValue>> = {
      ...schema.createDefaultValues(),
      ...decodeVisualEngineeringProperties(element.properties, schema)
    };
    const style = elementStyle(values);

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.group) {
      return <div className="visual-editor-object visual-editor-group" style={style} data-object-id={element.id ?? undefined}>
        {(element.children ?? []).map((child, index) => <CanonicalElement key={child.id ?? `${child.key}-${index}`} element={child} />)}
      </div>;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.image) {
      const assetId = assetReferenceId(values[VISUAL_PROPERTY_KEYS.assetRef]);
      return <div className="visual-editor-object visual-editor-image" style={style} data-object-id={element.id ?? undefined}>
        {assetId ? <img
          src={visualAssetContentUrl(assetId)} alt={element.key} draggable={false}
          style={{ width: '100%', height: '100%', objectFit: imageFit(values[VISUAL_PROPERTY_KEYS.imageFit]), objectPosition: `${percent(values[VISUAL_PROPERTY_KEYS.imagePositionX])}% ${percent(values[VISUAL_PROPERTY_KEYS.imagePositionY])}%` }}
        /> : <span className="visual-editor-image-placeholder">{element.key}</span>}
      </div>;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.line) {
      return <div className="visual-editor-object visual-editor-line" style={lineStyle(style, values)} data-object-id={element.id ?? undefined} />;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.polygon) {
      const points = readPolygonPoints(element);
      if (points.length < 3) throw new Error(`Polygon '${element.key}' requires at least three valid vertices.`);
      const bounds = polygonBounds(points);
      const normalizedPoints = points.map(point => ({ x: point.x - bounds.minX, y: point.y - bounds.minY }));
      const strokeStyle = stringValue(values[VISUAL_PROPERTY_KEYS.strokeStyle], 'solid');
      return <div className="visual-editor-object visual-editor-polygon" style={{ ...style, background: 'transparent', border: 0, overflow: 'visible' }} data-object-id={element.id ?? undefined}>
        <svg width="100%" height="100%" viewBox={`0 0 ${Math.max(bounds.width, 1)} ${Math.max(bounds.height, 1)}`} preserveAspectRatio="none" aria-label={element.key}>
          <polygon
            points={polygonPointsAttribute(normalizedPoints)}
            fill={stringValue(values[VISUAL_PROPERTY_KEYS.fillColor], '#00000000')}
            stroke={stringValue(values[VISUAL_PROPERTY_KEYS.strokeColor], '#000000')}
            strokeWidth={numberValue(values[VISUAL_PROPERTY_KEYS.strokeWidth], 1)}
            strokeDasharray={strokeStyle === 'dashed' ? '8 5' : strokeStyle === 'dotted' ? '2 4' : undefined}
            vectorEffect="non-scaling-stroke"
          />
        </svg>
      </div>;
    }

    const text = stringValue(values[VISUAL_PROPERTY_KEYS.text]);
    const className = `visual-editor-object visual-editor-${element.type.replace('core.', '')}`;
    const content = text || element.key;
    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.button) {
      return <button type="button" tabIndex={-1} className={className} style={style} data-object-id={element.id ?? undefined}>{content}</button>;
    }
    return <div className={className} style={style} data-object-id={element.id ?? undefined}>{content}</div>;
  } catch (reason) {
    return <div className="visual-editor-object-error" title={reason instanceof Error ? reason.message : String(reason)}>{element.key || element.type || 'invalid visual object'}</div>;
  }
}

function LegacyCompatibilityElement({ element }: { element: VisualElementEngineering }) {
  const x = legacyNumber(element.properties?.x, 18);
  const y = legacyNumber(element.properties?.y, 18);
  const label = legacyString(element.properties?.label) || element.key || element.type;
  return <div className="visual-editor-object visual-editor-legacy-placeholder" style={{ left: x, top: y }} data-object-id={element.id ?? undefined} data-legacy-object-type={element.type} title={`Legacy visual type: ${element.type}`}>
    <strong>{label}</strong><span>{element.type}</span>
  </div>;
}

function elementStyle(values: Readonly<Record<string, VisualPropertyValue>>): CSSProperties {
  const visible = booleanValue(values[VISUAL_PROPERTY_KEYS.visible], true);
  const strokeStyle = stringValue(values[VISUAL_PROPERTY_KEYS.strokeStyle], 'solid');
  return {
    position: 'absolute',
    left: numberValue(values[VISUAL_PROPERTY_KEYS.x]), top: numberValue(values[VISUAL_PROPERTY_KEYS.y]),
    width: numberValue(values[VISUAL_PROPERTY_KEYS.width], 100), height: numberValue(values[VISUAL_PROPERTY_KEYS.height], 100),
    zIndex: numberValue(values[VISUAL_PROPERTY_KEYS.zIndex]), display: visible ? 'flex' : 'none',
    opacity: numberValue(values[VISUAL_PROPERTY_KEYS.opacity], 1),
    transform: `rotate(${numberValue(values[VISUAL_PROPERTY_KEYS.rotation])}deg) scale(${numberValue(values[VISUAL_PROPERTY_KEYS.scaleX], 1)}, ${numberValue(values[VISUAL_PROPERTY_KEYS.scaleY], 1)})`,
    transformOrigin: 'center center', boxSizing: 'border-box', overflow: 'hidden',
    background: stringValue(values[VISUAL_PROPERTY_KEYS.backgroundColor]) || stringValue(values[VISUAL_PROPERTY_KEYS.fillColor]) || undefined,
    borderColor: stringValue(values[VISUAL_PROPERTY_KEYS.strokeColor]) || undefined,
    borderWidth: numberValue(values[VISUAL_PROPERTY_KEYS.strokeWidth], 0),
    borderStyle: strokeStyle === 'dashed' ? 'dashed' : strokeStyle === 'dotted' ? 'dotted' : 'solid',
    borderRadius: numberValue(values[VISUAL_PROPERTY_KEYS.cornerRadius]),
    color: stringValue(values[VISUAL_PROPERTY_KEYS.textColor]) || undefined,
    fontFamily: normalizeFontFamily(stringValue(values[VISUAL_PROPERTY_KEYS.fontFamily])),
    fontSize: numberValue(values[VISUAL_PROPERTY_KEYS.fontSize], 14), fontWeight: numberValue(values[VISUAL_PROPERTY_KEYS.fontWeight], 400),
    fontStyle: stringValue(values[VISUAL_PROPERTY_KEYS.fontStyle], 'normal') as CSSProperties['fontStyle'],
    textAlign: stringValue(values[VISUAL_PROPERTY_KEYS.horizontalAlignment], 'left') as CSSProperties['textAlign'],
    alignItems: verticalAlignment(values[VISUAL_PROPERTY_KEYS.verticalAlignment]), justifyContent: horizontalFlexAlignment(values[VISUAL_PROPERTY_KEYS.horizontalAlignment]),
    whiteSpace: 'pre-wrap', overflowWrap: 'anywhere'
  };
}

function lineStyle(base: CSSProperties, values: Readonly<Record<string, VisualPropertyValue>>): CSSProperties {
  return { ...base, height: 0, minHeight: 0, overflow: 'visible', background: 'transparent', borderWidth: 0, borderTopWidth: numberValue(values[VISUAL_PROPERTY_KEYS.strokeWidth], 1), borderTopColor: stringValue(values[VISUAL_PROPERTY_KEYS.strokeColor], '#000000'), borderTopStyle: stringValue(values[VISUAL_PROPERTY_KEYS.strokeStyle], 'solid') as CSSProperties['borderTopStyle'] };
}
function assetReferenceId(value: VisualPropertyValue | undefined): string | null { if (!value || typeof value !== 'object' || !('assetId' in value)) return null; return typeof value.assetId === 'string' && value.assetId.length > 0 ? value.assetId : null; }
function imageFit(value: VisualPropertyValue | undefined): CSSProperties['objectFit'] { const fit = stringValue(value, 'contain'); return fit === 'cover' ? 'cover' : fit === 'fill' ? 'fill' : fit === 'native' ? 'none' : 'contain'; }
function percent(value: VisualPropertyValue | undefined): number { return Math.max(0, Math.min(1, numberValue(value, 0.5))) * 100; }
function numberValue(value: VisualPropertyValue | undefined, fallback = 0): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback; }
function booleanValue(value: VisualPropertyValue | undefined, fallback: boolean): boolean { return typeof value === 'boolean' ? value : fallback; }
function stringValue(value: VisualPropertyValue | undefined, fallback = ''): string { return typeof value === 'string' ? value : fallback; }
function legacyNumber(value: VisualEngineeringPropertyValue | undefined, fallback: number): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback; }
function legacyString(value: VisualEngineeringPropertyValue | undefined): string { return typeof value === 'string' ? value : ''; }
function normalizeFontFamily(value: string): string | undefined { return !value ? undefined : value === 'system' ? 'system-ui, sans-serif' : value; }
function verticalAlignment(value: VisualPropertyValue | undefined): CSSProperties['alignItems'] { const alignment = stringValue(value, 'middle'); return alignment === 'top' ? 'flex-start' : alignment === 'bottom' ? 'flex-end' : 'center'; }
function horizontalFlexAlignment(value: VisualPropertyValue | undefined): CSSProperties['justifyContent'] { const alignment = stringValue(value, 'left'); return alignment === 'center' ? 'center' : alignment === 'right' ? 'flex-end' : 'flex-start'; }
