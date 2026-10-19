import React, { type CSSProperties } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { VisualElementEngineering } from '../types';
import { describeLiveValueFreshness } from '../../runtime/liveValueFreshness';
import type { VisualDynamicSample } from './visualDynamicRuntime';
import { measurementBandPercentages, measurementQualityState, measurementScale, type MeasurementQualityState } from './measurementVisualModel';
import { VISUAL_PROPERTY_KEYS, type BuiltinVisualObjectType, type VisualPropertyValue } from '../../visual-runtime';
import './MeasurementVisualElement.css';

export type MeasurementVisualElementProps = Readonly<{
  element: VisualElementEngineering;
  objectType: BuiltinVisualObjectType;
  values: Readonly<Record<string, VisualPropertyValue>>;
  sample?: VisualDynamicSample;
  hasValueBinding: boolean;
  style: CSSProperties;
  runtimeObjectId?: string;
  title?: string;
  locale: EngineeringLocale;
  onClick?: (event: React.MouseEvent) => void;
}>;

const TEXT = {
  'pt-BR': { good: 'Boa', uncertain: 'Incerta', bad: 'Ruim', badCommunication: 'Falha de comunicação', badConfiguration: 'Falha de configuração', badDevice: 'Falha do dispositivo', stale: 'Desatualizada', disabled: 'Desabilitada', unknown: 'Desconhecida', fresh: 'Atual', aging: 'Envelhecendo', unavailable: 'Indisponível', noTimestamp: 'Sem horário de amostra', limits: 'Limites', actual: 'Atual', target: 'Meta', band: 'Faixa', bandThresholds: 'Limites das faixas', invalidBandLimits: 'Limites das faixas inválidos', dial: 'Mostrador semicircular', ring: 'Anel de medição', bar: 'Barra graduada', thermometer: 'Régua vertical', bands: 'Faixas de intervalo', comparison: 'Valor atual e meta', configured: 'Configurado' },
  en: { good: 'Good', uncertain: 'Uncertain', bad: 'Bad', badCommunication: 'Communication fault', badConfiguration: 'Configuration fault', badDevice: 'Device fault', stale: 'Stale', disabled: 'Disabled', unknown: 'Unknown', fresh: 'Fresh', aging: 'Aging', unavailable: 'Unavailable', noTimestamp: 'No sample time', limits: 'Limits', actual: 'Actual', target: 'Target', band: 'Band', bandThresholds: 'Band thresholds', invalidBandLimits: 'Invalid band limits', dial: 'Semicircular dial', ring: 'Measurement ring', bar: 'Graduated bar', thermometer: 'Vertical ruler', bands: 'Range bands', comparison: 'Actual vs target', configured: 'Configured' },
  es: { good: 'Buena', uncertain: 'Incierta', bad: 'Mala', badCommunication: 'Fallo de comunicación', badConfiguration: 'Fallo de configuración', badDevice: 'Fallo del dispositivo', stale: 'Desactualizada', disabled: 'Deshabilitada', unknown: 'Desconocida', fresh: 'Actual', aging: 'Envejeciendo', unavailable: 'No disponible', noTimestamp: 'Sin hora de muestra', limits: 'Límites', actual: 'Actual', target: 'Objetivo', band: 'Banda', bandThresholds: 'Límites de banda', invalidBandLimits: 'Límites de banda no válidos', dial: 'Indicador semicircular', ring: 'Anillo de medición', bar: 'Barra graduada', thermometer: 'Regla vertical', bands: 'Bandas de rango', comparison: 'Valor actual y objetivo', configured: 'Configurado' }
} as const;

const QUALITY_COLORS: Record<MeasurementQualityState, string> = {
  good: '#166534', uncertain: '#92400E', bad: '#991B1B', 'bad-communication': '#991B1B',
  'bad-configuration': '#991B1B', 'bad-device': '#991B1B', stale: '#92400E', disabled: '#475569',
  unknown: '#475569', unavailable: '#991B1B'
};

