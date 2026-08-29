import React, { useEffect, useMemo, useRef, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import type {
  CommunicationDriverDiagnostic,
  EngineeringSnapshot,
  RuntimeDiagnosticsView
} from '../types';
import {
  buildProjectReferenceCatalog,
  type ClientMemoryDefinitionView,
  type ProjectReferenceDescriptor
} from '../project-reference/projectReferenceModel';
import { ProjectReferenceBrowser } from '../project-reference/ProjectReferenceBrowser';
import {
  initializeClientMemory,
  readClientMemoryValue
} from '../../runtime/clientMemory';
import './development-monitor.css';

type TagSnapshot = Readonly<{
  path: string;
  type: string;
  value: unknown;
  quality?: string | number | null;
  sourceTimestamp?: string | null;
  serverTimestamp?: string | null;
}>;

type MonitorSample = Readonly<{
  reference: string;
  value: unknown;
  dataType: string;
  quality?: string | number | null;
  state?: string | number | null;
  sourceTimestamp?: string | null;
  observedAt: string;
  detail?: string | null;
}>;

type RuntimeTagMessage = Readonly<{
  type?: string;
  tag?: TagSnapshot;
  state?: string;
}>;

export function DevelopmentMonitorWorkspace({
  snapshot,
  locale
}: Readonly<{ snapshot: EngineeringSnapshot; locale: EngineeringLocale }>) {
  const copy = monitorCopy(locale);
  const [clientDefinitions, setClientDefinitions] = useState<readonly ClientMemoryDefinitionView[]>([]);
  const [driverDiagnostics, setDriverDiagnostics] = useState<readonly CommunicationDriverDiagnostic[]>([]);
  const [selectedReferences, setSelectedReferences] = useState<readonly string[]>(() => loadWatchList());
  const [samples, setSamples] = useState<ReadonlyMap<string, MonitorSample>>(() => new Map());
  const [quickAdd, setQuickAdd] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const socketRef = useRef<WebSocket | null>(null);

  useEffect(() => {
    let cancelled = false;
    void initializeClientMemory()
      .then(definitions => {
        if (cancelled) return;
        setClientDefinitions(Object.freeze(definitions.map(definition => Object.freeze({
          name: definition.name,
          dataType: definition.dataType,
          initialValue: definition.initialValue,
          readOnly: false
        }))));
      })
      .catch(() => {
        if (!cancelled) setClientDefinitions(Object.freeze([]));
      });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    sessionStorage.setItem('elitescada.engineering.monitor.watchlist', JSON.stringify(selectedReferences));
  }, [selectedReferences]);

  const catalog = useMemo(() => buildProjectReferenceCatalog(
    snapshot.package,
    clientDefinitions,
    { driverKeys: driverDiagnostics.map(driver => driver.dataSourceKey) }
  ), [snapshot.package, clientDefinitions, driverDiagnostics]);

  const descriptorByReference = useMemo(
    () => new Map(catalog.map(item => [item.reference, item])),
    [catalog]
  );

  useEffect(() => {
    let cancelled = false;
    const refresh = async () => {
      try {
        const [tagResponse, diagnosticResponse] = await Promise.all([
          fetch('/api/tags', { credentials: 'same-origin' }),
          fetch('/api/diagnostics/runtime', { credentials: 'same-origin' })
        ]);
        const tags = tagResponse.ok ? await tagResponse.json() as TagSnapshot[] : [];
        const diagnostics = diagnosticResponse.ok ? await diagnosticResponse.json() as RuntimeDiagnosticsView : {};
        if (cancelled) return;
        const drivers = diagnostics.runtime?.communicationDrivers ?? [];
        setDriverDiagnostics(Object.freeze([...drivers]));
        setSamples(current => mergeBatchSamples(
          current,
          selectedReferences,
          descriptorByReference,
          tags,
          drivers,
          clientDefinitions
        ));
      } catch {
        if (cancelled) return;
        setSamples(current => markUnavailable(current, selectedReferences, descriptorByReference));
      }
    };

    void refresh();
    const timer = window.setInterval(() => void refresh(), 1500);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [selectedReferences, descriptorByReference, clientDefinitions]);

  useEffect(() => {
    socketRef.current?.close();
    socketRef.current = null;

    const tagPaths = selectedReferences
      .map(reference => descriptorByReference.get(reference))
      .filter((descriptor): descriptor is ProjectReferenceDescriptor =>
        descriptor != null && (descriptor.family === 'tag' || descriptor.family === 'serverMemory'))
      .map(descriptor => descriptor.reference);
    if (tagPaths.length === 0) return undefined;

    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const socket = new WebSocket(`${protocol}//${window.location.host}/api/realtime`);
    socketRef.current = socket;
    socket.addEventListener('open', () => {
      socket.send(JSON.stringify({ paths: tagPaths }));
    });
    socket.addEventListener('message', event => {
      try {
        const message = JSON.parse(String(event.data)) as RuntimeTagMessage;
        if (message.type !== 'tag' || !message.tag?.path) return;
        const tag = message.tag;
        setSamples(current => {
          const next = new Map(current);
          next.set(tag.path, Object.freeze({
            reference: tag.path,
            value: tag.value,
            dataType: tag.type,
            quality: tag.quality ?? null,
            sourceTimestamp: tag.sourceTimestamp ?? tag.serverTimestamp ?? null,
            observedAt: new Date().toISOString()
          }));
          return next;
        });
      } catch {
        // Malformed realtime data is ignored; the bounded batch refresh remains fallback evidence.
      }
    });
    return () => {
      socket.close();
      if (socketRef.current === socket) socketRef.current = null;
    };
  }, [selectedReferences, descriptorByReference]);

  const addReference = (reference: string) => {
    if (!descriptorByReference.has(reference)) {
      setMessage(copy.notFound);
      return;
    }
    setSelectedReferences(current => current.includes(reference)
      ? current
      : Object.freeze([...current, reference]));
    setQuickAdd('');
    setMessage(null);
  };

  const quickAddReference = () => {
    const candidate = quickAdd.trim();
    if (!candidate) return;
    const exact = catalog.filter(item => item.reference === candidate || item.label === candidate);
    if (exact.length === 1) {
      addReference(exact[0].reference);
      return;
    }
    if (exact.length > 1) {
      setMessage(copy.ambiguous);
      return;
    }
    setMessage(copy.notFound);
  };

  return <div className="eng-section development-monitor" data-testid="engineering-development-monitor">
    <header className="development-monitor__header">
      <div><span>{copy.eyebrow}</span><h1>{copy.title}</h1><p>{copy.description}</p></div>
      <div className="development-monitor__readonly"><strong>{copy.readOnly}</strong><span>{copy.readOnlyHint}</span></div>
    </header>

    <div className="development-monitor__add">
      <label>
        <span>{copy.quickAdd}</span>
        <div>
          <input
            value={quickAdd}
            placeholder={copy.quickAddPlaceholder}
            onChange={event => { setQuickAdd(event.currentTarget.value); setMessage(null); }}
            onKeyDown={event => { if (event.key === 'Enter') quickAddReference(); }}
          />
          <button type="button" onClick={quickAddReference}>{copy.add}</button>
        </div>
      </label>
      {message ? <div role="alert">{message}</div> : null}
    </div>

    <ProjectReferenceBrowser
      references={catalog}
      locale={locale}
      onSelect={reference => addReference(reference.reference)}
      title={copy.browse}
    />

    <section className="development-monitor__table-panel">
      <header>
        <strong>{copy.watchTable}</strong>
        <span>{selectedReferences.length} {copy.entries}</span>
        <button type="button" disabled={selectedReferences.length === 0} onClick={() => setSelectedReferences(Object.freeze([]))}>{copy.clear}</button>
      </header>
      <div className="development-monitor__table-wrap">
        <table>
          <thead><tr>
            <th>{copy.reference}</th><th>{copy.source}</th><th>{copy.value}</th><th>{copy.dataType}</th><th>{copy.quality}</th><th>{copy.timestamp}</th><th />
          </tr></thead>
          <tbody>
            {selectedReferences.length === 0 ? <tr><td colSpan={7}>{copy.empty}</td></tr> : selectedReferences.map(reference => {
              const descriptor = descriptorByReference.get(reference);
              const sample = samples.get(reference);
              return <tr key={reference} data-monitor-reference={reference}>
                <td><strong>{descriptor?.label ?? reference}</strong><code>{reference}</code></td>
                <td>{descriptor?.family ?? '—'}</td>
                <td className="mono">{formatMonitorValue(sample?.value)}</td>
                <td>{sample?.dataType ?? descriptor?.dataType ?? '—'}</td>
                <td className={qualityClass(sample)}>{formatQuality(sample)}</td>
                <td>{formatTimestamp(sample?.sourceTimestamp ?? sample?.observedAt, locale)}</td>
                <td><button type="button" aria-label={`${copy.remove} ${reference}`} onClick={() => setSelectedReferences(current => Object.freeze(current.filter(item => item !== reference)))}>×</button></td>
              </tr>;
            })}
          </tbody>
        </table>
      </div>
      <footer>{copy.architectureHint}</footer>
    </section>
  </div>;
}

function mergeBatchSamples(
  current: ReadonlyMap<string, MonitorSample>,
  selected: readonly string[],
  descriptors: ReadonlyMap<string, ProjectReferenceDescriptor>,
  tags: readonly TagSnapshot[],
  drivers: readonly CommunicationDriverDiagnostic[],
  clientDefinitions: readonly ClientMemoryDefinitionView[]
): ReadonlyMap<string, MonitorSample> {
  const next = new Map(current);
  const tagByPath = new Map(tags.map(tag => [tag.path, tag]));
  const driverByKey = new Map(drivers.map(driver => [driver.dataSourceKey, driver]));
  const now = new Date().toISOString();

  for (const reference of selected) {
    const descriptor = descriptors.get(reference);
    if (!descriptor) continue;

    if (descriptor.family === 'tag' || descriptor.family === 'serverMemory') {
      const tag = tagByPath.get(reference);
      next.set(reference, tag ? Object.freeze({
        reference,
        value: tag.value,
        dataType: tag.type || descriptor.dataType,
        quality: tag.quality ?? null,
        sourceTimestamp: tag.sourceTimestamp ?? tag.serverTimestamp ?? null,
        observedAt: now
      }) : unavailableSample(descriptor, now));
      continue;
    }

    if (descriptor.family === 'clientMemory') {
      const definition = clientDefinitions.find(candidate => candidate.name === reference);
      next.set(reference, Object.freeze({
        reference,
        value: definition ? readClientMemoryValue(reference) : null,
        dataType: definition?.dataType ?? descriptor.dataType,
        state: definition ? 'LocalSession' : 'Unavailable',
        sourceTimestamp: null,
        observedAt: now
      }));
      continue;
    }

    if (reference === 'system.runtime.tagCount') {
      next.set(reference, sample(reference, tags.length, 'Int32', 'Available', now));
      continue;
    }
    if (reference === 'system.runtime.driverCount') {
      next.set(reference, sample(reference, drivers.length, 'Int32', 'Available', now));
      continue;
    }

    if (descriptor.family === 'driverDiagnostic') {
      const parts = reference.split(':');
      const driverKey = parts[1] ?? '';
      const field = parts.slice(2).join(':');
      const driver = driverByKey.get(driverKey);
      if (!driver) {
        next.set(reference, unavailableSample(descriptor, now));
        continue;
      }
      const driverRecord = driver as unknown as Record<string, unknown>;
      next.set(reference, Object.freeze({
        reference,
        value: driverRecord[field] ?? null,
        dataType: descriptor.dataType,
        state: driver.state,
        sourceTimestamp: driver.capturedAt ?? driver.stateChangedAt ?? null,
        observedAt: now,
        detail: driver.lastError ?? null
      }));
    }
  }
  return next;
}

function markUnavailable(
  current: ReadonlyMap<string, MonitorSample>,
  selected: readonly string[],
  descriptors: ReadonlyMap<string, ProjectReferenceDescriptor>
): ReadonlyMap<string, MonitorSample> {
  const next = new Map(current);
  const now = new Date().toISOString();
  for (const reference of selected) {
    const descriptor = descriptors.get(reference);
    if (descriptor) next.set(reference, unavailableSample(descriptor, now));
  }
  return next;
}

function unavailableSample(descriptor: ProjectReferenceDescriptor, now: string): MonitorSample {
  return Object.freeze({
    reference: descriptor.reference,
    value: null,
    dataType: descriptor.dataType,
    state: 'Unavailable',
    sourceTimestamp: null,
    observedAt: now
  });
}

function sample(reference: string, value: unknown, dataType: string, state: string, observedAt: string): MonitorSample {
  return Object.freeze({ reference, value, dataType, state, observedAt });
}

function formatMonitorValue(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'string') return value;
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (typeof value === 'number') return Number.isFinite(value) ? String(value) : '—';
  try { return JSON.stringify(value); } catch { return String(value); }
}

function formatQuality(sample: MonitorSample | undefined): string {
  if (!sample) return 'Unavailable';
  if (sample.quality !== undefined && sample.quality !== null) return String(sample.quality);
  if (sample.state !== undefined && sample.state !== null) return String(sample.state);
  return 'N/A';
}

function qualityClass(sample: MonitorSample | undefined): string {
  const value = formatQuality(sample).toLowerCase();
  return value.includes('good') || value.includes('available') || value.includes('connected') || value.includes('localsession') ? 'is-good'
    : value === 'n/a' ? '' : 'is-bad';
}

function formatTimestamp(value: string | null | undefined, locale: EngineeringLocale): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(date);
}

