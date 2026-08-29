import React, { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from '../../i18n';
import { ProjectReferenceBrowser } from '../../project-reference/ProjectReferenceBrowser';
import type { ProjectReferenceDescriptor } from '../../project-reference/projectReferenceModel';
import type { VisualEditorBindingEditorContractProps } from '../visualEditorContracts';
import {
  compatibleBindingSources,
  createBindingRemoveIntent,
  createBindingSetIntent,
  findVisualBinding,
  isBindingSourceCompatible,
  listBindableVisualProperties,
  normalizeBindingSourceCatalog,
  type BindableVisualProperty
} from './bindingEditorModel';

export type BindingEditorCopy = Readonly<{
  title: string;
  destination: string;
  source: string;
  apply: string;
  remove: string;
  noDestinations: string;
  noSources: string;
  current: string;
  browse: string;
  exactReference: string;
  exactReferencePlaceholder: string;
  exactNotFound: string;
}>;

export type BindingEditorProps = VisualEditorBindingEditorContractProps & Readonly<{
  copy?: Partial<BindingEditorCopy>;
  locale?: EngineeringLocale;
}>;

type Computed<T> = Readonly<{
  value: T;
  error: string | null;
}>;

const DEFAULT_COPY: BindingEditorCopy = {
  title: 'Binding',
  destination: 'Visual property',
  source: 'Source',
  apply: 'Apply binding',
  remove: 'Remove binding',
  noDestinations: 'This object has no bindable visual properties.',
  noSources: 'No compatible canonical binding sources are available for this property.',
  current: 'Current binding',
  browse: 'Browse project references',
  exactReference: 'Exact reference',
  exactReferencePlaceholder: 'Type the exact canonical path/reference',
  exactNotFound: 'No compatible source matches this exact reference.'
};

export function BindingEditor({
  element,
  sourceCatalog,
  onMutationIntent,
  copy,
  locale = 'pt-BR'
}: BindingEditorProps) {
  const text: BindingEditorCopy = { ...DEFAULT_COPY, ...copy };
  const [actionError, setActionError] = useState<string | null>(null);
  const [browserOpen, setBrowserOpen] = useState(false);
  const [exactReference, setExactReference] = useState('');

  const destinationResult = useMemo(() => computeDestinations(element.type), [element.type]);
  const destinations = destinationResult.value;

  const firstBoundDestination = destinations.find(item =>
    element.bindings?.some(binding => binding.key === item.key)
  )?.key;
  const [propertyKey, setPropertyKey] = useState(firstBoundDestination ?? destinations[0]?.key ?? '');

  useEffect(() => {
    if (destinations.some(item => item.key === propertyKey)) return;
    setPropertyKey(firstBoundDestination ?? destinations[0]?.key ?? '');
  }, [destinations, firstBoundDestination, propertyKey]);

  const selectedDestination = destinations.find(item => item.key === propertyKey);
  const sourceResult = useMemo(
    () => computeSources(sourceCatalog, selectedDestination),
    [sourceCatalog, selectedDestination]
  );
  const sources = sourceResult.value;

  const existing = propertyKey ? findVisualBinding(element, propertyKey) : undefined;
  const existingSourceKey = existing ? sourceIdentity(existing.kind, existing.target) : undefined;
  const [sourceKey, setSourceKey] = useState(
    existingSourceKey && sources.some(item => sourceIdentity(item.kind, item.target) === existingSourceKey)
      ? existingSourceKey
      : sources[0] ? sourceIdentity(sources[0].kind, sources[0].target) : ''
  );

  useEffect(() => {
    const current = propertyKey ? findVisualBinding(element, propertyKey) : undefined;
    const currentKey = current ? sourceIdentity(current.kind, current.target) : '';
    if (current && currentKey && sources.some(item => sourceIdentity(item.kind, item.target) === currentKey)) {
      setSourceKey(currentKey);
      setExactReference(current.target);
      return;
    }
    if (!sources.some(item => sourceIdentity(item.kind, item.target) === sourceKey)) {
      const first = sources[0];
      setSourceKey(first ? sourceIdentity(first.kind, first.target) : '');
      setExactReference(first?.target ?? '');
    }
  }, [element, propertyKey, sourceKey, sources]);

  function apply() {
    setActionError(null);
    const source = sources.find(item => sourceIdentity(item.kind, item.target) === sourceKey);
    if (!source || !propertyKey) return;
    try {
      onMutationIntent(createBindingSetIntent(element, propertyKey, source, existing?.direction));
    } catch (cause) {
      setActionError(errorText(cause));
    }
  }

  function remove() {
    setActionError(null);
    if (!propertyKey) return;
    try {
      onMutationIntent(createBindingRemoveIntent(element, propertyKey));
    } catch (cause) {
      setActionError(errorText(cause));
    }
  }

  function resolveExactReference() {
    setActionError(null);
    const target = exactReference.trim();
    if (!target || !selectedDestination) return;
    const matches = sources.filter(item => item.target === target);
    if (matches.length !== 1) {
      setActionError(text.exactNotFound);
      return;
    }
    setSourceKey(sourceIdentity(matches[0].kind, matches[0].target));
  }

  const browserReferences = useMemo(
    () => normalizeBindingSourceCatalog(sourceCatalog).map(toProjectReference),
    [sourceCatalog]
  );
  const selectedSource = sources.find(item => sourceIdentity(item.kind, item.target) === sourceKey);
  const error = actionError ?? destinationResult.error ?? sourceResult.error;

  return (
    <section className="visual-binding-editor" aria-label={text.title} data-testid="visual-binding-editor">
      <header><strong>{text.title}</strong></header>

      {destinations.length === 0 ? (
        <p>{text.noDestinations}</p>
      ) : (
        <>
          <label>
            <span>{text.destination}</span>
            <select
              aria-label={text.destination}
              value={propertyKey}
              onChange={event => {
                setActionError(null);
                setPropertyKey(event.target.value);
              }}
            >
              {destinations.map(destination => (
                <option key={destination.key} value={destination.key}>
                  {destination.key} · {destination.type}
                </option>
              ))}
            </select>
          </label>

          <label>
            <span>{text.source}</span>
            <select
              aria-label={text.source}
              value={sourceKey}
              disabled={sources.length === 0}
              onChange={event => {
                setActionError(null);
                setSourceKey(event.target.value);
                const selected = sources.find(item => sourceIdentity(item.kind, item.target) === event.target.value);
                setExactReference(selected?.target ?? '');
              }}
            >
              {sources.length === 0 ? (
                <option value="">{text.noSources}</option>
              ) : sources.map(source => (
                <option key={sourceIdentity(source.kind, source.target)} value={sourceIdentity(source.kind, source.target)}>
                  {source.label} · {source.kind} · {source.target}
                </option>
              ))}
            </select>
          </label>

          <label className="visual-binding-editor__exact-reference">
            <span>{text.exactReference}</span>
            <div>
              <input
                value={exactReference}
                placeholder={text.exactReferencePlaceholder}
                onChange={event => setExactReference(event.currentTarget.value)}
                onKeyDown={event => { if (event.key === 'Enter') resolveExactReference(); }}
              />
              <button type="button" onClick={resolveExactReference}>OK</button>
            </div>
          </label>

          <button type="button" onClick={() => setBrowserOpen(open => !open)} aria-expanded={browserOpen}>
            {text.browse}
          </button>

          {browserOpen && selectedDestination ? (
            <ProjectReferenceBrowser
              references={browserReferences}
              locale={locale}
              selectedReference={selectedSource?.target ?? null}
              isSelectable={reference => isBrowserReferenceCompatible(selectedDestination, reference, sources)}
              onSelect={reference => {
                const source = sources.find(item => item.target === reference.reference);
                if (!source) return;
                setSourceKey(sourceIdentity(source.kind, source.target));
                setExactReference(source.target);
                setBrowserOpen(false);
                setActionError(null);
              }}
              title={text.browse}
            />
          ) : null}

          {existing && (
            <p data-testid="visual-binding-current">
              <strong>{text.current}:</strong> {existing.kind} · {existing.target}
              {existing.metadata?.presentationMode === 'scalar-text' ? ' · scalar text' : ''}
            </p>
          )}

          <div className="visual-binding-editor__actions">
            <button type="button" onClick={apply} disabled={!propertyKey || !sourceKey}>{text.apply}</button>
            <button type="button" onClick={remove} disabled={!existing}>{text.remove}</button>
          </div>
        </>
      )}

      {error && <div role="alert">{error}</div>}
    </section>
  );
}

function computeDestinations(objectType: string): Computed<readonly BindableVisualProperty[]> {
  try {
    return { value: listBindableVisualProperties({ type: objectType }), error: null };
  } catch (cause) {
    return { value: [], error: errorText(cause) };
  }
}

function computeSources(
  sourceCatalog: VisualEditorBindingEditorContractProps['sourceCatalog'],
  destination: BindableVisualProperty | undefined
): Computed<readonly VisualEditorBindingEditorContractProps['sourceCatalog'][number][]> {
  if (!destination) return { value: [], error: null };
  try {
    return { value: compatibleBindingSources(destination, sourceCatalog), error: null };
  } catch (cause) {
    return { value: [], error: errorText(cause) };
  }
}

function toProjectReference(source: VisualEditorBindingEditorContractProps['sourceCatalog'][number]): ProjectReferenceDescriptor {
  return Object.freeze({
    reference: source.target,
    label: source.label,
    family: source.family ?? inferFamily(source.kind),
    dataType: source.dataType ?? 'Unknown',
    engineeringUnit: source.engineeringUnit ?? null,
    writable: source.writable,
    bindingKind: source.kind === 'ClientMemory' ? 'ClientMemory' : source.kind === 'Tag' ? 'Tag' : undefined,
    pathSegments: Object.freeze(source.target.split(/[/.\\]+/g).filter(Boolean))
  });
}

function isBrowserReferenceCompatible(
  destination: BindableVisualProperty,
  reference: ProjectReferenceDescriptor,
  sources: readonly VisualEditorBindingEditorContractProps['sourceCatalog'][number][]
): boolean {
  const source = sources.find(item => item.target === reference.reference);
  return source ? isBindingSourceCompatible(destination, source) : false;
}

function inferFamily(kind: string): ProjectReferenceDescriptor['family'] {
  return kind === 'ClientMemory' ? 'clientMemory' : 'tag';
}

function sourceIdentity(kind: string, target: string): string {
  return `${kind.trim().toLowerCase()}\u0000${target}`;
}

function errorText(cause: unknown): string {
  return cause instanceof Error ? cause.message : String(cause);
}