export function MeasurementVisualElement({ element, objectType, values, sample, hasValueBinding, style, runtimeObjectId, title, locale, onClick }: MeasurementVisualElementProps) {
  const copy = TEXT[locale];
  const [now, setNow] = React.useState(() => Date.now());
  const showFreshness = values[VISUAL_PROPERTY_KEYS.measurementFreshnessVisible] !== false;
  React.useEffect(() => {
    if (!showFreshness || !hasValueBinding) return undefined;
    const timer = window.setInterval(() => setNow(Date.now()), 3000);
    return () => window.clearInterval(timer);
  }, [showFreshness, hasValueBinding]);

  const minimum = numeric(values[VISUAL_PROPERTY_KEYS.minimum], 0);
  const maximum = numeric(values[VISUAL_PROPERTY_KEYS.maximum], 100);
  const decimals = values[VISUAL_PROPERTY_KEYS.decimalPlacesEnabled] === true
    ? Math.max(0, Math.min(12, Math.trunc(numeric(values[VISUAL_PROPERTY_KEYS.decimalPlaces], 2))))
    : null;
  const unit = string(values[VISUAL_PROPERTY_KEYS.measurementUnit], '').trim() ||
    (hasValueBinding ? string(element.bindings?.find(binding => binding.key === VISUAL_PROPERTY_KEYS.value)?.metadata?.engineeringUnit, '').trim() : '');
  const sampleNumber = typeof sample?.value === 'number' && Number.isFinite(sample.value) ? sample.value : null;
  const staticNumber = numeric(values[VISUAL_PROPERTY_KEYS.value], Number.NaN);
  const value = hasValueBinding ? sampleNumber : (Number.isFinite(staticNumber) ? staticNumber : null);
  const sampleUnavailable = hasValueBinding && Boolean(sample?.state && sample.state !== 'LocalSession');
  const valueAvailable = value !== null && !sampleUnavailable;
  const displayValue = valueAvailable ? format(value!, decimals, locale) : '—';
  const displayWithUnit = unit ? `${displayValue} ${unit}` : displayValue;
  const scale = measurementScale(value ?? minimum, minimum, maximum);
  const quality = measurementQualityState(sample, hasValueBinding);
  const freshness = hasValueBinding && showFreshness && !sampleUnavailable
    ? describeLiveValueFreshness(sample?.timestamp, now, numeric(values[VISUAL_PROPERTY_KEYS.measurementStaleAfterSeconds], 30) * 1000)
    : null;
  const freshnessLabel = freshness === null ? null : freshness.state === 'fresh'
    ? `${copy.fresh} · ${ageLabel(freshness.ageMilliseconds)}`
    : freshness.state === 'aging'
      ? `${copy.aging} · ${ageLabel(freshness.ageMilliseconds)}`
      : freshness.state === 'stale'
        ? `${copy.stale} · ${ageLabel(freshness.ageMilliseconds)}`
        : copy.noTimestamp;
  const qualityText = qualityLabel(quality, copy);
  const widgetLabel = objectType === 'core.measurementDial' ? copy.dial
    : objectType === 'core.measurementRing' ? copy.ring
      : objectType === 'core.measurementBar' ? copy.bar
        : objectType === 'core.measurementThermometer' ? copy.thermometer
          : objectType === 'core.measurementBands' ? copy.bands : copy.comparison;
  const target = numeric(values[VISUAL_PROPERTY_KEYS.measurementTarget], 50);
  const targetScale = measurementScale(target, minimum, maximum);
  const bands = measurementBandPercentages(
    minimum,
    maximum,
    numeric(values[VISUAL_PROPERTY_KEYS.measurementBandOneEnd], 33),
    numeric(values[VISUAL_PROPERTY_KEYS.measurementBandTwoEnd], 66)
  );
  const theme = string(values[VISUAL_PROPERTY_KEYS.measurementTheme], 'inherit');
  const themeColors = measurementTheme(theme);
  const configuredTrackColor = string(values[VISUAL_PROPERTY_KEYS.trackColor], '#6B7280');
  const configuredFillColor = string(values[VISUAL_PROPERTY_KEYS.thumbColor], '#E5E7EB');
  const trackColor = theme === 'inherit' ? configuredTrackColor : themeColors.track;
  const fillColor = theme === 'inherit' ? configuredFillColor : themeColors.accent;
  const qualityColor = QUALITY_COLORS[quality];
  const description = [
    widgetLabel,
    `${copy.actual}: ${displayWithUnit}`,
    `${copy.limits}: ${format(minimum, decimals, locale)}–${format(maximum, decimals, locale)}${unit ? ` ${unit}` : ''}`,
    objectType === 'core.measurementTarget' ? `${copy.target}: ${format(target, decimals, locale)}${unit ? ` ${unit}` : ''}` : '',
    objectType === 'core.measurementBands' ? `${copy.bandThresholds}: ${format(numeric(values[VISUAL_PROPERTY_KEYS.measurementBandOneEnd], 33), decimals, locale)}${unit ? ` ${unit}` : ''}, ${format(numeric(values[VISUAL_PROPERTY_KEYS.measurementBandTwoEnd], 66), decimals, locale)}${unit ? ` ${unit}` : ''}` : '',
    showQuality(values) ? qualityText : '',
    freshnessLabel ?? ''
  ].filter(Boolean).join('. ');
  const cssVars = {
    '--measurement-foreground': themeColors.foreground,
    '--measurement-surface': themeColors.surface,
    '--measurement-track': theme === 'inherit' ? trackColor : themeColors.track,
    '--measurement-fill': theme === 'inherit' ? fillColor : themeColors.accent,
    '--measurement-good': themeColors.good,
    '--measurement-warning': themeColors.warning,
    '--measurement-bad': themeColors.bad
  } as CSSProperties;
  const common = { minimum, maximum, scale, value, displayValue, displayWithUnit, unit, decimals, locale, copy };

  return <div
    className={`visual-measurement visual-measurement--${objectType.replace('core.measurement', '').toLowerCase()} visual-measurement--${theme}`}
    style={{ ...style, ...cssVars, color: themeColors.foreground, background: themeColors.surface }}
    data-testid="visual-measurement-widget"
    data-widget-type={objectType}
    data-object-id={element.id ?? undefined}
    data-runtime-object-id={runtimeObjectId}
    data-dynamic-state={valueAvailable ? 'available' : 'unavailable'}
    data-quality-state={quality}
    data-freshness-state={freshness?.state ?? 'unavailable'}
    role="img"
    aria-label={description}
    title={title}
    onClick={onClick}
  >
    <div className="visual-measurement__graphic" aria-hidden="true">
      {objectType === 'core.measurementDial' ? <Dial {...common} trackColor={trackColor} fillColor={fillColor} /> : null}
      {objectType === 'core.measurementRing' ? <Ring {...common} trackColor={trackColor} fillColor={fillColor} /> : null}
      {objectType === 'core.measurementBar' ? <Bar {...common} trackColor={trackColor} fillColor={fillColor} /> : null}
      {objectType === 'core.measurementThermometer' ? <Thermometer {...common} trackColor={trackColor} fillColor={fillColor} /> : null}
      {objectType === 'core.measurementBands' ? <Bands {...common} bands={bands} bandOneEnd={numeric(values[VISUAL_PROPERTY_KEYS.measurementBandOneEnd], 33)} bandTwoEnd={numeric(values[VISUAL_PROPERTY_KEYS.measurementBandTwoEnd], 66)} valuePercent={scale.percent} copy={copy} /> : null}
      {objectType === 'core.measurementTarget' ? <Target {...common} target={target} targetPercent={targetScale.percent} trackColor={trackColor} fillColor={fillColor} copy={copy} /> : null}
    </div>
    <div className="visual-measurement__status" aria-hidden="true">
      {showQuality(values) ? <span className="visual-measurement__badge" style={{ borderColor: qualityColor, color: qualityColor }}>{hasValueBinding ? qualityText : copy.configured}</span> : null}
      {freshnessLabel ? <span className={`visual-measurement__badge visual-measurement__badge--${freshness?.state ?? 'unavailable'}`}>{freshnessLabel}</span> : null}
    </div>
  </div>;
}

