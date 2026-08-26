import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage
} from './api';
import { editorTranslator } from './editorI18n';
import type { EngineeringLocale } from './i18n';
import type {
  AlarmEngineering,
  DataSourceEngineering,
  EngineeringPackageView,
  ImportPreviewView,
  TagEngineering
} from './types';
import './structured-editors.css';

const tagDataTypes = ['boolean', 'int16', 'int32', 'int64', 'float', 'double', 'string', 'dateTime', 'enum'];
const alarmTypes = ['digital', 'high', 'highHigh', 'low', 'lowLow', 'communication', 'system'];
const alarmPriorities = ['low', 'medium', 'high', 'critical'];
const analogAlarmTypes = new Set(['high', 'highHigh', 'low', 'lowLow']);
const NEW_TAG_IDENTITY = 'draft:new-tag';
const NEW_DATASOURCE_IDENTITY = 'draft:new-datasource';
const NEW_ALARM_IDENTITY = 'draft:new-alarm';

type EditorProps = {
  model: EngineeringPackageView;
  locale: EngineeringLocale;
};

type MutationState = {
  preview: ImportPreviewView | null;
  error: string | null;
  previewing: boolean;
  applying: boolean;
  canApply: boolean;
  validate: (candidate: EngineeringPackageView) => Promise<void>;
  apply: () => Promise<void>;
  invalidate: () => void;
};

