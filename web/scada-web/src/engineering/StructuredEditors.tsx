import React, { useEffect, useMemo, useState } from 'react';
import { previewEngineeringPackage } from './api';
import { editorTranslator } from './editorI18n';
import type { EngineeringLocale } from './i18n';
import type {
  DataSourceEngineering,
  EngineeringPackageView,
  ImportPreviewView,
  TagEngineering
} from './types';
import './structured-editors.css';

type EditorProps = {
  model: EngineeringPackageView;
  locale: EngineeringLocale;
};

const tagDataTypes = ['boolean', 'int16', 'int32', 'int64', 'float', 'double', 'string', 'dateTime', 'enum'];

export function TagEditor({ model, locale }: EditorProps) {
  const text = useMemo(() => editorTranslator(locale), [locale]);
  const tags = model.tags;
  const [query, setQuery] = useState('');
  const [selectedIdentity, setSelectedIdentity] = useState<string | null>(() => tags[0] ? tagIdentity(tags[0]) : null);
  const selected = tags.find(tag => tagIdentity(tag) === selectedIdentity) ?? tags[0] ?? null;
  const [draft, setDraft] = useState<TagEngineering | null>(() => selected ? clone(selected) : null);
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);

  useEffect(() => {
    if (!selected) {
      setDraft(null);
      return;
    }
    setSelectedIdentity(tagIdentity(selected));
    setDraft(clone(selected));
    setPreview(null);
    setPreviewError(null);
  }, [selectedIdentity]);

  useEffect(() => {
    setPreview(null);
    setPreviewError(null);
  }, [draft]);

  const filtered = tags.filter(tag => {
    const haystack = `${tag.path} ${tag.name} ${tag.source ?? ''} ${tag.address ?? ''}`.toLowerCase();
    return haystack.includes(query.trim().toLowerCase());
  });

  const changed = selected && draft ? JSON.stringify(selected) !== JSON.stringify(draft) : false;

  const reset = () => {
    if (selected) setDraft(clone(selected));
    setPreview(null);
    setPreviewError(null);
  };

  const runPreview = async () => {
    if (!selected || !draft) return;
    setPreviewing(true);
    setPreview(null);
    setPreviewError(null);
    try {
      const candidate = clone(model);
      const identity = tagIdentity(selected);
      candidate.tags = candidate.tags.map(tag => tagIdentity(tag) === identity ? clone(draft) : tag);
      setPreview(await previewEngineeringPackage(candidate));
    } catch (reason) {
      setPreviewError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setPreviewing(false);
    }
  };

  return (
    <EditorShell
      title={text('editor.tagsTitle')}
      description={text('editor.tagsDescription')}
      locale={locale}
    >
      <div className="eng-editor-layout">
        <EntityPicker
          label="TAGs"
          query={query}
          onQuery={setQuery}
          searchLabel={text('editor.search')}
          emptyLabel={text('editor.noResults')}
        >
          {filtered.map(tag => {
            const identity = tagIdentity(tag);
            return (
              <button
                type="button"
                className={identity === tagIdentity(selected ?? tag) ? 'selected' : ''}
                key={identity}
                onClick={() => setSelectedIdentity(identity)}
              >
                <strong>{tag.name}</strong>
                <code>{tag.path}</code>
                <span>{tag.dataType} · {tag.source ?? '—'}</span>
              </button>
            );
          })}
        </EntityPicker>

        <section className="eng-editor-form-panel">
          {!draft || !selected ? (
            <div className="eng-editor-empty">{text('editor.noSelection')}</div>
          ) : (
            <>
              <EditorStatus original={selected} draft={draft} changed={changed} locale={locale} />
              <div className="eng-editor-form-grid">
                <TextField label={text('editor.field.name')} value={draft.name} onChange={value => updateTag(setDraft, tag => ({ ...tag, name: value }))} />
                <TextField label={text('editor.field.path')} value={draft.path} mono onChange={value => updateTag(setDraft, tag => ({ ...tag, path: value }))} />
                <SelectField
                  label={text('editor.field.type')}
                  value={draft.dataType}
                  options={tagDataTypes}
                  onChange={value => updateTag(setDraft, tag => ({ ...tag, dataType: value }))}
                />
                <TextField label={text('editor.field.source')} value={draft.source ?? ''} mono onChange={value => updateTag(setDraft, tag => ({ ...tag, source: emptyToNull(value) }))} />
                <TextField label={text('editor.field.address')} value={draft.address ?? ''} mono onChange={value => updateTag(setDraft, tag => ({ ...tag, address: emptyToNull(value) }))} />
                <TextField label={text('editor.field.unit')} value={draft.engineeringUnit ?? ''} onChange={value => updateTag(setDraft, tag => ({ ...tag, engineeringUnit: emptyToNull(value) }))} />
                <NumberField label={text('editor.field.scaleMinimum')} value={draft.scaleMinimum} onChange={value => updateTag(setDraft, tag => ({ ...tag, scaleMinimum: value }))} />
                <NumberField label={text('editor.field.scaleMaximum')} value={draft.scaleMaximum} onChange={value => updateTag(setDraft, tag => ({ ...tag, scaleMaximum: value }))} />
                <label className="eng-editor-check">
                  <input
                    type="checkbox"
                    checked={draft.readOnly}
                    onChange={event => updateTag(setDraft, tag => ({ ...tag, readOnly: event.target.checked }))}
                  />
                  <span>{text('editor.field.readOnly')}</span>
                </label>
                <label className="eng-editor-check">
                  <input
                    type="checkbox"
                    checked={draft.historian?.enabled === true}
                    onChange={event => updateTag(setDraft, tag => ({
                      ...tag,
                      historian: { ...(tag.historian ?? {}), enabled: event.target.checked }
                    }))}
                  />
                  <span>{text('editor.field.historian')}</span>
                </label>
                <TextField label={text('editor.field.strategy')} value={draft.historian?.strategy ?? ''} onChange={value => updateTag(setDraft, tag => ({
                  ...tag,
                  historian: { ...(tag.historian ?? {}), strategy: value }
                }))} />
                <NumberField label={text('editor.field.deadband')} value={draft.historian?.deadband} onChange={value => updateTag(setDraft, tag => ({
                  ...tag,
                  historian: { ...(tag.historian ?? {}), deadband: value }
                }))} />
                <NumberField label={text('editor.field.period')} value={draft.historian?.periodMilliseconds} integer onChange={value => updateTag(setDraft, tag => ({
                  ...tag,
                  historian: { ...(tag.historian ?? {}), periodMilliseconds: value }
                }))} />
                <NumberField label={text('editor.field.maximumPeriod')} value={draft.historian?.maximumPeriodMilliseconds} integer onChange={value => updateTag(setDraft, tag => ({
                  ...tag,
                  historian: { ...(tag.historian ?? {}), maximumPeriodMilliseconds: value }
                }))} />
                <TextAreaField label={text('editor.field.description')} value={draft.description ?? ''} onChange={value => updateTag(setDraft, tag => ({ ...tag, description: emptyToNull(value) }))} />
              </div>
              <EditorActions
                changed={changed}
                previewing={previewing}
                onReset={reset}
                onPreview={() => void runPreview()}
                locale={locale}
              />
              <PreviewPanel preview={preview} error={previewError} locale={locale} />
            </>
          )}
        </section>
      </div>
    </EditorShell>
  );
}