type GraphicsProps = Readonly<{ minimum: number; maximum: number; scale: ReturnType<typeof measurementScale>; value: number | null; displayValue: string; displayWithUnit: string; unit: string; decimals: number | null; locale: EngineeringLocale; copy: typeof TEXT[EngineeringLocale] }>;

function Dial({ scale, displayWithUnit, minimum, maximum, trackColor, fillColor, ...rest }: GraphicsProps & { trackColor: string; fillColor: string }) {
  return <>
    <svg viewBox="0 0 160 104" preserveAspectRatio="none"><path d="M 18 86 A 62 62 0 0 1 142 86" pathLength="100" fill="none" stroke={trackColor} strokeWidth="12" strokeLinecap="round"/><path d="M 18 86 A 62 62 0 0 1 142 86" pathLength="100" fill="none" stroke={fillColor} strokeWidth="12" strokeLinecap="round" strokeDasharray={`${scale.percent} 100`}/></svg>
    <strong>{displayWithUnit}</strong><div className="visual-measurement__limits"><span>{format(minimum, rest.decimals, rest.locale)}</span><span>{format(maximum, rest.decimals, rest.locale)}</span></div>
  </>;
}

function Ring({ scale, displayWithUnit, trackColor, fillColor }: GraphicsProps & { trackColor: string; fillColor: string }) {
  return <>
    <svg viewBox="0 0 100 100"><circle cx="50" cy="50" r="38" pathLength="100" fill="none" stroke={trackColor} strokeWidth="10"/><circle cx="50" cy="50" r="38" pathLength="100" fill="none" stroke={fillColor} strokeWidth="10" strokeDasharray={`${scale.percent} 100`} transform="rotate(-90 50 50)" strokeLinecap="round"/></svg>
    <strong>{displayWithUnit}</strong>
  </>;
}

