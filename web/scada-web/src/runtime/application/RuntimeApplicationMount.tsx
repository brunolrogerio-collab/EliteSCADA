import React, { useEffect, useMemo, useRef, useState } from 'react';
import { appShellText, useAppShellLocale } from '../../appShellI18n';
import { UserSessionMenu } from '../../auth/UserSessionMenu';
import type { ScriptEngineeringContext } from '../../engineering/scripts/scriptEngineeringTypes';
import { RuntimeAlarmCenter } from '../RuntimeAlarmCenter';
import { HistoricalTimeRangeControls } from '../HistoricalTimeRangeControls';
import {
  HistoricalPlaybackProvider,
  useHistoricalPlayback
} from '../historical-playback/HistoricalPlaybackContext';
import { RuntimeVisualNavigator } from '../visual-navigation/RuntimeVisualNavigator';
import {
  loadRuntimeApplicationProjection,
  RuntimeApplicationProjectionError,
  RuntimeApplicationTransportError,
  runtimeVisualAssetContentUrl,
  type RuntimeApplicationProjection
} from './runtimeApplicationApi';
import { resolveRuntimeStartupScreen } from './runtimeStartupScreen';

const REFRESH_INTERVAL_MS = 1500;
const RETRYABLE_RUNTIME_PROJECTION_STATUSES = new Set([502, 503, 504]);
const TRUNCATED_RESPONSE_SIGNATURE = 'content-length header of network response exceeds response body';

export function RuntimeApplicationMount({ showHistoryNavigation = false }: { showHistoryNavigation?: boolean } = {}) {
  const locale = useAppShellLocale();
  const text = appShellText(locale);
  const [projection, setProjection] = useState<RuntimeApplicationProjection | null>(null);
  const lastSuccessfulProjection = useRef<RuntimeApplicationProjection | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    let disposed = false;
    let inFlight = false;
    let activeController: AbortController | null = null;

    const refresh = async () => {
      if (disposed || inFlight) return;
      inFlight = true;
      const controller = new AbortController();
      activeController = controller;
      try {
        const next = await loadRuntimeApplicationProjection(controller.signal);
        if (disposed) return;
        lastSuccessfulProjection.current = next;
        setProjection(current => sameRuntimeProjection(current, next) ? current : next);
        setError(null);
      } catch (reason) {
        if (disposed || controller.signal.aborted) return;
        const failure = reason instanceof Error ? reason : new Error(String(reason));
        if (lastSuccessfulProjection.current && isRetryableRuntimeProjectionFailure(failure)) {
          setError(null);
          return;
        }
        lastSuccessfulProjection.current = null;
        setProjection(null);
        setError(failure);
      } finally {
        if (activeController === controller) activeController = null;
        inFlight = false;
      }
    };

    void refresh();
    const timer = window.setInterval(() => void refresh(), REFRESH_INTERVAL_MS);
    return () => {
      disposed = true;
      window.clearInterval(timer);
      activeController?.abort();
    };
  }, []);

  if (error) {
    const status = error instanceof RuntimeApplicationProjectionError ? error.status : 500;
    return <main className="shell" data-testid="runtime-application-error">
      <section className="runtime-visual-diagnostic" role="alert" data-diagnostic-code="HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE">
        <strong>HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE</strong>
        <span>{text.runtimeUnavailable} ({status}) {error.message}</span>
      </section>
    </main>;
  }

  if (!projection) {
    return <main className="shell" data-testid="runtime-application-loading">
      <section className="runtime-visual-diagnostic" role="status">
        <strong>Runtime</strong><span>…</span>
      </section>
    </main>;
  }

  if (projection.mode !== 'engineering') {
    return <main className="shell" data-testid="runtime-neutral">
      <section className="runtime-visual-diagnostic" role="status" data-diagnostic-code="RUNTIME_ACTIVE_REVISION_NOT_SELECTED">
        <strong>{text.runtimeNotActive}</strong>
        <span>{text.runtimeNotActiveDescription}</span>
      </section>
    </main>;
  }
  return <EngineeringRuntimeApplication projection={projection} locale={locale} showHistoryNavigation={showHistoryNavigation} />;
}

function EngineeringRuntimeApplication({
  projection,
  locale,
  showHistoryNavigation
}: {
  projection: RuntimeApplicationProjection;
  locale: ReturnType<typeof useAppShellLocale>;
  showHistoryNavigation: boolean;
}) {
  return <HistoricalPlaybackProvider engineeringPackage={projection.package!}>
    <EngineeringRuntimeApplicationContent
      projection={projection}
      locale={locale}
      showHistoryNavigation={showHistoryNavigation}
    />
  </HistoricalPlaybackProvider>;
}