export function DataSourceEditor({ model, locale }: EditorProps) {
  const text = useMemo(() => editorTranslator(locale), [locale]);
  const sources = model.dataSources ?? [];
  const [query, setQuery] = useState('');
  const [selectedIdentity, setSelectedIdentity] = useState<string | null>(() => sources[0] ? dataSourceIdentity(sources[0]) : null);
  const selected = sources.find(source => dataSourceIdentity(source) === selectedIdentity) ?? sources[0] ?? null;
  const [draft, setDraft] = useState<DataSourceEngineering | null>(() => selected ? clone(selected) : null);
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);

  useEffect(() => {
    if (!selected) {
      setDraft(null);
      return;
    }
    setSelectedIdentity(dataSourceIdentity(selected));
    setDraft(clone(selected));
    setPreview(null);
    setPreviewError(null);
  }, [selectedIdentity]);

  useEffect(() => {
    setPreview(null);
    setPreviewError(null);
  }, [draft]);

  const filtered = sources.filter(source => {
    const haystack = `${source.key} ${source.name} ${source.driver}`.toLowerCase();
    return haystack.includes(query.trim().toLowerCase());
  });

  const changed = selected && draft ? JSON.stringify(selected) !== JSON.stringify(draft) : false;

  const reset = () => {
    if (selected) setDraft(clone(selected));
    setPreview(null);
    setPreviewError(null);
  };

  const runPreview = async () => {
    if (!selected || !draft) return;
    setPreviewing(true);
    setPreview(null);
    setPreviewError(null);
    try {
      const candidate = clone(model);
      const identity = dataSourceIdentity(selected);
      candidate.dataSources = (candidate.dataSources ?? []).map(source =>
        dataSourceIdentity(source) === identity ? clone(draft) : source);
      setPreview(await previewEngineeringPackage(candidate));
    } catch (reason) {
      setPreviewError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setPreviewing(false);
    }
  };

  return (
    <EditorShell
      title={text('editor.dataSourcesTitle')}
      description={text('editor.dataSourcesDescription')}
      locale={locale}
    >
      <div className="eng-editor-layout">
        <EntityPicker
          label="Data Sources"
          query={query}
          onQuery={setQuery}
          searchLabel={text('editor.search')}
          emptyLabel={text('editor.noResults')}
        >
          {filtered.map(source => {
            const identity = dataSourceIdentity(source);
            return (
              <button
                type="button"
                className={identity === dataSourceIdentity(selected ?? source) ? 'selected' : ''}
                key={identity}
                onClick={() => setSelectedIdentity(identity)}
              >
                <strong>{source.name}</strong>
                <code>{source.key}</code>
                <span>{source.driver}</span>
              </button>
            );
          })}
        </EntityPicker>

        <section className="eng-editor-form-panel">
          {!draft || !selected ? (
            <div className="eng-editor-empty">{text('editor.noSelection')}</div>
          ) : (
            <>
              <EditorStatus original={selected} draft={draft} changed={changed} locale={locale} />
              <div className="eng-editor-form-grid">
                <TextField label={text('editor.field.name')} value={draft.name} onChange={value => updateDataSource(setDraft, source => ({ ...source, name: value }))} />
                <TextField label={text('editor.field.key')} value={draft.key} mono onChange={value => updateDataSource(setDraft, source => ({ ...source, key: value }))} />
                <TextField label={text('editor.field.driver')} value={draft.driver} mono onChange={value => updateDataSource(setDraft, source => ({ ...source, driver: value }))} />
                <label className="eng-editor-check">
                  <input
                    type="checkbox"
                    checked={draft.enabled !== false}
                    onChange={event => updateDataSource(setDraft, source => ({ ...source, enabled: event.target.checked }))}
                  />
                  <span>{text('editor.field.enabled')}</span>
                </label>
              </div>

              <DictionaryEditor
                title={text('editor.settings')}
                hint={text('editor.settingsHint')}
                value={draft.settings ?? {}}
                keyLabel={text('editor.settingKey')}
                valueLabel={text('editor.settingValue')}
                addLabel={text('editor.addSetting')}
                removeLabel={text('editor.removeSetting')}
                onChange={settings => updateDataSource(setDraft, source => ({ ...source, settings }))}
              />

              <ReadOnlyDictionary
                title={text('editor.secretReferences')}
                hint={text('editor.secretReferencesHint')}
                value={draft.secretReferences ?? {}}
              />

              <EditorActions
                changed={changed}
                previewing={previewing}
                onReset={reset}
                onPreview={() => void runPreview()}
                locale={locale}
              />
              <PreviewPanel preview={preview} error={previewError} locale={locale} />
            </>
          )}
        </section>
      </div>
    </EditorShell>
  );
}