export function TagEditor({ model, locale }: EditorProps) {
  const text = useMemo(() => editorTranslator(locale), [locale]);
  const mutation = useSecuredMutation(model, locale);
  const tags = model.tags;
  const [query, setQuery] = useState('');
  const [selectedIdentity, setSelectedIdentity] = useState<string | null>(() => tags[0] ? tagIdentity(tags[0]) : null);
  const isNew = selectedIdentity === NEW_TAG_IDENTITY;
  const selected = !isNew && selectedIdentity
    ? tags.find(tag => tagIdentity(tag) === selectedIdentity) ?? null
    : null;
  const [draft, setDraft] = useState<TagEngineering | null>(() => selected ? clone(selected) : null);

  useEffect(() => {
    if (selectedIdentity === NEW_TAG_IDENTITY) {
      setDraft(newTagDraft());
      mutation.invalidate();
      return;
    }
    const current = selectedIdentity ? tags.find(tag => tagIdentity(tag) === selectedIdentity) ?? null : null;
    if (current) {
      setDraft(clone(current));
      mutation.invalidate();
      return;
    }
    if (tags[0]) setSelectedIdentity(tagIdentity(tags[0]));
    else setDraft(null);
  }, [selectedIdentity, tags]);

  useEffect(() => mutation.invalidate(), [draft]);

  const changed = draft
    ? isNew
      ? JSON.stringify(draft) !== JSON.stringify(newTagDraft())
      : selected ? JSON.stringify(selected) !== JSON.stringify(draft) : false
    : false;
  useBeforeUnload(changed || mutation.applying);

  const chooseIdentity = (identity: string) => {
    if (identity === selectedIdentity) return;
    if (changed && !window.confirm(text('editor.discardConfirm'))) return;
    setSelectedIdentity(identity);
  };

  const reset = () => {
    if (isNew) setDraft(newTagDraft());
    else if (selected) setDraft(clone(selected));
    mutation.invalidate();
  };

  const preview = async () => {
    if (!draft || (!isNew && !selected)) return;
    const candidate = clone(model);
    if (isNew) candidate.tags = [...candidate.tags, clone(draft)];
    else if (selected) {
      const identity = tagIdentity(selected);
      candidate.tags = candidate.tags.map(tag => tagIdentity(tag) === identity ? clone(draft) : tag);
    }
    await mutation.validate(candidate);
  };

  const filtered = tags.filter(tag =>
    `${tag.path} ${tag.name} ${tag.source ?? ''} ${tag.address ?? ''}`
      .toLowerCase()
      .includes(query.trim().toLowerCase()));

  return (
    <EditorShell title={text('editor.tagsTitle')} description={text('editor.tagsDescription')} locale={locale}>
      <div className="eng-editor-layout">
        <EntityPicker
          label="TAGs"
          query={query}
          onQuery={setQuery}
          searchLabel={text('editor.search')}
          emptyLabel={text('editor.noResults')}
          actionLabel={text('editor.newTag')}
          actionActive={isNew}
          onAction={() => chooseIdentity(NEW_TAG_IDENTITY)}
        >
          {filtered.map(tag => {
            const identity = tagIdentity(tag);
            return (
              <button type="button" className={identity === selectedIdentity ? 'selected' : ''} key={identity} onClick={() => chooseIdentity(identity)}>
                <strong>{tag.name}</strong><code>{tag.path}</code><span>{tag.dataType} · {tag.source ?? '—'}</span>
              </button>
            );
          })}
        </EntityPicker>

        <section className="eng-editor-form-panel">
          {!draft || (!isNew && !selected) ? <div className="eng-editor-empty">{text('editor.noSelection')}</div> : (
            <>
              <EditorStatus original={selected} draft={draft} changed={changed} isNew={isNew} locale={locale} />
              <div className="eng-editor-form-grid">
                <TextField label={text('editor.field.name')} value={draft.name} onChange={value => updateTag(setDraft, tag => ({ ...tag, name: value }))} />
                <TextField label={text('editor.field.path')} value={draft.path} mono onChange={value => updateTag(setDraft, tag => ({ ...tag, path: value }))} />
                <SelectField label={text('editor.field.type')} value={draft.dataType} options={tagDataTypes} onChange={value => updateTag(setDraft, tag => ({ ...tag, dataType: value }))} />
                <TextField label={text('editor.field.source')} value={draft.source ?? ''} mono onChange={value => updateTag(setDraft, tag => ({ ...tag, source: emptyToNull(value) }))} />
                <TextField label={text('editor.field.address')} value={draft.address ?? ''} mono onChange={value => updateTag(setDraft, tag => ({ ...tag, address: emptyToNull(value) }))} />
                <TextField label={text('editor.field.unit')} value={draft.engineeringUnit ?? ''} onChange={value => updateTag(setDraft, tag => ({ ...tag, engineeringUnit: emptyToNull(value) }))} />
                <NumberField label={text('editor.field.scaleMinimum')} value={draft.scaleMinimum} onChange={value => updateTag(setDraft, tag => ({ ...tag, scaleMinimum: value }))} />
                <NumberField label={text('editor.field.scaleMaximum')} value={draft.scaleMaximum} onChange={value => updateTag(setDraft, tag => ({ ...tag, scaleMaximum: value }))} />
                <BooleanField label={text('editor.field.readOnly')} checked={draft.readOnly} onChange={value => updateTag(setDraft, tag => ({ ...tag, readOnly: value }))} />
                <BooleanField label={text('editor.field.historian')} checked={draft.historian?.enabled === true} onChange={value => updateTag(setDraft, tag => ({ ...tag, historian: { ...(tag.historian ?? {}), enabled: value } }))} />
                <TextField label={text('editor.field.strategy')} value={draft.historian?.strategy ?? ''} onChange={value => updateTag(setDraft, tag => ({ ...tag, historian: { ...(tag.historian ?? {}), strategy: value } }))} />
                <NumberField label={text('editor.field.deadband')} value={draft.historian?.deadband} onChange={value => updateTag(setDraft, tag => ({ ...tag, historian: { ...(tag.historian ?? {}), deadband: value } }))} />
                <NumberField label={text('editor.field.period')} value={draft.historian?.periodMilliseconds} integer onChange={value => updateTag(setDraft, tag => ({ ...tag, historian: { ...(tag.historian ?? {}), periodMilliseconds: value } }))} />
                <NumberField label={text('editor.field.maximumPeriod')} value={draft.historian?.maximumPeriodMilliseconds} integer onChange={value => updateTag(setDraft, tag => ({ ...tag, historian: { ...(tag.historian ?? {}), maximumPeriodMilliseconds: value } }))} />
                <TextAreaField label={text('editor.field.description')} value={draft.description ?? ''} onChange={value => updateTag(setDraft, tag => ({ ...tag, description: emptyToNull(value) }))} />
              </div>
              <MutationActions changed={changed} mutation={mutation} onReset={reset} onPreview={() => void preview()} locale={locale} />
              <PreviewPanel mutation={mutation} locale={locale} />
            </>
          )}
        </section>
      </div>
    </EditorShell>
  );
}

