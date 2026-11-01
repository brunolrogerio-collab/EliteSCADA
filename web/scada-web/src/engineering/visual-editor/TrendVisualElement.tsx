import React from 'react';
import { createPortal } from 'react-dom';
import type { CSSProperties } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { VisualElementEngineering } from '../types';
import {
  readTrendPens,
  VISUAL_PROPERTY_KEYS,
  type TrendVisualPen,
  type VisualPropertyValue
} from '../../visual-runtime';
import {
  createHistoricalTimeRangeState,
  historicalDurationSeconds,
  validateHistoricalTimeRange,
  type HistoricalTimeRangeState
} from '../../runtime/historicalTimeRange';
import { HistoricalTimeRangeControls } from '../../runtime/HistoricalTimeRangeControls';
import {
  loadReadableRuntimeTags,
  openRuntimeTagSocket,
  parseRuntimeTagRealtimeMessage
} from '../../runtime/liveTagTransport';
import {
  appendTrendLiveSample,
  buildTrendLiveSeries,
  isUsableTrendQuality,
  pruneTrendLiveBuffers,
  trendSampleFromRuntimeMessage,
  trendSampleFromRuntimeSnapshot,
  type TrendLiveBuffers
} from '../../runtime/trendLiveSeriesModel';
import {
  buildTrendHistoricalQuery,
  buildTrendSeries,
  executeTrendHistoricalQuery,
  trendQueryRange,
  type TrendSample,
  type TrendSeries
} from '../../runtime/trendVisualQueryModel';
import { formatTrendAxisLabel } from './trendAxisNumberFormat';

export type TrendVisualElementProps = Readonly<{
  element: VisualElementEngineering;
  values: Readonly<Record<string, VisualPropertyValue>>;
  style: CSSProperties;
  runtimeObjectId?: string;
  title?: string;
  locale: EngineeringLocale;
  enabled: boolean;
  operatorTimeRangeControls?: boolean;
  dataQueriesEnabled?: boolean;
  onClick?: (event: React.MouseEvent) => void;
}>;

type LoadState =
  | Readonly<{ kind: 'idle' | 'loading'; series: readonly TrendSeries[] }>
  | Readonly<{ kind: 'ready'; series: readonly TrendSeries[]; from: number; to: number }>
  | Readonly<{ kind: 'error'; series: readonly TrendSeries[]; message: string }>;

const COPY = {
  'pt-BR': {
    noData: 'Sem dados',
    loadingHistory: 'Carregando histórico…',
    loadingLive: 'Conectando dados ao vivo…',
    historyError: 'Histórico indisponível',
    liveError: 'Dados ao vivo indisponíveis',
    left: 'Esquerda',
    right: 'Direita',
    showPen: 'Ativar pena',
    hidePen: 'Desativar pena',
    chartMenu: 'Controles do gráfico',
    zoomIn: 'Ampliar',
    zoomOut: 'Reduzir',
    resetZoom: 'Redefinir zoom',
    restoreScale: 'Usar escala configurada',
    scale: 'Escala manual do operador',
    axisLeft: 'Eixo esquerdo',
    axisRight: 'Eixo direito',
    minimum: 'Mínimo',
    maximum: 'Máximo',
    applyScale: 'Aplicar escala',
    close: 'Fechar controles',
    invalidScale: 'O máximo precisa ser maior que o mínimo.',
    enableReferenceLine: 'Ativar linha de referência',
    disableReferenceLine: 'Desativar linha de referência'
  },
  en: {
    noData: 'No data',
    loadingHistory: 'Loading history…',
    loadingLive: 'Connecting live data…',
    historyError: 'History unavailable',
    liveError: 'Live data unavailable',
    left: 'Left',
    right: 'Right',
    showPen: 'Enable pen',
    hidePen: 'Disable pen',
    chartMenu: 'Chart controls',
    zoomIn: 'Zoom in',
    zoomOut: 'Zoom out',
    resetZoom: 'Reset zoom',
    restoreScale: 'Use configured scale',
    scale: 'Operator manual scale',
    axisLeft: 'Left axis',
    axisRight: 'Right axis',
    minimum: 'Minimum',
    maximum: 'Maximum',
    applyScale: 'Apply scale',
    close: 'Close controls',
    invalidScale: 'Maximum must be greater than minimum.',
    enableReferenceLine: 'Enable reference line',
    disableReferenceLine: 'Disable reference line'
  },
  es: {
    noData: 'Sin datos',
    loadingHistory: 'Cargando histórico…',
    loadingLive: 'Conectando datos en vivo…',
    historyError: 'Histórico no disponible',
    liveError: 'Datos en vivo no disponibles',
    left: 'Izquierda',
    right: 'Derecha',
    showPen: 'Activar pluma',
    hidePen: 'Desactivar pluma',
    chartMenu: 'Controles del gráfico',
    zoomIn: 'Ampliar',
    zoomOut: 'Reducir',
    resetZoom: 'Restablecer zoom',
    restoreScale: 'Usar escala configurada',
    scale: 'Escala manual del operador',
    axisLeft: 'Eje izquierdo',
    axisRight: 'Eje derecho',
    minimum: 'Mínimo',
    maximum: 'Máximo',
    applyScale: 'Aplicar escala',
    close: 'Cerrar controles',
    invalidScale: 'El máximo debe ser mayor que el mínimo.',
    enableReferenceLine: 'Activar línea de referencia',
    disableReferenceLine: 'Desactivar línea de referencia'
  }
} as const;

