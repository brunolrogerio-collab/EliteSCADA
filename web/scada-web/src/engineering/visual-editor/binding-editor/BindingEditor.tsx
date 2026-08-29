import React, { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from '../../i18n';
import { ProjectReferenceBrowser } from '../../project-reference/ProjectReferenceBrowser';
import type { ProjectReferenceDescriptor } from '../../project-reference/projectReferenceModel';
import type { VisualEditorBindingEditorContractProps } from '../visualEditorContracts';
import {
  bindingSourceIdentity,
  compatibleBindingSources,
  createBindingRemoveIntent,
  createBindingSetIntent,
  createTagBitBindingSource,
  findBindingSourceForBinding,
  findVisualBinding,
  isBindingSourceCompatible,
  listBindableVisualProperties,
  normalizeBindingSourceCatalog,
  resolveBindingSourceReference,
  type BindableVisualProperty
} from './bindingEditorModel';

export type BindingEditorCopy = Readonly<{
  title: string;
  destination: string;
  source: string;
  bit: string;
  bitHint: string;
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
  bit: 'Bit',
  bitHint: 'Select one bit from the authoritative integer TAG.',
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
  const text: BindingEditorCopy = { ...DEFAULT_COPY, ...localizedBitCopy(locale), ...copy };
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
  const existingBaseSource = existing ? findBindingSourceForBinding(existing, sources) : undefined;
  const existingSourceKey = existingBaseSource ? bindingSourceIdentity(existingBaseSource) : undefined;
  const existingBitIndex = existing?.tagReference?.selector?.kind === 'bit'
    ? existing.tagReference.selector.index
    : null;
  const [sourceKey, setSourceKey] = useState(
    existingSourceKey && sources.some(item => bindingSourceIdentity(item) === existingSourceKey)
      ? existingSourceKey
      : sources[0] ? bindingSourceIdentity(sources[0]) : ''
  );
  const [bitIndex, setBitIndex] = useState<number | null>(
    existingBitIndex ?? existingBaseSource?.selectorCapability?.minIndex ?? null
  );

  useEffect(() => {
    const current = propertyKey ? findVisualBinding(element, propertyKey) : undefined;
    const currentSource = current ? findBindingSourceForBinding(current, sources) : undefined;
    const currentKey = currentSource ? bindingSourceIdentity(currentSource) : '';
    if (currentSource && currentKey && sources.some(item => bindingSourceIdentity(item) === currentKey)) {
      setSourceKey(currentKey);
      setBitIndex(current?.tagReference?.selector?.kind === 'bit'
        ? current.tagReference.selector.index
        : initialBitIndex(selectedDestination, currentSource));
      setExactReference(current.target);
      return;
    }
    if (!sources.some(item => bindingSourceIdentity(item) === sourceKey)) {
      const first = sources[0];
      setSourceKey(first ? bindingSourceIdentity(first) : '');
      setBitIndex(initialBitIndex(selectedDestination, first));
      setExactReference(first?.target ?? '');
    }
  }, [element, propertyKey, selectedDestination, sourceKey, sources]);

  const selectedSource = sources.find(item => bindingSourceIdentity(item) === sourceKey);
  const selectedBitCapability = bitSelectorCapability(selectedDestination, selectedSource);

  function apply() {
    setActionError(null);
    const source = sources.find(item => bindingSourceIdentity(item) === sourceKey);
    if (!source || !propertyKey) return;
    try {
      const effectiveSource = bitSelectorCapability(selectedDestination, source)
        ? createTagBitBindingSource(source, bitIndex ?? Number.NaN)
        : source;
      onMutationIntent(createBindingSetIntent(element, propertyKey, effectiveSource, existing?.direction));
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
    const resolved = resolveBindingSourceReference(sourceCatalog, target);
    if (resolved.status !== 'found' || !resolved.source || !isBindingSourceCompatible(selectedDestination, resolved.source)) {
      setActionError(text.exactNotFound);
      return;
    }

    const selector = resolved.source.tagReference?.selector;
    if (selector?.kind === 'bit' && resolved.source.tagReference?.tagId) {
      const tagId = resolved.source.tagReference.tagId.toLocaleLowerCase();
      const base = sources.find(item =>
        item.kind === 'Tag' &&
        item.tagReference?.tagId?.toLocaleLowerCase() === tagId &&
        !item.tagReference?.selector
      );
      if (!base || !bitSelectorCapability(selectedDestination, base)) {
        setActionError(text.exactNotFound);
        return;
      }
      setSourceKey(bindingSourceIdentity(base));
      setBitIndex(selector.index);
      setExactReference(resolved.source.target);
      return;
    }

    const source = sources.find(item => bindingSourceIdentity(item) === bindingSourceIdentity(resolved.source!));
    if (!source) {
      setActionError(text.exactNotFound);
      return;
    }
    setSourceKey(bindingSourceIdentity(source));
    setBitIndex(initialBitIndex(selectedDestination, source));
    setExactReference(source.target);
  }

  const browserReferences = useMemo(
    () => normalizeBindingSourceCatalog(sourceCatalog).map(toProjectReference),
    [sourceCatalog]
  );
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
                const selected = sources.find(item => bindingSourceIdentity(item) === event.target.value);
                setBitIndex(initialBitIndex(selectedDestination, selected));
                setExactReference(selected?.target ?? '');
              }}
            >
              {sources.length === 0 ? (
                <option value="">{text.noSources}</option>
              ) : sources.map(source => (
                <option key={bindingSourceIdentity(source)} value={bindingSourceIdentity(source)}>
                  {source.label} · {source.kind} · {source.target}
                </option>
              ))}
            </select>
          </label>

          {selectedBitCapability ? (
            <label className="visual-binding-editor__bit-selector">
              <span>{text.bit}</span>
              <input
                type="number"
                min={selectedBitCapability.minIndex}
                max={selectedBitCapability.maxIndex}
                step={1}
                value={bitIndex ?? selectedBitCapability.minIndex}
                onChange={event => {
                  setActionError(null);
                  const parsed = Number(event.currentTarget.value);
                  setBitIndex(Number.isInteger(parsed) ? parsed : null);
                  if (selectedSource) {
                    setExactReference(Number.isInteger(parsed)
                      ? `${selectedSource.target}.${parsed.toString().padStart(2, '0')}`
                      : selectedSource.target);
                  }
                }}
              />
              <small>{text.bitHint} {selectedBitCapability.minIndex}…{selectedBitCapability.maxIndex}</small>
            </label>
          ) : null}

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
              isSelectable={reference => isBrowserReferenceCompatible(reference, sources)}
              onSelect={reference => {
                const source = sources.find(item => item.target === reference.reference);
                if (!source) return;
                setSourceKey(bindingSourceIdentity(source));
                setBitIndex(initialBitIndex(selectedDestination, source));
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

function bitSelectorCapability(
  destination: BindableVisualProperty | undefined,
  source: VisualEditorBindingEditorContractProps['sourceCatalog'][number] | undefined
) {
  if (!destination || !source || isBindingSourceCompatible(destination, source)) return null;
  return source.selectorCapability?.kind === 'bit' ? source.selectorCapability : null;
}

function initialBitIndex(
  destination: BindableVisualProperty | undefined,
  source: VisualEditorBindingEditorContractProps['sourceCatalog'][number] | undefined
): number | null {
  const capability = bitSelectorCapability(destination, source);
  return capability ? capability.minIndex : null;
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
    pathSegments: Object.freeze(source.target.split(/[/.\\]+/g).filter(Boolean)),
    tagReference: source.tagReference ?? null,
    selectorCapability: source.selectorCapability ?? null
  });
}

function isBrowserReferenceCompatible(
  reference: ProjectReferenceDescriptor,
  sources: readonly VisualEditorBindingEditorContractProps['sourceCatalog'][number][]
): boolean {
  return sources.some(item => item.target === reference.reference);
}

function inferFamily(kind: string): ProjectReferenceDescriptor['family'] {
  return kind === 'ClientMemory' ? 'clientMemory' : 'tag';
}

function localizedBitCopy(locale: EngineeringLocale): Pick<BindingEditorCopy, 'bit' | 'bitHint'> {
  if (locale === 'en') return { bit: 'Bit', bitHint: 'Select one bit from the authoritative integer TAG.' };
  if (locale === 'es') return { bit: 'Bit', bitHint: 'Seleccione un bit del TAG entero autoritativo.' };
  return { bit: 'Bit', bitHint: 'Selecione um bit da TAG inteira autoritativa.' };
}

function errorText(cause: unknown): string {
  return cause instanceof Error ? cause.message : String(cause);
}