export function DataSourceEditor({ model, locale }: EditorProps) {
  const text = useMemo(() => editorTranslator(locale), [locale]);
  const mutation = useSecuredMutation(model, locale);
  const sources = model.dataSources ?? [];
  const [query, setQuery] = useState('');
  const [selectedIdentity, setSelectedIdentity] = useState<string | null>(() => sources[0] ? dataSourceIdentity(sources[0]) : null);
  const isNew = selectedIdentity === NEW_DATASOURCE_IDENTITY;
  const selected = !isNew && selectedIdentity ? sources.find(source => dataSourceIdentity(source) === selectedIdentity) ?? null : null;
  const [draft, setDraft] = useState<DataSourceEngineering | null>(() => selected ? clone(selected) : null);

  useEffect(() => {
    if (selectedIdentity === NEW_DATASOURCE_IDENTITY) {
      setDraft(newDataSourceDraft());
      mutation.invalidate();
      return;
    }
    const current = selectedIdentity ? sources.find(source => dataSourceIdentity(source) === selectedIdentity) ?? null : null;
    if (current) {
      setDraft(clone(current));
      mutation.invalidate();
      return;
    }
    if (sources[0]) setSelectedIdentity(dataSourceIdentity(sources[0]));
    else setDraft(null);
  }, [selectedIdentity, sources]);

  useEffect(() => mutation.invalidate(), [draft]);

  const changed = draft
    ? isNew
      ? JSON.stringify(draft) !== JSON.stringify(newDataSourceDraft())
      : selected ? JSON.stringify(selected) !== JSON.stringify(draft) : false
    : false;
  useBeforeUnload(changed || mutation.applying);

  const chooseIdentity = (identity: string) => {
    if (identity === selectedIdentity) return;
    if (changed && !window.confirm(text('editor.discardConfirm'))) return;
    setSelectedIdentity(identity);
  };

  const reset = () => {
    if (isNew) setDraft(newDataSourceDraft());
    else if (selected) setDraft(clone(selected));
    mutation.invalidate();
  };

  const preview = async () => {
    if (!draft || (!isNew && !selected)) return;
    const candidate = clone(model);
    if (isNew) candidate.dataSources = [...(candidate.dataSources ?? []), clone(draft)];
    else if (selected) {
      const identity = dataSourceIdentity(selected);
      candidate.dataSources = (candidate.dataSources ?? []).map(source => dataSourceIdentity(source) === identity ? clone(draft) : source);
    }
    await mutation.validate(candidate);
  };

  const filtered = sources.filter(source =>
    `${source.key} ${source.name} ${source.driver}`.toLowerCase().includes(query.trim().toLowerCase()));

  return (
    <EditorShell title={text('editor.dataSourcesTitle')} description={text('editor.dataSourcesDescription')} locale={locale}>
      <div className="eng-editor-layout">
        <EntityPicker
          label="Data Sources"
          query={query}
          onQuery={setQuery}
          searchLabel={text('editor.search')}
          emptyLabel={text('editor.noResults')}
          actionLabel={text('editor.newDataSource')}
          actionActive={isNew}
          onAction={() => chooseIdentity(NEW_DATASOURCE_IDENTITY)}
        >
          {filtered.map(source => {
            const identity = dataSourceIdentity(source);
            return (
              <button type="button" className={identity === selectedIdentity ? 'selected' : ''} key={identity} onClick={() => chooseIdentity(identity)}>
                <strong>{source.name}</strong><code>{source.key}</code><span>{source.driver}</span>
              </button>
            );
          })}
        </EntityPicker>

        <section className="eng-editor-form-panel">
          {!draft || (!isNew && !selected) ? <div className="eng-editor-empty">{text('editor.noSelection')}</div> : (
            <>
              <EditorStatus original={selected} draft={draft} changed={changed} isNew={isNew} locale={locale} />
              <div className="eng-editor-form-grid">
                <TextField label={text('editor.field.name')} value={draft.name} onChange={value => updateDataSource(setDraft, source => ({ ...source, name: value }))} />
                <TextField label={text('editor.field.key')} value={draft.key} mono onChange={value => updateDataSource(setDraft, source => ({ ...source, key: value }))} />
                <TextField label={text('editor.field.driver')} value={draft.driver} mono onChange={value => updateDataSource(setDraft, source => ({ ...source, driver: value }))} />
                <BooleanField label={text('editor.field.enabled')} checked={draft.enabled !== false} onChange={value => updateDataSource(setDraft, source => ({ ...source, enabled: value }))} />
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
              <ReadOnlyDictionary title={text('editor.secretReferences')} hint={text('editor.secretReferencesHint')} value={draft.secretReferences ?? {}} />
              <MutationActions changed={changed} mutation={mutation} onReset={reset} onPreview={() => void preview()} locale={locale} />
              <PreviewPanel mutation={mutation} locale={locale} />
            </>
          )}
        </section>
      </div>
    </EditorShell>
  );
}

