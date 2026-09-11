import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { clientMemory } from '../../runtime/clientMemory';
import type { EngineeringLocale } from '../i18n';
import { loadEngineeringSnapshot } from '../api';
import type { ScriptVisualEventReference } from './scriptEngineeringTypes';
import {
  buildScriptAssistantCatalog,
  filterScriptAssistantCatalog,
  type ScriptAssistantCatalog,
  type ScriptAssistantSnippet,
  type ScriptAssistantTag,
  type ScriptAssistantVisualDefinition,
  type ScriptAssistantVisualObject,
  type ScriptAssistantVisualProperty
} from './scriptAssistantModel';
import { scriptAssistantCopy, type ScriptAssistantCopy } from './scriptAssistantCopy';
import './script-assistant.css';

type ScriptAssistantSection = 'tags' | 'screens' | 'popups' | 'clientMemory' | 'capabilities';

type ScriptAssistantPanelProps = {
  locale: EngineeringLocale;
  visualEventReferences: readonly ScriptVisualEventReference[];
  onInsert(code: string): void;
};

export function ScriptAssistantPanel({
  locale,
  visualEventReferences,
  onInsert
}: ScriptAssistantPanelProps) {
  const copy = useMemo(() => scriptAssistantCopy(locale), [locale]);
  const [catalog, setCatalog] = useState<ScriptAssistantCatalog | null>(null);
  const [section, setSection] = useState<ScriptAssistantSection>('tags');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [wizardObject, setWizardObject] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [snapshot] = await Promise.all([
        loadEngineeringSnapshot(),
        clientMemory.ensureInitialized()
      ]);
      setCatalog(buildScriptAssistantCatalog(snapshot.package, clientMemory.snapshotSources()));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : copy.loadError);
    } finally {
      setLoading(false);
    }
  }, [copy.loadError]);

  useEffect(() => { void load(); }, [load]);

  const filtered = useMemo(
    () => catalog ? filterScriptAssistantCatalog(catalog, search) : null,
    [catalog, search]
  );
  const visualTargetPolicy = useMemo(
    () => buildVisualTargetPolicy(visualEventReferences, copy),
    [visualEventReferences, copy]
  );

  function insert(code: string) {
    setNotice(null);
    onInsert(code);
  }

  function startAction(object: ScriptAssistantVisualObject) {
    setWizardObject(current => current === object.canonicalReference ? null : object.canonicalReference);
  }

  if (loading && !catalog) {
    return <section className="script-assistant"><div className="script-assistant__state">{copy.loading}</div></section>;
  }

  return (
    <section className="script-assistant" data-testid="script-assistant" aria-label={copy.title}>
      <header className="script-assistant__header">
        <div>
          <strong>{copy.title}</strong>
          <span>{copy.subtitle}</span>
        </div>
        <button type="button" className="secondary" onClick={() => void load()} disabled={loading}>{copy.refresh}</button>
      </header>

      {error && <div className="script-assistant__message script-assistant__message--error" role="alert">{copy.loadError}: {error}</div>}
      {notice && <div className="script-assistant__message" role="status">{notice}</div>}

      <div className="script-assistant__search">
        <input
          aria-label={copy.search}
          placeholder={copy.search}
          value={search}
          onChange={event => setSearch(event.target.value)}
        />
      </div>

      <div className="script-assistant__tabs" role="tablist" aria-label={copy.title}>
        {([
          ['tags', copy.tags],
          ['screens', copy.screens],
          ['popups', copy.popups],
          ['clientMemory', copy.clientMemory],
          ['capabilities', copy.capabilities]
        ] as const).map(([key, label]) => (
          <button
            key={key}
            type="button"
            role="tab"
            aria-selected={section === key}
            className={section === key ? 'is-selected' : ''}
            onClick={() => setSection(key)}
          >
            {label}
          </button>
        ))}
      </div>

      <div className="script-assistant__body">
        {!filtered ? null : section === 'tags' ? (
          <TagSection tags={filtered.tags} copy={copy} onInsert={insert} />
        ) : section === 'screens' ? (
          <VisualSection
            definitions={filtered.screens}
            copy={copy}
            visualTargetPolicy={visualTargetPolicy}
            wizardObject={wizardObject}
            onStartAction={startAction}
            onNavigate={setSection}
            onInsert={insert}
            onNotice={setNotice}
          />
        ) : section === 'popups' ? (
          <VisualSection
            definitions={filtered.popups}
            copy={copy}
            visualTargetPolicy={visualTargetPolicy}
            wizardObject={wizardObject}
            onStartAction={startAction}
            onNavigate={setSection}
            onInsert={insert}
            onNotice={setNotice}
          />
        ) : section === 'clientMemory' ? (
          <div className="script-assistant__cards">
            {filtered.clientMemory.length === 0 ? <Empty copy={copy} /> : filtered.clientMemory.map(memory => (
              <article className="script-assistant__card" key={memory.id || memory.path}>
                <div className="script-assistant__card-heading">
                  <strong>{memory.name}</strong>
                  <code>{memory.path}</code>
                </div>
                <div className="script-assistant__meta">
                  <span>{copy.dataType}: <code>{memory.dataType}</code></span>
                  <span>{memory.readOnly ? copy.readOnly : copy.writable}</span>
                  <span>{memory.sourceName}</span>
                </div>
                <SnippetActions snippets={memory.snippets} copy={copy} onInsert={insert} />
              </article>
            ))}
          </div>
        ) : (
          <div className="script-assistant__cards">
            <p className="script-assistant__hint">{copy.apiHint}</p>
            {filtered.capabilities.length === 0 ? <Empty copy={copy} /> : filtered.capabilities.map(item => (
              <article className="script-assistant__card script-assistant__card--compact" key={item.capability}>
                <strong><code>{item.capability}</code></strong>
                <code>{item.pythonApi}</code>
              </article>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

function TagSection({
  tags,
  copy,
  onInsert
}: {
  tags: readonly ScriptAssistantTag[];
  copy: ScriptAssistantCopy;
  onInsert(code: string): void;
}) {
  if (tags.length === 0) return <Empty copy={copy} />;
  return (
    <div className="script-assistant__cards">
      {tags.map(tag => (
        <article className="script-assistant__card" key={tag.id || tag.path}>
          <div className="script-assistant__card-heading">
            <strong>{tag.name}</strong>
            <code>{tag.path}</code>
          </div>
          <div className="script-assistant__meta">
            <span>{copy.canonicalReference}: <code>{tag.canonicalReference ?? '—'}</code></span>
            <span>{copy.dataType}: <code>{tag.dataType}</code>{tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}</span>
            <span>{copy.source}: {tag.sourceLabel ?? '—'} · {sourceStatusLabel(tag.sourceIdentityStatus, copy)}</span>
            {tag.dataSourceId && <span>DataSourceId: <code>{tag.dataSourceId}</code></span>}
            {tag.driver && <span>{copy.driver}: <code>{tag.driver}</code></span>}
            <span>{tag.readOnly ? copy.readOnly : copy.writable}</span>
          </div>
          {tag.description && <p className="script-assistant__hint">{tag.description}</p>}
          {!tag.readOnly && <p className="script-assistant__hint">{copy.authorizedAtRuntime}</p>}
          <SnippetActions snippets={tag.snippets} copy={copy} onInsert={onInsert} />
        </article>
      ))}
    </div>
  );
}

function VisualSection({
  definitions,
  copy,
  visualTargetPolicy,
  wizardObject,
  onStartAction,
  onNavigate,
  onInsert,
  onNotice
}: {
  definitions: readonly ScriptAssistantVisualDefinition[];
  copy: ScriptAssistantCopy;
  visualTargetPolicy: VisualTargetPolicy;
  wizardObject: string | null;
  onStartAction(object: ScriptAssistantVisualObject): void;
  onNavigate(section: ScriptAssistantSection): void;
  onInsert(code: string): void;
  onNotice(message: string): void;
}) {
  if (definitions.length === 0) return <Empty copy={copy} />;
  return (
    <div className="script-assistant__cards">
      {definitions.map(definition => (
        <details className="script-assistant__definition" key={`${definition.kind}:${definition.id}`} open>
          <summary>
            <strong>{definition.name}</strong>
            <code>{definition.key}</code>
            {definition.route && <span>{definition.route}</span>}
          </summary>
          <div className="script-assistant__objects">
            {definition.objects.length === 0 ? <Empty copy={copy} /> : definition.objects.map(object => (
              <VisualObjectCard
                key={object.canonicalReference}
                object={object}
                copy={copy}
                visualTargetPolicy={visualTargetPolicy}
                wizardOpen={wizardObject === object.canonicalReference}
                onStartAction={onStartAction}
                onNavigate={onNavigate}
                onInsert={onInsert}
                onNotice={onNotice}
                depth={0}
              />
            ))}
          </div>
        </details>
      ))}
    </div>
  );
}

function VisualObjectCard({
  object,
  copy,
  visualTargetPolicy,
  wizardOpen,
  onStartAction,
  onNavigate,
  onInsert,
  onNotice,
  depth
}: {
  object: ScriptAssistantVisualObject;
  copy: ScriptAssistantCopy;
  visualTargetPolicy: VisualTargetPolicy;
  wizardOpen: boolean;
  onStartAction(object: ScriptAssistantVisualObject): void;
  onNavigate(section: ScriptAssistantSection): void;
  onInsert(code: string): void;
  onNotice(message: string): void;
  depth: number;
}) {
  const context = visualTargetPolicy.forTarget(object.canonicalReference);
  return (
    <article className="script-assistant__object" style={{ marginInlineStart: depth * 12 }}>
      <div className="script-assistant__card-heading">
        <strong>{object.key}</strong>
        <code>{object.type}</code>
      </div>
      <div className="script-assistant__meta">
        <span>{copy.canonicalReference}: <code>{object.canonicalReference}</code></span>
        {object.equipmentPath && <span>{copy.equipment}: <code>{object.equipmentPath}</code></span>}
        {object.dynamoKey && <span>{copy.dynamo}: <code>{object.dynamoKey}</code></span>}
      </div>

      {object.events.length > 0 && (
        <details className="script-assistant__events">
          <summary>{copy.events}</summary>
          {object.events.map(eventName => (
            <div className="script-assistant__event" key={eventName}>
              <strong>{eventName}</strong>
              <button type="button" className="secondary" onClick={() => onStartAction(object)}>{copy.addAction}</button>
            </div>
          ))}
          {wizardOpen && (
            <div className="script-assistant__wizard" data-testid="script-action-wizard">
              <span>{copy.chooseAction}</span>
              <div className="script-assistant__actions">
                <button type="button" onClick={() => onNavigate('tags')}>{copy.actionTag}</button>
                <button type="button" onClick={() => onNotice(context.reason ?? copy.runtimeContextOnly)}>{copy.actionVisual}</button>
                <button type="button" onClick={() => onNavigate('clientMemory')}>{copy.actionMemory}</button>
                <button type="button" onClick={() => { onInsert('# Custom Python action'); onNotice(copy.customInserted); }}>{copy.actionCustom}</button>
              </div>
            </div>
          )}
        </details>
      )}

      {object.dynamoKey && (
        <details className="script-assistant__properties">
          <summary>{copy.publicInterface}</summary>
          <p className="script-assistant__hint">{copy.dynamoHint}</p>
          {object.publicDynamoParameters.map(parameter => (
            <div className="script-assistant__property" key={parameter.key}>
              <strong>{parameter.key}</strong>
              <code>{parameter.parameterKind}</code>
              {parameter.tagReference && <span>{copy.tag}: <code>{parameter.tagReference}</code></span>}
              <span>{formatValue(parameter.value)}</span>
            </div>
          ))}
        </details>
      )}

      <details className="script-assistant__properties">
        <summary>{copy.properties} ({object.properties.length})</summary>
        {object.schemaStatus === 'unknown' && <p className="script-assistant__hint">{copy.schemaUnknown}</p>}
        {object.properties.length === 0 && object.schemaStatus === 'canonical' && <p className="script-assistant__hint">{copy.noProperties}</p>}
        {object.properties.map(property => (
          <VisualPropertyRow
            key={property.key}
            property={property}
            copy={copy}
            context={context}
            onInsert={onInsert}
          />
        ))}
      </details>

      {object.children.map(child => (
        <VisualObjectCard
          key={child.canonicalReference}
          object={child}
          copy={copy}
          visualTargetPolicy={visualTargetPolicy}
          wizardOpen={false}
          onStartAction={onStartAction}
          onNavigate={onNavigate}
          onInsert={onInsert}
          onNotice={onNotice}
          depth={depth + 1}
        />
      ))}
    </article>
  );
}

function VisualPropertyRow({
  property,
  copy,
  context,
  onInsert
}: {
  property: ScriptAssistantVisualProperty;
  copy: ScriptAssistantCopy;
  context: VisualTargetDecision;
  onInsert(code: string): void;
}) {
  return (
    <div className="script-assistant__property">
      <div>
        <strong>{property.key}</strong>
        <code>{property.type}</code>
        {property.category && <span>{property.category}</span>}
      </div>
      <div className="script-assistant__meta">
        <span>{copy.runtimeRead}: {property.runtimeReadable ? copy.yes : copy.no}</span>
        <span>{copy.runtimeWrite}: {property.runtimeWritable ? copy.yes : copy.no}</span>
        <span>{copy.binding}: {property.supportsBinding ? copy.yes : copy.no}</span>
        <span>{copy.animation}: {property.animatable ? copy.yes : copy.no}</span>
        <span>{copy.current}: <code>{formatValue(property.currentValue)}</code></span>
        {property.allowedValues.length > 0 && <span>{copy.enum}: <code>{property.allowedValues.join(' | ')}</code></span>}
      </div>
      {!context.allowed && <p className="script-assistant__hint">{context.reason}</p>}
      <SnippetActions
        snippets={property.snippets}
        copy={copy}
        onInsert={onInsert}
        additionalDisabledReason={context.allowed ? null : context.reason}
      />
    </div>
  );
}

function SnippetActions({
  snippets,
  copy,
  onInsert,
  additionalDisabledReason = null
}: {
  snippets: readonly ScriptAssistantSnippet[];
  copy: ScriptAssistantCopy;
  onInsert(code: string): void;
  additionalDisabledReason?: string | null;
}) {
  return (
    <div className="script-assistant__actions">
      {snippets.map(snippet => {
        const disabledReason = !snippet.enabled ? snippet.reason : additionalDisabledReason;
        return (
          <button
            type="button"
            key={snippet.kind}
            disabled={Boolean(disabledReason)}
            title={disabledReason ?? snippet.code}
            onClick={() => onInsert(snippet.code)}
          >
            {snippetLabel(snippet.kind, copy)}
          </button>
        );
      })}
    </div>
  );
}

function Empty({ copy }: { copy: ScriptAssistantCopy }) {
  return <p className="script-assistant__hint">{copy.empty}</p>;
}

function sourceStatusLabel(status: ScriptAssistantTag['sourceIdentityStatus'], copy: ScriptAssistantCopy): string {
  if (status === 'stable') return copy.stableSource;
  if (status === 'legacy') return copy.legacySource;
  if (status === 'unresolved') return copy.unresolvedSource;
  return copy.noSource;
}

function snippetLabel(kind: ScriptAssistantSnippet['kind'], copy: ScriptAssistantCopy): string {
  if (kind.endsWith('-read')) return `${copy.insert} · ${copy.read}`;
  if (kind.endsWith('-write')) return `${copy.insert} · ${copy.write}`;
  if (kind === 'visual-property-clear') return `${copy.insert} · ${copy.clear}`;
  return `${copy.insert} · ${copy.tween}`;
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'string') return value;
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

type VisualTargetDecision = Readonly<{ allowed: boolean; reason: string | null }>;
type VisualTargetPolicy = Readonly<{ forTarget(reference: string): VisualTargetDecision }>;

function buildVisualTargetPolicy(
  references: readonly ScriptVisualEventReference[],
  copy: ScriptAssistantCopy
): VisualTargetPolicy {
  const targets = [...new Set(references
    .map(reference => reference.visualObjectId?.trim())
    .filter((value): value is string => Boolean(value)))];

  return Object.freeze({
    forTarget(reference: string): VisualTargetDecision {
      if (targets.length === 0) return Object.freeze({ allowed: false, reason: copy.noVisualTarget });
      if (targets.length > 1) return Object.freeze({ allowed: false, reason: copy.multipleVisualTargets });
      if (targets[0] !== reference) return Object.freeze({ allowed: false, reason: copy.runtimeContextOnly });
      return Object.freeze({ allowed: true, reason: null });
    }
  });
}
