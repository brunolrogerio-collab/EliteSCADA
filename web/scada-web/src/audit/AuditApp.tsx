import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { AuditApiError, loadAuditDiagnostics, loadAuditPage } from './api';
import { auditOutcomeLabel, sortedAuditDetails } from './contract';
import { defaultAuditFilters } from './types';
import type { AuditApiErrorKind } from './api';
import type { AuditDiagnostics, AuditEventView, AuditFilterState } from './types';
import './audit.css';

type AuditLocale = 'pt-BR' | 'en' | 'es';
const localeKey = 'elitescada.engineering.locale';

const messages = {
  'pt-BR': {
    title: 'Auditoria',
    subtitle: 'Eventos administrativos e operacionais protegidos',
    filters: 'Filtros',
    from: 'De',
    to: 'Até',
    subject: 'Usuário / subject',
    action: 'Ação',
    outcome: 'Resultado',
    all: 'Todos',
    succeeded: 'Sucesso',
    denied: 'Negado',
    failed: 'Falha',
    targetKind: 'Tipo de recurso',
    targetId: 'Recurso / chave',
    area: 'Área',
    correlation: 'Correlation ID',
    pageSize: 'Eventos por página',
    apply: 'Consultar',
    clear: 'Limpar',
    refresh: 'Atualizar',
    loading: 'Carregando auditoria…',
    empty: 'Nenhum evento corresponde aos filtros atuais.',
    unauthenticated: 'A sessão não está autenticada. Entre novamente para consultar a auditoria.',
    forbidden: 'Este usuário não possui permissão SystemAdmin para consultar a auditoria.',
    invalidQuery: 'Os filtros enviados não foram aceitos pelo servidor.',
    unavailable: 'Não foi possível acessar o serviço de auditoria.',
    server: 'O serviço de auditoria retornou uma resposta inesperada.',
    timestamp: 'Data/hora',
    actor: 'Identidade',
    resource: 'Recurso',
    details: 'Metadados',
    page: 'Página',
    previous: 'Anterior',
    next: 'Próxima',
    diagnostics: 'Diagnósticos',
    diagnosticsSubtitle: 'Saúde do armazenamento, buffer e retenção',
    persisted: 'Persistidos',
    appendFailures: 'Falhas de persistência',
    queueDepth: 'Fila pendente',
    forwarded: 'Encaminhados',
    bufferFailures: 'Falhas de encaminhamento',
    rejected: 'Rejeitados por limite',
    droppedShutdown: 'Descartados no shutdown',
    retention: 'Retenção',
    enabled: 'Ativa',
    disabled: 'Desativada',
    finite: 'Finita',
    indefinite: 'Indefinida',
    lastPersisted: 'Última persistência',
    lastFailure: 'Última falha',
    lastRetention: 'Última retenção',
    never: 'Nunca',
    noDetails: 'Sem metadados',
    project: 'Projeto',
    source: 'Origem'
  },
  en: {
    title: 'Audit',
    subtitle: 'Protected administrative and operational events',
    filters: 'Filters',
    from: 'From',
    to: 'To',
    subject: 'User / subject',
    action: 'Action',
    outcome: 'Outcome',
    all: 'All',
    succeeded: 'Succeeded',
    denied: 'Denied',
    failed: 'Failed',
    targetKind: 'Resource kind',
    targetId: 'Resource / key',
    area: 'Area',
    correlation: 'Correlation ID',
    pageSize: 'Events per page',
    apply: 'Query',
    clear: 'Clear',
    refresh: 'Refresh',
    loading: 'Loading audit…',
    empty: 'No events match the current filters.',
    unauthenticated: 'The session is not authenticated. Sign in again to query Audit.',
    forbidden: 'This user does not have SystemAdmin permission to query Audit.',
    invalidQuery: 'The server rejected the supplied filters.',
    unavailable: 'The Audit service could not be reached.',
    server: 'The Audit service returned an unexpected response.',
    timestamp: 'Timestamp',
    actor: 'Identity',
    resource: 'Resource',
    details: 'Metadata',
    page: 'Page',
    previous: 'Previous',
    next: 'Next',
    diagnostics: 'Diagnostics',
    diagnosticsSubtitle: 'Storage, buffer and retention health',
    persisted: 'Persisted',
    appendFailures: 'Append failures',
    queueDepth: 'Pending queue',
    forwarded: 'Forwarded',
    bufferFailures: 'Forward failures',
    rejected: 'Rejected by limit',
    droppedShutdown: 'Dropped on shutdown',
    retention: 'Retention',
    enabled: 'Enabled',
    disabled: 'Disabled',
    finite: 'Finite',
    indefinite: 'Indefinite',
    lastPersisted: 'Last persisted',
    lastFailure: 'Last failure',
    lastRetention: 'Last retention',
    never: 'Never',
    noDetails: 'No metadata',
    project: 'Project',
    source: 'Source'
  },
  es: {
    title: 'Auditoría',
    subtitle: 'Eventos administrativos y operativos protegidos',
    filters: 'Filtros',
    from: 'Desde',
    to: 'Hasta',
    subject: 'Usuario / subject',
    action: 'Acción',
    outcome: 'Resultado',
    all: 'Todos',
    succeeded: 'Éxito',
    denied: 'Denegado',
    failed: 'Falla',
    targetKind: 'Tipo de recurso',
    targetId: 'Recurso / clave',
    area: 'Área',
    correlation: 'Correlation ID',
    pageSize: 'Eventos por página',
    apply: 'Consultar',
    clear: 'Limpiar',
    refresh: 'Actualizar',
    loading: 'Cargando auditoría…',
    empty: 'Ningún evento coincide con los filtros actuales.',
    unauthenticated: 'La sesión no está autenticada. Ingrese nuevamente para consultar la auditoría.',
    forbidden: 'Este usuario no tiene permiso SystemAdmin para consultar la auditoría.',
    invalidQuery: 'El servidor rechazó los filtros enviados.',
    unavailable: 'No fue posible acceder al servicio de auditoría.',
    server: 'El servicio de auditoría devolvió una respuesta inesperada.',
    timestamp: 'Fecha/hora',
    actor: 'Identidad',
    resource: 'Recurso',
    details: 'Metadatos',
    page: 'Página',
    previous: 'Anterior',
    next: 'Siguiente',
    diagnostics: 'Diagnósticos',
    diagnosticsSubtitle: 'Salud de almacenamiento, buffer y retención',
    persisted: 'Persistidos',
    appendFailures: 'Fallas de persistencia',
    queueDepth: 'Cola pendiente',
    forwarded: 'Enviados',
    bufferFailures: 'Fallas de envío',
    rejected: 'Rechazados por límite',
    droppedShutdown: 'Descartados al cerrar',
    retention: 'Retención',
    enabled: 'Activa',
    disabled: 'Desactivada',
    finite: 'Finita',
    indefinite: 'Indefinida',
    lastPersisted: 'Última persistencia',
    lastFailure: 'Última falla',
    lastRetention: 'Última retención',
    never: 'Nunca',
    noDetails: 'Sin metadatos',
    project: 'Proyecto',
    source: 'Origen'
  }
} as const;

