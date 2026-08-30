import React, { type CSSProperties } from 'react';
import { visualAssetContentUrl } from '../api';
import type { EngineeringLocale } from '../i18n';
import type {
  BindingEngineering,
  DynamoEngineering,
  VisualElementEngineering,
  VisualEngineeringPropertyValue
} from '../types';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  decodeVisualEngineeringProperties,
  getBuiltinVisualObjectSchema,
  supportsAnalogFill,
  VISUAL_PROPERTY_KEYS,
  type VisualObjectPropertySchema,
  type VisualPropertyValue
} from '../../visual-runtime';
import {
  asCanonicalDynamo,
  asCanonicalVisualElement,
  composeDynamoRuntime,
  resolveDynamoDefinition,
  runtimeDynamoElementIdentity
} from '../../runtime/visual-navigation/runtimeVisualNavigationModel';
import { polygonBounds, polygonPointsAttribute, readPolygonPoints } from './polygonGeometry';
import {
  formatVisualScalarText,
  useVisualBindingSamples,
  type VisualLiveScalarSample
} from './visualEditorLiveValues';
import { resolveVisualDynamicState } from './visualDynamicRuntime';

export type CanonicalVisualEvent = Readonly<{
  element: VisualElementEngineering;
  eventKey: string;
  runtimeObjectId?: string;
}>;

export type CanonicalVisualRendererProps = {
  elements: readonly VisualElementEngineering[] | null | undefined;
  emptyLabel: string;
  locale?: EngineeringLocale;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
};

const builtinVisualTypes = new Set<string>(Object.values(BUILTIN_VISUAL_OBJECT_TYPES));
const emptyElements = Object.freeze([]) as readonly VisualElementEngineering[];

export function CanonicalVisualRenderer({
  elements,
  emptyLabel,
  locale = 'pt-BR',
  dynamoDefinitions,
  onVisualEvent
}: CanonicalVisualRendererProps) {
  const rootElements = elements ?? emptyElements;
  const runtimeBindingElements = React.useMemo(
    () => collectRuntimeBindingElements(rootElements, dynamoDefinitions),
    [rootElements, dynamoDefinitions]
  );
  const liveSamples = useVisualBindingSamples(runtimeBindingElements);
  if (rootElements.length === 0) return <div className="visual-editor-renderer-empty">{emptyLabel}</div>;

  return <div className="visual-editor-renderer-stage" data-testid="visual-editor-canonical-renderer">
    {rootElements.map((element, index) => <CanonicalElement
      key={element.id ?? `${element.key}-${index}`}
      element={element}
      locale={locale}
      liveSamples={liveSamples}
      dynamoDefinitions={dynamoDefinitions}
      onVisualEvent={onVisualEvent}
    />)}
  </div>;
}

