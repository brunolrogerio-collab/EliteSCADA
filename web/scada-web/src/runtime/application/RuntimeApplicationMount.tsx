import React, { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from '../../engineering/i18n';
import type { ScriptEngineeringContext } from '../../engineering/scripts/scriptEngineeringTypes';
import { BasicTrendViewer } from '../BasicTrendViewer';
import { RuntimeAlarmCenter } from '../operations';
import { RuntimeTagInspector } from '../RuntimeTagInspector';
import { RuntimeVisualNavigator } from '../visual-navigation/RuntimeVisualNavigator';
import {
  loadRuntimeApplicationProjection,
  RuntimeApplicationProjectionError,
  type RuntimeApplicationProjection
} from './runtimeApplicationApi';
import { SimulationRuntimeApp } from './SimulationRuntimeApp';

const REFRESH_INTERVAL_MS = 1500;

function resolveRuntimeLocale(): EngineeringLocale {
  const stored = window.localStorage.getItem('elitescada.engineering.locale');
  return stored === 'en' || stored === 'es' ? stored : 'pt-BR';
}

export function RuntimeApplicationMount() {
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
        <span>Runtime application projection failed closed ({status}). {error.message}</span>
      </section>
    </main>;
  }

  if (!projection) {
    return <main className="shell" data-testid="runtime-application-loading">
      <section className="runtime-visual-diagnostic" role="status">
        <strong>Runtime</strong><span>Loading active application…</span>
      </section>
    </main>;
  }

  if (projection.mode === 'simulation') return <SimulationRuntimeApp />;
  return <EngineeringRuntimeApplication projection={projection} />;
}

function EngineeringRuntimeApplication({ projection }: { projection: RuntimeApplicationProjection }) {
  const locale = useMemo(resolveRuntimeLocale, []);
  const engineeringPackage = projection.package!;
  const initialScreenKey = useMemo(() => {
    const keys = (engineeringPackage.screens ?? [])
      .map(screen => screen.key?.trim())
      .filter((key): key is string => Boolean(key))
      .sort((left, right) => left.localeCompare(right, 'en', { sensitivity: 'base' }));
    return keys[0] ?? '';
  }, [engineeringPackage]);

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

  if (!initialScreenKey) {
    return <main className="shell" data-testid="runtime-engineering-application">
      <header className="topbar">
        <div><strong>{projection.projectName || projection.projectKey}</strong><span>Runtime Engineering · rev {projection.revision}</span></div>
      </header>
      <section className="runtime-visual-diagnostic" role="alert" data-diagnostic-code="HMI_RUNTIME_SCREEN_REQUIRED">
        <strong>HMI_RUNTIME_SCREEN_REQUIRED</strong>
        <span>The Active Engineering revision contains no canonical Screen to mount.</span>
      </section>
    </main>;
  }

  return <main
    className="shell runtime-engineering-application"
    data-testid="runtime-engineering-application"
    data-runtime-project-key={projection.projectKey ?? undefined}
    data-runtime-revision={projection.revision ?? undefined}
  >
    <header className="topbar">
      <div>
        <strong>{projection.projectName || projection.projectKey}</strong>
        <span>Runtime Engineering · rev {projection.revision}</span>
      </div>
      <div className="connection online">ACTIVE ENGINEERING</div>
    </header>

    <RuntimeAlarmCenter locale={locale} />
    <RuntimeTagInspector locale={locale} />
    <BasicTrendViewer locale={locale} />

    <section className="process-card runtime-engineering-canvas" data-testid="runtime-engineering-canvas">
      <div className="process-title">{initialScreenKey}</div>
      <RuntimeVisualNavigator
        engineeringPackage={engineeringPackage}
        initialScreenKey={initialScreenKey}
        locale={locale}
        scriptContext={scriptContext}
        emptyLabel="Active Screen has no visual objects."
      />
    </section>
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