function resolveLocale(): AuditLocale {
  const stored = window.localStorage.getItem(localeKey);
  if (stored === 'pt-BR' || stored === 'en' || stored === 'es') return stored;
  const browser = navigator.language.toLowerCase();
  if (browser.startsWith('es')) return 'es';
  if (browser.startsWith('en')) return 'en';
  return 'pt-BR';
}

function formatDateTime(value?: string | null, locale = 'pt-BR', fallback = '—') {
  if (!value) return fallback;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? fallback : parsed.toLocaleString(locale);
}

function accessErrorKind(error: unknown): AuditApiErrorKind {
  return error instanceof AuditApiError ? error.kind : 'unavailable';
}

export function AuditApp() {
  const locale = useMemo(resolveLocale, []);
  const t = messages[locale];
  const [draftFilters, setDraftFilters] = useState<AuditFilterState>({ ...defaultAuditFilters });
  const [activeFilters, setActiveFilters] = useState<AuditFilterState>({ ...defaultAuditFilters });
  const [events, setEvents] = useState<AuditEventView[]>([]);
  const [pageIndex, setPageIndex] = useState(0);
  const [pageCursors, setPageCursors] = useState<Array<string | null>>([null]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState<AuditApiErrorKind | null>(null);
  const [diagnostics, setDiagnostics] = useState<AuditDiagnostics | null>(null);
  const [diagnosticsLoading, setDiagnosticsLoading] = useState(true);
  const [diagnosticsError, setDiagnosticsError] = useState<AuditApiErrorKind | null>(null);

  const loadPage = useCallback(async (filters: AuditFilterState, cursor: string | null) => {
    setLoading(true);
    setListError(null);
    try {
      const result = await loadAuditPage(filters, cursor);
      setEvents(result.events);
      setNextCursor(result.nextCursor);
    } catch (error) {
      setEvents([]);
      setNextCursor(null);
      setListError(accessErrorKind(error));
    } finally {
      setLoading(false);
    }
  }, []);

  const refreshDiagnostics = useCallback(async () => {
    setDiagnosticsLoading(true);
    setDiagnosticsError(null);
    try {
      setDiagnostics(await loadAuditDiagnostics());
    } catch (error) {
      setDiagnostics(null);
      setDiagnosticsError(accessErrorKind(error));
    } finally {
      setDiagnosticsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadPage(defaultAuditFilters, null);
    void refreshDiagnostics();
  }, [loadPage, refreshDiagnostics]);

  const applyFilters = (event: React.FormEvent) => {
    event.preventDefault();
    const filters = { ...draftFilters };
    setActiveFilters(filters);
    setPageIndex(0);
    setPageCursors([null]);
    void loadPage(filters, null);
  };

  const clearFilters = () => {
    const filters = { ...defaultAuditFilters };
    setDraftFilters(filters);
    setActiveFilters(filters);
    setPageIndex(0);
    setPageCursors([null]);
    void loadPage(filters, null);
  };

  const previousPage = () => {
    if (pageIndex <= 0) return;
    const targetIndex = pageIndex - 1;
    const cursor = pageCursors[targetIndex] ?? null;
    setPageIndex(targetIndex);
    void loadPage(activeFilters, cursor);
  };

  const nextPage = () => {
    if (!nextCursor) return;
    const targetIndex = pageIndex + 1;
    setPageCursors(current => {
      const updated = current.slice(0, targetIndex);
      updated[targetIndex] = nextCursor;
      return updated;
    });
    setPageIndex(targetIndex);
    void loadPage(activeFilters, nextCursor);
  };

  const refresh = () => {
    void loadPage(activeFilters, pageCursors[pageIndex] ?? null);
    void refreshDiagnostics();
  };

  const errorText = (kind: AuditApiErrorKind | null) => {
    if (kind === 'unauthenticated') return t.unauthenticated;
    if (kind === 'forbidden') return t.forbidden;
    if (kind === 'invalid-query') return t.invalidQuery;
    if (kind === 'server') return t.server;
    return t.unavailable;
  };

  return (
    <main className="audit-shell">
      <header className="audit-header">
        <div>
          <span className="audit-kicker">EliteSCADA</span>
          <h1>{t.title}</h1>
          <p>{t.subtitle}</p>
        </div>
        <button type="button" className="audit-secondary" onClick={refresh} disabled={loading || diagnosticsLoading}>
          {t.refresh}
        </button>
      </header>

      <section className="audit-panel" aria-labelledby="audit-filter-title">
        <div className="audit-section-heading">
          <div><h2 id="audit-filter-title">{t.filters}</h2></div>
          <span>{t.page} {pageIndex + 1}</span>
        </div>
        <form className="audit-filter-grid" onSubmit={applyFilters}>
          <FilterField label={t.from}>
            <input type="datetime-local" value={draftFilters.fromLocal} onChange={event => setDraftFilters(current => ({ ...current, fromLocal: event.target.value }))} />
          </FilterField>
          <FilterField label={t.to}>
            <input type="datetime-local" value={draftFilters.toLocal} onChange={event => setDraftFilters(current => ({ ...current, toLocal: event.target.value }))} />
          </FilterField>
          <FilterField label={t.subject}>
            <input value={draftFilters.subjectId} onChange={event => setDraftFilters(current => ({ ...current, subjectId: event.target.value }))} />
          </FilterField>
          <FilterField label={t.action}>
            <input value={draftFilters.action} placeholder="command.execute" onChange={event => setDraftFilters(current => ({ ...current, action: event.target.value }))} />
          </FilterField>
          <FilterField label={t.outcome}>
            <select value={draftFilters.outcome} onChange={event => setDraftFilters(current => ({ ...current, outcome: event.target.value as AuditFilterState['outcome'] }))}>
              <option value="">{t.all}</option>
              <option value="Succeeded">{t.succeeded}</option>
              <option value="Denied">{t.denied}</option>
              <option value="Failed">{t.failed}</option>
            </select>
          </FilterField>
          <FilterField label={t.targetKind}>
            <input value={draftFilters.targetKind} placeholder="command" onChange={event => setDraftFilters(current => ({ ...current, targetKind: event.target.value }))} />
          </FilterField>
          <FilterField label={t.targetId}>
            <input value={draftFilters.targetId} onChange={event => setDraftFilters(current => ({ ...current, targetId: event.target.value }))} />
          </FilterField>
          <FilterField label={t.area}>
            <input value={draftFilters.area} onChange={event => setDraftFilters(current => ({ ...current, area: event.target.value }))} />
          </FilterField>
          <FilterField label={t.correlation}>
            <input value={draftFilters.correlationId} onChange={event => setDraftFilters(current => ({ ...current, correlationId: event.target.value }))} />
          </FilterField>
          <FilterField label={t.pageSize}>
            <input type="number" min={1} max={1000} value={draftFilters.pageSize} onChange={event => setDraftFilters(current => ({ ...current, pageSize: Number(event.target.value) }))} />
          </FilterField>
          <div className="audit-filter-actions">
            <button type="submit" className="audit-primary" disabled={loading}>{t.apply}</button>
            <button type="button" className="audit-secondary" onClick={clearFilters} disabled={loading}>{t.clear}</button>
          </div>
        </form>
      </section>

      <section className="audit-panel" aria-live="polite">
        {loading && <div className="audit-state">{t.loading}</div>}
        {!loading && listError && <div className={`audit-state audit-error audit-error-${listError}`} role="alert">{errorText(listError)}</div>}
        {!loading && !listError && events.length === 0 && <div className="audit-state">{t.empty}</div>}
        {!loading && !listError && events.length > 0 && (
          <div className="audit-table-wrap">
            <table className="audit-table">
              <thead>
                <tr>
                  <th>{t.timestamp}</th>
                  <th>{t.outcome}</th>
                  <th>{t.action}</th>
                  <th>{t.actor}</th>
                  <th>{t.resource}</th>
                  <th>{t.area}</th>
                  <th>{t.details}</th>
                </tr>
              </thead>
              <tbody>
                {events.map(event => <AuditEventRow key={event.id} event={event} locale={locale} labels={t} />)}
              </tbody>
            </table>
          </div>
        )}
        <div className="audit-pagination">
          <button type="button" className="audit-secondary" onClick={previousPage} disabled={loading || pageIndex === 0}>{t.previous}</button>
          <span>{t.page} {pageIndex + 1}</span>
          <button type="button" className="audit-secondary" onClick={nextPage} disabled={loading || !nextCursor}>{t.next}</button>
        </div>
      </section>

      <section className="audit-panel" aria-labelledby="audit-diagnostics-title">
        <div className="audit-section-heading">
          <div><h2 id="audit-diagnostics-title">{t.diagnostics}</h2><p>{t.diagnosticsSubtitle}</p></div>
        </div>
        {diagnosticsLoading && <div className="audit-state">{t.loading}</div>}
        {!diagnosticsLoading && diagnosticsError && <div className={`audit-state audit-error audit-error-${diagnosticsError}`} role="alert">{errorText(diagnosticsError)}</div>}
        {!diagnosticsLoading && diagnostics && (
          <div className="audit-diagnostics-grid">
            <DiagnosticCard title={t.persisted} value={diagnostics.store.persistedCount} detail={`${t.lastPersisted}: ${formatDateTime(diagnostics.store.lastPersistedAtUtc, locale, t.never)}`} />
            <DiagnosticCard title={t.appendFailures} value={diagnostics.store.appendFailureCount} detail={`${t.lastFailure}: ${formatDateTime(diagnostics.store.lastAppendFailureAtUtc, locale, t.never)}`} warning={diagnostics.store.appendFailureCount > 0} />
            <DiagnosticCard title={t.queueDepth} value={diagnostics.buffer.queueDepth} detail={`${t.forwarded}: ${diagnostics.buffer.successfullyForwardedCount}`} warning={diagnostics.buffer.queueDepth > 0} />
            <DiagnosticCard title={t.bufferFailures} value={diagnostics.buffer.forwardFailureCount} detail={`${t.rejected}: ${diagnostics.buffer.rejectedCount} · ${t.droppedShutdown}: ${diagnostics.buffer.droppedOnShutdownCount}`} warning={diagnostics.buffer.forwardFailureCount > 0 || diagnostics.buffer.rejectedCount > 0 || diagnostics.buffer.droppedOnShutdownCount > 0} />
            <DiagnosticCard title={t.retention} value={diagnostics.retention.enabled ? t.enabled : t.disabled} detail={`${diagnostics.retention.finiteRetentionActive ? t.finite : t.indefinite} · ${t.lastRetention}: ${formatDateTime(diagnostics.store.lastRetentionRunAtUtc, locale, t.never)}`} />
          </div>
        )}
      </section>
    </main>
  );
}

function FilterField({ label, children }: { label: string; children: React.ReactNode }) {
  return <label className="audit-filter-field"><span>{label}</span>{children}</label>;
}

function AuditEventRow({
  event,
  locale,
  labels
}: {
  event: AuditEventView;
  locale: AuditLocale;
  labels: (typeof messages)[AuditLocale];
}) {
  const outcome = auditOutcomeLabel(event.outcome);
  const details = sortedAuditDetails(event.details);
  return (
    <tr>
      <td><time dateTime={event.timestampUtc}>{formatDateTime(event.timestampUtc, locale)}</time></td>
      <td><span className={`audit-outcome audit-outcome-${outcome}`}>{labels[outcome === 'unknown' ? 'failed' : outcome]}</span></td>
      <td><code>{event.action}</code></td>
      <td><strong>{event.displayName || event.subjectId}</strong>{event.displayName && <small>{event.subjectId}</small>}</td>
      <td><strong>{event.targetKind}</strong><small>{event.targetId}</small></td>
      <td>{event.area || '—'}</td>
      <td>
        <details className="audit-details">
          <summary>{details.length > 0 ? `${labels.details} (${details.length})` : labels.noDetails}</summary>
          {details.length > 0 && <dl>{details.map(([key, value]) => <React.Fragment key={key}><dt>{key}</dt><dd>{value}</dd></React.Fragment>)}</dl>}
          {(event.projectKey || event.source || event.correlationId) && (
            <dl className="audit-context">
              {event.projectKey && <><dt>{labels.project}</dt><dd>{event.projectKey}{event.revision ? ` · r${event.revision}` : ''}</dd></>}
              {event.source && <><dt>{labels.source}</dt><dd>{event.source}</dd></>}
              {event.correlationId && <><dt>Correlation ID</dt><dd>{event.correlationId}</dd></>}
            </dl>
          )}
        </details>
      </td>
    </tr>
  );
}

function DiagnosticCard({ title, value, detail, warning = false }: { title: string; value: React.ReactNode; detail: string; warning?: boolean }) {
  return (
    <article className={`audit-diagnostic-card${warning ? ' audit-diagnostic-warning' : ''}`}>
      <span>{title}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  );
}