export function AlarmEditor({ model, locale }: EditorProps) {
  const text = useMemo(() => editorTranslator(locale), [locale]);
  const mutation = useSecuredMutation(model, locale);
  const alarms = model.alarms;
  const [query, setQuery] = useState('');
  const [selectedIdentity, setSelectedIdentity] = useState<string | null>(() => alarms[0] ? alarmIdentity(alarms[0]) : null);
  const isNew = selectedIdentity === NEW_ALARM_IDENTITY;
  const selected = !isNew && selectedIdentity ? alarms.find(alarm => alarmIdentity(alarm) === selectedIdentity) ?? null : null;
  const [draft, setDraft] = useState<AlarmEngineering | null>(() => selected ? clone(selected) : null);

  useEffect(() => {
    if (selectedIdentity === NEW_ALARM_IDENTITY) {
      setDraft(newAlarmDraft());
      mutation.invalidate();
      return;
    }
    const current = selectedIdentity ? alarms.find(alarm => alarmIdentity(alarm) === selectedIdentity) ?? null : null;
    if (current) {
      setDraft(clone(current));
      mutation.invalidate();
      return;
    }
    if (alarms[0]) setSelectedIdentity(alarmIdentity(alarms[0]));
    else setDraft(null);
  }, [selectedIdentity, alarms]);

  useEffect(() => mutation.invalidate(), [draft]);

  const changed = draft
    ? isNew
      ? JSON.stringify(draft) !== JSON.stringify(newAlarmDraft())
      : selected ? JSON.stringify(selected) !== JSON.stringify(draft) : false
    : false;
  useBeforeUnload(changed || mutation.applying);

  const chooseIdentity = (identity: string) => {
    if (identity === selectedIdentity) return;
    if (changed && !window.confirm(text('editor.discardConfirm'))) return;
    setSelectedIdentity(identity);
  };

  const reset = () => {
    if (isNew) setDraft(newAlarmDraft());
    else if (selected) setDraft(clone(selected));
    mutation.invalidate();
  };

  const preview = async () => {
    if (!draft || (!isNew && !selected)) return;
    const candidate = clone(model);
    if (isNew) candidate.alarms = [...candidate.alarms, clone(draft)];
    else if (selected) {
      const identity = alarmIdentity(selected);
      candidate.alarms = candidate.alarms.map(alarm => alarmIdentity(alarm) === identity ? clone(draft) : alarm);
    }
    await mutation.validate(candidate);
  };

  const setAlarmType = (type: string) => updateAlarm(setDraft, alarm => ({
    ...alarm,
    type,
    setpoint: analogAlarmTypes.has(type) ? alarm.setpoint : null,
    digitalActiveValue: type === 'digital' ? (alarm.digitalActiveValue ?? true) : alarm.digitalActiveValue
  }));

  const filtered = alarms.filter(alarm =>
    `${alarm.name} ${alarm.tagPath ?? alarm.tagId ?? ''} ${alarm.type} ${alarm.priority} ${alarm.area ?? ''} ${alarm.alarmClass ?? ''}`
      .toLowerCase().includes(query.trim().toLowerCase()));

  return (
    <EditorShell title={text('editor.alarmsTitle')} description={text('editor.alarmsDescription')} locale={locale}>
      <div className="eng-editor-layout">
        <EntityPicker
          label={locale === 'pt-BR' ? 'Alarmes' : locale === 'es' ? 'Alarmas' : 'Alarms'}
          query={query}
          onQuery={setQuery}
          searchLabel={text('editor.search')}
          emptyLabel={text('editor.noResults')}
          actionLabel={text('editor.newAlarm')}
          actionActive={isNew}
          onAction={() => chooseIdentity(NEW_ALARM_IDENTITY)}
        >
          {filtered.map(alarm => {
            const identity = alarmIdentity(alarm);
            return (
              <button type="button" className={identity === selectedIdentity ? 'selected' : ''} key={identity} onClick={() => chooseIdentity(identity)}>
                <strong>{alarm.name}</strong><code>{alarm.tagPath ?? alarm.tagId ?? '—'}</code><span>{alarm.type} · {alarm.priority}</span>
              </button>
            );
          })}
        </EntityPicker>

        <section className="eng-editor-form-panel">
          {!draft || (!isNew && !selected) ? <div className="eng-editor-empty">{text('editor.noSelection')}</div> : (
            <>
              <EditorStatus original={selected} draft={draft} changed={changed} isNew={isNew} locale={locale} />
              <div className="eng-editor-form-grid">
                <TextField label={text('editor.field.name')} value={draft.name} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, name: value }))} />
                <TextField label={text('editor.field.tagPath')} value={draft.tagPath ?? ''} mono onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, tagId: null, tagPath: emptyToNull(value) }))} />
                <SelectField label={text('editor.field.alarmType')} value={draft.type} options={alarmTypes} onChange={setAlarmType} />
                <SelectField label={text('editor.field.priority')} value={draft.priority} options={alarmPriorities} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, priority: value }))} />
                {analogAlarmTypes.has(draft.type) && <NumberField label={text('editor.field.setpoint')} value={draft.setpoint} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, setpoint: value }))} />}
                {draft.type === 'digital' && <BooleanField label={text('editor.field.digitalActiveValue')} checked={draft.digitalActiveValue !== false} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, digitalActiveValue: value }))} />}
                <TextField label={text('editor.field.alarmClass')} value={draft.alarmClass ?? ''} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, alarmClass: emptyToNull(value) }))} />
                <TextField label={text('editor.field.area')} value={draft.area ?? ''} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, area: emptyToNull(value) }))} />
                <NumberField label={text('editor.field.activationDelay')} value={draft.activationDelayMilliseconds} integer onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, activationDelayMilliseconds: value }))} />
                <BooleanField label={text('editor.field.enabled')} checked={draft.enabled !== false} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, enabled: value }))} />
                <BooleanField label={text('editor.field.requiresAcknowledgement')} checked={draft.requiresAcknowledgement !== false} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, requiresAcknowledgement: value }))} />
                <BooleanField label={text('editor.field.shelvingAllowed')} checked={draft.shelvingAllowed !== false} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, shelvingAllowed: value }))} />
                <TextAreaField label={text('editor.field.message')} value={draft.message ?? ''} onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, message: emptyToNull(value) }))} />
              </div>
              <MutationActions changed={changed} mutation={mutation} onReset={reset} onPreview={() => void preview()} locale={locale} />
              <PreviewPanel mutation={mutation} locale={locale} />
            </>
          )}
        </section>
      </div>
    </EditorShell>
  );
}