function EditorShell({
  title,
  description,
  locale,
  children
}: {
  title: string;
  description: string;
  locale: EngineeringLocale;
  children: React.ReactNode;
}) {
  const text = editorTranslator(locale);
  return (
    <div className="eng-section eng-editor-section">
      <header className="eng-editor-header">
        <div>
          <span className="eng-editor-eyebrow">{text('editor.previewOnly')}</span>
          <h1>{title}</h1>
          <p>{description}</p>
        </div>
        <div className="eng-editor-safety-note">
          <strong>{text('editor.previewOnly')}</strong>
          <span>{text('editor.previewOnlyHint')}</span>
        </div>
      </header>
      {children}
    </div>
  );
}

function EntityPicker({
  label,
  query,
  onQuery,
  searchLabel,
  emptyLabel,
  children
}: {
  label: string;
  query: string;
  onQuery: (value: string) => void;
  searchLabel: string;
  emptyLabel: string;
  children: React.ReactNode;
}) {
  const hasChildren = React.Children.count(children) > 0;
  return (
    <aside className="eng-editor-picker">
      <header>
        <strong>{label}</strong>
        <input
          type="search"
          aria-label={searchLabel}
          placeholder={searchLabel}
          value={query}
          onChange={event => onQuery(event.target.value)}
        />
      </header>
      <div className="eng-editor-picker-list">
        {hasChildren ? children : <span className="eng-editor-picker-empty">{emptyLabel}</span>}
      </div>
    </aside>
  );
}

