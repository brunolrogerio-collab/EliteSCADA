import React from 'react';
import type { CSSProperties } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { VisualElementEngineering } from '../types';
import {
  readTrendPens,
  VISUAL_PROPERTY_KEYS,
  type TrendVisualPen,
  type VisualPropertyValue
} from '../../visual-runtime';
import { executeHistoricalQuery } from '../../runtime/historical-browser/historicalQueryApi';
import {
  buildTrendHistoricalQuery,
  buildTrendSeries,
  trendQueryRange,
  type TrendSeries
} from '../../runtime/trendVisualQueryModel';

export type TrendVisualElementProps = Readonly<{
  element: VisualElementEngineering;
  values: Readonly<Record<string, VisualPropertyValue>>;
  style: CSSProperties;
  runtimeObjectId?: string;
  title?: string;
  locale: EngineeringLocale;
  enabled: boolean;
  onClick?: (event: React.MouseEvent) => void;
}>;

type LoadState =
  | Readonly<{ kind: 'idle' | 'loading'; series: readonly TrendSeries[] }>
  | Readonly<{ kind: 'ready'; series: readonly TrendSeries[]; from: number; to: number }>
  | Readonly<{ kind: 'error'; series: readonly TrendSeries[]; message: string }>;

const COPY = {
  'pt-BR': { noData: 'Sem dados', loading: 'Carregando histórico…', error: 'Histórico indisponível', left: 'Esquerda', right: 'Direita' },
  en: { noData: 'No data', loading: 'Loading history…', error: 'History unavailable', left: 'Left', right: 'Right' },
  es: { noData: 'Sin datos', loading: 'Cargando histórico…', error: 'Histórico no disponible', left: 'Izquierda', right: 'Derecha' }
} as const;