function CanonicalElement({
  element,
  locale,
  liveSamples,
  dynamoDefinitions,
  onVisualEvent,
  runtimeIdentityPrefix
}: {
  element: VisualElementEngineering;
  locale: EngineeringLocale;
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
  runtimeIdentityPrefix?: string;
}) {
  if (element.dynamoKey && dynamoDefinitions) {
    return <CanonicalDynamoElement
      element={element}
      locale={locale}
      liveSamples={liveSamples}
      dynamoDefinitions={dynamoDefinitions}
      onVisualEvent={onVisualEvent}
    />;
  }

  const runtimeObjectId = runtimeElementIdentity(element, runtimeIdentityPrefix);
  const onClick = visualClickHandler(element, onVisualEvent, runtimeObjectId);

  if (!builtinVisualTypes.has(element.type)) {
    return <LegacyCompatibilityElement
      element={element}
      runtimeObjectId={runtimeObjectId}
      onClick={onClick}
    />;
  }

  try {
    const schema = getBuiltinVisualObjectSchema(element.type);
    const baseValues: Readonly<Record<string, VisualPropertyValue>> = {
      ...schema.createDefaultValues(),
      ...decodeVisualEngineeringProperties(registeredScalarProperties(element, schema), schema)
    };
    const dynamic = resolveVisualDynamicState(element, baseValues, liveSamples);
    const values = dynamic.values;
    const style = elementStyle(values);
    const diagnosticTitle = dynamic.diagnostics.length > 0
      ? dynamic.diagnostics.map(item => `${item.propertyKey ? `${item.propertyKey}: ` : ''}${item.message}`).join('\n')
      : undefined;
    const diagnosticState = dynamic.diagnostics.length > 0 ? 'unavailable' : 'available';

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.group) {
      return <div
        className="visual-editor-object visual-editor-group"
        style={style}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        title={diagnosticTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      >
        {(element.children ?? []).map((child, index) => <CanonicalElement
          key={child.id ?? `${child.key}-${index}`}
          element={child}
          locale={locale}
          liveSamples={liveSamples}
          dynamoDefinitions={dynamoDefinitions}
          onVisualEvent={onVisualEvent}
          runtimeIdentityPrefix={runtimeIdentityPrefix}
        />)}
      </div>;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.image) {
      const assetId = assetReferenceId(values[VISUAL_PROPERTY_KEYS.assetRef]);
      return <div
        className="visual-editor-object visual-editor-image"
        style={style}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        title={diagnosticTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      >
        {assetId ? <img
          src={visualAssetContentUrl(assetId)} alt={element.key} draggable={false}
          style={{ width: '100%', height: '100%', objectFit: imageFit(values[VISUAL_PROPERTY_KEYS.imageFit]), objectPosition: `${percent(values[VISUAL_PROPERTY_KEYS.imagePositionX])}% ${percent(values[VISUAL_PROPERTY_KEYS.imagePositionY])}%` }}
        /> : <span className="visual-editor-image-placeholder">{element.key}</span>}
      </div>;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.line) {
      return <div
        className="visual-editor-object visual-editor-line"
        style={lineStyle(style, values)}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        title={diagnosticTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      />;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.polygon) {
      const points = readPolygonPoints(element);
      if (points.length < 3) throw new Error(`Polygon '${element.key}' requires at least three valid vertices.`);
      const bounds = polygonBounds(points);
      const normalizedPoints = points.map(point => ({ x: point.x - bounds.minX, y: point.y - bounds.minY }));
      const strokeStyle = stringValue(values[VISUAL_PROPERTY_KEYS.strokeStyle], 'solid');
      return <div
        className="visual-editor-object visual-editor-polygon"
        style={{ ...style, background: 'transparent', border: 0, overflow: 'visible' }}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        title={diagnosticTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      >
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

    const staticText = stringValue(values[VISUAL_PROPERTY_KEYS.text]);
    const textBinding = dynamicTextBinding(element.bindings);
    const textSample = textBinding ? bindingSample(liveSamples, textBinding) : undefined;
    const dynamicText = textBinding
      ? formatVisualScalarText(textSample, textBinding, locale)
      : null;
    const className = `visual-editor-object visual-editor-${element.type.replace('core.', '')}${dynamicText && !dynamicText.available ? ' visual-editor-dynamic-unavailable' : ''}`;
    const content = dynamicText?.text || staticText || element.key;
    const sourceTitle = dynamicText ? `${textBinding!.target} · ${dynamicText.state}` : undefined;
    const title = [sourceTitle, diagnosticTitle].filter(Boolean).join('\n') || undefined;
    const fill = analogFillOverlay(element, dynamic.analogFill);

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.button) {
      return <button
        type="button"
        tabIndex={onClick ? 0 : -1}
        className={className}
        style={style}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        title={title}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      >
        {fill}{content}
      </button>;
    }
    return <div
      className={className}
      style={style}
      data-object-id={element.id ?? undefined}
      data-runtime-object-id={runtimeObjectId}
      title={title}
      data-dynamic-reference={textBinding?.target}
      data-dynamic-state={diagnosticState}
      onClick={onClick}
    >
      {fill}{content}
    </div>;
  } catch (reason) {
    return <div className="visual-editor-object-error" title={reason instanceof Error ? reason.message : String(reason)}>{element.key || element.type || 'invalid visual object'}</div>;
  }
}

function CanonicalDynamoElement({
  element,
  locale,
  liveSamples,
  dynamoDefinitions,
  onVisualEvent
}: {
  element: VisualElementEngineering;
  locale: EngineeringLocale;
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
}) {
  try {
    const definition = resolveDynamoDefinition(dynamoDefinitions, element.dynamoKey!);
    const composition = composeDynamoRuntime(element, definition);
    const schema = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.group);
    const baseValues: Readonly<Record<string, VisualPropertyValue>> = {
      ...schema.createDefaultValues(),
      ...decodeVisualEngineeringProperties(registeredScalarProperties(element, schema), schema)
    };
    const dynamic = resolveVisualDynamicState(element, baseValues, liveSamples);
    const style = elementStyle(dynamic.values);
    const runtimeObjectId = composition.instanceId;
    const onClick = visualClickHandler(element, onVisualEvent, runtimeObjectId);
    const diagnosticTitle = dynamic.diagnostics.length > 0
      ? dynamic.diagnostics.map(item => `${item.propertyKey ? `${item.propertyKey}: ` : ''}${item.message}`).join('\n')
      : undefined;

    return <div
      className="visual-editor-object visual-editor-group visual-editor-dynamo"
      style={style}
      data-object-id={element.id ?? undefined}
      data-runtime-object-id={runtimeObjectId}
      data-dynamo-key={composition.definitionKey}
      data-dynamo-definition-id={composition.definitionId}
      data-dynamo-instance-id={composition.instanceId}
      data-dynamo-parameter-count={composition.parameters.size}
      data-dynamic-state={dynamic.diagnostics.length > 0 ? 'unavailable' : 'available'}
      title={diagnosticTitle}
      onClick={onClick}
    >
      {composition.elements.map((child, index) => <CanonicalElement
        key={child.id ?? `${child.key}-${index}`}
        element={child}
        locale={locale}
        liveSamples={liveSamples}
        dynamoDefinitions={dynamoDefinitions}
        onVisualEvent={onVisualEvent}
        runtimeIdentityPrefix={composition.instanceId}
      />)}
    </div>;
  } catch (reason) {
    const message = reason instanceof Error ? reason.message : String(reason);
    const code = reason && typeof reason === 'object' && 'code' in reason
      ? String((reason as { code?: unknown }).code ?? 'VISUAL_RUNTIME_DYNAMO_FAILED')
      : 'VISUAL_RUNTIME_DYNAMO_FAILED';
    return <div
      className="visual-editor-object-error"
      data-testid="visual-runtime-dynamo-diagnostic"
      data-diagnostic-code={code}
      title={message}
    >{element.key || element.dynamoKey || 'invalid Dynamo'}</div>;
  }
}

function analogFillOverlay(
  element: VisualElementEngineering,
  analogFill: ReturnType<typeof resolveVisualDynamicState>['analogFill']
): React.ReactNode {
  if (!analogFill || !supportsAnalogFill(element.type)) return null;
  return <span
    aria-hidden="true"
    data-testid="visual-analog-fill"
    data-fill-percent={analogFill.presentation.percent}
    style={{
      position: 'absolute',
      inset: 0,
      zIndex: 0,
      pointerEvents: 'none',
      background: analogFill.fillColor,
      clipPath: analogFill.presentation.clipPath,
      borderRadius: 'inherit'
    }}
  />;
}

function dynamicTextBinding(bindings: readonly BindingEngineering[] | null | undefined): BindingEngineering | null {
  const binding = bindings?.find(candidate => {
    const kind = candidate.kind?.trim().toLowerCase();
    return candidate.key === VISUAL_PROPERTY_KEYS.text && (kind === 'tag' || kind === 'clientmemory');
  });
  return binding ?? null;
}

function bindingSample(samples: ReadonlyMap<string, VisualLiveScalarSample>, binding: BindingEngineering): VisualLiveScalarSample | undefined {
  if (binding.tagReference?.tagId) {
    const byId = samples.get(`tag:${binding.tagReference.tagId.trim().toLocaleLowerCase()}`);
    if (byId) return byId;
  }
  return samples.get(binding.target);
}

function registeredScalarProperties(element: VisualElementEngineering, schema: VisualObjectPropertySchema): Readonly<Record<string, unknown>> {
  const projected: Record<string, unknown> = Object.create(null) as Record<string, unknown>;
  for (const [key, value] of Object.entries(element.properties ?? {})) {
    if (schema.declares(key)) projected[key] = value;
  }
  return projected;
}

function LegacyCompatibilityElement({
  element,
  runtimeObjectId,
  onClick
}: {
  element: VisualElementEngineering;
  runtimeObjectId?: string;
  onClick?: (event: React.MouseEvent) => void;
}) {
  const x = legacyNumber(element.properties?.x, 18);
  const y = legacyNumber(element.properties?.y, 18);
  const label = legacyString(element.properties?.label) || element.key || element.type;
  return <div
    className="visual-editor-object visual-editor-legacy-placeholder"
    style={{ left: x, top: y }}
    data-object-id={element.id ?? undefined}
    data-runtime-object-id={runtimeObjectId}
    data-legacy-object-type={element.type}
    title={`Legacy visual type: ${element.type}`}
    onClick={onClick}
  >
    <strong>{label}</strong><span>{element.type}</span>
  </div>;
}

function visualClickHandler(
  element: VisualElementEngineering,
  onVisualEvent: ((event: CanonicalVisualEvent) => void) | undefined,
  runtimeObjectId: string | undefined
): ((event: React.MouseEvent) => void) | undefined {
  if (!onVisualEvent) return undefined;
  const hasClickAction = (asCanonicalVisualElement(element).actions ?? [])
    .some(action => action.eventKey?.toLocaleLowerCase('en-US') === 'click');
  if (!hasClickAction) return undefined;

  return event => {
    event.stopPropagation();
    onVisualEvent(Object.freeze({
      element,
      eventKey: 'click',
      runtimeObjectId
    }));
  };
}

function runtimeElementIdentity(
  element: VisualElementEngineering,
  runtimeIdentityPrefix: string | undefined
): string | undefined {
  if (!runtimeIdentityPrefix) return element.id ?? undefined;
  return runtimeDynamoElementIdentity(runtimeIdentityPrefix, element.id ?? '');
}

function collectRuntimeBindingElements(
  rootElements: readonly VisualElementEngineering[],
  dynamoDefinitions: readonly DynamoEngineering[] | null | undefined
): readonly VisualElementEngineering[] {
  if (!dynamoDefinitions || dynamoDefinitions.length === 0) return rootElements;
  const definitionElements = dynamoDefinitions.flatMap(definition =>
    [...(asCanonicalDynamo(definition).elements ?? [])]
  );
  return Object.freeze([...rootElements, ...definitionElements]);
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
