import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { RuntimeTagInspector } from '../../runtime/RuntimeTagInspector';
import {
  loadRuntimeApplicationProjection,
  type RuntimeApplicationProjection
} from '../../runtime/application/runtimeApplicationApi';
import type { EngineeringLocale } from '../i18n';
import type { EngineeringSnapshot } from '../types';

type RuntimeContextLoader = (signal?: AbortSignal) => Promise<RuntimeApplicationProjection>;

type EngineeringTagMonitorWorkspaceProps = {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  runtimeContextLoader?: RuntimeContextLoader;
  contextRefreshIntervalMs?: number;
};

type Copy = {
  eyebrow: string;
  title: string;
  description: string;
  readOnly: string;
  engineeringContext: string;
  workingRevision: string;
  observedSource: string;
  activeRuntime: string;
  activeIdentity: string;
  activeSimulation: string;
  activeUnavailable: string;
  activatedAt: string;
  alignment: string;
  aligned: string;
  differs: string;
  simulationBoundary: string;
  contextRefreshing: string;
};

const copy: Record<EngineeringLocale, Copy> = {
  'pt-BR': {
    eyebrow: 'Engenharia / Diagnósticos',
    title: 'TAG Monitor',
    description: 'Diagnóstico de engenharia somente leitura. O contexto Working identifica o projeto aberto; valores, qualidade, timestamps e histórico continuam vindo do Active Runtime real.',
    readOnly: 'Somente leitura',
    engineeringContext: 'Contexto Engineering',
    workingRevision: 'Revisão Working',
    observedSource: 'Fonte observada',
    activeRuntime: 'Active Runtime',
    activeIdentity: 'Identidade ativa',
    activeSimulation: 'Simulação / demo',
    activeUnavailable: 'Contexto do Active Runtime indisponível',
    activatedAt: 'Ativado em',
    alignment: 'Working x Active',
    aligned: 'Working alinhado com Active',
    differs: 'Working difere do Active',
    simulationBoundary: 'Working é contexto de engenharia; a simulação ativa permanece a autoridade dos dados observados.',
    contextRefreshing: 'Atualizando identidade do Active Runtime…'
  },
  en: {
    eyebrow: 'Engineering / Diagnostics',
    title: 'TAG Monitor',
    description: 'Read-only engineering diagnostics. Working identifies the open project context; values, quality, timestamps and history still come from the real Active Runtime.',
    readOnly: 'Read-only',
    engineeringContext: 'Engineering context',
    workingRevision: 'Working revision',
    observedSource: 'Observed source',
    activeRuntime: 'Active Runtime',
    activeIdentity: 'Active identity',
    activeSimulation: 'Simulation / demo',
    activeUnavailable: 'Active Runtime context unavailable',
    activatedAt: 'Activated at',
    alignment: 'Working vs Active',
    aligned: 'Working aligned with Active',
    differs: 'Working differs from Active',
    simulationBoundary: 'Working is engineering context; the active simulation remains authoritative for observed data.',
    contextRefreshing: 'Refreshing Active Runtime identity…'
  },
  es: {
    eyebrow: 'Ingeniería / Diagnósticos',
    title: 'TAG Monitor',
    description: 'Diagnóstico de ingeniería de solo lectura. Working identifica el contexto del proyecto abierto; valores, calidad, timestamps e histórico siguen viniendo del Active Runtime real.',
    readOnly: 'Solo lectura',
    engineeringContext: 'Contexto Engineering',
    workingRevision: 'Revisión Working',
    observedSource: 'Fuente observada',
    activeRuntime: 'Active Runtime',
    activeIdentity: 'Identidad activa',
    activeSimulation: 'Simulación / demo',
    activeUnavailable: 'Contexto del Active Runtime no disponible',
    activatedAt: 'Activado en',
    alignment: 'Working vs Active',
    aligned: 'Working alineado con Active',
    differs: 'Working difiere del Active',
    simulationBoundary: 'Working es contexto de ingeniería; la simulación activa sigue siendo la autoridad de los datos observados.',
    contextRefreshing: 'Actualizando identidad del Active Runtime…'
  }
};