export function TrendVisualElement({
  element,
  values,
  style,
  runtimeObjectId,
  title,
  locale,
  enabled,
  onClick
}: TrendVisualElementProps) {
  const text = COPY[locale];
  const pens = React.useMemo(() => readTrendPens(element), [element]);
  const visiblePens = React.useMemo(() => pens.filter(pen => pen.visible), [pens]);
  const mode = stringValue(values[VISUAL_PROPERTY_KEYS.trendMode], 'history');
  const windowSeconds = integerValue(values[VISUAL_PROPERTY_KEYS.trendWindowSeconds], 3600);
  const refreshSeconds = integerValue(values[VISUAL_PROPERTY_KEYS.trendRefreshSeconds], 5);
  const showLegend = booleanValue(values[VISUAL_PROPERTY_KEYS.trendLegendVisible], true);
  const showGrid = booleanValue(values[VISUAL_PROPERTY_KEYS.trendGridVisible], true);
  const showAxes = booleanValue(values[VISUAL_PROPERTY_KEYS.trendAxesVisible], true);
  const showQuality = booleanValue(values[VISUAL_PROPERTY_KEYS.trendQualityVisible], true);
  const [state, setState] = React.useState<LoadState>({ kind: 'idle', series: Object.freeze([]) });

  React.useEffect(() => {
    if (visiblePens.length === 0) {
      setState({ kind: 'idle', series: Object.freeze([]) });
      return;
    }
    let disposed = false;
    let active: AbortController | null = null;

    const load = async () => {
      active?.abort();
      const controller = new AbortController();
      active = controller;
      setState(previous => ({ kind: 'loading', series: previous.series }));
      try {
        const response = await executeHistoricalQuery(
          buildTrendHistoricalQuery(visiblePens, windowSeconds),
          controller.signal
        );
        if (disposed || controller.signal.aborted || active !== controller) return;
        const range = trendQueryRange(response);
        setState({ kind: 'ready', series: buildTrendSeries(response, visiblePens), from: range.from, to: range.to });
      } catch (reason) {
        if (disposed || controller.signal.aborted || active !== controller) return;
        setState(previous => ({
          kind: 'error',
          series: previous.series,
          message: reason instanceof Error ? reason.message : String(reason)
        }));
      }
    };

    void load();
    const interval = mode === 'live'
      ? window.setInterval(() => { void load(); }, Math.max(1, refreshSeconds) * 1000)
      : null;
    return () => {
      disposed = true;
      active?.abort();
      if (interval !== null) window.clearInterval(interval);
    };
  }, [mode, refreshSeconds, visiblePens, windowSeconds]);

  const series = state.series;
  const hasSamples = series.some(item => item.samples.length > 0);
  const range = state.kind === 'ready'
    ? { from: state.from, to: state.to }
    : inferRange(series, windowSeconds);

  return <div
    className="visual-editor-object visual-editor-trend"
    style={{ ...style, display: style.display === 'none' ? 'none' : 'block', background: colorValue(values[VISUAL_PROPERTY_KEYS.backgroundColor], '#0B1220F2') }}
    data-testid="visual-trend"
    data-object-id={element.id ?? undefined}
    data-runtime-object-id={runtimeObjectId}
    data-enabled={enabled}
    data-trend-mode={mode}
    data-trend-pen-count={visiblePens.length}
    data-trend-state={state.kind}
    title={combineTitle(title, state.kind === 'error' ? state.message : undefined)}
    onClick={onClick}
  >
    <svg width="100%" height="100%" viewBox="0 0 1000 400" preserveAspectRatio="none" role="img" aria-label={element.key}>
      <rect x="0" y="0" width="1000" height="400" fill="transparent" />
      {showGrid ? <TrendGrid /> : null}
      {showAxes ? <TrendAxes /> : null}
      {range ? series.map(item => <TrendSeriesPath key={item.pen.id} series={item} range={range} showQuality={showQuality} />) : null}
    </svg>

    {!hasSamples ? <div style={overlayStyle} data-testid="visual-trend-empty">
      {state.kind === 'loading' ? text.loading : state.kind === 'error' ? text.error : text.noData}
    </div> : null}

    {showLegend && visiblePens.length > 0 ? <div style={legendStyle} data-testid="visual-trend-legend">
      {seriesWithEmpty(visiblePens, series).map(item => {
        const latest = item.samples[item.samples.length - 1];
        return <span key={item.pen.id} style={legendItemStyle} data-pen-id={item.pen.id}>
          <i aria-hidden="true" style={{ width: 10, height: 10, borderRadius: 2, background: item.pen.color, display: 'inline-block' }} />
          <strong>{item.pen.label}</strong>
          <span>{latest ? formatNumber(latest.value, locale) : '—'}{item.pen.unit ? ` ${item.pen.unit}` : ''}</span>
          {showQuality && latest ? <small>{latest.quality}</small> : null}
          <small>{item.pen.axis === 'left' ? text.left : text.right}</small>
        </span>;
      })}
    </div> : null}
  </div>;
}

function TrendGrid() {
  const lines = [0, 1, 2, 3, 4, 5];
  return <g stroke="#64748B55" strokeWidth="1" vectorEffect="non-scaling-stroke">
    {lines.map(index => <line key={`h-${index}`} x1="0" x2="1000" y1={index * 80} y2={index * 80} />)}
    {lines.map(index => <line key={`v-${index}`} y1="0" y2="400" x1={index * 200} x2={index * 200} />)}
  </g>;
}

function TrendAxes() {
  return <g stroke="#94A3B8" strokeWidth="1.5" vectorEffect="non-scaling-stroke">
    <line x1="0" x2="0" y1="0" y2="400" />
    <line x1="999" x2="999" y1="0" y2="400" />
    <line x1="0" x2="1000" y1="399" y2="399" />
  </g>;
}

