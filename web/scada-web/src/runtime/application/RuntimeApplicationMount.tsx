import React, { useEffect, useMemo, useRef, useState } from 'react';
import { appShellText, useAppShellLocale } from '../../appShellI18n';
import { UserSessionMenu } from '../../auth/UserSessionMenu';
import type { ScriptEngineeringContext } from '../../engineering/scripts/scriptEngineeringTypes';
import { RuntimeAlarmCenter } from '../RuntimeAlarmCenter';
import { HistoricalDataBrowserRuntime } from '../historical-browser/HistoricalDataBrowserRuntime';
import { historicalBrowserCopy } from '../historical-browser/historicalBrowserI18n';
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
  const text = appShellText(locale);
  const historyText = historicalBrowserCopy(locale);
  const fullscreenRoot = useRef<HTMLElement>(null);
  const [isFullscreen, setIsFullscreen] = useState(Boolean(document.fullscreenElement));
  const [alarmsOpen, setAlarmsOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
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

  const toggleAlarms = () => {
    setAlarmsOpen(current => {
      const next = !current;
      if (next) setHistoryOpen(false);
      return next;
    });
  };

  const toggleHistory = () => {
    setHistoryOpen(current => {
      const next = !current;
      if (next) setAlarmsOpen(false);
      return next;
    });
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
  >
    <header className="runtime-operator-bar">
      <div className="runtime-operator-context">
        <strong>{projection.projectName || projection.projectKey}</strong>
        <span>rev {projection.revision}</span>
      </div>
      {showHistoryNavigation ? <nav className="runtime-view-navigation runtime-view-navigation--inline" aria-label="Runtime views">
        <a href="/" className={!historyOpen ? 'active' : undefined} aria-current={!historyOpen ? 'page' : undefined}>{text.runtimeOverview}</a>
        <button
          type="button"
          className={historyOpen ? 'active' : undefined}
          aria-expanded={historyOpen}
          aria-controls="runtime-history-overlay"
          onClick={toggleHistory}
        >{text.runtimeHistory}</button>
      </nav> : null}
      <div className="runtime-operator-actions">
        <button type="button" className="runtime-operator-button" aria-expanded={alarmsOpen} onClick={toggleAlarms}>
          {text.alarms}
        </button>
        <button type="button" className="runtime-operator-button" onClick={() => void toggleFullscreen()}>
          {isFullscreen ? text.exitFullscreen : text.fullscreen}
        </button>
        {isFullscreen ? <UserSessionMenu locale={locale} includeRuntimeSessionControls /> : null}
      </div>
    </header>

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

    {historyOpen ? <aside id="runtime-history-overlay" className="runtime-operator-overlay runtime-operator-overlay--history" aria-label={historyText.title} data-testid="runtime-history-overlay">
      <div className="runtime-operator-overlay-header">
        <strong>{historyText.title}</strong>
        <button type="button" className="runtime-operator-button" onClick={() => setHistoryOpen(false)}>{historyText.closeHistory}</button>
      </div>
      <div className="runtime-operator-overlay-content runtime-history-overlay-content">
        <HistoricalDataBrowserRuntime locale={locale} />
      </div>
    </aside> : null}

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
