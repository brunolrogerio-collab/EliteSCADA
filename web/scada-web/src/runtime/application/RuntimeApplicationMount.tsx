import React, { useEffect, useMemo, useRef, useState } from 'react';
import { appShellText, useAppShellLocale } from '../../appShellI18n';
import { UserSessionMenu } from '../../auth/UserSessionMenu';
import type { ScriptEngineeringContext } from '../../engineering/scripts/scriptEngineeringTypes';
import { RuntimeAlarmCenter } from '../RuntimeAlarmCenter';
import { RuntimeVisualNavigator } from '../visual-navigation/RuntimeVisualNavigator';
import {
  loadRuntimeApplicationProjection,
  RuntimeApplicationProjectionError,
  runtimeVisualAssetContentUrl,
  type RuntimeApplicationProjection
} from './runtimeApplicationApi';
import { SimulationRuntimeApp } from './SimulationRuntimeApp';

const REFRESH_INTERVAL_MS = 1500;

export function RuntimeApplicationMount() {
  const locale = useAppShellLocale();
  const text = appShellText(locale);
  const [projection, setProjection] = useState<RuntimeApplicationProjection | null>(null);
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
        setProjection(current => sameRuntimeProjection(current, next) ? current : next);
        setError(null);
      } catch (reason) {
        if (disposed || controller.signal.aborted) return;
        setProjection(null);
        setError(reason instanceof Error ? reason : new Error(String(reason)));
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

  if (projection.mode === 'simulation') return <SimulationRuntimeApp />;
  return <EngineeringRuntimeApplication projection={projection} locale={locale} />;
}

function EngineeringRuntimeApplication({
  projection,
  locale
}: {
  projection: RuntimeApplicationProjection;
  locale: ReturnType<typeof useAppShellLocale>;
}) {
  const text = appShellText(locale);
  const fullscreenRoot = useRef<HTMLElement>(null);
  const [isFullscreen, setIsFullscreen] = useState(Boolean(document.fullscreenElement));
  const [alarmsOpen, setAlarmsOpen] = useState(false);
  const engineeringPackage = projection.package!;
  const initialScreenKey = useMemo(() => {
    const keys = (engineeringPackage.screens ?? [])
      .map(screen => screen.key?.trim())
      .filter((key): key is string => Boolean(key))
      .sort((left, right) => left.localeCompare(right, 'en', { sensitivity: 'base' }));
    return keys[0] ?? '';
  }, [engineeringPackage]);

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

  if (!initialScreenKey) {
    return <main className="shell" data-testid="runtime-engineering-application">
      <section className="runtime-visual-diagnostic" role="alert" data-diagnostic-code="HMI_RUNTIME_SCREEN_REQUIRED">
        <strong>HMI_RUNTIME_SCREEN_REQUIRED</strong>
        <span>{text.runtimeUnavailable}</span>
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
      <div className="runtime-operator-actions">
        <button type="button" className="runtime-operator-button" aria-expanded={alarmsOpen} onClick={() => setAlarmsOpen(value => !value)}>
          {text.alarms}
        </button>
        <button type="button" className="runtime-operator-button" onClick={() => void toggleFullscreen()}>
          {isFullscreen ? text.exitFullscreen : text.fullscreen}
        </button>
        {isFullscreen ? <UserSessionMenu locale={locale} /> : null}
      </div>
    </header>

    <section className="runtime-engineering-canvas" data-testid="runtime-engineering-canvas">
      <RuntimeVisualNavigator
        engineeringPackage={engineeringPackage}
        initialScreenKey={initialScreenKey}
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

function sameRuntimeProjection(
  current: RuntimeApplicationProjection | null,
  next: RuntimeApplicationProjection
): boolean {
  if (!current || current.mode !== next.mode) return false;
  if (next.mode === 'simulation') return true;
  return current.projectKey === next.projectKey &&
    current.revision === next.revision &&
    current.activatedAtUtc === next.activatedAtUtc;
}