function useSecuredMutation(model: EngineeringPackageView, locale: EngineeringLocale): MutationState {
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<EngineeringPackageView | null>(null);
  const [validatedChangeVersion, setValidatedChangeVersion] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);

  const invalidate = useCallback(() => {
    setPreview(null);
    setCandidate(null);
    setValidatedChangeVersion(null);
    setError(null);
  }, []);

  const validate = useCallback(async (nextCandidate: EngineeringPackageView) => {
    setPreviewing(true);
    setError(null);
    setPreview(null);
    setCandidate(null);
    setValidatedChangeVersion(null);
    try {
      const before = await loadEngineeringWorkspace();
      const nextPreview = await previewEngineeringPackage(nextCandidate);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion) {
        throw new Error(mutationText(locale).workspaceChanged);
      }
      setPreview(nextPreview);
      setCandidate(clone(nextCandidate));
      setValidatedChangeVersion(after.changeVersion);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setPreviewing(false);
    }
  }, [locale]);

  const apply = useCallback(async () => {
    if (!candidate || !preview?.canApply || validatedChangeVersion === null) return;
    setApplying(true);
    setError(null);
    try {
      await applyEngineeringPackage(candidate, validatedChangeVersion);
      window.location.reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setPreview(null);
      setCandidate(null);
      setValidatedChangeVersion(null);
    } finally {
      setApplying(false);
    }
  }, [candidate, preview, validatedChangeVersion]);

  // A new official model invalidates any candidate retained by a mounted editor instance.
  useEffect(() => invalidate(), [model]);

  return {
    preview,
    error,
    previewing,
    applying,
    canApply: Boolean(preview?.canApply && candidate && validatedChangeVersion !== null),
    validate,
    apply,
    invalidate
  };
}