function Bar({ scale, displayWithUnit, minimum, maximum, trackColor, fillColor, decimals, locale }: GraphicsProps & { trackColor: string; fillColor: string }) {
  return <>
    <strong>{displayWithUnit}</strong><div className="visual-measurement__track" style={{ background: trackColor }}><span style={{ width: `${scale.percent}%`, background: fillColor }} /></div>
    <div className="visual-measurement__limits"><span>{format(minimum, decimals, locale)}</span><span>{format(maximum, decimals, locale)}</span></div>
  </>;
}

function Thermometer({ scale, displayWithUnit, minimum, maximum, trackColor, fillColor, decimals, locale }: GraphicsProps & { trackColor: string; fillColor: string }) {
  return <>
    <strong>{displayWithUnit}</strong><div className="visual-measurement__thermometer"><span className="visual-measurement__thermometer-track" style={{ background: trackColor }}><span style={{ height: `${scale.percent}%`, background: fillColor }} /></span><span className="visual-measurement__thermometer-limits"><small>{format(maximum, decimals, locale)}</small><small>{format(minimum, decimals, locale)}</small></span></div>
  </>;
}

function Bands({ scale, displayWithUnit, decimals, locale, minimum, maximum, bands, bandOneEnd, bandTwoEnd, valuePercent, unit, copy }: GraphicsProps & { bands: Readonly<{ first: number; second: number; valid: boolean }>; bandOneEnd: number; bandTwoEnd: number; valuePercent: number; unit: string; copy: typeof TEXT[EngineeringLocale] }) {
  return <>
    <strong>{displayWithUnit}</strong><div className="visual-measurement__band-track">
      <span style={{ width: `${bands.first}%`, background: 'var(--measurement-good)' }} />
      <span style={{ width: `${Math.max(0, bands.second - bands.first)}%`, background: 'var(--measurement-warning)' }} />
      <span style={{ width: `${Math.max(0, 100 - bands.second)}%`, background: 'var(--measurement-bad)' }} />
      {scale.valid ? <i style={{ left: `${valuePercent}%` }} /> : null}
    </div>
    <div className="visual-measurement__band-labels"><span>{format(minimum, decimals, locale)}</span><span>{format(maximum, decimals, locale)}</span></div>
    <small className="visual-measurement__band-thresholds">{copy.bandThresholds}: {withUnit(bandOneEnd, unit, decimals, locale)} · {withUnit(bandTwoEnd, unit, decimals, locale)}</small>
    {!bands.valid ? <small className="visual-measurement__configuration-error">{copy.invalidBandLimits}</small> : null}
  </>;
}