export function TrendVisualElement({
  element,
  values,
  style,
  runtimeObjectId,
  title,
  locale,
  enabled,
  operatorTimeRangeControls = false,
  dataQueriesEnabled = true,
  onClick
}: TrendVisualElementProps) {
  const text = COPY[locale];
  const pens = React.useMemo(() => readTrendPens(element), [element]);
  const persistedPenSignature = pens.map(pen => `${pen.id}:${pen.visible}`).join('|');
  const [operatorPenVisibility, setOperatorPenVisibility] = React.useState<Readonly<Record<string, boolean>>>({});
  React.useEffect(() => setOperatorPenVisibility({}), [element.id, persistedPenSignature]);
  const visiblePens = React.useMemo(
    () => pens.filter(pen => operatorPenVisibility[pen.id] ?? pen.visible),
    [operatorPenVisibility, pens]
  );
  const mode = stringValue(values[VISUAL_PROPERTY_KEYS.trendMode], 'history');
  const windowSeconds = integerValue(values[VISUAL_PROPERTY_KEYS.trendWindowSeconds], 3600);
  const refreshSeconds = integerValue(values[VISUAL_PROPERTY_KEYS.trendRefreshSeconds], 5);
  const showLegend = booleanValue(values[VISUAL_PROPERTY_KEYS.trendLegendVisible], true);
  const showGrid = booleanValue(values[VISUAL_PROPERTY_KEYS.trendGridVisible], true);
  const showAxes = booleanValue(values[VISUAL_PROPERTY_KEYS.trendAxesVisible], true);
  const showLeftAxis = booleanValue(values[VISUAL_PROPERTY_KEYS.trendLeftAxisVisible], true);
  const showRightAxis = booleanValue(values[VISUAL_PROPERTY_KEYS.trendRightAxisVisible], true);
  const showAxisLabels = booleanValue(values[VISUAL_PROPERTY_KEYS.trendAxisLabelsVisible], true);
  const showTimeAxis = booleanValue(values[VISUAL_PROPERTY_KEYS.trendTimeAxisVisible], true);
  const axisFontSize = numberValue(values[VISUAL_PROPERTY_KEYS.trendAxisFontSize], 16);
  const legendFontSize = numberValue(values[VISUAL_PROPERTY_KEYS.trendLegendFontSize], 14);
  const showQuality = booleanValue(values[VISUAL_PROPERTY_KEYS.trendQualityVisible], true);
  const engineeredTimeRange = React.useMemo(
    () => createHistoricalTimeRangeState({
      mode: mode === 'live' ? 'live' : 'relative',
      durationSeconds: windowSeconds
    }),
    [mode, windowSeconds]
  );
  const [operatorTimeRange, setOperatorTimeRange] = React.useState<HistoricalTimeRangeState>(() => engineeredTimeRange);
  const [zoomFactor, setZoomFactor] = React.useState(1);
  const [operatorScale, setOperatorScale] = React.useState<Partial<Record<TrendVisualPen['axis'], TrendDomain>>>({});
  const isEditingTimeRange = React.useRef(false);
  const [scaleDraft, setScaleDraft] = React.useState<Readonly<Record<TrendVisualPen['axis'], Readonly<{ minimum: string; maximum: string }>>>>({
    left: { minimum: '0', maximum: '100' }, right: { minimum: '0', maximum: '100' }
  });
  const [chartMenu, setChartMenu] = React.useState<Readonly<{ left: number; top: number }> | null>(null);
  const [referenceLineEnabled, setReferenceLineEnabled] = React.useState(false);
  const [referenceEpochMilliseconds, setReferenceEpochMilliseconds] = React.useState<number | null>(null);
  const chartRef = React.useRef<HTMLDivElement>(null);
  const [refreshRevision, setRefreshRevision] = React.useState(0);
  const activeTimeRange = operatorTimeRangeControls ? operatorTimeRange : engineeredTimeRange;
  React.useEffect(() => {
    setZoomFactor(1);
    setOperatorScale({});
    setChartMenu(null);
    setReferenceEpochMilliseconds(null);
  }, [element.id, activeTimeRange]);
  React.useEffect(() => {
    setReferenceLineEnabled(false);
    setReferenceEpochMilliseconds(null);
  }, [element.id]);
  React.useEffect(() => {
    if (!chartMenu) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setChartMenu(null);
    };
    const closeOnOutsidePointer = (event: PointerEvent) => {
      const menu = document.querySelector('[data-testid="visual-trend-context-menu"]');
      if (event.target instanceof Node && menu && !menu.contains(event.target)) setChartMenu(null);
    };
    window.addEventListener('keydown', closeOnEscape);
    window.addEventListener('pointerdown', closeOnOutsidePointer, true);
    return () => {
      window.removeEventListener('keydown', closeOnEscape);
      window.removeEventListener('pointerdown', closeOnOutsidePointer, true);
    };
  }, [chartMenu]);
  const activeDurationSeconds = historicalDurationSeconds(activeTimeRange);
  const [state, setState] = React.useState<LoadState>({ kind: 'idle', series: Object.freeze([]) });

  React.useEffect(() => {
    if (!dataQueriesEnabled) {
      setState({ kind: 'idle', series: Object.freeze([]) });
      return undefined;
    }
    if (visiblePens.length === 0) {
      setState({ kind: 'idle', series: Object.freeze([]) });
      return;
    }
    if (!validateHistoricalTimeRange(activeTimeRange).ok) {
      setState(previous => ({ kind: 'error', series: previous.series, message: 'Invalid historical time range.' }));
      return;
    }

    if (activeTimeRange.mode === 'live') {
      let disposed = false;
      let buffers: TrendLiveBuffers = new Map();
      const controller = new AbortController();
      let socket: WebSocket | null = null;

      const publish = (now = Date.now()) => {
        if (disposed) return;
        buffers = pruneTrendLiveBuffers(buffers, visiblePens, activeDurationSeconds, now);
        setState({
          kind: 'ready',
          series: buildTrendLiveSeries(visiblePens, buffers),
          from: now - activeDurationSeconds * 1000,
          to: now
        });
      };

      const refreshSnapshots = async () => {
        try {
          const tags = await loadReadableRuntimeTags(controller.signal);
          if (disposed || controller.signal.aborted) return;
          const byId = new Map(tags.map(tag => [normalizeId(tag.id), tag]));
          const now = Date.now();
          buffers = pruneTrendLiveBuffers(buffers, visiblePens, activeDurationSeconds, now);
          for (const pen of visiblePens) {
            const tag = byId.get(normalizeId(pen.tagId));
            if (!tag) continue;
            const sample = trendSampleFromRuntimeSnapshot(pen, tag);
            if (sample) buffers = appendTrendLiveSample(buffers, pen, sample, activeDurationSeconds, now);
          }
          publish(now);
        } catch (reason) {
          if (disposed || controller.signal.aborted) return;
          setState(previous => ({
            kind: 'error',
            series: previous.series,
            message: reason instanceof Error ? reason.message : String(reason)
          }));
        }
      };

      setState(previous => ({ kind: 'loading', series: previous.series }));
      void refreshSnapshots();
      const interval = window.setInterval(() => { void refreshSnapshots(); }, Math.max(1, refreshSeconds) * 1000);

      try {
        socket = openRuntimeTagSocket();
        socket.addEventListener('message', event => {
          const message = parseRuntimeTagRealtimeMessage(String(event.data));
          if (!message) return;
          const pen = visiblePens.find(candidate => normalizeId(candidate.tagId) === normalizeId(message.tag.id));
          if (!pen) return;
          const sample = trendSampleFromRuntimeMessage(pen, message);
          if (!sample) return;
          const now = Date.now();
          buffers = appendTrendLiveSample(buffers, pen, sample, activeDurationSeconds, now);
          publish(now);
        });
      } catch {
        socket = null;
      }

      return () => {
        disposed = true;
        controller.abort();
        window.clearInterval(interval);
        socket?.close();
      };
    }

    let disposed = false;
    let active: AbortController | null = null;

    const loadHistory = async () => {
      active?.abort();
      const controller = new AbortController();
      active = controller;
      setState(previous => ({ kind: 'loading', series: previous.series }));
      try {
        const response = await executeTrendHistoricalQuery(
          buildTrendHistoricalQuery(visiblePens, activeTimeRange),
          controller.signal
        );
        if (disposed || controller.signal.aborted || active !== controller) return;
        const range = trendQueryRange(response);
        setState({
          kind: 'ready',
          series: buildTrendSeries(response, visiblePens),
          from: range.from,
          to: range.to
        });
      } catch (reason) {
        if (disposed || controller.signal.aborted || active !== controller) return;
        setState(previous => ({
          kind: 'error',
          series: previous.series,
          message: reason instanceof Error ? reason.message : String(reason)
        }));
      }
    };

    void loadHistory();
    if (activeTimeRange.mode === 'absolute') {
      return () => {
        disposed = true;
        active?.abort();
      };
    }
    const interval = window.setInterval(() => {
      if (!isEditingTimeRange.current) void loadHistory();
    }, Math.max(1, refreshSeconds) * 1000);
    return () => {
      disposed = true;
      active?.abort();
      window.clearInterval(interval);
    };
  }, [activeDurationSeconds, activeTimeRange, dataQueriesEnabled, refreshRevision, refreshSeconds, visiblePens]);

  const series = state.series;
  const hasSamples = series.some(item => item.samples.some(sample => isUsableTrendQuality(sample.quality) && sample.value !== null));
  const range = state.kind === 'ready'
    ? { from: state.from, to: state.to }
    : inferRange(series, activeDurationSeconds);
  const isLive = activeTimeRange.mode === 'live';
  const configuredLeftDomain = axisDomain('left', visiblePens, series);
  const configuredRightDomain = axisDomain('right', visiblePens, series);
  const leftDomain = operatorScale.left ?? configuredLeftDomain;
  const rightDomain = operatorScale.right ?? configuredRightDomain;
  const leftUnit = axisUnit('left', visiblePens);
  const rightUnit = axisUnit('right', visiblePens);
  const visibleRange = range ? zoomTrendRange(range, zoomFactor) : null;
  const chartPlot = plotForAxisFontSize(axisFontSize);
  const referenceSamples = React.useMemo(() => {
    const result = new Map<string, TrendSample>();
    if (referenceEpochMilliseconds === null) return result;
    for (const item of series) {
      const sample = nearestTrendSample(item.samples, referenceEpochMilliseconds);
      if (sample) result.set(item.pen.id, sample);
    }
    return result;
  }, [referenceEpochMilliseconds, series]);
  const referenceX = referenceLineEnabled && referenceEpochMilliseconds !== null && visibleRange
    ? chartPlot.left + ((referenceEpochMilliseconds - visibleRange.from) / Math.max(1, visibleRange.to - visibleRange.from)) * (chartPlot.right - chartPlot.left)
    : null;
  const canApplyScale = (['left', 'right'] as const).every(axis => !visiblePens.some(pen => pen.axis === axis) || isValidScaleDraft(scaleDraft[axis]));

  React.useEffect(() => {
    if (!referenceLineEnabled || !visibleRange) return;
    setReferenceEpochMilliseconds(current => current === null
      ? (visibleRange.from + visibleRange.to) / 2
      : clamp(current, visibleRange.from, visibleRange.to));
  }, [referenceLineEnabled, visibleRange?.from, visibleRange?.to]);

  const moveReferenceLine = (clientX: number) => {
    if (!referenceLineEnabled || !visibleRange) return;
    const chart = chartRef.current;
    if (!chart) return;
    const bounds = chart.getBoundingClientRect();
    if (bounds.width <= 0) return;
    const chartX = clamp((clientX - bounds.left) / bounds.width * CHART_WIDTH, chartPlot.left, chartPlot.right);
    const fraction = (chartX - chartPlot.left) / Math.max(1, chartPlot.right - chartPlot.left);
    setReferenceEpochMilliseconds(visibleRange.from + fraction * (visibleRange.to - visibleRange.from));
  };

  const toggleReferenceLine = () => {
    const nextEnabled = !referenceLineEnabled;
    setReferenceLineEnabled(nextEnabled);
    setReferenceEpochMilliseconds(nextEnabled && visibleRange
      ? (visibleRange.from + visibleRange.to) / 2
      : null);
    setChartMenu(null);
  };

  const openChartMenu = (event: React.MouseEvent<HTMLDivElement>) => {
    if (!operatorTimeRangeControls) return;
    event.preventDefault();
    event.stopPropagation();
    const menuWidth = 310;
    const menuHeight = 470;
    setScaleDraft({
      left: draftScale(operatorScale.left ?? configuredLeftDomain),
      right: draftScale(operatorScale.right ?? configuredRightDomain)
    });
    setChartMenu({
      left: clamp(event.clientX, 8, Math.max(8, window.innerWidth - menuWidth - 8)),
      top: clamp(event.clientY, 8, Math.max(8, window.innerHeight - menuHeight - 8))
    });
  };

  const applyOperatorScale = () => {
    if (!canApplyScale) return;
    const next: Partial<Record<TrendVisualPen['axis'], TrendDomain>> = {};
    for (const axis of ['left', 'right'] as const) {
      if (!visiblePens.some(pen => pen.axis === axis)) continue;
      const domain = parseScaleDraft(scaleDraft[axis]);
      if (domain) next[axis] = domain;
    }
    setOperatorScale(next);
    setChartMenu(null);
  };

  const openChartMenuFromKeyboard = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (referenceLineEnabled && visibleRange && (event.key === 'ArrowLeft' || event.key === 'ArrowRight')) {
      event.preventDefault();
      event.stopPropagation();
      const amount = Math.max(1, (visibleRange.to - visibleRange.from) / 100);
      setReferenceEpochMilliseconds(current => clamp(
        (current ?? (visibleRange.from + visibleRange.to) / 2) + (event.key === 'ArrowRight' ? amount : -amount),
        visibleRange.from,
        visibleRange.to
      ));
      return;
    }
    if (!operatorTimeRangeControls || !(event.shiftKey && event.key === 'F10')) return;
    event.preventDefault();
    const rect = event.currentTarget.getBoundingClientRect();
    setScaleDraft({
      left: draftScale(operatorScale.left ?? configuredLeftDomain),
      right: draftScale(operatorScale.right ?? configuredRightDomain)
    });
    setChartMenu({
      left: clamp(rect.left + rect.width - 310, 8, Math.max(8, window.innerWidth - 318)),
      top: clamp(rect.top + 8, 8, Math.max(8, window.innerHeight - 478))
    });
  };

  return <div
    className="visual-editor-object visual-editor-trend"
    style={{ ...style, display: style.display === 'none' ? 'none' : 'flex', flexDirection: 'column', alignItems: 'stretch', boxSizing: 'border-box', minWidth: 0, minHeight: 0, overflow: 'hidden', gap: 4, background: colorValue(values[VISUAL_PROPERTY_KEYS.backgroundColor], '#0B1220F2') }}
    data-testid="visual-trend"
    data-object-id={element.id ?? undefined}
    data-runtime-object-id={runtimeObjectId}
    data-enabled={enabled}
    data-trend-mode={activeTimeRange.mode}
    data-trend-from={state.kind === 'ready' ? new Date(state.from).toISOString() : undefined}
    data-trend-to={state.kind === 'ready' ? new Date(state.to).toISOString() : undefined}
    data-trend-source={isLive ? 'runtime-tags' : 'historian'}
    data-trend-quality-policy="good-only"
    data-trend-pen-count={visiblePens.length}
    data-trend-configured-pen-count={pens.length}
    data-trend-state={state.kind}
    data-trend-axis-font-size={axisFontSize}
    data-trend-legend-font-size={legendFontSize}
    data-trend-zoom-factor={zoomFactor}
    data-trend-time-axis-visible={showTimeAxis}
    data-trend-reference-enabled={referenceLineEnabled}
    data-trend-left-domain={leftDomain ? `${leftDomain.minimum},${leftDomain.maximum}` : undefined}
    data-trend-right-domain={rightDomain ? `${rightDomain.minimum},${rightDomain.maximum}` : undefined}
    title={combineTitle(title, state.kind === 'error' ? state.message : undefined)}
    onClick={onClick}
  >
    {operatorTimeRangeControls ? <HistoricalTimeRangeControls
      locale={locale}
      value={operatorTimeRange}
      onChange={setOperatorTimeRange}
      onEditingChange={editing => { isEditingTimeRange.current = editing; }}
      applyOnRefresh
      onRefresh={() => setRefreshRevision(current => current + 1)}
      disabled={!enabled}
      compact
      placement="toolbar"
    /> : null}
    <div
      ref={chartRef}
      style={chartStyle}
      data-testid="visual-trend-chart"
      data-reference-line-enabled={referenceLineEnabled}
      tabIndex={operatorTimeRangeControls ? 0 : undefined}
      aria-label={operatorTimeRangeControls ? text.chartMenu : undefined}
      aria-haspopup={operatorTimeRangeControls ? 'dialog' : undefined}
      onPointerMove={event => moveReferenceLine(event.clientX)}
      onPointerDown={event => {
        if (!referenceLineEnabled) return;
        event.stopPropagation();
        moveReferenceLine(event.clientX);
      }}
      onClick={event => {
        if (!referenceLineEnabled) return;
        event.stopPropagation();
        moveReferenceLine(event.clientX);
      }}
      onKeyDown={openChartMenuFromKeyboard}
      onContextMenu={openChartMenu}
    >
      <svg width="100%" height="100%" viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`} preserveAspectRatio="none" role="img" aria-label={element.key} style={{ cursor: referenceLineEnabled ? 'crosshair' : undefined }}>
        <rect x="0" y="0" width={CHART_WIDTH} height={CHART_HEIGHT} fill="transparent" />
        {showGrid ? <TrendGrid plot={chartPlot} /> : null}
        {showAxes ? <TrendAxes
          leftVisible={showLeftAxis}
          rightVisible={showRightAxis}
          labelsVisible={showAxisLabels}
          leftDomain={leftDomain}
          rightDomain={rightDomain}
          leftUnit={leftUnit}
          rightUnit={rightUnit}
          axisFontSize={axisFontSize}
          locale={locale}
          plot={chartPlot}
          timeAxisVisible={showTimeAxis}
          range={visibleRange}
        /> : null}
        {visibleRange ? series.map(item => <TrendSeriesPath
          key={item.pen.id}
          series={item}
          range={visibleRange}
          domain={item.pen.axis === 'left' ? leftDomain : rightDomain}
          plot={chartPlot}
        />) : null}
        {referenceX !== null && referenceEpochMilliseconds !== null && visibleRange ? <g
          data-testid="visual-trend-reference-line"
          data-reference-time={new Date(referenceEpochMilliseconds).toISOString()}
          pointerEvents="none"
        >
          <title>{new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(referenceEpochMilliseconds))}</title>
          <line x1={referenceX} x2={referenceX} y1={chartPlot.top} y2={chartPlot.bottom} stroke="#FDE047" strokeWidth="2" strokeDasharray="6 4" vectorEffect="non-scaling-stroke" />
          {series.map(item => {
            const sample = referenceSamples.get(item.pen.id);
            const domain = item.pen.axis === 'left' ? leftDomain : rightDomain;
            if (!sample) return null;
            const sampleValue = sample.value;
            if (sampleValue === null) return null;
            if (!isUsableTrendQuality(sample.quality) || !domain) return null;
            return <circle
              key={item.pen.id}
              cx={referenceX}
              cy={trendSampleY({ ...sample, value: sampleValue }, domain, chartPlot)}
              r="5"
              fill={item.pen.color}
              stroke="#F8FAFC"
              strokeWidth="1.5"
              data-testid="visual-trend-reference-point"
              data-pen-id={item.pen.id}
            />;
          })}
        </g> : null}
      </svg>
      {chartMenu ? createPortal(<div
        role="dialog"
        aria-label={text.chartMenu}
        data-testid="visual-trend-context-menu"
        onClick={event => event.stopPropagation()}
        onContextMenu={event => event.preventDefault()}
        style={{ ...operatorMenuStyle, left: chartMenu.left, top: chartMenu.top }}
      >
        <div style={operatorMenuHeaderStyle}>
          <strong>{text.chartMenu}</strong>
          <button type="button" aria-label={text.close} onClick={() => setChartMenu(null)} style={operatorMenuCloseStyle}>×</button>
        </div>
        <div style={operatorMenuActionsStyle}>
          <button
            type="button"
            data-testid="trend-reference-line-toggle"
            aria-pressed={referenceLineEnabled}
            onClick={toggleReferenceLine}
          >{referenceLineEnabled ? text.disableReferenceLine : text.enableReferenceLine}</button>
        </div>
        <div style={operatorMenuActionsStyle}>
          <button type="button" onClick={() => setZoomFactor(current => Math.min(MAX_ZOOM_FACTOR, current * 1.5))} disabled={zoomFactor >= MAX_ZOOM_FACTOR}>{text.zoomIn}</button>
          <button type="button" onClick={() => setZoomFactor(current => Math.max(1, current / 1.5))} disabled={zoomFactor <= 1}>{text.zoomOut}</button>
          <button type="button" onClick={() => setZoomFactor(1)} disabled={zoomFactor === 1}>{text.resetZoom}</button>
        </div>
        <strong>{text.scale}</strong>
        {(['left', 'right'] as const).filter(axis => visiblePens.some(pen => pen.axis === axis)).map(axis => <fieldset key={axis} data-testid={`trend-axis-scale-${axis}`} style={operatorFieldsetStyle}>
          <legend>{axis === 'left' ? text.axisLeft : text.axisRight}</legend>
          <label style={operatorInputLabelStyle}>{text.minimum}<input
            type="number" step="any" value={scaleDraft[axis].minimum} data-testid={`trend-axis-${axis}-minimum`}
            onChange={event => {
              const minimum = event.currentTarget.value;
              setScaleDraft(current => ({ ...current, [axis]: { ...current[axis], minimum } }));
            }}
            style={operatorInputStyle}
          /></label>
          <label style={operatorInputLabelStyle}>{text.maximum}<input
            type="number" step="any" value={scaleDraft[axis].maximum} data-testid={`trend-axis-${axis}-maximum`}
            onChange={event => {
              const maximum = event.currentTarget.value;
              setScaleDraft(current => ({ ...current, [axis]: { ...current[axis], maximum } }));
            }}
            style={operatorInputStyle}
          /></label>
        </fieldset>)}
        {!canApplyScale ? <small role="alert" style={{ color: '#fca5a5' }}>{text.invalidScale}</small> : null}
        <div style={operatorMenuActionsStyle}>
          <button type="button" onClick={applyOperatorScale} disabled={!canApplyScale}>{text.applyScale}</button>
          <button type="button" onClick={() => { setOperatorScale({}); setChartMenu(null); }}>{text.restoreScale}</button>
        </div>
      </div>, document.body) : null}
      {!hasSamples ? <div style={overlayStyle} data-testid="visual-trend-empty">
      {state.kind === 'loading'
        ? (isLive ? text.loadingLive : text.loadingHistory)
        : state.kind === 'error'
          ? (isLive ? text.liveError : text.historyError)
          : text.noData}
      </div> : null}
    </div>

    {showLegend && pens.length > 0 ? <div style={{ ...legendStyle, fontSize: legendFontSize }} data-testid="visual-trend-legend">
      {seriesWithEmpty(pens, series).map(item => {
        const latest = item.samples[item.samples.length - 1];
        const displaySample = referenceLineEnabled ? referenceSamples.get(item.pen.id) : latest;
        const displayUsable = displaySample
          ? isUsableTrendQuality(displaySample.quality) && displaySample.value !== null
          : false;
        const penVisible = operatorPenVisibility[item.pen.id] ?? item.pen.visible;
        const penLabel = <>
          <i aria-hidden="true" style={{ width: 10, height: 10, borderRadius: 2, background: item.pen.color, display: 'inline-block' }} />
          <strong>{item.pen.label}</strong>
          <span
            data-testid={referenceLineEnabled ? 'visual-trend-reference-value' : undefined}
            data-reference-value={referenceLineEnabled && displaySample?.value !== null ? displaySample?.value : undefined}
            title={referenceLineEnabled && displaySample
              ? new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(displaySample.epochMilliseconds))
              : undefined}
          >{penVisible && displaySample && displayUsable && displaySample.value !== null ? formatNumber(displaySample.value, locale) : '—'}{item.pen.unit ? ` ${item.pen.unit}` : ''}</span>
          {showQuality && displaySample ? <small>{displaySample.quality}</small> : null}
          <small>{item.pen.axis === 'left' ? text.left : text.right}</small>
        </>;
        const style = { ...legendItemStyle, opacity: penVisible ? 1 : 0.55 };
        return operatorTimeRangeControls
          ? <button
              key={item.pen.id}
              type="button"
              style={style}
              data-pen-id={item.pen.id}
              data-testid="visual-trend-pen-toggle"
              aria-pressed={penVisible}
              aria-label={`${penVisible ? text.hidePen : text.showPen}: ${item.pen.label}`}
              title={`${penVisible ? text.hidePen : text.showPen}: ${item.pen.label}`}
              onClick={event => {
                event.stopPropagation();
                setOperatorPenVisibility(current => Object.freeze({ ...current, [item.pen.id]: !penVisible }));
              }}
            >{penLabel}</button>
          : <span key={item.pen.id} style={style} data-pen-id={item.pen.id}>{penLabel}</span>;
      })}
    </div> : null}
  </div>;
}

const CHART_WIDTH = 1000;
const CHART_HEIGHT = 400;
type TrendDomain = Readonly<{ minimum: number; maximum: number }>;
type TrendPlot = Readonly<{ left: number; right: number; top: number; bottom: number }>;
const MAX_ZOOM_FACTOR = 16;

function plotForAxisFontSize(fontSize: number): TrendPlot {
  // Reserve a line above the plot for the unit so it never collides with the
  // maximum tick label, including when the operator enlarges axis typography.
  const margin = Math.max(80, fontSize * 5.5 + 16);
  return Object.freeze({ left: margin, right: CHART_WIDTH - margin, top: Math.max(44, fontSize * 1.8), bottom: CHART_HEIGHT - Math.max(32, fontSize * 1.2) });
}

function zoomTrendRange(range: Readonly<{ from: number; to: number }>, factor: number) {
  const span = Math.max(1, range.to - range.from);
  const visibleSpan = span / Math.max(1, factor);
  const center = range.from + span / 2;
  return Object.freeze({ from: center - visibleSpan / 2, to: center + visibleSpan / 2 });
}

function draftScale(domain: TrendDomain | null): Readonly<{ minimum: string; maximum: string }> {
  return Object.freeze({
    minimum: String(domain?.minimum ?? 0),
    maximum: String(domain?.maximum ?? 100)
  });
}

function parseScaleDraft(draft: Readonly<{ minimum: string; maximum: string }>): TrendDomain | null {
  if (!draft.minimum.trim() || !draft.maximum.trim()) return null;
  const minimum = Number(draft.minimum);
  const maximum = Number(draft.maximum);
  if (!Number.isFinite(minimum) || !Number.isFinite(maximum) || maximum <= minimum) return null;
  return Object.freeze({ minimum, maximum });
}

function isValidScaleDraft(draft: Readonly<{ minimum: string; maximum: string }>): boolean {
  return parseScaleDraft(draft) !== null;
}

function TrendGrid({ plot }: Readonly<{ plot: TrendPlot }>) {
  const lines = [0, 1, 2, 3, 4, 5];
  return <g stroke="#64748B55" strokeWidth="1" vectorEffect="non-scaling-stroke">
    {lines.map(index => {
      const y = plot.top + index * (plot.bottom - plot.top) / (lines.length - 1);
      return <line key={`h-${index}`} x1={plot.left} x2={plot.right} y1={y} y2={y} />;
    })}
    {lines.map(index => {
      const x = plot.left + index * (plot.right - plot.left) / (lines.length - 1);
      return <line key={`v-${index}`} y1={plot.top} y2={plot.bottom} x1={x} x2={x} />;
    })}
  </g>;
}

function TrendAxes({
  leftVisible,
  rightVisible,
  labelsVisible,
  leftDomain,
  rightDomain,
  leftUnit,
  rightUnit,
  axisFontSize,
  locale,
  plot,
  timeAxisVisible,
  range
}: Readonly<{
  leftVisible: boolean;
  rightVisible: boolean;
  labelsVisible: boolean;
  leftDomain: Readonly<{ minimum: number; maximum: number }> | null;
  rightDomain: Readonly<{ minimum: number; maximum: number }> | null;
  leftUnit: string | null;
  rightUnit: string | null;
  axisFontSize: number;
  locale: EngineeringLocale;
  plot: TrendPlot;
  timeAxisVisible: boolean;
  range: Readonly<{ from: number; to: number }> | null;
}>) {
  return <g vectorEffect="non-scaling-stroke">
    <g stroke="#94A3B8" strokeWidth="1.5">
      {leftVisible ? <line x1={plot.left} x2={plot.left} y1={plot.top} y2={plot.bottom} /> : null}
      {rightVisible ? <line x1={plot.right} x2={plot.right} y1={plot.top} y2={plot.bottom} /> : null}
      {timeAxisVisible ? <line x1={plot.left} x2={plot.right} y1={plot.bottom} y2={plot.bottom} /> : null}
    </g>
    {labelsVisible && leftVisible && leftDomain ? <AxisScale domain={leftDomain} side="left" unit={leftUnit} fontSize={axisFontSize} locale={locale} plot={plot} /> : null}
    {labelsVisible && rightVisible && rightDomain ? <AxisScale domain={rightDomain} side="right" unit={rightUnit} fontSize={axisFontSize} locale={locale} plot={plot} /> : null}
    {timeAxisVisible && range ? <TimeScale range={range} plot={plot} locale={locale} fontSize={axisFontSize} /> : null}
  </g>;
}

function TimeScale({ range, plot, locale, fontSize }: Readonly<{
  range: Readonly<{ from: number; to: number }>;
  plot: TrendPlot;
  locale: EngineeringLocale;
  fontSize: number;
}>) {
  const span = Math.max(1, range.to - range.from);
  const dateOptions: Intl.DateTimeFormatOptions = span > 180 * 24 * 60 * 60 * 1000
    ? { year: '2-digit', month: '2-digit', day: '2-digit' }
    : span > 2 * 24 * 60 * 60 * 1000
      ? { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }
      : { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' };
  const formatter = new Intl.DateTimeFormat(locale, dateOptions);
  const labelFontSize = Math.min(fontSize, 18);
  return <g fill="#CBD5E1" fontSize={labelFontSize} fontFamily="sans-serif" data-testid="visual-trend-time-axis">
    {Array.from({ length: 6 }, (_, index) => {
      const fraction = index / 5;
      const x = plot.left + fraction * (plot.right - plot.left);
      const anchor = index === 0 ? 'start' : index === 5 ? 'end' : 'middle';
      const epoch = range.from + fraction * span;
      return <g key={index}>
        <line x1={x} x2={x} y1={plot.bottom} y2={plot.bottom + 4} stroke="#94A3B8" strokeWidth="1" />
        <text x={x} y={CHART_HEIGHT - 8} textAnchor={anchor} fontSize={labelFontSize} data-testid="visual-trend-time-label">
          {formatter.format(new Date(epoch))}
        </text>
      </g>;
    })}
  </g>;
}

function AxisScale({ domain, side, unit, fontSize, locale, plot }: Readonly<{
  domain: Readonly<{ minimum: number; maximum: number }>;
  side: 'left' | 'right';
  unit: string | null;
  fontSize: number;
  locale: EngineeringLocale;
  plot: TrendPlot;
}>) {
  const anchor = side === 'left' ? 'end' : 'start';
  const x = side === 'left' ? plot.left - 8 : plot.right + 8;
  return <g fill="#CBD5E1" fontSize={fontSize} fontFamily="sans-serif" textAnchor={anchor}>
    {Array.from({ length: 6 }, (_, index) => {
      const fraction = index / 5;
      const y = plot.top + fraction * (plot.bottom - plot.top);
      const value = domain.maximum - fraction * (domain.maximum - domain.minimum);
      return <text key={index} x={x} y={y + fontSize * 0.35} fontSize={fontSize} data-axis-side={side} data-testid="visual-trend-axis-label">{formatTrendAxisLabel(value, locale)}</text>;
    })}
    {unit ? <text x={x} y={fontSize} fontSize={Math.max(8, fontSize - 2)} opacity="0.8" data-testid="visual-trend-axis-unit">{unit}</text> : null}
  </g>;
}

function TrendSeriesPath({
  series,
  range,
  domain,
  plot
}: Readonly<{ series: TrendSeries; range: Readonly<{ from: number; to: number }>; domain: Readonly<{ minimum: number; maximum: number }> | null; plot: TrendPlot }>) {
  if (!domain) return null;
  let drawing = false;
  const commands: string[] = [];
  for (const sample of series.samples) {
    if (!isUsableTrendQuality(sample.quality) || sample.value === null ||
        sample.epochMilliseconds < range.from || sample.epochMilliseconds > range.to) {
      drawing = false;
      continue;
    }
    const fractionX = (sample.epochMilliseconds - range.from) / Math.max(1, range.to - range.from);
    const fractionY = (sample.value - domain.minimum) / Math.max(Number.EPSILON, domain.maximum - domain.minimum);
    const x = plot.left + fractionX * (plot.right - plot.left);
    const y = plot.bottom - fractionY * (plot.bottom - plot.top);
    commands.push(`${drawing ? 'L' : 'M'}${clamp(x, plot.left, plot.right).toFixed(2)} ${clamp(y, plot.top, plot.bottom).toFixed(2)}`);
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

function nearestTrendSample(samples: readonly TrendSample[], epochMilliseconds: number): TrendSample | null {
  let nearest: TrendSample | null = null;
  let distance = Number.POSITIVE_INFINITY;
  for (const sample of samples) {
    const nextDistance = Math.abs(sample.epochMilliseconds - epochMilliseconds);
    if (nextDistance < distance) {
      nearest = sample;
      distance = nextDistance;
    }
  }
  return nearest;
}

function trendSampleY(
  sample: TrendSample & { value: number },
  domain: TrendDomain,
  plot: TrendPlot
): number {
  const fraction = (sample.value - domain.minimum) / Math.max(Number.EPSILON, domain.maximum - domain.minimum);
  return clamp(plot.bottom - fraction * (plot.bottom - plot.top), plot.top, plot.bottom);
}

function axisDomain(
  axis: TrendVisualPen['axis'],
  pens: readonly TrendVisualPen[],
  series: readonly TrendSeries[]
): Readonly<{ minimum: number; maximum: number }> | null {
  const axisPens = pens.filter(pen => pen.axis === axis);
  if (axisPens.length === 0) return null;
  const fixedPens = axisPens.filter((pen): pen is TrendVisualPen & { scale: { mode: 'fixed'; minimum: number; maximum: number } } => pen.scale.mode === 'fixed');
  if (fixedPens.length > 0) {
    return Object.freeze({
      minimum: Math.min(...fixedPens.map(pen => pen.scale.minimum)),
      maximum: Math.max(...fixedPens.map(pen => pen.scale.maximum))
    });
  }
  const penIds = new Set(axisPens.map(pen => pen.id));
  const values = series
    .filter(item => penIds.has(item.pen.id))
    .flatMap(item => item.samples
      .filter((sample): sample is typeof sample & { value: number } =>
        isUsableTrendQuality(sample.quality) && sample.value !== null)
      .map(sample => sample.value));
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

function axisUnit(axis: TrendVisualPen['axis'], pens: readonly TrendVisualPen[]): string | null {
  const units = [...new Set(pens.filter(pen => pen.axis === axis).map(pen => pen.unit.trim()).filter(Boolean))];
  return units.length === 1 ? units[0] : null;
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

function formatNumber(value: number, locale: EngineeringLocale): string {
  return new Intl.NumberFormat(locale, { maximumFractionDigits: 3 }).format(value);
}

function normalizeId(value: string): string {
  return value.trim().toLocaleLowerCase('en-US');
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
function numberValue(value: VisualPropertyValue | undefined, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? clamp(value, 8, 32) : fallback;
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
const chartStyle: CSSProperties = {
  position: 'relative', flex: '1 1 0%', alignSelf: 'stretch', width: '100%', minWidth: 0, minHeight: 0, overflow: 'hidden'
};
const legendStyle: CSSProperties = {
  flex: '0 0 auto', display: 'flex', flexWrap: 'wrap', gap: 6, maxHeight: '32%', overflow: 'auto',
  padding: 4, color: '#E2E8F0', fontSize: 11
};
const legendItemStyle: CSSProperties = {
  display: 'inline-flex', alignItems: 'center', gap: 4, background: '#020617B8', color: '#E2E8F0',
  border: '1px solid #475569', padding: '3px 6px', borderRadius: 3, font: 'inherit', cursor: 'pointer'
};
const operatorMenuStyle: CSSProperties = {
  position: 'fixed', zIndex: 10000, width: 300, maxHeight: '85vh', overflow: 'auto',
  display: 'grid', gap: 8, padding: 12, border: '1px solid #64748B', borderRadius: 8,
  background: '#0F172A', color: '#E2E8F0', boxShadow: '0 10px 30px #0009', font: '13px sans-serif'
};
const operatorMenuHeaderStyle: CSSProperties = { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8 };
const operatorMenuCloseStyle: CSSProperties = { border: '1px solid #475569', borderRadius: 4, background: 'transparent', color: 'inherit', cursor: 'pointer' };
const operatorMenuActionsStyle: CSSProperties = { display: 'flex', flexWrap: 'wrap', gap: 6 };
const operatorFieldsetStyle: CSSProperties = { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, margin: 0, minWidth: 0, border: '1px solid #475569', borderRadius: 6 };
const operatorInputLabelStyle: CSSProperties = { display: 'grid', gap: 3, minWidth: 0, fontSize: 11 };
const operatorInputStyle: CSSProperties = { width: '100%', minWidth: 0, boxSizing: 'border-box' };