function MutationActions({
  changed,
  mutation,
  onReset,
  onPreview,
  locale
}: {
  changed: boolean;
  mutation: MutationState;
  onReset: () => void;
  onPreview: () => void;
  locale: EngineeringLocale;
}) {
  const text = editorTranslator(locale);
  const extra = mutationText(locale);
  return (
    <div className="eng-editor-actions">
      <button type="button" className="secondary" onClick={onReset} disabled={!changed || mutation.previewing || mutation.applying}>
        {text('editor.reset')}
      </button>
      <button type="button" className="secondary" onClick={onPreview} disabled={!changed || mutation.previewing || mutation.applying} data-testid="engineering-preview">
        {mutation.previewing ? text('editor.previewing') : text('editor.preview')}
      </button>
      <button type="button" className="primary" onClick={() => void mutation.apply()} disabled={!changed || !mutation.canApply || mutation.previewing || mutation.applying} data-testid="engineering-apply">
        {mutation.applying ? extra.applying : extra.apply}
      </button>
    </div>
  );
}

function PreviewPanel({ mutation, locale }: { mutation: MutationState; locale: EngineeringLocale }) {
  const text = editorTranslator(locale);
  const preview = mutation.preview;
  const issues = preview?.items.flatMap(item => item.issues ?? []) ?? [];
  return (
    <section className="eng-preview-panel" aria-live="polite">
      <header>
        <div>
          <span>{text('editor.validation')}</span>
          <strong className={preview ? (preview.canApply ? 'valid' : 'invalid') : ''}>
            {mutation.error
              ? text('editor.previewFailed')
              : preview
                ? (preview.canApply ? text('editor.valid') : text('editor.invalid'))
                : text('editor.notValidated')}
          </strong>
        </div>
        {preview && (
          <div className="eng-preview-counts">
            <span><b>{preview.errorCount}</b> {text('editor.errors')}</span>
            <span data-testid="preview-create-count"><b>{preview.createCount}</b> {text('editor.creates')}</span>
            <span><b>{preview.updateCount}</b> {text('editor.updates')}</span>
            <span><b>{preview.skipCount}</b> {text('editor.skips')}</span>
          </div>
        )}
      </header>
      {mutation.error && <pre className="eng-preview-error">{mutation.error}</pre>}
      {issues.length > 0 && (
        <div className="eng-preview-issues">
          {issues.map((issue, index) => (
            <div className={issue.isError ? 'error' : 'warning'} key={`${issue.code}-${issue.entityKey}-${index}`}>
              <strong>{issue.code}</strong><span>{issue.message}</span><small>{text('editor.issueEntity')}: {issue.entityKey}</small>
            </div>
          ))}
        </div>
      )}
      <footer>{text('editor.workspaceUntouched')}</footer>
    </section>
  );
}