function EditorStatus<T>({
  original,
  draft,
  changed,
  locale
}: {
  original: T;
  draft: T;
  changed: boolean;
  locale: EngineeringLocale;
}) {
  const text = editorTranslator(locale);
  return (
    <div className="eng-editor-status">
      <div>
        <span>{text('editor.draft')}</span>
        <strong>{changed ? text('editor.changed') : text('editor.original')}</strong>
      </div>
      <small>{changed ? diffCount(original, draft) : 0}</small>
    </div>
  );
}

function EditorActions({
  changed,
  previewing,
  onReset,
  onPreview,
  locale
}: {
  changed: boolean;
  previewing: boolean;
  onReset: () => void;
  onPreview: () => void;
  locale: EngineeringLocale;
}) {
  const text = editorTranslator(locale);
  return (
    <div className="eng-editor-actions">
      <button type="button" className="secondary" onClick={onReset} disabled={!changed || previewing}>
        {text('editor.reset')}
      </button>
      <button type="button" className="primary" onClick={onPreview} disabled={previewing}>
        {previewing ? text('editor.previewing') : text('editor.preview')}
      </button>
    </div>
  );
}

function PreviewPanel({
  preview,
  error,
  locale
}: {
  preview: ImportPreviewView | null;
  error: string | null;
  locale: EngineeringLocale;
}) {
  const text = editorTranslator(locale);
  const issues = preview?.items.flatMap(item => item.issues ?? []) ?? [];
  return (
    <section className="eng-preview-panel" aria-live="polite">
      <header>
        <div>
          <span>{text('editor.validation')}</span>
          <strong className={preview ? (preview.canApply ? 'valid' : 'invalid') : ''}>
            {error
              ? text('editor.previewFailed')
              : preview
                ? (preview.canApply ? text('editor.valid') : text('editor.invalid'))
                : text('editor.notValidated')}
          </strong>
        </div>
        {preview && (
          <div className="eng-preview-counts">
            <span><b>{preview.errorCount}</b> {text('editor.errors')}</span>
            <span><b>{preview.createCount}</b> {text('editor.creates')}</span>
            <span><b>{preview.updateCount}</b> {text('editor.updates')}</span>
            <span><b>{preview.skipCount}</b> {text('editor.skips')}</span>
          </div>
        )}
      </header>
      {error && <pre className="eng-preview-error">{error}</pre>}
      {issues.length > 0 && (
        <div className="eng-preview-issues">
          {issues.map((issue, index) => (
            <div className={issue.isError ? 'error' : 'warning'} key={`${issue.code}-${issue.entityKey}-${index}`}>
              <strong>{issue.code}</strong>
              <span>{issue.message}</span>
              <small>{text('editor.issueEntity')}: {issue.entityKey}</small>
            </div>
          ))}
        </div>
      )}
      <footer>{text('editor.workspaceUntouched')}</footer>
    </section>
  );
}