function TrendSeriesPath({
  series,
  range,
  showQuality
}: Readonly<{ series: TrendSeries; range: Readonly<{ from: number; to: number }>; showQuality: boolean }>) {
  const domain = valueDomain(series.pen, series.samples.map(sample => sample.value));
  if (!domain) return null;
  let drawing = false;
  const commands: string[] = [];
  for (const sample of series.samples) {
    const bad = showQuality && isBadQuality(sample.quality);
    if (bad) {
      drawing = false;
      continue;
    }
    const x = ((sample.epochMilliseconds - range.from) / Math.max(1, range.to - range.from)) * 1000;
    const y = 400 - ((sample.value - domain.minimum) / Math.max(Number.EPSILON, domain.maximum - domain.minimum)) * 400;
    commands.push(`${drawing ? 'L' : 'M'}${clamp(x, 0, 1000).toFixed(2)} ${clamp(y, 0, 400).toFixed(2)}`);
    drawing = true;
  }
  if (commands.length === 0) return null;
  return <path
    d={commands.join(' ')}
    fill="none"
    stroke={series.pen.color}
    strokeWidth={series.pen.lineWidth}
    strokeDasharray={dasharray(series.pen.lineStyle)}
    vectorEffect="non-scaling-stroke"
    data-testid="visual-trend-series"
    data-pen-id={series.pen.id}
  />;
}

function valueDomain(pen: TrendVisualPen, values: readonly number[]): Readonly<{ minimum: number; maximum: number }> | null {
  if (pen.scale.mode === 'fixed') return pen.scale;
  if (values.length === 0) return null;
  let minimum = Math.min(...values);
  let maximum = Math.max(...values);
  if (minimum === maximum) {
    const padding = Math.max(Math.abs(minimum) * 0.05, 1);
    minimum -= padding;
    maximum += padding;
  } else {
    const padding = (maximum - minimum) * 0.05;
    minimum -= padding;
    maximum += padding;
  }
  return Object.freeze({ minimum, maximum });
}

function inferRange(series: readonly TrendSeries[], windowSeconds: number): Readonly<{ from: number; to: number }> | null {
  const epochs = series.flatMap(item => item.samples.map(sample => sample.epochMilliseconds));
  if (epochs.length === 0) return null;
  const to = Math.max(...epochs);
  return Object.freeze({ from: Math.min(Math.min(...epochs), to - windowSeconds * 1000), to });
}

function seriesWithEmpty(pens: readonly TrendVisualPen[], series: readonly TrendSeries[]): readonly TrendSeries[] {
  const byId = new Map(series.map(item => [item.pen.id, item]));
  return pens.map(pen => byId.get(pen.id) ?? Object.freeze({ pen, samples: Object.freeze([]) }));
}

function dasharray(style: TrendVisualPen['lineStyle']): string | undefined {
  return style === 'dashed' ? '10 6' : style === 'dotted' ? '2 5' : undefined;
}

function isBadQuality(value: string): boolean {
  const quality = value.trim().toLowerCase();
  return quality.startsWith('bad') || quality === 'invalid';
}

function formatNumber(value: number, locale: EngineeringLocale): string {
  return new Intl.NumberFormat(locale, { maximumFractionDigits: 3 }).format(value);
}

function stringValue(value: VisualPropertyValue | undefined, fallback: string): string {
  return typeof value === 'string' ? value : fallback;
}
function colorValue(value: VisualPropertyValue | undefined, fallback: string): string {
  return typeof value === 'string' ? value : fallback;
}
function booleanValue(value: VisualPropertyValue | undefined, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback;
}
function integerValue(value: VisualPropertyValue | undefined, fallback: number): number {
  return typeof value === 'number' && Number.isSafeInteger(value) ? value : fallback;
}
function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}
function combineTitle(...parts: Array<string | undefined>): string | undefined {
  const value = parts.filter((part): part is string => Boolean(part)).join('\n');
  return value || undefined;
}

const overlayStyle: CSSProperties = {
  position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center',
  color: '#CBD5E1', fontSize: 13, pointerEvents: 'none'
};
const legendStyle: CSSProperties = {
  position: 'absolute', left: 8, right: 8, top: 6, display: 'flex', flexWrap: 'wrap', gap: 8,
  color: '#E2E8F0', fontSize: 11, pointerEvents: 'none'
};
const legendItemStyle: CSSProperties = {
  display: 'inline-flex', alignItems: 'center', gap: 4, background: '#020617B8', padding: '2px 5px', borderRadius: 3
};