function Target({ scale, target, targetPercent, displayWithUnit, minimum, maximum, trackColor, fillColor, unit, decimals, locale, copy }: GraphicsProps & { target: number; targetPercent: number; trackColor: string; fillColor: string; copy: typeof TEXT[EngineeringLocale] }) {
  return <>
    <div className="visual-measurement__comparison"><span>{copy.actual}</span><strong>{displayWithUnit}</strong></div>
    <div className="visual-measurement__target-track" style={{ background: trackColor }}>
      <span style={{ width: `${scale.percent}%`, background: fillColor }} />
      <i style={{ left: `${targetPercent}%` }} />
    </div>
    <div className="visual-measurement__limits"><span>{format(minimum, decimals, locale)}</span><span>{copy.target}: {withUnit(target, unit, decimals, locale)}</span><span>{format(maximum, decimals, locale)}</span></div>
  </>;
}

function measurementTheme(theme: string) {
  if (theme === 'dark') return { foreground: '#F8FAFC', surface: '#0F172A', track: '#475569', accent: '#38BDF8', good: '#34D399', warning: '#FBBF24', bad: '#F87171' };
  if (theme === 'high-contrast') return { foreground: '#000000', surface: '#FFFFFF', track: '#111111', accent: '#003B8E', good: '#005A00', warning: '#8A4200', bad: '#9B0000' };
  return { foreground: 'var(--text-primary, #111827)', surface: theme === 'light' ? '#FFFFFF' : 'transparent', track: '#6B7280', accent: '#0E7490', good: '#166534', warning: '#B45309', bad: '#991B1B' };
}

function qualityLabel(quality: MeasurementQualityState, copy: typeof TEXT[EngineeringLocale]): string {
  switch (quality) {
    case 'good': return copy.good;
    case 'uncertain': return copy.uncertain;
    case 'bad': return copy.bad;
    case 'bad-communication': return copy.badCommunication;
    case 'bad-configuration': return copy.badConfiguration;
    case 'bad-device': return copy.badDevice;
    case 'stale': return copy.stale;
    case 'disabled': return copy.disabled;
    case 'unavailable': return copy.unavailable;
    default: return copy.unknown;
  }
}

function showQuality(values: Readonly<Record<string, VisualPropertyValue>>) {
  return values[VISUAL_PROPERTY_KEYS.measurementQualityVisible] !== false;
}

function format(value: number, decimals: number | null, locale: EngineeringLocale) {
  if (!Number.isFinite(value)) return '—';
  return new Intl.NumberFormat(locale, decimals === null
    ? { maximumFractionDigits: 6 }
    : { minimumFractionDigits: decimals, maximumFractionDigits: decimals }).format(value);
}

function withUnit(value: number, unit: string, decimals: number | null, locale: EngineeringLocale) {
  const formatted = format(value, decimals, locale);
  return unit ? `${formatted} ${unit}` : formatted;
}

function ageLabel(age: number | null) {
  if (age === null) return '—';
  return `${Math.floor(age / 1000)} s`;
}

function numeric(value: unknown, fallback: number) {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function string(value: unknown, fallback: string) {
  return typeof value === 'string' ? value : fallback;
}