function EngineeringRuntimeApplicationContent({
  projection,
  locale,
  showHistoryNavigation
}: {
  projection: RuntimeApplicationProjection;
  locale: ReturnType<typeof useAppShellLocale>;
  showHistoryNavigation: boolean;
}) {
  const text = appShellText(locale);
  const playback = useHistoricalPlayback();
  const playbackText = historicalPlaybackCopy(locale);
  const fullscreenRoot = useRef<HTMLElement>(null);
  const [isFullscreen, setIsFullscreen] = useState(Boolean(document.fullscreenElement));
  const [alarmsOpen, setAlarmsOpen] = useState(false);
  const engineeringPackage = projection.package!;
  const startup = useMemo(
    () => resolveRuntimeStartupScreen(engineeringPackage),
    [engineeringPackage]
  );

  useEffect(() => {
    const changed = () => setIsFullscreen(document.fullscreenElement === fullscreenRoot.current);
    document.addEventListener('fullscreenchange', changed);
    return () => document.removeEventListener('fullscreenchange', changed);
  }, []);

  useEffect(() => {
    if (playback.mode === 'historicalPlayback') setAlarmsOpen(false);
  }, [playback.mode]);

  const scriptContext = useMemo<ScriptEngineeringContext>(() => ({
    workspace: {
      projectKey: projection.projectKey ?? null,
      projectName: projection.projectName ?? null,
      baseRevision: projection.revision ?? null,
      isDirty: false,
      changeVersion: 0
    },
    scripts: engineeringPackage.scripts ?? [],
    visualEventReferences: engineeringPackage.scriptVisualEventReferences ?? []
  }), [engineeringPackage, projection.projectKey, projection.projectName, projection.revision]);

  const toggleFullscreen = async () => {
    if (document.fullscreenElement) await document.exitFullscreen();
    else await fullscreenRoot.current?.requestFullscreen();
  };

  if (!startup.screenKey) {
    return <main className="shell" data-testid="runtime-engineering-application">
      <section className="runtime-visual-diagnostic" role="alert" data-diagnostic-code={startup.diagnosticCode ?? undefined}>
        <strong>{startup.diagnosticCode}</strong>
        <span>{text.runtimeUnavailable} {startup.detail}</span>
      </section>
    </main>;
  }

  return <main
    ref={fullscreenRoot}
    className="runtime-operator-application"
    data-testid="runtime-engineering-application"
    data-runtime-project-key={projection.projectKey ?? undefined}
    data-runtime-revision={projection.revision ?? undefined}
    data-runtime-fullscreen={isFullscreen || undefined}
    data-runtime-temporal-mode={playback.mode === 'historicalPlayback' ? 'historical-playback' : 'live'}
    data-runtime-historical-at={playback.atUtc ?? undefined}
  >
    <header className="runtime-operator-bar">
      <div className="runtime-operator-context">
        <strong>{projection.projectName || projection.projectKey}</strong>
        <span>rev {projection.revision}</span>
      </div>
      {showHistoryNavigation ? <nav className="runtime-view-navigation runtime-view-navigation--inline" aria-label="Runtime views">
        <a href="/" className="active" aria-current="page">{text.runtimeOverview}</a>
        <a href="/runtime/history">{text.runtimeHistory}</a>
      </nav> : null}
      <div className="runtime-operator-actions">
        {playback.mode === 'live' ? (
          <button
            type="button"
            className="runtime-operator-button"
            data-testid="runtime-enter-historical-playback"
            onClick={playback.enterPlayback}
          >
            {playbackText.enter}
          </button>
        ) : (
          <button
            type="button"
            className="runtime-operator-button runtime-playback-exit"
            data-testid="runtime-exit-historical-playback"
            onClick={playback.exitPlayback}
          >
            {playbackText.exit}
          </button>
        )}
        <button
          type="button"
          className="runtime-operator-button"
          aria-expanded={alarmsOpen}
          disabled={playback.mode === 'historicalPlayback'}
          title={playback.mode === 'historicalPlayback' ? playbackText.readOnly : undefined}
          onClick={() => setAlarmsOpen(value => !value)}
        >
          {text.alarms}
        </button>
        <button type="button" className="runtime-operator-button" onClick={() => void toggleFullscreen()}>
          {isFullscreen ? text.exitFullscreen : text.fullscreen}
        </button>
        {isFullscreen ? <UserSessionMenu locale={locale} includeRuntimeSessionControls /> : null}
      </div>
    </header>

    {playback.mode === 'historicalPlayback' ? <section
      className="runtime-playback-panel"
      data-testid="runtime-historical-playback-panel"
      data-runtime-session-control
      aria-label={playbackText.title}
    >
      <div className="runtime-playback-panel__header">
        <strong>{playbackText.title}</strong>
        <span>{playbackText.readOnly}</span>
      </div>
      <HistoricalTimeRangeControls
        locale={locale}
        value={playback.timeRange}
        onChange={playback.setTimeRange}
        onRefresh={playback.refresh}
      />
      <label className="runtime-playback-position">
        <span>{playbackText.instant}</span>
        <input
          type="range"
          min={0}
          max={1000}
          step={1}
          value={Math.round(playback.position * 1000)}
          aria-label={playbackText.instant}
          onChange={event => playback.setPosition(Number(event.target.value) / 1000)}
        />
        <output>{playback.atUtc ?? '—'}</output>
      </label>
      <div
        className="runtime-playback-status"
        role={playback.loadState === 'error' ? 'alert' : 'status'}
        data-playback-load-state={playback.loadState}
        data-playback-gap-count={playback.gapCount}
      >
        {playback.loadState === 'loading' ? playbackText.loading : null}
        {playback.loadState === 'ready'
          ? `${playbackText.ready}: ${playback.resolvedTagCount} · ${playbackText.gaps}: ${playback.gapCount}`
          : null}
        {playback.loadState === 'error' ? `${playbackText.error}: ${playback.error ?? ''}` : null}
      </div>
    </section> : null}

    <section className="runtime-engineering-canvas" data-testid="runtime-engineering-canvas">
      <RuntimeVisualNavigator
        engineeringPackage={engineeringPackage}
        initialScreenKey={startup.screenKey}
        locale={locale}
        scriptContext={scriptContext}
        emptyLabel={text.emptyVisual}
        visualAssetUrl={runtimeVisualAssetContentUrl}
      />
    </section>

    {alarmsOpen ? <aside className="runtime-operator-overlay" aria-label={text.alarms}>
      <div className="runtime-operator-overlay-header">
        <strong>{text.alarms}</strong>
        <button type="button" className="runtime-operator-button" onClick={() => setAlarmsOpen(false)}>{text.closeAlarms}</button>
      </div>
      <div className="runtime-operator-overlay-content">
        <RuntimeAlarmCenter locale={locale} />
      </div>
    </aside> : null}
  </main>;
}