export function EngineeringTagMonitorWorkspace({
  snapshot,
  locale,
  runtimeContextLoader = loadRuntimeApplicationProjection,
  contextRefreshIntervalMs = 5000
}: EngineeringTagMonitorWorkspaceProps) {
  const text = copy[locale];
  const [activeRuntime, setActiveRuntime] = useState<RuntimeApplicationProjection | null>(null);
  const [contextError, setContextError] = useState<string | null>(null);
  const [contextRefreshing, setContextRefreshing] = useState(false);
  const contextAbort = useRef<AbortController | null>(null);

  const refreshContext = useCallback(async () => {
    contextAbort.current?.abort();
    const controller = new AbortController();
    contextAbort.current = controller;
    setContextRefreshing(true);
    try {
      const next = await runtimeContextLoader(controller.signal);
      if (controller.signal.aborted) return;
      setActiveRuntime(next);
      setContextError(null);
    } catch (reason) {
      if (controller.signal.aborted) return;
      setActiveRuntime(null);
      setContextError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      if (contextAbort.current === controller) {
        contextAbort.current = null;
        setContextRefreshing(false);
      }
    }
  }, [runtimeContextLoader]);

  useEffect(() => {
    void refreshContext();
    const timer = contextRefreshIntervalMs > 0
      ? window.setInterval(() => void refreshContext(), contextRefreshIntervalMs)
      : undefined;
    return () => {
      if (timer !== undefined) window.clearInterval(timer);
      contextAbort.current?.abort();
    };
  }, [contextRefreshIntervalMs, refreshContext]);

  const workspace = snapshot.workspace;
  const workingProject = workspace.projectName ?? workspace.projectKey ?? '—';
  const workingRevision = workspace.baseRevision == null ? '—' : String(workspace.baseRevision);
  const activeIdentity = activeRuntime?.mode === 'engineering'
    ? `${activeRuntime.projectName ?? activeRuntime.projectKey ?? '—'} · rev ${activeRuntime.revision ?? '—'}`
    : activeRuntime?.mode === 'simulation'
      ? text.activeSimulation
      : '—';
  const boundary = useMemo(() => {
    if (!activeRuntime) return text.activeUnavailable;
    if (activeRuntime.mode === 'simulation') return text.simulationBoundary;
    const sameProject = !workspace.projectKey || !activeRuntime.projectKey ||
      workspace.projectKey.localeCompare(activeRuntime.projectKey, undefined, { sensitivity: 'accent' }) === 0;
    const sameRevision = workspace.baseRevision != null && activeRuntime.revision != null &&
      workspace.baseRevision === activeRuntime.revision;
    return !workspace.isDirty && sameProject && sameRevision ? text.aligned : text.differs;
  }, [activeRuntime, text.activeUnavailable, text.aligned, text.differs, text.simulationBoundary, workspace.baseRevision, workspace.isDirty, workspace.projectKey]);

  return (
    <div
      className="eng-section"
      data-testid="engineering-tag-monitor"
      data-active-runtime-project={activeRuntime?.projectKey ?? undefined}
      data-active-runtime-revision={activeRuntime?.revision ?? undefined}
    >
      <header className="eng-section-header">
        <div>
          <span className="eng-eyebrow">{text.eyebrow}</span>
          <h1>{text.title}</h1>
          <p>{text.description}</p>
        </div>
        <div className="eng-section-meta"><span>{text.readOnly}</span></div>
      </header>

      <div className="eng-diagnostic-grid" data-testid="tag-monitor-context">
        <ContextFact label={text.engineeringContext} value={workingProject} />
        <ContextFact label={text.workingRevision} value={workingRevision} />
        <ContextFact label={text.observedSource} value={text.activeRuntime} />
        <ContextFact label={text.activeIdentity} value={activeIdentity} />
      </div>

      <section className="eng-panel" data-testid="tag-monitor-runtime-boundary">
        <div className="eng-empty">
          <strong>{text.alignment}</strong>
          <span>{boundary}</span>
          {activeRuntime?.activatedAtUtc && <span>{text.activatedAt}: {formatMoment(activeRuntime.activatedAtUtc, locale)}</span>}
          {contextRefreshing && <span>{text.contextRefreshing}</span>}
          {contextError && <span>{contextError}</span>}
        </div>
      </section>

      <RuntimeTagInspector locale={locale} />
    </div>
  );
}

function ContextFact({ label, value }: { label: string; value: string }) {
  return <div className="eng-diagnostic-card"><span>{label}</span><strong>{value}</strong></div>;
}

function formatMoment(value: string, locale: EngineeringLocale) {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
}