function DictionaryEditor({
  title,
  hint,
  value,
  keyLabel,
  valueLabel,
  addLabel,
  removeLabel,
  onChange
}: {
  title: string;
  hint: string;
  value: Record<string, string>;
  keyLabel: string;
  valueLabel: string;
  addLabel: string;
  removeLabel: string;
  onChange: (next: Record<string, string>) => void;
}) {
  const entries = Object.entries(value);

  const updateEntry = (index: number, key: string, nextValue: string) => {
    const nextEntries = entries.map((entry, current) => current === index ? [key, nextValue] as [string, string] : entry);
    onChange(Object.fromEntries(nextEntries.filter(([entryKey]) => entryKey.length > 0)));
  };

  const removeEntry = (index: number) => onChange(Object.fromEntries(entries.filter((_, current) => current !== index)));

  const addEntry = () => {
    let index = 1;
    let key = 'setting';
    while (Object.prototype.hasOwnProperty.call(value, key)) key = `setting${++index}`;
    onChange({ ...value, [key]: '' });
  };

  return (
    <section className="eng-dictionary-editor">
      <header><strong>{title}</strong><span>{hint}</span></header>
      {entries.map(([key, entryValue], index) => (
        <div className="eng-dictionary-row" key={`${key}-${index}`}>
          <label><span>{keyLabel}</span><input value={key} onChange={event => updateEntry(index, event.target.value, entryValue)} /></label>
          <label><span>{valueLabel}</span><input value={entryValue} onChange={event => updateEntry(index, key, event.target.value)} /></label>
          <button type="button" onClick={() => removeEntry(index)}>{removeLabel}</button>
        </div>
      ))}
      <button type="button" className="eng-add-setting" onClick={addEntry}>+ {addLabel}</button>
    </section>
  );
}

function ReadOnlyDictionary({ title, hint, value }: { title: string; hint: string; value: Record<string, string> }) {
  const entries = Object.entries(value);
  return (
    <section className="eng-readonly-dictionary">
      <header><strong>{title}</strong><span>{hint}</span></header>
      {entries.length === 0
        ? <span className="eng-readonly-empty">—</span>
        : entries.map(([key, reference]) => (
          <div key={key}><code>{key}</code><span>→</span><code>{reference}</code></div>
        ))}
    </section>
  );
}

function TextField({
  label,
  value,
  onChange,
  mono = false
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  mono?: boolean;
}) {
  return (
    <label className="eng-editor-field">
      <span>{label}</span>
      <input className={mono ? 'mono' : ''} value={value} onChange={event => onChange(event.target.value)} />
    </label>
  );
}

function TextAreaField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="eng-editor-field eng-editor-field-wide">
      <span>{label}</span>
      <textarea rows={3} value={value} onChange={event => onChange(event.target.value)} />
    </label>
  );
}

function SelectField({
  label,
  value,
  options,
  onChange
}: {
  label: string;
  value: string;
  options: string[];
  onChange: (value: string) => void;
}) {
  const effectiveOptions = options.includes(value) ? options : [value, ...options];
  return (
    <label className="eng-editor-field">
      <span>{label}</span>
      <select value={value} onChange={event => onChange(event.target.value)}>
        {effectiveOptions.map(option => <option key={option} value={option}>{option}</option>)}
      </select>
    </label>
  );
}

function NumberField({
  label,
  value,
  onChange,
  integer = false
}: {
  label: string;
  value?: number | null;
  onChange: (value: number | null) => void;
  integer?: boolean;
}) {
  return (
    <label className="eng-editor-field">
      <span>{label}</span>
      <input
        type="number"
        step={integer ? 1 : 'any'}
        value={value ?? ''}
        onChange={event => onChange(parseNullableNumber(event.target.value, integer))}
      />
    </label>
  );
}

function updateTag(
  setter: React.Dispatch<React.SetStateAction<TagEngineering | null>>,
  update: (current: TagEngineering) => TagEngineering
) {
  setter(current => current ? update(current) : current);
}

function updateDataSource(
  setter: React.Dispatch<React.SetStateAction<DataSourceEngineering | null>>,
  update: (current: DataSourceEngineering) => DataSourceEngineering
) {
  setter(current => current ? update(current) : current);
}

function tagIdentity(tag: TagEngineering) {
  return tag.id ? `id:${tag.id}` : `path:${tag.path}`;
}

function dataSourceIdentity(source: DataSourceEngineering) {
  return source.id ? `id:${source.id}` : `key:${source.key}`;
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function emptyToNull(value: string): string | null {
  return value.trim().length === 0 ? null : value;
}

function parseNullableNumber(value: string, integer: boolean): number | null {
  if (value.trim().length === 0) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return null;
  return integer ? Math.trunc(parsed) : parsed;
}

function diffCount(original: unknown, draft: unknown): number {
  if (!isRecord(original) || !isRecord(draft)) return JSON.stringify(original) === JSON.stringify(draft) ? 0 : 1;
  const keys = new Set([...Object.keys(original), ...Object.keys(draft)]);
  let count = 0;
  for (const key of keys) {
    if (JSON.stringify(original[key]) !== JSON.stringify(draft[key])) count++;
  }
  return count;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
