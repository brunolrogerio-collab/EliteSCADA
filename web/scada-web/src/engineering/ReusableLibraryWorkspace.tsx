import React, { useCallback, useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import type { EngineeringSnapshot } from './types';
import {
  associateReusableLibrary,
  disassociateReusableLibrary,
  exportReusableLibrary,
  incorporateReusableLibraryResource,
  loadReusableLibraries,
  loadReusableLibraryResources,
  triggerReusableLibraryDownload,
  type ReusableLibraryDescriptor,
  type ReusableLibraryResource
} from './reusableLibraryApi';
import './reusable-library-workspace.css';

const ORIGIN_PREFIX = 'elitescada.reusable.origin.';

type ExportCandidate = {
  kind: string;
  resourceId: string;
  name: string;
  sourceKey: string;
  metadata?: Record<string, string> | null;
};

type Copy = ReturnType<typeof libraryCopy>;

export function ReusableLibraryWorkspace({
  locale,
  snapshot,
  onReload
}: {
  locale: EngineeringLocale;
  snapshot: EngineeringSnapshot;
  onReload: () => Promise<void>;
}) {
  const copy = useMemo(() => libraryCopy(locale), [locale]);
  const [libraries, setLibraries] = useState<ReusableLibraryDescriptor[]>([]);
  const [selectedLibraryId, setSelectedLibraryId] = useState<string | null>(null);
  const [resources, setResources] = useState<ReusableLibraryResource[]>([]);
  const [search, setSearch] = useState('');
  const [busy, setBusy] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [exportName, setExportName] = useState('EliteSCADA Library');
  const [exportVersion, setExportVersion] = useState('1.0.0');
  const [selectedExport, setSelectedExport] = useState<Set<string>>(() => new Set());

  const exportCandidates = useMemo(() => collectExportCandidates(snapshot), [snapshot]);
  const provenance = useMemo(
    () => exportCandidates.filter(candidate => candidate.metadata?.[`${ORIGIN_PREFIX}libraryId`]),
    [exportCandidates]
  );

  const refresh = useCallback(async (preferredLibraryId?: string | null) => {
    setLoading(true);
    setError(null);
    try {
      const next = await loadReusableLibraries();
      setLibraries(next);
      const requested = preferredLibraryId ?? selectedLibraryId;
      const selected = next.find(item => item.libraryId === requested)?.libraryId ?? next[0]?.libraryId ?? null;
      setSelectedLibraryId(selected);
      if (!selected) {
        setResources([]);
      } else {
        const catalog = await loadReusableLibraryResources(selected);
        setResources(catalog.resources ?? []);
      }
    } catch (cause) {
      setError(errorText(cause));
    } finally {
      setLoading(false);
    }
  }, [selectedLibraryId]);

  useEffect(() => { void refresh(); }, []); // catalog is backend authority; initial projection only

  async function selectLibrary(libraryId: string) {
    setSelectedLibraryId(libraryId);
    setBusy('select');
    setError(null);
    setNotice(null);
    try {
      const catalog = await loadReusableLibraryResources(libraryId);
      setResources(catalog.resources ?? []);
    } catch (cause) {
      setError(errorText(cause));
      setResources([]);
    } finally {
      setBusy(null);
    }
  }

  async function associate(file: File | null) {
    if (!file) return;
    await perform('associate', async () => {
      const result = await associateReusableLibrary(file);
      if (result.workingChanged) throw new Error(copy.associationMutationError);
      await refresh(result.association.libraryId);
      setNotice(result.added ? copy.associated : copy.alreadyAssociated);
    });
  }

  async function disassociate() {
    if (!selectedLibraryId) return;
    await perform('disassociate', async () => {
      await disassociateReusableLibrary(selectedLibraryId);
      await refresh(null);
      setNotice(copy.disassociated);
    });
  }

  async function useResource(resource: ReusableLibraryResource) {
    if (!selectedLibraryId) return;
    await perform(`use:${resource.kind}:${resource.resourceId}`, async () => {
      const result = await incorporateReusableLibraryResource(
        selectedLibraryId,
        resource,
        snapshot.workspace.changeVersion
      );
      await onReload();
      setNotice(result.incorporated
        ? copy.incorporated(result.closureCount)
        : copy.deduplicated(result.closureCount));
    });
  }

  async function exportLibrary() {
    const selections = exportCandidates
      .filter(candidate => selectedExport.has(exportKey(candidate)))
      .map(candidate => ({ kind: candidate.kind, resourceId: candidate.resourceId }));
    if (!exportName.trim() || !exportVersion.trim() || selections.length === 0) return;

    await perform('export', async () => {
      const download = await exportReusableLibrary({
        libraryId: crypto.randomUUID(),
        name: exportName.trim(),
        version: exportVersion.trim(),
        resources: selections
      });
      triggerReusableLibraryDownload(download);
      setNotice(copy.exported(selections.length));
    });
  }

  async function perform(name: string, action: () => Promise<void>) {
    setBusy(name);
    setError(null);
    setNotice(null);
    try { await action(); } catch (cause) { setError(errorText(cause)); } finally { setBusy(null); }
  }

  const query = search.trim().toLocaleLowerCase(locale);
  const filteredResources = resources.filter(resource => !query || [
    resource.displayName,
    resource.sourceKey,
    resource.kind,
    resource.resourceId
  ].some(value => value.toLocaleLowerCase(locale).includes(query)));
  const selectedLibrary = libraries.find(item => item.libraryId === selectedLibraryId) ?? null;

  return (
    <div className="eng-section reusable-library-workspace" data-testid="reusable-library-workspace">
      <header className="eng-section-header">
        <div>
          <span className="eng-eyebrow">{copy.eyebrow}</span>
          <h1>{copy.title}</h1>
          <p>{copy.description}</p>
        </div>
        <div className="eng-section-meta">
          <strong>{libraries.length} {copy.libraries}</strong>
          <span>{copy.working}: v{snapshot.workspace.changeVersion}</span>
        </div>
      </header>

      {error && <p className="reusable-library-workspace__error" role="alert">{error}</p>}
      {notice && <p className="reusable-library-workspace__notice" role="status">{notice}</p>}

      <section className="eng-panel reusable-library-workspace__boundary" data-testid="reusable-library-boundary">
        <strong>{copy.boundaryTitle}</strong>
        <p>{copy.boundaryText}</p>
      </section>

      <div className="reusable-library-workspace__grid">
        <section className="eng-panel reusable-library-workspace__catalog">
          <div className="reusable-library-workspace__panel-header">
            <div><h2>{copy.catalog}</h2><p>{copy.catalogHint}</p></div>
            <button type="button" onClick={() => void refresh()} disabled={Boolean(busy) || loading}>{copy.refresh}</button>
          </div>

          <label className="reusable-library-workspace__file">
            <span>{copy.associate}</span>
            <input
              data-testid="reusable-library-associate-input"
              type="file"
              accept=".escadalib,application/vnd.elitescada.resource-library"
              disabled={Boolean(busy)}
              onChange={event => void associate(event.target.files?.[0] ?? null)}
            />
          </label>

          {loading && libraries.length === 0 ? <p>{copy.loading}</p> : null}
          {!loading && libraries.length === 0 ? <p className="reusable-library-workspace__empty">{copy.noLibraries}</p> : null}

          <div className="reusable-library-workspace__libraries">
            {libraries.map(library => (
              <button
                type="button"
                key={library.libraryId}
                className={library.libraryId === selectedLibraryId ? 'active' : ''}
                onClick={() => void selectLibrary(library.libraryId)}
                disabled={Boolean(busy)}
              >
                <strong>{library.name}</strong>
                <span>v{library.version} · {library.resourceCount} {copy.resources}</span>
              </button>
            ))}
          </div>

          {selectedLibrary && (
            <div className="reusable-library-workspace__selected">
              <code>{selectedLibrary.libraryId}</code>
              <small>SHA-256 {selectedLibrary.contentSha256.slice(0, 16)}…</small>
              <button data-testid="reusable-library-disassociate" type="button" onClick={() => void disassociate()} disabled={Boolean(busy)}>{copy.disassociate}</button>
            </div>
          )}
        </section>

        <section className="eng-panel reusable-library-workspace__resources">
          <div className="reusable-library-workspace__panel-header">
            <div><h2>{copy.availableResources}</h2><p>{copy.useHint}</p></div>
            <input
              aria-label={copy.search}
              placeholder={copy.search}
              value={search}
              onChange={event => setSearch(event.target.value)}
            />
          </div>

          {!selectedLibrary ? <p className="reusable-library-workspace__empty">{copy.selectLibrary}</p> : null}
          {selectedLibrary && filteredResources.length === 0 ? <p className="reusable-library-workspace__empty">{copy.noResources}</p> : null}
          <div className="reusable-library-workspace__resource-list">
            {filteredResources.map(resource => {
              const operation = `use:${resource.kind}:${resource.resourceId}`;
              return (
                <article key={`${resource.kind}:${resource.resourceId}`} data-testid="reusable-library-resource">
                  <div>
                    <span className="reusable-library-workspace__kind">{kindLabel(resource.kind, locale)}</span>
                    <strong>{resource.displayName}</strong>
                    <code>{resource.sourceKey}</code>
                  </div>
                  <div className="reusable-library-workspace__dependencies">
                    <span>{copy.dependencies}: {resource.dependencies.length}</span>
                    {resource.dependencies.map(dependency => (
                      <small key={`${dependency.kind}:${dependency.resourceId}`}>{kindLabel(dependency.kind, locale)} · {dependency.resourceId}</small>
                    ))}
                  </div>
                  <button
                    data-testid="reusable-library-use"
                    type="button"
                    disabled={Boolean(busy)}
                    onClick={() => void useResource(resource)}
                  >{busy === operation ? copy.using : copy.use}</button>
                </article>
              );
            })}
          </div>
        </section>
      </div>

      <section className="eng-panel reusable-library-workspace__export" data-testid="reusable-library-export">
        <div className="reusable-library-workspace__panel-header">
          <div><h2>{copy.createLibrary}</h2><p>{copy.createHint}</p></div>
          <button type="button" onClick={() => void exportLibrary()} disabled={Boolean(busy) || !exportName.trim() || !exportVersion.trim() || selectedExport.size === 0}>{copy.export}</button>
        </div>
        <div className="reusable-library-workspace__export-fields">
          <label><span>{copy.name}</span><input value={exportName} onChange={event => setExportName(event.target.value)} /></label>
          <label><span>{copy.version}</span><input value={exportVersion} onChange={event => setExportVersion(event.target.value)} /></label>
        </div>
        <div className="reusable-library-workspace__export-list">
          {exportCandidates.map(candidate => {
            const key = exportKey(candidate);
            return (
              <label key={key}>
                <input
                  type="checkbox"
                  checked={selectedExport.has(key)}
                  onChange={event => setSelectedExport(previous => {
                    const next = new Set(previous);
                    if (event.target.checked) next.add(key); else next.delete(key);
                    return next;
                  })}
                />
                <span><strong>{candidate.name}</strong><small>{kindLabel(candidate.kind, locale)} · {candidate.sourceKey}</small></span>
              </label>
            );
          })}
        </div>
      </section>

      <section className="eng-panel reusable-library-workspace__provenance" data-testid="reusable-library-provenance">
        <div className="reusable-library-workspace__panel-header"><div><h2>{copy.provenance}</h2><p>{copy.provenanceHint}</p></div></div>
        {provenance.length === 0 ? <p className="reusable-library-workspace__empty">{copy.noProvenance}</p> : (
          <div className="reusable-library-workspace__provenance-list">
            {provenance.map(candidate => (
              <article key={exportKey(candidate)}>
                <strong>{candidate.name}</strong>
                <span>{kindLabel(candidate.kind, locale)}</span>
                <code>{candidate.metadata?.[`${ORIGIN_PREFIX}libraryId`]}</code>
                <small>v{candidate.metadata?.[`${ORIGIN_PREFIX}libraryVersion`]} · {copy.informationalOnly}</small>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function collectExportCandidates(snapshot: EngineeringSnapshot): ExportCandidate[] {
  const result: ExportCandidate[] = [];
  const add = (kind: string, items: unknown, keyField: string) => {
    if (!Array.isArray(items)) return;
    for (const raw of items) {
      if (!raw || typeof raw !== 'object') continue;
      const item = raw as Record<string, unknown>;
      const id = typeof item.id === 'string' ? item.id : '';
      if (!id) continue;
      result.push({
        kind,
        resourceId: id,
        name: typeof item.name === 'string' ? item.name : id,
        sourceKey: typeof item[keyField] === 'string' ? String(item[keyField]) : id,
        metadata: isStringMap(item.metadata) ? item.metadata : null
      });
    }
  };

  const model = snapshot.package as Record<string, unknown>;
  add('equipment-template', model.templates, 'key');
  add('dynamo', model.dynamos, 'key');
  add('screen', model.screens, 'key');
  add('popup', model.popups, 'key');
  add('script', model.scripts, 'path');
  add('visual-asset', model.visualAssets, 'key');
  return result.sort((left, right) => left.kind.localeCompare(right.kind) || left.name.localeCompare(right.name));
}

function isStringMap(value: unknown): value is Record<string, string> {
  return Boolean(value) && typeof value === 'object' && Object.values(value as Record<string, unknown>).every(item => typeof item === 'string');
}

function exportKey(candidate: Pick<ExportCandidate, 'kind' | 'resourceId'>) {
  return `${candidate.kind}:${candidate.resourceId}`;
}

function kindLabel(kind: string, locale: EngineeringLocale) {
  const labels: Record<string, Record<EngineeringLocale, string>> = {
    'equipment-template': { 'pt-BR': 'Template', en: 'Template', es: 'Template' },
    dynamo: { 'pt-BR': 'Dínamo', en: 'Dynamo', es: 'Dínamo' },
    screen: { 'pt-BR': 'Tela', en: 'Screen', es: 'Pantalla' },
    popup: { 'pt-BR': 'Popup', en: 'Popup', es: 'Popup' },
    script: { 'pt-BR': 'Script', en: 'Script', es: 'Script' },
    'visual-asset': { 'pt-BR': 'Imagem/Vetor', en: 'Image/Vector', es: 'Imagen/Vector' }
  };
  return labels[kind]?.[locale] ?? kind;
}

function errorText(cause: unknown) {
  return cause instanceof Error ? cause.message : String(cause);
}

function libraryCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Engineering reuse', title: 'Reusable Libraries', description: 'Associate .escadalib catalogs without importing them. Only resources explicitly used in this project are incorporated into Working.', libraries: 'libraries', resources: 'resources', working: 'Working', boundaryTitle: 'Association is not import', boundaryText: 'Associated libraries are Engineering-time catalogs only. Incorporated resources become project-owned canonical content; Runtime never resolves the library.', catalog: 'Associated libraries', catalogHint: 'The catalog belongs to the backend Engineering session/project scope and can disappear without breaking incorporated content.', refresh: 'Refresh', associate: 'Associate .escadalib', loading: 'Loading libraries…', noLibraries: 'No reusable library is associated.', disassociate: 'Disassociate', associated: 'Library associated without changing Working.', alreadyAssociated: 'This exact library is already associated.', disassociated: 'Library disassociated. Project-owned resources remain unchanged.', associationMutationError: 'Library association unexpectedly reported a Working mutation.', availableResources: 'Library resources', useHint: 'Use copies only the selected resource and its validated dependency closure into canonical Working.', search: 'Search resources', selectLibrary: 'Select an associated library.', noResources: 'No resources match this search.', dependencies: 'Dependencies', use: 'Use', using: 'Using…', incorporated: (count: number) => `Resource incorporated with ${count} item(s) in its validated closure.`, deduplicated: (count: number) => `The ${count} closure item(s) are already identical project-owned content; Working was not mutated.`, createLibrary: 'Create reusable library', createHint: 'Select canonical project resources. The backend validates portability and automatically includes supported transitive dependencies.', name: 'Library name', version: 'Version', export: 'Export .escadalib', exported: (count: number) => `.escadalib exported from ${count} selected canonical resource(s).`, provenance: 'Project provenance', provenanceHint: 'Origin is informational only. It never becomes a Runtime or external file dependency.', noProvenance: 'No current project resource carries reusable-library origin metadata.', informationalOnly: 'informational origin only'
  };
  if (locale === 'es') return {
    eyebrow: 'Reutilización de Ingeniería', title: 'Bibliotecas reutilizables', description: 'Asocie catálogos .escadalib sin importarlos. Solo los recursos usados explícitamente se incorporan al Working.', libraries: 'bibliotecas', resources: 'recursos', working: 'Working', boundaryTitle: 'Asociar no es importar', boundaryText: 'Las bibliotecas asociadas son catálogos de Ingeniería. Los recursos incorporados pasan a ser contenido canónico del proyecto; Runtime nunca resuelve la biblioteca.', catalog: 'Bibliotecas asociadas', catalogHint: 'El catálogo pertenece al scope de proyecto/sesión del backend y puede desaparecer sin romper contenido incorporado.', refresh: 'Actualizar', associate: 'Asociar .escadalib', loading: 'Cargando bibliotecas…', noLibraries: 'No hay bibliotecas reutilizables asociadas.', disassociate: 'Desasociar', associated: 'Biblioteca asociada sin cambiar Working.', alreadyAssociated: 'Esta biblioteca exacta ya está asociada.', disassociated: 'Biblioteca desasociada. Los recursos del proyecto permanecen sin cambios.', associationMutationError: 'La asociación informó inesperadamente una mutación de Working.', availableResources: 'Recursos de la biblioteca', useHint: 'Usar copia solamente el recurso seleccionado y su cierre de dependencias validado al Working canónico.', search: 'Buscar recursos', selectLibrary: 'Seleccione una biblioteca asociada.', noResources: 'Ningún recurso coincide con la búsqueda.', dependencies: 'Dependencias', use: 'Usar', using: 'Usando…', incorporated: (count: number) => `Recurso incorporado con ${count} elemento(s) en su cierre validado.`, deduplicated: (count: number) => `Los ${count} elemento(s) ya son contenido idéntico del proyecto; Working no cambió.`, createLibrary: 'Crear biblioteca reutilizable', createHint: 'Seleccione recursos canónicos. El backend valida portabilidad e incluye dependencias transitivas soportadas.', name: 'Nombre de la biblioteca', version: 'Versión', export: 'Exportar .escadalib', exported: (count: number) => `.escadalib exportado desde ${count} recurso(s) canónico(s) seleccionado(s).`, provenance: 'Procedencia del proyecto', provenanceHint: 'El origen es solo informativo. Nunca es dependencia de Runtime ni de archivo externo.', noProvenance: 'Ningún recurso actual tiene metadatos de origen de biblioteca reutilizable.', informationalOnly: 'origen solamente informativo'
  };
  return {
    eyebrow: 'Reuso de Engenharia', title: 'Bibliotecas reutilizáveis', description: 'Associe catálogos .escadalib sem importá-los. Somente os recursos usados explicitamente são incorporados ao Working.', libraries: 'bibliotecas', resources: 'recursos', working: 'Working', boundaryTitle: 'Associar não é importar', boundaryText: 'Bibliotecas associadas são apenas catálogos de Engenharia. Recursos incorporados tornam-se conteúdo canônico do projeto; o Runtime nunca resolve a biblioteca.', catalog: 'Bibliotecas associadas', catalogHint: 'O catálogo pertence ao escopo de projeto/sessão do backend e pode desaparecer sem quebrar conteúdo já incorporado.', refresh: 'Atualizar', associate: 'Associar .escadalib', loading: 'Carregando bibliotecas…', noLibraries: 'Nenhuma biblioteca reutilizável está associada.', disassociate: 'Desassociar', associated: 'Biblioteca associada sem alterar o Working.', alreadyAssociated: 'Esta biblioteca exata já está associada.', disassociated: 'Biblioteca desassociada. Os recursos pertencentes ao projeto permanecem inalterados.', associationMutationError: 'A associação informou inesperadamente uma mutação do Working.', availableResources: 'Recursos da biblioteca', useHint: 'Usar copia somente o recurso selecionado e seu closure de dependências validado para o Working canônico.', search: 'Buscar recursos', selectLibrary: 'Selecione uma biblioteca associada.', noResources: 'Nenhum recurso corresponde à busca.', dependencies: 'Dependências', use: 'Usar', using: 'Usando…', incorporated: (count: number) => `Recurso incorporado com ${count} item(ns) no closure validado.`, deduplicated: (count: number) => `Os ${count} item(ns) do closure já são conteúdo idêntico do projeto; o Working não foi alterado.`, createLibrary: 'Criar biblioteca reutilizável', createHint: 'Selecione recursos canônicos do projeto. O backend valida portabilidade e inclui automaticamente dependências transitivas suportadas.', name: 'Nome da biblioteca', version: 'Versão', export: 'Exportar .escadalib', exported: (count: number) => `.escadalib exportada a partir de ${count} recurso(s) canônico(s) selecionado(s).`, provenance: 'Proveniência no projeto', provenanceHint: 'A origem é apenas informativa. Nunca se torna dependência de Runtime ou de arquivo externo.', noProvenance: 'Nenhum recurso atual possui metadados de origem de biblioteca reutilizável.', informationalOnly: 'origem apenas informativa'
  };
}
