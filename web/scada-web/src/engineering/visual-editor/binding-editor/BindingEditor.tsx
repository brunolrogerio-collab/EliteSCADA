import React, { useEffect, useMemo, useState } from 'react';
import type { VisualEditorBindingEditorContractProps } from '../visualEditorContracts';
import {
  createBindingRemoveIntent,
  createBindingSetIntent,
  findVisualBinding,
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
}>;

export type BindingEditorProps = VisualEditorBindingEditorContractProps & Readonly<{
  copy?: Partial<BindingEditorCopy>;
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
  noSources: 'No canonical binding sources are available.',
  current: 'Current binding'
};

export function BindingEditor({
  element,
  sourceCatalog,
  onMutationIntent,
  copy
}: BindingEditorProps) {
  const text: BindingEditorCopy = { ...DEFAULT_COPY, ...copy };
  const [actionError, setActionError] = useState<string | null>(null);

  const destinationResult = useMemo(
    () => computeDestinations(element.type),
    [element.type]
  );
  const sourceResult = useMemo(
    () => computeSources(sourceCatalog),
    [sourceCatalog]
  );
  const destinations = destinationResult.value;
  const sources = sourceResult.value;

  const firstBoundDestination = destinations.find(item =>
    element.bindings?.some(binding => binding.key === item.key)
  )?.key;
  const [propertyKey, setPropertyKey] = useState(firstBoundDestination ?? destinations[0]?.key ?? '');

  useEffect(() => {
    if (destinations.some(item => item.key === propertyKey)) return;
    setPropertyKey(firstBoundDestination ?? destinations[0]?.key ?? '');
  }, [destinations, firstBoundDestination, propertyKey]);

  const existing = propertyKey ? findVisualBinding(element, propertyKey) : undefined;
  const existingSourceKey = existing
    ? sourceIdentity(existing.kind, existing.target)
    : undefined;
  const [sourceKey, setSourceKey] = useState(
    existingSourceKey && sources.some(item => sourceIdentity(item.kind, item.target) === existingSourceKey)
      ? existingSourceKey
      : sources[0] ? sourceIdentity(sources[0].kind, sources[0].target) : ''
  );

  useEffect(() => {
    const current = propertyKey ? findVisualBinding(element, propertyKey) : undefined;
    const currentKey = current ? sourceIdentity(current.kind, current.target) : '';
    if (currentKey && sources.some(item => sourceIdentity(item.kind, item.target) === currentKey)) {
      setSourceKey(currentKey);
      return;
    }
    if (!sources.some(item => sourceIdentity(item.kind, item.target) === sourceKey)) {
      setSourceKey(sources[0] ? sourceIdentity(sources[0].kind, sources[0].target) : '');
    }
  }, [element, propertyKey, sourceKey, sources]);

  function apply() {
    setActionError(null);
    const source = sources.find(item => sourceIdentity(item.kind, item.target) === sourceKey);
    if (!source || !propertyKey) return;
    try {
      onMutationIntent(createBindingSetIntent(
        element,
        propertyKey,
        source,
        existing?.direction
      ));
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

          {existing && (
            <p data-testid="visual-binding-current">
              <strong>{text.current}:</strong> {existing.kind} · {existing.target}
            </p>
          )}

          <div className="visual-binding-editor__actions">
            <button type="button" onClick={apply} disabled={!propertyKey || !sourceKey}>
              {text.apply}
            </button>
            <button type="button" onClick={remove} disabled={!existing}>
              {text.remove}
            </button>
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
  sourceCatalog: VisualEditorBindingEditorContractProps['sourceCatalog']
): Computed<ReturnType<typeof normalizeBindingSourceCatalog>> {
  try {
    return { value: normalizeBindingSourceCatalog(sourceCatalog), error: null };
  } catch (cause) {
    return { value: [], error: errorText(cause) };
  }
}

function sourceIdentity(kind: string, target: string): string {
  return `${kind}\u0000${target}`;
}

function errorText(cause: unknown): string {
  return cause instanceof Error ? cause.message : String(cause);
}
