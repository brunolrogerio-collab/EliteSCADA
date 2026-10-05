import React, { type CSSProperties } from 'react';
import { visualAssetContentUrl } from '../api';
import type { EngineeringLocale } from '../i18n';
import type {
  BindingEngineering,
  DynamoEngineering,
  EquipmentEngineering,
  TemplateEngineering,
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
import { NumericInputVisualElement } from './NumericInputVisualElement';
import './CanonicalVisualInteraction.css';
import { TrendVisualElement } from './TrendVisualElement';
import { SvgSymbolVisualElement } from './SvgSymbolVisualElement';
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
  equipmentDefinitions?: readonly EquipmentEngineering[] | null;
  templateDefinitions?: readonly TemplateEngineering[] | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
  onTagWrite?: SliderTagWrite;
  visualAssetUrl?: VisualAssetUrlResolver;
  showTechnicalFallbackText?: boolean;
  liveBindings?: boolean;
  bindingSamples?: ReadonlyMap<string, VisualLiveScalarSample>;
  operatorTimeRangeControls?: boolean;
};

const builtinVisualTypes = new Set<string>(Object.values(BUILTIN_VISUAL_OBJECT_TYPES));
const emptyElements = Object.freeze([]) as readonly VisualElementEngineering[];

export function CanonicalVisualRenderer({
  elements,
  emptyLabel,
  locale = 'pt-BR',
  dynamoDefinitions,
  equipmentDefinitions,
  templateDefinitions,
  onVisualEvent,
  onTagWrite,
  visualAssetUrl = visualAssetContentUrl,
  showTechnicalFallbackText = true,
  liveBindings = true,
  bindingSamples,
  operatorTimeRangeControls = false
}: CanonicalVisualRendererProps) {
  const rootElements = elements ?? emptyElements;
  const runtimeBindingElements = React.useMemo(
    () => collectRuntimeBindingElements(rootElements, dynamoDefinitions, equipmentDefinitions, templateDefinitions),
    [rootElements, dynamoDefinitions, equipmentDefinitions, templateDefinitions]
  );
  const liveSamples = useVisualBindingSamples(runtimeBindingElements, liveBindings && bindingSamples === undefined);
  const resolvedSamples = bindingSamples ?? liveSamples;
  if (rootElements.length === 0) return <div className="visual-editor-renderer-empty">{emptyLabel}</div>;

  return <div className="visual-editor-renderer-stage" data-testid="visual-editor-canonical-renderer">
    {rootElements.map((element, index) => <CanonicalElement
      key={element.id ?? `${element.key}-${index}`}
      element={element}
      locale={locale}
      liveSamples={resolvedSamples}
      dynamoDefinitions={dynamoDefinitions}
      equipmentDefinitions={equipmentDefinitions}
      templateDefinitions={templateDefinitions}
      onVisualEvent={onVisualEvent}
      onTagWrite={onTagWrite}
      visualAssetUrl={visualAssetUrl}
      showTechnicalFallbackText={showTechnicalFallbackText}
      operatorTimeRangeControls={operatorTimeRangeControls}
    />)}
  </div>;
}