function EditorShell({ title, description, locale, children }: { title: string; description: string; locale: EngineeringLocale; children: React.ReactNode }) {
  const extra = mutationText(locale);
  return (
    <div className="eng-section eng-editor-section">
      <header className="eng-editor-header">
        <div><span className="eng-editor-eyebrow">{extra.editing}</span><h1>{title}</h1><p>{description}</p></div>
        <div className="eng-editor-safety-note"><strong>{extra.previewGate}</strong><span>{extra.previewGateHint}</span></div>
      </header>
      {children}
    </div>
  );
}

function EntityPicker({ label, query, onQuery, searchLabel, emptyLabel, actionLabel, actionActive = false, onAction, children }: {
  label: string; query: string; onQuery: (value: string) => void; searchLabel: string; emptyLabel: string;
  actionLabel?: string; actionActive?: boolean; onAction?: () => void; children: React.ReactNode;
}) {
  const hasChildren = React.Children.count(children) > 0;
  return (
    <aside className="eng-editor-picker">
      <header>
        <div className="eng-editor-picker-title">
          <strong>{label}</strong>
          {actionLabel && onAction && <button type="button" className={actionActive ? 'active' : ''} onClick={onAction}>+ {actionLabel}</button>}
        </div>
        <input type="search" aria-label={searchLabel} placeholder={searchLabel} value={query} onChange={event => onQuery(event.target.value)} />
      </header>
      <div className="eng-editor-picker-list">{hasChildren ? children : <span className="eng-editor-picker-empty">{emptyLabel}</span>}</div>
    </aside>
  );
}

function EditorStatus<T>({ original, draft, changed, isNew, locale }: { original: T | null; draft: T; changed: boolean; isNew: boolean; locale: EngineeringLocale }) {
  const text = editorTranslator(locale);
  return (
    <div className="eng-editor-status">
      <div><span>{text('editor.draft')}</span><strong>{isNew ? text('editor.new') : changed ? text('editor.changed') : text('editor.original')}</strong></div>
      <small>{isNew ? '+' : changed && original ? diffCount(original, draft) : 0}</small>
    </div>
  );
}

function DictionaryEditor({ title, hint, value, keyLabel, valueLabel, addLabel, removeLabel, onChange }: {
  title: string; hint: string; value: Record<string, string>; keyLabel: string; valueLabel: string;
  addLabel: string; removeLabel: string; onChange: (next: Record<string, string>) => void;
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
      {entries.length === 0 ? <span className="eng-readonly-empty">—</span> : entries.map(([key, reference]) => <div key={key}><code>{key}</code><span>→</span><code>{reference}</code></div>)}
    </section>
  );
}

function TextField({ label, value, onChange, mono = false }: { label: string; value: string; onChange: (value: string) => void; mono?: boolean }) {
  return <label className="eng-editor-field"><span>{label}</span><input className={mono ? 'mono' : ''} value={value} onChange={event => onChange(event.target.value)} /></label>;
}

function TextAreaField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return <label className="eng-editor-field eng-editor-field-wide"><span>{label}</span><textarea rows={3} value={value} onChange={event => onChange(event.target.value)} /></label>;
}

function SelectField({ label, value, options, onChange }: { label: string; value: string; options: string[]; onChange: (value: string) => void }) {
  const effective = options.includes(value) ? options : [value, ...options];
  return <label className="eng-editor-field"><span>{label}</span><select value={value} onChange={event => onChange(event.target.value)}>{effective.map(option => <option key={option} value={option}>{option}</option>)}</select></label>;
}