type HistoricalPlaybackCopy = Readonly<{
  title: string;
  enter: string;
  exit: string;
  instant: string;
  readOnly: string;
  loading: string;
  ready: string;
  gaps: string;
  error: string;
}>;

function historicalPlaybackCopy(locale: ReturnType<typeof useAppShellLocale>): HistoricalPlaybackCopy {
  if (locale === 'en') return Object.freeze({
    title: 'Historical Playback',
    enter: 'Historical Playback',
    exit: 'Return to Live',
    instant: 'Historical instant',
    readOnly: 'Read-only · process actions blocked',
    loading: 'Loading historical state…',
    ready: 'Historical TAGs',
    gaps: 'gaps',
    error: 'Historical Playback error'
  });
  if (locale === 'es') return Object.freeze({
    title: 'Reproducción histórica',
    enter: 'Reproducción histórica',
    exit: 'Volver a vivo',
    instant: 'Instante histórico',
    readOnly: 'Solo lectura · acciones de proceso bloqueadas',
    loading: 'Cargando estado histórico…',
    ready: 'TAGs históricos',
    gaps: 'vacíos',
    error: 'Error de reproducción histórica'
  });
  return Object.freeze({
    title: 'Playback histórico',
    enter: 'Playback histórico',
    exit: 'Voltar ao Live',
    instant: 'Instante histórico',
    readOnly: 'Somente leitura · ações de processo bloqueadas',
    loading: 'Carregando estado histórico…',
    ready: 'TAGs históricos',
    gaps: 'gaps',
    error: 'Erro no Playback histórico'
  });
}

function isRetryableRuntimeProjectionFailure(failure: Error): boolean {
  if (failure instanceof RuntimeApplicationTransportError) return true;
  if (!(failure instanceof RuntimeApplicationProjectionError)) return false;
  if (RETRYABLE_RUNTIME_PROJECTION_STATUSES.has(failure.status)) return true;
  return failure.status === 500 &&
    failure.message.toLowerCase().includes(TRUNCATED_RESPONSE_SIGNATURE);
}

function sameRuntimeProjection(
  current: RuntimeApplicationProjection | null,
  next: RuntimeApplicationProjection
): boolean {
  if (!current || current.mode !== next.mode) return false;
  if (next.mode !== 'engineering') return true;
  return current.projectKey === next.projectKey &&
    current.revision === next.revision &&
    current.activatedAtUtc === next.activatedAtUtc;
}