function CanonicalElement({
  element,
  locale,
  liveSamples,
  dynamoDefinitions,
  equipmentDefinitions,
  templateDefinitions,
  onVisualEvent,
  runtimeIdentityPrefix,
  onTagWrite,
  visualAssetUrl,
  showTechnicalFallbackText,
  operatorTimeRangeControls
}: {
  element: VisualElementEngineering;
  locale: EngineeringLocale;
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  equipmentDefinitions?: readonly EquipmentEngineering[] | null;
  templateDefinitions?: readonly TemplateEngineering[] | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
  runtimeIdentityPrefix?: string;
  onTagWrite?: SliderTagWrite;
  visualAssetUrl: VisualAssetUrlResolver;
  showTechnicalFallbackText: boolean;
  operatorTimeRangeControls: boolean;
}) {
  if ((element.dynamoDefinitionId || element.dynamoKey) && dynamoDefinitions) {
    return <CanonicalDynamoElement
      element={element}
      locale={locale}
      liveSamples={liveSamples}
      dynamoDefinitions={dynamoDefinitions}
      equipmentDefinitions={equipmentDefinitions}
      templateDefinitions={templateDefinitions}
      onVisualEvent={onVisualEvent}
      onTagWrite={onTagWrite}
      visualAssetUrl={visualAssetUrl}
      showTechnicalFallbackText={showTechnicalFallbackText}
      operatorTimeRangeControls={operatorTimeRangeControls}
    />;
  }

  if (element.equipmentId && equipmentDefinitions && templateDefinitions) {
    return <CanonicalEquipmentElement
      element={element} locale={locale} liveSamples={liveSamples}
      equipmentDefinitions={equipmentDefinitions} templateDefinitions={templateDefinitions}
      onVisualEvent={onVisualEvent} onTagWrite={onTagWrite} visualAssetUrl={visualAssetUrl}
      showTechnicalFallbackText={showTechnicalFallbackText}
      operatorTimeRangeControls={operatorTimeRangeControls}
    />;
  }

  const runtimeObjectId = runtimeElementIdentity(element, runtimeIdentityPrefix);
  const onClick = visualClickHandler(element, onVisualEvent, runtimeObjectId);

  if (!builtinVisualTypes.has(element.type)) {
    return <LegacyCompatibilityElement
      element={element}
      runtimeObjectId={runtimeObjectId}
      onClick={onClick}
      showTechnicalFallbackText={showTechnicalFallbackText}
      operatorTimeRangeControls={operatorTimeRangeControls}
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
          equipmentDefinitions={equipmentDefinitions}
          templateDefinitions={templateDefinitions}
          onVisualEvent={onVisualEvent}
          runtimeIdentityPrefix={runtimeIdentityPrefix}
          onTagWrite={onTagWrite}
          visualAssetUrl={visualAssetUrl}
          showTechnicalFallbackText={showTechnicalFallbackText}
      operatorTimeRangeControls={operatorTimeRangeControls}
        />)}
      </div>;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol) {
      const assetId = assetReferenceId(values[VISUAL_PROPERTY_KEYS.assetRef]);
      return <div
        className="visual-editor-object visual-editor-svg-symbol"
        style={style}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        data-enabled={enabled}
        data-dynamo-interaction={element.metadata?.dynamoInteraction}
        role={element.metadata?.dynamoInteraction === 'momentary-button' ? 'button' : undefined}
        tabIndex={element.metadata?.dynamoInteraction === 'momentary-button' && enabled ? 0 : undefined}
        title={elementTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
        onKeyDown={element.metadata?.dynamoInteraction === 'momentary-button' ? event => {
          if ((event.key === 'Enter' || event.key === ' ') && onClick) {
            event.preventDefault();
            onClick(event as unknown as React.MouseEvent);
          }
        } : undefined}
      >
        {assetId ? <SvgSymbolVisualElement
          element={element}
          values={values}
          assetUrl={visualAssetUrl(assetId)}
        /> : showTechnicalFallbackText ? <span className="visual-editor-image-placeholder">{element.key}</span> : null}
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
          style={{ width: '100%', height: '100%', objectFit: imageFit(values[VISUAL_PROPERTY_KEYS.imageFit]), objectPosition: `${percent(values[VISUAL_PROPERTY_KEYS.imagePositionX])}% ${percent(values[VISUAL_PROPERTY_KEYS.imagePositionY])}%`, transform: `scale(${numberValue(values[VISUAL_PROPERTY_KEYS.imageZoom], 1)})`, transformOrigin: `${percent(values[VISUAL_PROPERTY_KEYS.imagePositionX])}% ${percent(values[VISUAL_PROPERTY_KEYS.imagePositionY])}%` }}
        /> : showTechnicalFallbackText ? <span className="visual-editor-image-placeholder">{element.key}</span> : null}
      </div>;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.videoPlayer) {
      const assetId = assetReferenceId(values[VISUAL_PROPERTY_KEYS.assetRef]);
      return <div className="visual-editor-object visual-editor-video-player" style={style}
        data-object-id={element.id ?? undefined} data-runtime-object-id={runtimeObjectId}
        data-enabled={enabled} title={elementTitle} data-dynamic-state={diagnosticState}>
        {assetId ? <video
          src={visualAssetUrl(assetId)}
          autoPlay={values[VISUAL_PROPERTY_KEYS.mediaAutoplay] === true}
          muted={values[VISUAL_PROPERTY_KEYS.mediaMuted] !== false}
          loop={values[VISUAL_PROPERTY_KEYS.mediaLoop] === true}
          controls={values[VISUAL_PROPERTY_KEYS.mediaControls] !== false}
          playsInline
          preload="metadata"
          style={{ width: '100%', height: '100%', objectFit: imageFit(values[VISUAL_PROPERTY_KEYS.imageFit]) }}
        /> : showTechnicalFallbackText ? <span className="visual-editor-image-placeholder">{element.key}</span> : null}
      </div>;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.pdfViewer) {
      const assetId = assetReferenceId(values[VISUAL_PROPERTY_KEYS.assetRef]);
      const page = Math.max(1, Math.trunc(numberValue(values[VISUAL_PROPERTY_KEYS.pdfInitialPage], 1)));
      const zoom = Math.max(25, Math.min(400, numberValue(values[VISUAL_PROPERTY_KEYS.pdfZoom], 100)));
      const toolbar = values[VISUAL_PROPERTY_KEYS.pdfToolbarVisible] !== false ? 1 : 0;
      return <div className="visual-editor-object visual-editor-pdf-viewer" style={style}
        data-object-id={element.id ?? undefined} data-runtime-object-id={runtimeObjectId}
        data-enabled={enabled} title={elementTitle} data-dynamic-state={diagnosticState}>
        {assetId ? <iframe
          title={element.key || 'PDF document'}
          src={`${visualAssetUrl(assetId)}#page=${page}&zoom=${zoom}&toolbar=${toolbar}`}
          sandbox="allow-scripts"
          referrerPolicy="no-referrer"
          style={{ width: '100%', height: '100%', border: 0 }}
        /> : showTechnicalFallbackText ? <span className="visual-editor-image-placeholder">{element.key}</span> : null}
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

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.arc) {
      const width = Math.max(numberValue(values[VISUAL_PROPERTY_KEYS.width], 1), 1);
      const height = Math.max(numberValue(values[VISUAL_PROPERTY_KEYS.height], 1), 1);
      const startAngle = numberValue(values[VISUAL_PROPERTY_KEYS.arcStartAngle], 0);
      const endAngle = numberValue(values[VISUAL_PROPERTY_KEYS.arcEndAngle], 90);
      const arcStyle = stringValue(values[VISUAL_PROPERTY_KEYS.arcStyle], 'pie');
      const path = ellipseArcPath(width, height, startAngle, endAngle, arcStyle);
      const strokeStyle = normalizeCanonicalStrokeStyle(values[VISUAL_PROPERTY_KEYS.strokeStyle]);
      const strokeWidth = effectiveStrokeWidth(
        strokeStyle,
        numberValue(values[VISUAL_PROPERTY_KEYS.strokeWidth], 1)
      );
      const gradient = polygonGradient(values);
      const gradientId = gradient
        ? `visual-arc-gradient-${stableDomToken(runtimeObjectId ?? element.id ?? element.key)}`
        : undefined;
      const fill = arcStyle === 'arc'
        ? 'none'
        : stringValue(values[VISUAL_PROPERTY_KEYS.fillStyle], 'solid') === 'none'
          ? 'none'
          : gradientId ? `url(#${gradientId})` : polygonSolidFill(values);
      return <div
        className="visual-editor-object visual-editor-arc"
        style={{ ...style, background: 'transparent', border: 0, overflow: 'visible' }}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        data-enabled={enabled}
        title={elementTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      >
        <svg width="100%" height="100%" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none" aria-label={element.key}>
          {gradient && gradientId ? <defs>
            <linearGradient id={gradientId} x1={gradient.x1} y1={gradient.y1} x2={gradient.x2} y2={gradient.y2}>
              <stop offset="0%" stopColor={gradient.primary} />
              <stop offset="100%" stopColor={gradient.secondary} />
            </linearGradient>
          </defs> : null}
          <path
            d={path}
            fill={fill}
            stroke={strokeStyle === 'none' ? 'none' : stringValue(values[VISUAL_PROPERTY_KEYS.strokeColor], '#000000')}
            strokeWidth={strokeWidth}
            strokeDasharray={svgStrokeDasharray(strokeStyle)}
            vectorEffect="non-scaling-stroke"
          />
        </svg>
      </div>;
    }

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.bezier) {
      const path = safeBezierPath(stringValue(values[VISUAL_PROPERTY_KEYS.bezierPath]));
      const strokeStyle = normalizeCanonicalStrokeStyle(values[VISUAL_PROPERTY_KEYS.strokeStyle]);
      const strokeWidth = effectiveStrokeWidth(
        strokeStyle,
        numberValue(values[VISUAL_PROPERTY_KEYS.strokeWidth], 1)
      );
      const gradient = polygonGradient(values);
      const gradientId = gradient
        ? `visual-bezier-gradient-${stableDomToken(runtimeObjectId ?? element.id ?? element.key)}`
        : undefined;
      return <div
        className="visual-editor-object visual-editor-bezier"
        style={{ ...style, background: 'transparent', border: 0, overflow: 'visible' }}
        data-object-id={element.id ?? undefined}
        data-runtime-object-id={runtimeObjectId}
        data-enabled={enabled}
        title={elementTitle}
        data-dynamic-state={diagnosticState}
        onClick={onClick}
      >
        <svg width="100%" height="100%" viewBox="0 0 100 100" preserveAspectRatio="none" aria-label={element.key}>
          {gradient && gradientId ? <defs>
            <linearGradient id={gradientId} x1={gradient.x1} y1={gradient.y1} x2={gradient.x2} y2={gradient.y2}>
              <stop offset="0%" stopColor={gradient.primary} />
              <stop offset="100%" stopColor={gradient.secondary} />
            </linearGradient>
          </defs> : null}
          <path
            d={path}
            fill={stringValue(values[VISUAL_PROPERTY_KEYS.fillStyle], 'solid') === 'none'
              ? 'none'
              : gradientId ? `url(#${gradientId})` : polygonSolidFill(values)}
            fillRule="evenodd"
            stroke={strokeStyle === 'none' ? 'none' : stringValue(values[VISUAL_PROPERTY_KEYS.strokeColor], '#000000')}
            strokeWidth={strokeWidth}
            strokeDasharray={svgStrokeDasharray(strokeStyle)}
            vectorEffect="non-scaling-stroke"
          />
        </svg>
      </div>;
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
            fillRule={stringValue(values[VISUAL_PROPERTY_KEYS.polygonFillRule], 'nonzero') === 'evenodd' ? 'evenodd' : 'nonzero'}
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
        operatorTimeRangeControls={operatorTimeRangeControls}
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

    if (element.type === BUILTIN_VISUAL_OBJECT_TYPES.numericInput) {
      const numericInputStyle = {
        ...style,
        '--numeric-input-color-editing': stringValue(values[VISUAL_PROPERTY_KEYS.textColorEditing], '#1565C0'),
        '--numeric-input-color-good': stringValue(values[VISUAL_PROPERTY_KEYS.textColorGood], stringValue(values[VISUAL_PROPERTY_KEYS.textColor], '#000000')),
        '--numeric-input-color-bad': stringValue(values[VISUAL_PROPERTY_KEYS.textColorBad], '#C62828')
      } as CSSProperties;
      return <NumericInputVisualElement
        element={element}
        values={values}
        diagnostics={dynamic.diagnostics}
        liveSamples={liveSamples}
        style={numericInputStyle}
        runtimeObjectId={runtimeObjectId}
        locale={locale}
        title={elementTitle}
        onTagWrite={onTagWrite}
      />;
    }

    const staticText = stringValue(values[VISUAL_PROPERTY_KEYS.text]);
    const textBinding = dynamicTextBinding(element.bindings);
    const textSample = textBinding ? bindingSample(liveSamples, textBinding) : undefined;
    const valueFormat = element.type === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay
      ? stringValue(values[VISUAL_PROPERTY_KEYS.valueFormat], 'default')
      : undefined;
    const configuredDecimals = element.type === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay
      && values[VISUAL_PROPERTY_KEYS.decimalPlacesEnabled] === true
      ? numberValue(values[VISUAL_PROPERTY_KEYS.decimalPlaces], 2)
      : undefined;
    const dynamicText = textBinding
      ? formatVisualScalarText(
        textSample,
        textBinding,
        locale,
        valueFormat,
        configuredDecimals,
        element.type !== BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay || values[VISUAL_PROPERTY_KEYS.showEngineeringUnit] !== false
      )
      : null;
    const className = `visual-editor-object visual-editor-${element.type.replace('core.', '')}${dynamicText && !dynamicText.available ? ' visual-editor-dynamic-unavailable' : ''}`;
    const content = dynamicText?.text || staticText || (showTechnicalFallbackText ? element.key : '');
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
    const dynamicValueStyle = element.type === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay && dynamicText
      ? { ...style, color: dynamicText.available
        ? stringValue(values[VISUAL_PROPERTY_KEYS.textColorGood], stringValue(values[VISUAL_PROPERTY_KEYS.textColor], '#000000'))
        : stringValue(values[VISUAL_PROPERTY_KEYS.textColorBad], '#C62828') }
      : style;
    return <div
      className={className}
      style={dynamicValueStyle}
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
  equipmentDefinitions,
  templateDefinitions,
  onVisualEvent,
  onTagWrite,
  visualAssetUrl,
  showTechnicalFallbackText,
  operatorTimeRangeControls
}: {
  element: VisualElementEngineering;
  locale: EngineeringLocale;
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>;
  dynamoDefinitions?: readonly DynamoEngineering[] | null;
  equipmentDefinitions?: readonly EquipmentEngineering[] | null;
  templateDefinitions?: readonly TemplateEngineering[] | null;
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
  onTagWrite?: SliderTagWrite;
  visualAssetUrl: VisualAssetUrlResolver;
  showTechnicalFallbackText: boolean;
  operatorTimeRangeControls: boolean;
}) {
  try {
    const definition = resolveDynamoDefinition(dynamoDefinitions, element.dynamoKey, element.dynamoDefinitionId);
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
        equipmentDefinitions={equipmentDefinitions}
        templateDefinitions={templateDefinitions}
        onVisualEvent={onVisualEvent}
        runtimeIdentityPrefix={composition.instanceId}
        onTagWrite={onTagWrite}
        visualAssetUrl={visualAssetUrl}
        showTechnicalFallbackText={showTechnicalFallbackText}
      operatorTimeRangeControls={operatorTimeRangeControls}
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

function CanonicalEquipmentElement({
  element, locale, liveSamples, equipmentDefinitions, templateDefinitions,
  onVisualEvent, onTagWrite, visualAssetUrl, showTechnicalFallbackText, operatorTimeRangeControls
}: {
  element: VisualElementEngineering;
  locale: EngineeringLocale;
  liveSamples: ReadonlyMap<string, VisualLiveScalarSample>;
  equipmentDefinitions: readonly EquipmentEngineering[];
  templateDefinitions: readonly TemplateEngineering[];
  onVisualEvent?: (event: CanonicalVisualEvent) => void;
  onTagWrite?: SliderTagWrite;
  visualAssetUrl: VisualAssetUrlResolver;
  showTechnicalFallbackText: boolean;
  operatorTimeRangeControls: boolean;
}) {
  const equipment = equipmentDefinitions.find(item => item.id === element.equipmentId);
  const template = equipment && templateDefinitions.find(item =>
    Boolean(equipment.templateId && item.id === equipment.templateId) ||
    Boolean(equipment.templateKey && item.key === equipment.templateKey)
  );
  if (!equipment || !template) {
    return <div className="visual-editor-object-error" data-testid="visual-runtime-equipment-diagnostic">
      {equipment ? `Template não encontrado: ${equipment.templateKey ?? equipment.templateId ?? ''}` : 'Equipamento não encontrado'}
    </div>;
  }
  const instanceId = `equipment:${equipment.id}`;
  const schema = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.group);
  const baseValues: Readonly<Record<string, VisualPropertyValue>> = {
    ...schema.createDefaultValues(),
    ...decodeVisualEngineeringProperties(registeredScalarProperties(element, schema), schema)
  };
  const dynamic = resolveVisualDynamicState(element, baseValues, liveSamples);
  const children = bindTemplateElements(template.elements ?? [], equipment.path);
  return <div className="visual-editor-object visual-editor-group visual-editor-equipment-instance"
    style={elementStyle(dynamic.values)} data-object-id={element.id ?? undefined}
    data-runtime-object-id={instanceId} data-equipment-id={equipment.id}
    data-template-id={template.id} data-template-key={template.key} data-equipment-path={equipment.path}
    onClick={visualClickHandler(element, onVisualEvent, instanceId)}>
    {children.map((child, index) => <CanonicalElement
      key={`${instanceId}:${child.id ?? `${child.key}-${index}`}`}
      element={child} locale={locale} liveSamples={liveSamples}
      equipmentDefinitions={equipmentDefinitions} templateDefinitions={templateDefinitions}
      onVisualEvent={onVisualEvent} runtimeIdentityPrefix={instanceId}
      onTagWrite={onTagWrite} visualAssetUrl={visualAssetUrl}
      showTechnicalFallbackText={showTechnicalFallbackText}
      operatorTimeRangeControls={operatorTimeRangeControls}
    />)}
  </div>;
}

function bindTemplateElements(elements: readonly VisualElementEngineering[], equipmentPath: string): VisualElementEngineering[] {
  const substitute = (value: string) => value.replaceAll('{equipmentPath}', equipmentPath);
  return elements.map(element => ({
    ...element,
    bindings: element.bindings?.map(binding => ({ ...binding, target: substitute(binding.target) })),
    children: element.children ? bindTemplateElements(element.children, equipmentPath) : element.children
  }));
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
  onClick,
  showTechnicalFallbackText
}: {
  element: VisualElementEngineering;
  runtimeObjectId?: string;
  onClick?: (event: React.MouseEvent) => void;
  showTechnicalFallbackText: boolean;
  operatorTimeRangeControls: boolean;
}) {
  const x = legacyNumber(element.properties?.x, 18);
  const y = legacyNumber(element.properties?.y, 18);
  const authoredLabel = legacyString(element.properties?.label);
  const label = authoredLabel || (showTechnicalFallbackText ? element.key || element.type : '');
  return <div
    className="visual-editor-object visual-editor-legacy-placeholder"
    style={{ left: x, top: y }}
    data-object-id={element.id ?? undefined}
    data-runtime-object-id={runtimeObjectId}
    data-legacy-object-type={element.type}
    title={`Legacy visual type: ${element.type}`}
    onClick={onClick}
  >
    {label ? <strong>{label}</strong> : null}{showTechnicalFallbackText ? <span>{element.type}</span> : null}
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
  dynamoDefinitions: readonly DynamoEngineering[] | null | undefined,
  equipmentDefinitions: readonly EquipmentEngineering[] | null | undefined,
  templateDefinitions: readonly TemplateEngineering[] | null | undefined
): readonly VisualElementEngineering[] {
  const dynamoElements = (dynamoDefinitions ?? []).flatMap(definition =>
    [...(asCanonicalDynamo(definition).elements ?? [])]
  );
  const equipmentElements: VisualElementEngineering[] = [];
  const addEquipmentTemplates = (elements: readonly VisualElementEngineering[]) => {
    for (const element of elements) {
      if (element.equipmentId) {
        const equipment = equipmentDefinitions?.find(item => item.id === element.equipmentId);
        const template = equipment && templateDefinitions?.find(item =>
          Boolean(equipment.templateId && item.id === equipment.templateId) ||
          Boolean(equipment.templateKey && item.key === equipment.templateKey)
        );
        if (equipment && template) equipmentElements.push(...bindTemplateElements(template.elements ?? [], equipment.path));
      }
      if (element.children) addEquipmentTemplates(element.children);
    }
  };
  addEquipmentTemplates(rootElements);
  if (dynamoDefinitions) addEquipmentTemplates(dynamoElements);
  return Object.freeze([...rootElements, ...dynamoElements, ...equipmentElements]);
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

function ellipseArcPath(
  width: number,
  height: number,
  startDegrees: number,
  endDegrees: number,
  arcStyle: string
): string {
  const normalize = (degrees: number) => ((degrees % 360) + 360) % 360;
  const start = normalize(startDegrees);
  const end = normalize(endDegrees);
  let sweep = (end - start + 360) % 360;
  if (Math.abs(endDegrees - startDegrees) >= 360) sweep = 359.999;
  if (sweep < 0.001) return '';

  const point = (degrees: number) => {
    const radians = degrees * Math.PI / 180;
    return {
      x: width / 2 + (width / 2) * Math.cos(radians),
      y: height / 2 - (height / 2) * Math.sin(radians)
    };
  };
  const from = point(start);
  const to = point(start + sweep);
  const largeArc = sweep > 180 ? 1 : 0;
  const arc = `M ${from.x} ${from.y} A ${width / 2} ${height / 2} 0 ${largeArc} 0 ${to.x} ${to.y}`;
  if (arcStyle === 'chord') return `${arc} Z`;
  if (arcStyle === 'pie') return `${arc} L ${width / 2} ${height / 2} Z`;
  return arc;
}

function safeBezierPath(path: string): string {
  const normalized = path.trim();
  if (!normalized || !/^[MmLlHhVvCcSsQqTtAaZz0-9eE+\-.,\s]+$/.test(normalized)) {
    return 'M 0 50 C 20 0 80 0 100 50 C 80 100 20 100 0 50 Z';
  }
  return normalized;
}

function fillBackground(values: Readonly<Record<string, VisualPropertyValue>>): string | undefined {
  const backgroundColor = stringValue(values[VISUAL_PROPERTY_KEYS.backgroundColor]);
  if (backgroundColor && !isTransparentColor(backgroundColor)) return backgroundColor;

  const hasFill = Object.prototype.hasOwnProperty.call(values, VISUAL_PROPERTY_KEYS.fillStyle) ||
    Object.prototype.hasOwnProperty.call(values, VISUAL_PROPERTY_KEYS.fillColor);
  if (!hasFill) return backgroundColor || undefined;

  const primary = stringValue(values[VISUAL_PROPERTY_KEYS.fillColor], '#808080FF');
  if (!Object.prototype.hasOwnProperty.call(values, VISUAL_PROPERTY_KEYS.fillStyle)) return primary || undefined;

  const fillStyle = stringValue(values[VISUAL_PROPERTY_KEYS.fillStyle], 'solid');
  if (fillStyle === 'none') return 'transparent';
  if (fillStyle !== 'gradient') return primary || undefined;

  const secondary = stringValue(values[VISUAL_PROPERTY_KEYS.fillSecondaryColor], '#00000000');
  return `linear-gradient(${gradientAngle(values[VISUAL_PROPERTY_KEYS.gradientDirection])}, ${primary}, ${secondary})`;
}

function isTransparentColor(value: string): boolean {
  const normalized = value.trim().toLowerCase();
  if (normalized === 'transparent') return true;
  if (/^#[\da-f]{4}$/.test(normalized)) return normalized.endsWith('0');
  if (/^#[\da-f]{8}$/.test(normalized)) return normalized.endsWith('00');
  const rgba = /^rgba\(\s*[^,]+\s*,\s*[^,]+\s*,\s*[^,]+\s*,\s*([\d.]+)\s*\)$/.exec(normalized);
  return rgba?.[1] !== undefined && Number(rgba[1]) === 0;
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
    primary: stringValue(values[VISUAL_PROPERTY_KEYS.fillColor], '#808080FF'),
    secondary: stringValue(values[VISUAL_PROPERTY_KEYS.fillSecondaryColor], '#00000000'),
    ...coordinates
  };
}

function polygonSolidFill(values: Readonly<Record<string, VisualPropertyValue>>): string {
  if (stringValue(values[VISUAL_PROPERTY_KEYS.fillStyle], 'solid') === 'none') return 'none';
  return stringValue(values[VISUAL_PROPERTY_KEYS.fillColor], '#808080FF');
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
