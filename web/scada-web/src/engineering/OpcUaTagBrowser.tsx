import React, { useEffect, useMemo, useState } from 'react';
import { loadDataSourceTypeCatalog } from './DataSourceCatalogEditor';
import type { DataSourceTypeDefinition } from './DataSourceCatalogEditor.logic';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage
} from './api';
import {
  browseEngineeringDataSource,
  discoverEngineeringDataSource,
  testEngineeringDataSourceConnection,
  type DriverBrowseNodeView,
  type DriverBrowsePageView,
  type DriverConnectionTestResultView,
  type DriverDiscoveryCandidateView
} from './driverEngineeringApi';
import type { EngineeringLocale } from './i18n';
import type { DataSourceEngineering, EngineeringPackageView } from './types';
import type {
  CommunicationTagBindingEngineering,
  TagSourceAwareEngineering
} from './TagSourceSelector.logic';

type Props = {
  tag: TagSourceAwareEngineering;
  source: DataSourceEngineering;
  model: EngineeringPackageView;
  locale: EngineeringLocale;
  onChange: (tag: TagSourceAwareEngineering) => void;
};

type BrowseLocation = Readonly<{
  parentNodeId: string | null;
  label: string;
}>;

export function OpcUaTagBrowser({ tag, source, model, locale, onChange }: Props) {
  const text = useMemo(() => copy(locale), [locale]);
  const [schema, setSchema] = useState<DataSourceTypeDefinition | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [connection, setConnection] = useState<DriverConnectionTestResultView | null>(null);
  const [discovery, setDiscovery] = useState<DriverDiscoveryCandidateView[]>([]);
  const [nodes, setNodes] = useState<DriverBrowseNodeView[]>([]);
  const [continuationToken, setContinuationToken] = useState<string | null>(null);
  const [location, setLocation] = useState<BrowseLocation>({ parentNodeId: null, label: text.objects });
  const [history, setHistory] = useState<BrowseLocation[]>([]);
  const [selected, setSelected] = useState<Record<string, DriverBrowseNodeView>>({});
  const [query, setQuery] = useState('');
  const [pathPrefix, setPathPrefix] = useState(() => `${sanitizeSegment(source.key)}.OPCUA`);
  const [busy, setBusy] = useState<'test' | 'discover' | 'browse' | 'preview' | 'apply' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [bulkPreview, setBulkPreview] = useState<Awaited<ReturnType<typeof previewEngineeringPackage>> | null>(null);
  const [bulkCandidate, setBulkCandidate] = useState<EngineeringPackageView | null>(null);
  const [bulkChangeVersion, setBulkChangeVersion] = useState<number | null>(null);

  useEffect(() => {
    let alive = true;
    void loadDataSourceTypeCatalog()
      .then(types => {
        if (!alive) return;
        const definition = types.find(type => type.typeKey.toLowerCase() === source.driver.toLowerCase()) ?? null;
        setSchema(definition);
        setCatalogError(definition ? null : text.schemaMissing);
      })
      .catch(reason => {
        if (alive) setCatalogError(reason instanceof Error ? reason.message : String(reason));
      });
    return () => { alive = false; };
  }, [source.driver, text.schemaMissing]);

  useEffect(() => {
    setConnection(null);
    setDiscovery([]);
    setNodes([]);
    setContinuationToken(null);
    setLocation({ parentNodeId: null, label: text.objects });
    setHistory([]);
    setSelected({});
    setBulkPreview(null);
    setBulkCandidate(null);
    setBulkChangeVersion(null);
    setError(null);
    setPathPrefix(`${sanitizeSegment(source.key)}.OPCUA`);
  }, [source.id, source.key, text.objects]);

  if (!source.id) {
    return <small role="alert">{text.stableIdRequired}</small>;
  }

  const visibleNodes = nodes.filter(node => {
    const needle = query.trim().toLowerCase();
    if (!needle) return true;
    return `${node.displayName} ${node.nodeId} ${node.portableAddress ?? ''} ${node.stableIdentity}`
      .toLowerCase()
      .includes(needle);
  });
  const selectedNodes = Object.values(selected);

  const runTest = async () => {
    setBusy('test');
    setError(null);
    try {
      setConnection(await testEngineeringDataSourceConnection(source.id!));
    } catch (reason) {
      setConnection(null);
      setError(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  const runDiscover = async () => {
    setBusy('discover');
    setError(null);
    try {
      setDiscovery(await discoverEngineeringDataSource(source.id!, { maximumResults: 100 }));
    } catch (reason) {
      setDiscovery([]);
      setError(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  const browse = async (nextLocation: BrowseLocation, append = false, token?: string | null) => {
    setBusy('browse');
    setError(null);
    try {
      const page = await browseEngineeringDataSource(source.id!, {
        parentNodeId: nextLocation.parentNodeId,
        continuationToken: token ?? null,
        pageSize: 200
      });
      setLocation(nextLocation);
      setNodes(current => append ? mergeNodes(current, page) : [...page.nodes]);
      setContinuationToken(page.continuationToken ?? null);
    } catch (reason) {
      setError(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  const openContainer = (node: DriverBrowseNodeView) => {
    const parentNodeId = node.portableAddress ?? node.nodeId;
    setHistory(current => [...current, location]);
    void browse({ parentNodeId, label: node.displayName });
  };

  const goBack = () => {
    const previous = history.at(-1);
    if (!previous) return;
    setHistory(current => current.slice(0, -1));
    void browse(previous);
  };

  const toggleNode = (node: DriverBrowseNodeView) => {
    if (node.isContainer || !node.portableAddress) return;
    setSelected(current => {
      const next = { ...current };
      if (next[node.stableIdentity]) delete next[node.stableIdentity];
      else next[node.stableIdentity] = node;
      return next;
    });
    invalidateBulk();
  };

  const useNode = (node: DriverBrowseNodeView) => {
    if (!node.portableAddress) return;
    const binding = buildBinding(schema, node.portableAddress, tag.communicationBinding);
    if (!binding) {
      setError(text.schemaMissing);
      return;
    }
    onChange({
      ...tag,
      address: node.portableAddress,
      communicationBinding: binding,
      dataType: normalizeDataType(node.suggestedDataType, tag.dataType),
      engineeringUnit: node.engineeringUnit ?? tag.engineeringUnit ?? null,
      readOnly: !node.isWritable
    });
  };

  const previewBulk = async () => {
    if (selectedNodes.length === 0) return;
    setBusy('preview');
    setError(null);
    try {
      const candidate = buildBulkCandidate(model, source, schema, selectedNodes, pathPrefix);
      const workspace = await loadEngineeringWorkspace();
      const preview = await previewEngineeringPackage(candidate);
      setBulkCandidate(candidate);
      setBulkChangeVersion(workspace.changeVersion);
      setBulkPreview(preview);
    } catch (reason) {
      setBulkCandidate(null);
      setBulkChangeVersion(null);
      setBulkPreview(null);
      setError(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  const applyBulk = async () => {
    if (!bulkCandidate || bulkChangeVersion == null || !bulkPreview?.canApply) return;
    if (!window.confirm(text.applyConfirm)) return;
    setBusy('apply');
    setError(null);
    try {
      await applyEngineeringPackage(bulkCandidate, bulkChangeVersion);
      window.location.reload();
    } catch (reason) {
      setBulkCandidate(null);
      setBulkChangeVersion(null);
      setBulkPreview(null);
      setError(asMessage(reason));
    } finally {
      setBusy(null);
    }
  };

  const invalidateBulk = () => {
    setBulkPreview(null);
    setBulkCandidate(null);
    setBulkChangeVersion(null);
  };

  return (
    <section className="eng-dictionary-editor eng-editor-field-wide" data-testid="opcua-tag-browser">
      <header>
        <strong>{text.title}</strong>
        <span>{text.help}</span>
      </header>

      <div className="eng-editor-actions">
        <button type="button" className="secondary" disabled={busy !== null} onClick={() => void runTest()} data-testid="opcua-test-connection">
          {busy === 'test' ? text.testing : text.test}
        </button>
        <button type="button" className="secondary" disabled={busy !== null} onClick={() => void runDiscover()} data-testid="opcua-discover">
          {busy === 'discover' ? text.discovering : text.discover}
        </button>
        <button type="button" className="secondary" disabled={busy !== null} onClick={() => void browse({ parentNodeId: null, label: text.objects })} data-testid="opcua-browse-root">
          {busy === 'browse' ? text.browsing : text.browse}
        </button>
      </div>

      {connection && (
        <div className="eng-mutation-detail" data-testid="opcua-connection-result">
          <strong>{connection.succeeded ? text.connectionOk : text.connectionFailed}</strong>
          {connection.sanitizedEndpoint && <code>{connection.sanitizedEndpoint}</code>}
          {connection.observedIdentity && <span>{connection.observedIdentity}</span>}
        </div>
      )}

      {discovery.length > 0 && (
        <div className="eng-bulk-entities" data-testid="opcua-discovery-results">
          {discovery.map(candidate => (
            <div key={candidate.candidateId}>
              <strong>{candidate.displayName}</strong>
              <code>{candidate.sanitizedEndpoint ?? candidate.stableIdentity}</code>
            </div>
          ))}
        </div>
      )}

      {(nodes.length > 0 || history.length > 0) && (
        <>
          <div className="eng-editor-actions">
            <button type="button" className="secondary" disabled={history.length === 0 || busy !== null} onClick={goBack}>{text.back}</button>
            <strong>{location.label}</strong>
            <span>{nodes.length} {text.nodes}</span>
          </div>
          <label className="eng-editor-field eng-editor-field-wide">
            <span>{text.search}</span>
            <input value={query} onChange={event => setQuery(event.target.value)} placeholder={text.searchPlaceholder} data-testid="opcua-browse-search" />
          </label>
          <div className="eng-bulk-entities" data-testid="opcua-browse-results">
            {visibleNodes.map(node => (
              <div key={node.stableIdentity}>
                {node.isContainer ? (
                  <button type="button" className="secondary" disabled={busy !== null} onClick={() => openContainer(node)}>
                    {text.open} {node.displayName}
                  </button>
                ) : (
                  <label>
                    <input
                      type="checkbox"
                      checked={Boolean(selected[node.stableIdentity])}
                      disabled={!node.portableAddress}
                      onChange={() => toggleNode(node)}
                    />
                    <span>
                      <strong>{node.displayName}</strong>
                      <code>{node.portableAddress ?? node.nodeId}</code>
                      <small>{formatNodeMeta(node, text)}</small>
                    </span>
                    <button type="button" className="secondary" disabled={!node.portableAddress} onClick={() => useNode(node)}>{text.useCurrent}</button>
                  </label>
                )}
              </div>
            ))}
          </div>
          {continuationToken && (
            <div className="eng-editor-actions">
              <button
                type="button"
                className="secondary"
                disabled={busy !== null}
                onClick={() => void browse(location, true, continuationToken)}
                data-testid="opcua-load-more"
              >{text.loadMore}</button>
            </div>
          )}
        </>
      )}

      {selectedNodes.length > 0 && (
        <section className="eng-mutation-card" data-testid="opcua-bulk-import">
          <header>
            <strong>{text.bulkTitle}</strong>
            <span>{selectedNodes.length} {text.selected}</span>
          </header>
          <label className="eng-editor-field">
            <span>{text.pathPrefix}</span>
            <input
              className="mono"
              value={pathPrefix}
              onChange={event => { setPathPrefix(event.target.value); invalidateBulk(); }}
              data-testid="opcua-import-prefix"
            />
          </label>
          <div className="eng-editor-actions">
            <button type="button" className="secondary" disabled={busy !== null} onClick={() => void previewBulk()} data-testid="opcua-import-preview">
              {busy === 'preview' ? text.previewing : text.preview}
            </button>
            <button type="button" disabled={busy !== null || !bulkPreview?.canApply} onClick={() => void applyBulk()} data-testid="opcua-import-apply">
              {busy === 'apply' ? text.applying : text.apply}
            </button>
          </div>
          {bulkPreview && (
            <small data-testid="opcua-import-preview-result">
              {text.previewResult}: {bulkPreview.createCount} {text.create}, {bulkPreview.updateCount} {text.update}, {bulkPreview.errorCount} {text.errors}.
            </small>
          )}
        </section>
      )}

      {catalogError && <pre className="eng-preview-error" role="alert">{catalogError}</pre>}
      {error && <pre className="eng-preview-error" role="alert">{error}</pre>}
    </section>
  );
}

function buildBinding(
  type: DataSourceTypeDefinition | null,
  portableAddress: string,
  current?: CommunicationTagBindingEngineering | null
): CommunicationTagBindingEngineering | null {
  const schema = type?.configurationSchema;
  if (!schema) return null;

  const settings: Record<string, string> = {};
  const settingKeys = new Set(['samplinginterval', 'queuesize', 'discardoldest']);
  for (const field of schema.tagBindingFields) {
    if (!settingKeys.has(field.key.toLowerCase())) continue;
    const existing = current?.schemaId === schema.schemaId
      ? current.settings?.[field.key]
      : undefined;
    const value = existing ?? field.defaultValue;
    if (value != null && value !== '') settings[field.key] = value;
  }

  return {
    contractVersion: 1,
    schemaId: schema.schemaId,
    schemaVersion: schema.schemaVersion,
    portableAddress,
    settings
  };
}

function buildBulkCandidate(
  model: EngineeringPackageView,
  source: DataSourceEngineering,
  type: DataSourceTypeDefinition | null,
  nodes: readonly DriverBrowseNodeView[],
  prefix: string
): EngineeringPackageView {
  if (!source.id) throw new Error('OPC UA bulk import requires a stable Data Source Id.');
  const schema = type?.configurationSchema;
  if (!schema) throw new Error('OPC UA Driver binding schema is unavailable.');

  const normalizedPrefix = normalizePathPrefix(prefix);
  const candidate = JSON.parse(JSON.stringify(model)) as EngineeringPackageView;
  const usedPaths = new Set(candidate.tags.map(tag => tag.path.toLowerCase()));
  const newTags: TagSourceAwareEngineering[] = [];

  nodes.forEach((node, index) => {
    if (!node.portableAddress || node.isContainer) return;
    const baseName = sanitizeSegment(node.displayName) || `Node${index + 1}`;
    const path = uniquePath(`${normalizedPrefix}.${baseName}`, usedPaths);
    usedPaths.add(path.toLowerCase());
    const binding = buildBinding(type, node.portableAddress);
    if (!binding) throw new Error('OPC UA Driver binding schema is unavailable.');

    newTags.push({
      name: baseName,
      path,
      dataType: normalizeDataType(node.suggestedDataType, 'string'),
      source: source.key,
      dataSourceId: source.id,
      address: node.portableAddress,
      engineeringUnit: node.engineeringUnit ?? null,
      description: node.metadata?.['opcUa.description'] ?? null,
      readOnly: !node.isWritable,
      metadata: {
        ...(node.metadata ?? {}),
        'opcUa.stableIdentity': node.stableIdentity
      },
      communicationBinding: binding
    });
  });

  if (newTags.length === 0) throw new Error('Select at least one browse variable with a portable address.');
  candidate.tags = [...candidate.tags, ...newTags];
  return candidate;
}

function normalizeDataType(value: string | number | null | undefined, fallback: string): string {
  const byNumber = ['boolean', 'int16', 'int32', 'int64', 'float', 'double', 'string', 'dateTime', 'enum'];
  if (typeof value === 'number') return byNumber[value] ?? fallback;
  if (typeof value !== 'string' || !value.trim()) return fallback;
  const normalized = value.trim().toLowerCase();
  const map: Record<string, string> = {
    boolean: 'boolean', int16: 'int16', int32: 'int32', int64: 'int64',
    float: 'float', double: 'double', string: 'string', datetime: 'dateTime', enum: 'enum'
  };
  return map[normalized] ?? fallback;
}

function mergeNodes(current: readonly DriverBrowseNodeView[], page: DriverBrowsePageView): DriverBrowseNodeView[] {
  const map = new Map(current.map(node => [node.stableIdentity, node]));
  for (const node of page.nodes) map.set(node.stableIdentity, node);
  return [...map.values()];
}

function uniquePath(base: string, used: ReadonlySet<string>): string {
  if (!used.has(base.toLowerCase())) return base;
  for (let index = 2; index < 10000; index++) {
    const candidate = `${base}_${index}`;
    if (!used.has(candidate.toLowerCase())) return candidate;
  }
  throw new Error(`Could not create a unique TAG path for '${base}'.`);
}

function normalizePathPrefix(value: string): string {
  const segments = value.split('.').map(sanitizeSegment).filter(Boolean);
  if (segments.length === 0) throw new Error('TAG path prefix is required.');
  return segments.join('.');
}

function sanitizeSegment(value: string): string {
  return value.trim().replace(/[^A-Za-z0-9_]+/g, '_').replace(/^_+|_+$/g, '') || 'Node';
}

function formatNodeMeta(node: DriverBrowseNodeView, text: ReturnType<typeof copy>): string {
  const access = node.isWritable ? text.readWrite : node.isReadable ? text.readOnly : text.noAccess;
  const type = node.suggestedDataType == null ? text.unknownType : String(node.suggestedDataType);
  return `${type} · ${access}${node.engineeringUnit ? ` · ${node.engineeringUnit}` : ''}`;
}

function asMessage(reason: unknown): string {
  return reason instanceof Error ? reason.message : String(reason);
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'OPC UA Engineering tools', help: 'Test the configured source, discover endpoints and browse nodes without changing Runtime. Selected nodes become TAGs only through Preview/Apply.',
    test: 'Test connection', testing: 'Testing…', discover: 'Discover endpoints', discovering: 'Discovering…', browse: 'Browse Objects', browsing: 'Browsing…',
    connectionOk: 'Connection test succeeded', connectionFailed: 'Connection test failed', objects: 'Objects', back: 'Back', nodes: 'nodes', search: 'Search loaded nodes',
    searchPlaceholder: 'Name, NodeId or portable address', open: 'Open', useCurrent: 'Use for current TAG', loadMore: 'Load more', bulkTitle: 'Create TAGs from selected nodes',
    selected: 'selected', pathPrefix: 'TAG path prefix', preview: 'Preview import', previewing: 'Previewing…', apply: 'Apply import', applying: 'Applying…',
    previewResult: 'Preview', create: 'create', update: 'update', errors: 'errors', applyConfirm: 'Apply the previewed OPC UA TAG import to the Engineering workspace?',
    stableIdRequired: 'Save/Apply this Data Source first so it has a stable Id before using OPC UA Engineering tools.', schemaMissing: 'The backend-authoritative OPC UA binding schema is unavailable.',
    readWrite: 'read/write', readOnly: 'read-only', noAccess: 'no read access', unknownType: 'unknown type'
  };
  if (locale === 'es') return {
    title: 'Herramientas OPC UA de Engineering', help: 'Prueba la fuente configurada, descubre endpoints y navega nodos sin cambiar Runtime. Los nodos seleccionados se vuelven TAGs solo mediante Preview/Apply.',
    test: 'Probar conexión', testing: 'Probando…', discover: 'Descubrir endpoints', discovering: 'Descubriendo…', browse: 'Navegar Objects', browsing: 'Navegando…',
    connectionOk: 'Prueba de conexión correcta', connectionFailed: 'Prueba de conexión fallida', objects: 'Objects', back: 'Volver', nodes: 'nodos', search: 'Buscar nodos cargados',
    searchPlaceholder: 'Nombre, NodeId o dirección portátil', open: 'Abrir', useCurrent: 'Usar en el TAG actual', loadMore: 'Cargar más', bulkTitle: 'Crear TAGs desde nodos seleccionados',
    selected: 'seleccionados', pathPrefix: 'Prefijo de path de TAG', preview: 'Preview de importación', previewing: 'Validando…', apply: 'Aplicar importación', applying: 'Aplicando…',
    previewResult: 'Preview', create: 'crear', update: 'actualizar', errors: 'errores', applyConfirm: '¿Aplicar la importación OPC UA validada al workspace de Engineering?',
    stableIdRequired: 'Guarde/aplique primero este Data Source para obtener un Id estable antes de usar las herramientas OPC UA.', schemaMissing: 'El esquema OPC UA autoritativo del backend no está disponible.',
    readWrite: 'lectura/escritura', readOnly: 'solo lectura', noAccess: 'sin lectura', unknownType: 'tipo desconocido'
  };
  return {
    title: 'Ferramentas OPC UA de Engineering', help: 'Teste a fonte configurada, descubra endpoints e navegue pelos nós sem alterar o Runtime. Os nós selecionados só viram TAGs por Preview/Apply.',
    test: 'Testar conexão', testing: 'Testando…', discover: 'Descobrir endpoints', discovering: 'Descobrindo…', browse: 'Navegar Objects', browsing: 'Navegando…',
    connectionOk: 'Teste de conexão concluído', connectionFailed: 'Teste de conexão falhou', objects: 'Objects', back: 'Voltar', nodes: 'nós', search: 'Pesquisar nós carregados',
    searchPlaceholder: 'Nome, NodeId ou endereço portátil', open: 'Abrir', useCurrent: 'Usar no TAG atual', loadMore: 'Carregar mais', bulkTitle: 'Criar TAGs dos nós selecionados',
    selected: 'selecionados', pathPrefix: 'Prefixo do path dos TAGs', preview: 'Pré-visualizar importação', previewing: 'Validando…', apply: 'Aplicar importação', applying: 'Aplicando…',
    previewResult: 'Preview', create: 'criar', update: 'atualizar', errors: 'erros', applyConfirm: 'Aplicar a importação OPC UA validada ao workspace de Engineering?',
    stableIdRequired: 'Salve/aplique primeiro este Data Source para obter um Id estável antes de usar as ferramentas OPC UA.', schemaMissing: 'O schema OPC UA autoritativo do backend não está disponível.',
    readWrite: 'leitura/escrita', readOnly: 'somente leitura', noAccess: 'sem leitura', unknownType: 'tipo desconhecido'
  };
}