function loadWatchList(): readonly string[] {
  try {
    const raw = sessionStorage.getItem('elitescada.engineering.monitor.watchlist');
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? Object.freeze(parsed.filter((item): item is string => typeof item === 'string')) : Object.freeze([]);
  } catch {
    return Object.freeze([]);
  }
}

function monitorCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Engineering diagnostics', title: 'Development Monitor',
    description: 'Observe live TAGs, internal memories and runtime/driver diagnostics while developing the application.',
    readOnly: 'Read-only observation', readOnlyHint: 'Monitoring never writes or forces process values.',
    quickAdd: 'Exact quick-add', quickAddPlaceholder: 'Type a canonical reference or TAG path', add: 'Add', browse: 'Browse project references',
    notFound: 'Reference not found.', ambiguous: 'Reference is ambiguous; choose it from the tree.', watchTable: 'Watch table', entries: 'entries', clear: 'Clear',
    reference: 'Name / Reference', source: 'Source', value: 'Value', dataType: 'Data type', quality: 'Quality / State', timestamp: 'Last update', remove: 'Remove',
    empty: 'No variables are being monitored.', architectureHint: 'TAGs share one realtime subscription; diagnostics and browser-local memory use bounded batch refreshes.'
  };
  if (locale === 'es') return {
    eyebrow: 'Diagnóstico de Ingeniería', title: 'Monitor de Desarrollo',
    description: 'Observe TAGs, memorias internas y diagnósticos de runtime/driver durante el desarrollo.',
    readOnly: 'Observación de solo lectura', readOnlyHint: 'El monitor nunca escribe ni fuerza valores de proceso.',
    quickAdd: 'Agregar referencia exacta', quickAddPlaceholder: 'Escriba una referencia canónica o path de TAG', add: 'Agregar', browse: 'Explorar referencias del proyecto',
    notFound: 'Referencia no encontrada.', ambiguous: 'La referencia es ambigua; selecciónela en el árbol.', watchTable: 'Tabla de monitoreo', entries: 'entradas', clear: 'Limpiar',
    reference: 'Nombre / Referencia', source: 'Fuente', value: 'Valor', dataType: 'Tipo', quality: 'Calidad / Estado', timestamp: 'Última actualización', remove: 'Quitar',
    empty: 'No hay variables monitoreadas.', architectureHint: 'Los TAGs comparten una suscripción realtime; diagnósticos y memoria local usan actualización agrupada.'
  };
  return {
    eyebrow: 'Diagnóstico de Engenharia', title: 'Monitor de Desenvolvimento',
    description: 'Observe TAGs, memórias internas e diagnósticos de Runtime/driver enquanto desenvolve a aplicação.',
    readOnly: 'Observação somente leitura', readOnlyHint: 'O monitor nunca escreve nem força valores de processo.',
    quickAdd: 'Adicionar referência exata', quickAddPlaceholder: 'Digite uma referência canônica ou path de TAG', add: 'Adicionar', browse: 'Procurar referências do projeto',
    notFound: 'Referência não encontrada.', ambiguous: 'A referência é ambígua; selecione-a na árvore.', watchTable: 'Tabela de monitoramento', entries: 'entradas', clear: 'Limpar',
    reference: 'Nome / Referência', source: 'Fonte', value: 'Valor', dataType: 'Tipo', quality: 'Qualidade / Estado', timestamp: 'Última atualização', remove: 'Remover',
    empty: 'Nenhuma variável está sendo monitorada.', architectureHint: 'TAGs compartilham uma assinatura realtime; diagnósticos e memória local usam atualização agrupada e limitada.'
  };
}
