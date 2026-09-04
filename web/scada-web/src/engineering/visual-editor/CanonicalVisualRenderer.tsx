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
import { BrowserVisualElement } from './BrowserVisualElement';
import { polygonBounds, polygonPointsAttribute, readPolygonPoints } from './polygonGeometry';
import {
  formatVisualScalarText,
  useVisualBindingSamples,
  type VisualLiveScalarSample
} from './visualEditorLiveValues';
import { resolveVisualDynamicState } from './visualDynamicRuntime';
import { SliderVisualElement, type SliderTagWrite } from './SliderVisualElement';
import { TrendVisualElement } from './TrendVisualElement';
import {
  cssStrokeStyle,
  effectiveStrokeWidth,
  normalizeCanonicalStrokeStyle,
  svgStrokeDasharray
} from './visualStrokePresentation';

export type CanonicalVisualEvent = Readonly<{
  element: VisualElementEngineering;
  eventKey: string;
  runtimeObjectId?: string;
}>;

export type VisualAssetUrlResolver = (assetId: string) => string;

export type CanonicalVisualRendererProps = {
  elements: readonly VisualElementEngineering[] | null | undefined;
  emptyLabel: string;
  locale?: EngineeringLocale;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
  onTagWrite?: SliderTagWrite;
  visualAssetUrl?: VisualAssetUrlResolver;
};

const builtinVisualTypes = new Set<string>(Object.values(BUILTIN_VISUAL_OBJECT_TYPES));
const emptyElements = Object.freeze([]) as readonly VisualElementEngineering[];

export function CanonicalVisualRenderer({
  elements,
  emptyLabel,
  locale = 'pt-BR',
  dynamoDefinitions,
  onVisualEvent,
  onTagWrite,
  visualAssetUrl = visualAssetContentUrl
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
      onTagWrite={onTagWrite}
      visualAssetUrl={visualAssetUrl}
    />)}
  </div>;
}