function NumberField({ label, value, onChange, integer = false }: { label: string; value?: number | null; onChange: (value: number | null) => void; integer?: boolean }) {
  return <label className="eng-editor-field"><span>{label}</span><input type="number" step={integer ? 1 : 'any'} value={value ?? ''} onChange={event => onChange(parseNullableNumber(event.target.value, integer))} /></label>;
}

function BooleanField({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return <label className="eng-editor-check"><input type="checkbox" checked={checked} onChange={event => onChange(event.target.checked)} /><span>{label}</span></label>;
}

function useBeforeUnload(changed: boolean) {
  useEffect(() => {
    if (!changed) return undefined;
    const onBeforeUnload = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ''; };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [changed]);
}

function newTagDraft(): TagEngineering {
  return { name: '', path: '', dataType: 'double', readOnly: true, historian: { enabled: false, strategy: 'none' } };
}

function newDataSourceDraft(): DataSourceEngineering {
  return { key: '', name: '', driver: '', enabled: true, settings: {}, secretReferences: {} };
}

function newAlarmDraft(): AlarmEngineering {
  return {
    name: '', tagPath: '', type: 'high', priority: 'medium', setpoint: null,
    digitalActiveValue: true, activationDelayMilliseconds: null,
    requiresAcknowledgement: true, shelvingAllowed: true, enabled: true
  };
}

function updateTag(setter: React.Dispatch<React.SetStateAction<TagEngineering | null>>, update: (current: TagEngineering) => TagEngineering) {
  setter(current => current ? update(current) : current);
}
function updateDataSource(setter: React.Dispatch<React.SetStateAction<DataSourceEngineering | null>>, update: (current: DataSourceEngineering) => DataSourceEngineering) {
  setter(current => current ? update(current) : current);
}
function updateAlarm(setter: React.Dispatch<React.SetStateAction<AlarmEngineering | null>>, update: (current: AlarmEngineering) => AlarmEngineering) {
  setter(current => current ? update(current) : current);
}

function tagIdentity(tag: TagEngineering) { return tag.id ? `id:${tag.id}` : `path:${tag.path}`; }
function dataSourceIdentity(source: DataSourceEngineering) { return source.id ? `id:${source.id}` : `key:${source.key}`; }
function alarmIdentity(alarm: AlarmEngineering) { return alarm.id ? `id:${alarm.id}` : `key:${alarm.name}:${alarm.tagPath ?? alarm.tagId ?? ''}`; }
function clone<T>(value: T): T { return JSON.parse(JSON.stringify(value)) as T; }
function emptyToNull(value: string): string | null { return value.trim().length === 0 ? null : value; }
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
  for (const key of keys) if (JSON.stringify(original[key]) !== JSON.stringify(draft[key])) count++;
  return count;
}
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value); }

function mutationText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    editing: 'Secured Engineering editing',
    previewGate: 'Preview required before Apply',
    previewGateHint: 'Apply uses the public Engineering import pipeline and is available only after a valid preview. A changed Workspace invalidates the candidate.',
    apply: 'Apply to Workspace', applying: 'Applying...',
    workspaceChanged: 'The Engineering Workspace changed while this draft was being validated. Reload and validate again.'
  };
  if (locale === 'es') return {
    editing: 'Edición segura de Ingeniería',
    previewGate: 'Preview obligatorio antes de Aplicar',
    previewGateHint: 'Aplicar usa el pipeline público de Engineering y solo se habilita después de un preview válido. Un cambio del Workspace invalida el candidato.',
    apply: 'Aplicar al Workspace', applying: 'Aplicando...',
    workspaceChanged: 'El Engineering Workspace cambió durante la validación. Recargue y valide nuevamente.'
  };
  return {
    editing: 'Edição segura de Engenharia',
    previewGate: 'Preview obrigatório antes do Apply',
    previewGateHint: 'O Apply usa o pipeline público de Engineering e só é habilitado após preview válido. Qualquer mudança no Workspace invalida o candidato.',
    apply: 'Aplicar ao Workspace', applying: 'Aplicando...',
    workspaceChanged: 'O Engineering Workspace mudou durante a validação deste rascunho. Recarregue e valide novamente.'
  };
}
