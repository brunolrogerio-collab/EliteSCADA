import React, { useEffect, useMemo, useRef, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import type {
  CommunicationDriverDiagnostic,
  EngineeringSnapshot
} from '../types';
import { loadCommunicationDiagnostics } from '../api';
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
import {
  loadReadableRuntimeTags,
  openRuntimeTagSocket,
  parseRuntimeTagRealtimeMessage,
  type RuntimeTagSnapshot
} from '../../runtime/liveTagTransport';
import {
  applyMonitorRealtimeMessage,
  formatMonitorQuality,
  formatMonitorValue,
  markMonitorUnavailable,
  mergeMonitorBatchSamples,
  monitorQualityClass,
  resolveMonitorQuickAdd,
  type MonitorSample
} from './developmentMonitorModel';
import './development-monitor.css';

export function DevelopmentMonitorWorkspace({
  snapshot,
  locale
}: Readonly<{ snapshot: EngineeringSnapshot; locale: EngineeringLocale }>) {
  const copy = monitorCopy(locale);
  const [clientDefinitions, setClientDefinitions] = useState<readonly ClientMemoryDefinitionView[]>([]);
  const [driverDiagnostics, setDriverDiagnostics] = useState<readonly CommunicationDriverDiagnostic[]>([]);
  const [runtimeTags, setRuntimeTags] = useState<readonly RuntimeTagSnapshot[]>([]);
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
          path: definition.path,
          dataType: definition.dataType,
          initialValue: definition.initialValue,
          readOnly: definition.readOnly
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

  const driverKeySignature = useMemo(() => [...new Set(driverDiagnostics.map(driver => driver.dataSourceKey))].sort().join('\u0000'), [driverDiagnostics]);
  const driverKeys = useMemo(() => driverKeySignature ? driverKeySignature.split('\u0000') : [], [driverKeySignature]);
  const catalog = useMemo(() => buildProjectReferenceCatalog(
    snapshot.package,
    clientDefinitions,
    { driverKeys }
  ), [snapshot.package, clientDefinitions, driverKeys]);

  const descriptorByReference = useMemo(
    () => new Map(catalog.map(item => [item.reference, item])),
    [catalog]
  );
  const selectedReferenceSet = useMemo(() => new Set(selectedReferences), [selectedReferences]);

  useEffect(() => {
    let cancelled = false;
    const refresh = async () => {
      try {
        const [tags, drivers] = await Promise.all([
          loadReadableRuntimeTags(),
          loadCommunicationDiagnostics()
        ]);
        if (cancelled) return;
        setRuntimeTags(tags);
        setDriverDiagnostics(Object.freeze([...drivers]));
      } catch {
        if (!cancelled) setSamples(current => markMonitorUnavailable(current, selectedReferences, descriptorByReference));
      }
    };

    void refresh();
    const timer = window.setInterval(() => void refresh(), 1500);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [selectedReferences, descriptorByReference]);

  useEffect(() => {
    setSamples(current => mergeMonitorBatchSamples(
      current,
      selectedReferences,
      descriptorByReference,
      runtimeTags,
      driverDiagnostics,
      clientDefinitions,
      readClientMemoryValue
    ));
  }, [selectedReferences, descriptorByReference, runtimeTags, driverDiagnostics, clientDefinitions]);

  useEffect(() => {
    socketRef.current?.close();
    socketRef.current = null;

    const hasTagReferences = selectedReferences.some(reference => {
      const descriptor = descriptorByReference.get(reference);
      return descriptor?.family === 'tag' || descriptor?.family === 'serverMemory';
    });
    if (!hasTagReferences) return undefined;

    const socket = openRuntimeTagSocket();
    socketRef.current = socket;
    socket.addEventListener('message', event => {
      const realtime = parseRuntimeTagRealtimeMessage(String(event.data));
      if (!realtime) return;
      setSamples(current => applyMonitorRealtimeMessage(
        current,
        realtime,
        selectedReferenceSet,
        descriptorByReference
      ));
    });
    socket.addEventListener('close', () => {
      setSamples(current => {
        const tagReferences = selectedReferences.filter(reference => {
          const descriptor = descriptorByReference.get(reference);
          return descriptor?.family === 'tag' || descriptor?.family === 'serverMemory';
        });
        return markMonitorUnavailable(current, tagReferences, descriptorByReference);
      });
    });
    return () => {
      socket.close();
      if (socketRef.current === socket) socketRef.current = null;
    };
  }, [selectedReferences, selectedReferenceSet, descriptorByReference]);

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
    const result = resolveMonitorQuickAdd(catalog, quickAdd);
    if (result.status === 'found' && result.reference) {
      addReference(result.reference);
      return;
    }
    setMessage(result.status === 'ambiguous' ? copy.ambiguous : copy.notFound);
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
                <td>{descriptor ? projectSourceLabel(descriptor, locale) : '—'}</td>
                <td className="mono">{formatMonitorValue(sample?.value)}</td>
                <td>{sample?.dataType ?? descriptor?.dataType ?? '—'}</td>
                <td className={monitorQualityClass(sample)} title={sample?.detail ?? undefined}>{formatMonitorQuality(sample)}</td>
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

function projectSourceLabel(descriptor: ProjectReferenceDescriptor, locale: EngineeringLocale): string {
  const labels = {
    'pt-BR': { tag: 'TAG', serverMemory: 'Memória do Servidor', clientMemory: 'Memória do Cliente', system: 'Sistema', driverDiagnostic: 'Driver', asset: 'Asset' },
    en: { tag: 'TAG', serverMemory: 'Server Memory', clientMemory: 'Client Memory', system: 'System', driverDiagnostic: 'Driver', asset: 'Asset' },
    es: { tag: 'TAG', serverMemory: 'Memoria del Servidor', clientMemory: 'Memoria del Cliente', system: 'Sistema', driverDiagnostic: 'Driver', asset: 'Asset' }
  } as const;
  return labels[locale][descriptor.family];
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
    empty: 'No variables are being monitored.', architectureHint: 'TAGs share one realtime connection; diagnostics and browser-local memory use bounded batch refreshes.'
  };
  if (locale === 'es') return {
    eyebrow: 'Diagnóstico de Engineering', title: 'Monitor de Desarrollo',
    description: 'Observe TAGs, memorias internas y diagnósticos de runtime/drivers mientras desarrolla la aplicación.',
    readOnly: 'Observación de solo lectura', readOnlyHint: 'El monitor nunca escribe ni fuerza valores de proceso.',
    quickAdd: 'Agregar referencia exacta', quickAddPlaceholder: 'Escriba una referencia canónica o path de TAG', add: 'Agregar', browse: 'Explorar referencias del proyecto',
    notFound: 'Referencia no encontrada.', ambiguous: 'La referencia es ambigua; selecciónela en el árbol.', watchTable: 'Tabla de monitoreo', entries: 'entradas', clear: 'Limpiar',
    reference: 'Nombre / Referencia', source: 'Fuente', value: 'Valor', dataType: 'Tipo', quality: 'Calidad / Estado', timestamp: 'Última actualización', remove: 'Eliminar',
    empty: 'No hay variables monitoreadas.', architectureHint: 'Los TAGs comparten una conexión realtime; los diagnósticos y la memoria local usan actualizaciones agrupadas.'
  };
  return {
    eyebrow: 'Diagnóstico de Engenharia', title: 'Monitoramento de Desenvolvimento',
    description: 'Observe TAGs, memórias internas e diagnósticos de Runtime/Drivers durante o desenvolvimento da aplicação.',
    readOnly: 'Observação somente leitura', readOnlyHint: 'O monitor nunca escreve nem força valores de processo.',
    quickAdd: 'Adicionar referência exata', quickAddPlaceholder: 'Digite uma referência canônica ou path de TAG', add: 'Adicionar', browse: 'Procurar referências do projeto',
    notFound: 'Referência não encontrada.', ambiguous: 'A referência é ambígua; selecione-a na árvore.', watchTable: 'Tabela de monitoramento', entries: 'itens', clear: 'Limpar',
    reference: 'Nome / Referência', source: 'Fonte', value: 'Valor', dataType: 'Tipo', quality: 'Qualidade / Estado', timestamp: 'Última atualização', remove: 'Remover',
    empty: 'Nenhuma variável está sendo monitorada.', architectureHint: 'TAGs compartilham uma conexão realtime; diagnósticos e memória local usam atualização agrupada e limitada.'
  };
}