function CanonicalElement({
  element,
  locale,
  liveSamples,
  dynamoDefinitions,
  onVisualEvent,
  runtimeIdentityPrefix,
  onTagWrite,
  visualAssetUrl
}: {
  element: VisualElementEngineering;
  locale: EngineeringLocale;
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
  runtimeIdentityPrefix?: string;
  onTagWrite?: SliderTagWrite;
  visualAssetUrl: VisualAssetUrlResolver;
}) {
  if (element.dynamoKey && dynamoDefinitions) {
    return <CanonicalDynamoElement
      element={element}
      locale={locale}
      liveSamples={liveSamples}
      dynamoDefinitions={dynamoDefinitions}
      onVisualEvent={onVisualEvent}
      onTagWrite={onTagWrite}
      visualAssetUrl={visualAssetUrl}
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
    const enabled = booleanValue(values[VISUAL_PROPERTY_KEYS.enabled], true);
    const diagnosticTitle = dynamic.diagnostics.length > 0
      ? dynamic.diagnostics.map(item => `${item.propertyKey ? `${item.propertyKey}: ` : ''}${item.message}`).join('\n')
      : undefined;
    const tooltipTitle = optionalText(values[VISUAL_PROPERTY_KEYS.tooltip]);
    const elementTitle = combineTitles(tooltipTitle, diagnosticTitle);
    const diagnosticState = dynamic.diagnostics.length > 0 ? 'unavailable' : 'available';

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.group) {
      return <div
        className="visual-editor-object visual-editor-group"
        style={style}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        data-enabled={enabled}
        title={elementTitle}
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
          onTagWrite={onTagWrite}
          visualAssetUrl={visualAssetUrl}
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
        data-enabled={enabled}
        title={elementTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      >
        {assetId ? <img
          src={visualAssetUrl(assetId)} alt={element.key} draggable={false}
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
        data-enabled={enabled}
        title={elementTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      />;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.polygon) {
      const points = readPolygonPoints(element);
      if (points.length < 3) throw new Error(`Polygon '${element.key}' requires at least three valid vertices.`);
      const bounds = polygonBounds(points);
      const normalizedPoints = points.map(point => ({ x: point.x - bounds.minX, y: point.y - bounds.minY }));
      const strokeStyle = normalizeCanonicalStrokeStyle(values[VISUAL_PROPERTY_KEYS.strokeStyle]);
      const strokeWidth = effectiveStrokeWidth(
        strokeStyle,
        numberValue(values[VISUAL_PROPERTY_KEYS.strokeWidth], 1)
      );
      const gradient = polygonGradient(values);
      const gradientId = gradient
        ? `visual-gradient-${stableDomToken(runtimeObjectId ?? element.id ?? element.key)}`
        : undefined;
      return <div
        className="visual-editor-object visual-editor-polygon"
        style={{ ...style, background: 'transparent', border: 0, overflow: 'visible' }}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        data-enabled={enabled}
        title={elementTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      >
        <svg width="100%" height="100%" viewBox={`0 0 ${Math.max(bounds.width, 1)} ${Math.max(bounds.height, 1)}`} preserveAspectRatio="none" aria-label={element.key}>
          {gradient && gradientId ? <defs>
            <linearGradient id={gradientId} x1={gradient.x1} y1={gradient.y1} x2={gradient.x2} y2={gradient.y2}>
              <stop offset="0%" stopColor={gradient.primary} />
              <stop offset="100%" stopColor={gradient.secondary} />
            </linearGradient>
          </defs> : null}
          <polygon
            points={polygonPointsAttribute(normalizedPoints)}
            fill={gradientId ? `url(#${gradientId})` : polygonSolidFill(values)}
            stroke={strokeStyle === 'none' ? 'none' : stringValue(values[VISUAL_PROPERTY_KEYS.strokeColor], '#000000')}
            strokeWidth={strokeWidth}
            strokeDasharray={svgStrokeDasharray(strokeStyle)}
            vectorEffect="non-scaling-stroke"
          />
        </svg>
      </div>;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.trend) {
      return <TrendVisualElement
        element={element}
        values={values}
        style={style}
        runtimeObjectId={runtimeObjectId}
        title={elementTitle}
        locale={locale}
        enabled={enabled}
        onClick={onClick}
      />;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser ||
        element.type === BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser) {
      return <BrowserVisualElement
        element={element}
        style={style}
        runtimeObjectId={runtimeObjectId}
        title={elementTitle}
        locale={locale}
        enabled={enabled}
        onClick={onClick}
      />;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.slider) {
      return <SliderVisualElement
        element={element}
        values={values}
        diagnostics={dynamic.diagnostics}
        liveSamples={liveSamples}
        style={style}
        runtimeObjectId={runtimeObjectId}
        title={elementTitle}
        onTagWrite={onTagWrite}
      />;
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
    const title = combineTitles(sourceTitle, tooltipTitle, diagnosticTitle);
    const fill = analogFillOverlay(element, dynamic.analogFill);

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.button) {
      return <button
        type="button"
        tabIndex={onClick && enabled ? 0 : -1}
        disabled={!enabled}
        aria-disabled={!enabled}
        className={className}
        style={style}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        data-enabled={enabled}
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
      data-enabled={enabled}
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
  onVisualEvent,
  onTagWrite,
  visualAssetUrl
}: {
  element: VisualElementEngineering;
  locale: EngineeringLocale;
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
  onTagWrite?: SliderTagWrite;
  visualAssetUrl: VisualAssetUrlResolver;
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
    const enabled = booleanValue(dynamic.values[VISUAL_PROPERTY_KEYS.enabled], true);
    const runtimeObjectId = composition.instanceId;
    const onClick = visualClickHandler(element, onVisualEvent, runtimeObjectId);
    const diagnosticTitle = dynamic.diagnostics.length > 0
      ? dynamic.diagnostics.map(item => `${item.propertyKey ? `${item.propertyKey}: ` : ''}${item.message}`).join('\n')
      : undefined;
    const title = combineTitles(
      optionalText(dynamic.values[VISUAL_PROPERTY_KEYS.tooltip]),
      diagnosticTitle
    );

    return <div
      className="visual-editor-object visual-editor-group visual-editor-dynamo"
      style={style}
      data-object-id={element.id ?? undefined}
      data-runtime-object-id={runtimeObjectId}
      data-enabled={enabled}
      data-dynamo-key={composition.definitionKey}
      data-dynamo-definition-id={composition.definitionId}
      data-dynamo-instance-id={composition.instanceId}
      data-dynamo-parameter-count={composition.parameters.size}
      data-dynamic-state={dynamic.diagnostics.length > 0 ? 'unavailable' : 'available'}
      title={title}
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
        onTagWrite={onTagWrite}
        visualAssetUrl={visualAssetUrl}
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
  const enabled = booleanValue(values[VISUAL_PROPERTY_KEYS.enabled], true);
  const strokeStyle = normalizeCanonicalStrokeStyle(values[VISUAL_PROPERTY_KEYS.strokeStyle]);
  const strokeWidth = effectiveStrokeWidth(
    strokeStyle,
    numberValue(values[VISUAL_PROPERTY_KEYS.strokeWidth], 0)
  );
  const scaleX = numberValue(values[VISUAL_PROPERTY_KEYS.scaleX], 1) *
    (booleanValue(values[VISUAL_PROPERTY_KEYS.horizontalFlip], false) ? -1 : 1);
  const scaleY = numberValue(values[VISUAL_PROPERTY_KEYS.scaleY], 1) *
    (booleanValue(values[VISUAL_PROPERTY_KEYS.verticalFlip], false) ? -1 : 1);
  const textWrap = booleanValue(values[VISUAL_PROPERTY_KEYS.textWrap], true);
  const textOverflow = stringValue(values[VISUAL_PROPERTY_KEYS.textOverflow], 'clip');
  return {
    position: 'absolute',
    left: numberValue(values[VISUAL_PROPERTY_KEYS.x]), top: numberValue(values[VISUAL_PROPERTY_KEYS.y]),
    width: numberValue(values[VISUAL_PROPERTY_KEYS.width], 100), height: numberValue(values[VISUAL_PROPERTY_KEYS.height], 100),
    zIndex: numberValue(values[VISUAL_PROPERTY_KEYS.zIndex]), display: visible ? 'flex' : 'none',
    opacity: numberValue(values[VISUAL_PROPERTY_KEYS.opacity], 1),
    pointerEvents: enabled ? undefined : 'none',
    transform: `rotate(${numberValue(values[VISUAL_PROPERTY_KEYS.rotation])}deg) scale(${scaleX}, ${scaleY})`,
    transformOrigin: 'center center', boxSizing: 'border-box', overflow: 'hidden',
    background: fillBackground(values),
    filter: shadowFilter(values),
    borderColor: strokeStyle === 'none' ? 'transparent' : stringValue(values[VISUAL_PROPERTY_KEYS.strokeColor]) || undefined,
    borderWidth: strokeWidth,
    borderStyle: cssStrokeStyle(strokeStyle),
    borderRadius: numberValue(values[VISUAL_PROPERTY_KEYS.cornerRadius]),
    color: stringValue(values[VISUAL_PROPERTY_KEYS.textColor]) || undefined,
    fontFamily: normalizeFontFamily(stringValue(values[VISUAL_PROPERTY_KEYS.fontFamily])),
    fontSize: numberValue(values[VISUAL_PROPERTY_KEYS.fontSize], 14), fontWeight: numberValue(values[VISUAL_PROPERTY_KEYS.fontWeight], 400),
    fontStyle: stringValue(values[VISUAL_PROPERTY_KEYS.fontStyle], 'normal') as CSSProperties['fontStyle'],
    textDecorationLine: booleanValue(values[VISUAL_PROPERTY_KEYS.underline], false) ? 'underline' : 'none',
    lineHeight: numberValue(values[VISUAL_PROPERTY_KEYS.lineHeight], 1.2),
    textAlign: stringValue(values[VISUAL_PROPERTY_KEYS.horizontalAlignment], 'left') as CSSProperties['textAlign'],
    alignItems: verticalAlignment(values[VISUAL_PROPERTY_KEYS.verticalAlignment]), justifyContent: horizontalFlexAlignment(values[VISUAL_PROPERTY_KEYS.horizontalAlignment]),
    whiteSpace: textWrap ? 'pre-wrap' : 'pre',
    overflowWrap: textWrap ? 'anywhere' : 'normal',
    textOverflow: textOverflow === 'ellipsis' ? 'ellipsis' : 'clip'
  };
}

function lineStyle(base: CSSProperties, values: Readonly<Record<string, VisualPropertyValue>>): CSSProperties {
  const strokeStyle = normalizeCanonicalStrokeStyle(values[VISUAL_PROPERTY_KEYS.strokeStyle]);
  const strokeWidth = effectiveStrokeWidth(
    strokeStyle,
    numberValue(values[VISUAL_PROPERTY_KEYS.strokeWidth], 1)
  );
  return {
    ...base,
    height: 0,
    minHeight: 0,
    overflow: 'visible',
    background: 'transparent',
    borderWidth: 0,
    borderTopWidth: strokeWidth,
    borderTopColor: strokeStyle === 'none'
      ? 'transparent'
      : stringValue(values[VISUAL_PROPERTY_KEYS.strokeColor], '#000000'),
    borderTopStyle: cssStrokeStyle(strokeStyle)
  };
}

function fillBackground(values: Readonly<Record<string, VisualPropertyValue>>): string | undefined {
  const backgroundColor = stringValue(values[VISUAL_PROPERTY_KEYS.backgroundColor]);
  if (backgroundColor) return backgroundColor;

  const primary = stringValue(values[VISUAL_PROPERTY_KEYS.fillColor]);
  if (!Object.prototype.hasOwnProperty.call(values, VISUAL_PROPERTY_KEYS.fillStyle)) return primary || undefined;

  const fillStyle = stringValue(values[VISUAL_PROPERTY_KEYS.fillStyle], 'solid');
  if (fillStyle === 'none') return 'transparent';
  if (fillStyle !== 'gradient') return primary || undefined;

  const secondary = stringValue(values[VISUAL_PROPERTY_KEYS.fillSecondaryColor], '#00000000');
  return `linear-gradient(${gradientAngle(values[VISUAL_PROPERTY_KEYS.gradientDirection])}, ${primary || '#00000000'}, ${secondary})`;
}

function shadowFilter(values: Readonly<Record<string, VisualPropertyValue>>): string | undefined {
  if (!booleanValue(values[VISUAL_PROPERTY_KEYS.shadowEnabled], false)) return undefined;
  const x = numberValue(values[VISUAL_PROPERTY_KEYS.shadowOffsetX]);
  const y = numberValue(values[VISUAL_PROPERTY_KEYS.shadowOffsetY]);
  const blur = numberValue(values[VISUAL_PROPERTY_KEYS.shadowBlur]);
  const color = stringValue(values[VISUAL_PROPERTY_KEYS.shadowColor], '#00000066');
  return `drop-shadow(${x}px ${y}px ${blur}px ${color})`;
}

type PolygonGradient = Readonly<{
  primary: string;
  secondary: string;
  x1: string;
  y1: string;
  x2: string;
  y2: string;
}>;

function polygonGradient(values: Readonly<Record<string, VisualPropertyValue>>): PolygonGradient | null {
  if (stringValue(values[VISUAL_PROPERTY_KEYS.fillStyle], 'solid') !== 'gradient') return null;
  const direction = stringValue(values[VISUAL_PROPERTY_KEYS.gradientDirection], 'vertical');
  const coordinates = gradientCoordinates(direction);
  return {
    primary: stringValue(values[VISUAL_PROPERTY_KEYS.fillColor], '#00000000'),
    secondary: stringValue(values[VISUAL_PROPERTY_KEYS.fillSecondaryColor], '#00000000'),
    ...coordinates
  };
}

function polygonSolidFill(values: Readonly<Record<string, VisualPropertyValue>>): string {
  if (stringValue(values[VISUAL_PROPERTY_KEYS.fillStyle], 'solid') === 'none') return 'none';
  return stringValue(values[VISUAL_PROPERTY_KEYS.fillColor], '#00000000');
}

function gradientAngle(value: VisualPropertyValue | undefined): string {
  switch (stringValue(value, 'vertical')) {
    case 'horizontal': return '90deg';
    case 'diagonal-down': return '135deg';
    case 'diagonal-up': return '45deg';
    case 'vertical':
    default: return '180deg';
  }
}

function gradientCoordinates(direction: string): Readonly<{ x1: string; y1: string; x2: string; y2: string }> {
  switch (direction) {
    case 'horizontal': return { x1: '0%', y1: '0%', x2: '100%', y2: '0%' };
    case 'diagonal-down': return { x1: '0%', y1: '0%', x2: '100%', y2: '100%' };
    case 'diagonal-up': return { x1: '0%', y1: '100%', x2: '100%', y2: '0%' };
    case 'vertical':
    default: return { x1: '0%', y1: '0%', x2: '0%', y2: '100%' };
  }
}

function stableDomToken(value: string): string {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0).toString(36);
}

function assetReferenceId(value: VisualPropertyValue | undefined): string | null { if (!value || typeof value !== 'object' || !('assetId' in value)) return null; return typeof value.assetId === 'string' && value.assetId.length > 0 ? value.assetId : null; }
function imageFit(value: VisualPropertyValue | undefined): CSSProperties['objectFit'] { const fit = stringValue(value, 'contain'); return fit === 'cover' ? 'cover' : fit === 'fill' ? 'fill' : fit === 'native' ? 'none' : 'contain'; }
function percent(value: VisualPropertyValue | undefined): number { return Math.max(0, Math.min(1, numberValue(value, 0.5))) * 100; }
function numberValue(value: VisualPropertyValue | undefined, fallback = 0): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback; }
function booleanValue(value: VisualPropertyValue | undefined, fallback: boolean): boolean { return typeof value === 'boolean' ? value : fallback; }
function stringValue(value: VisualPropertyValue | undefined, fallback = ''): string { return typeof value === 'string' ? value : fallback; }
function optionalText(value: VisualPropertyValue | undefined): string | undefined { const result = stringValue(value).trim(); return result || undefined; }
function combineTitles(...parts: Array<string | undefined>): string | undefined { const result = parts.filter((part): part is string => Boolean(part)).join('\n'); return result || undefined; }
function legacyNumber(value: VisualEngineeringPropertyValue | undefined, fallback: number): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback; }
function legacyString(value: VisualEngineeringPropertyValue | undefined): string { return typeof value === 'string' ? value : ''; }
function normalizeFontFamily(value: string): string | undefined { return !value ? undefined : value === 'system' ? 'system-ui, sans-serif' : value; }
function verticalAlignment(value: VisualPropertyValue | undefined): CSSProperties['alignItems'] { const alignment = stringValue(value, 'middle'); return alignment === 'top' ? 'flex-start' : alignment === 'bottom' ? 'flex-end' : 'center'; }
function horizontalFlexAlignment(value: VisualPropertyValue | undefined): CSSProperties['justifyContent'] { const alignment = stringValue(value, 'left'); return alignment === 'center' ? 'center' : alignment === 'right' ? 'flex-end' : 'flex-start'; }
