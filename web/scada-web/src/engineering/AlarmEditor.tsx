import React, { useEffect, useMemo, useState } from 'react';
import { previewEngineeringPackage } from './api';
import { editorTranslator } from './editorI18n';
import type { EngineeringLocale } from './i18n';
import type { AlarmEngineering, EngineeringPackageView, ImportPreviewView } from './types';
import './structured-editors.css';

const NEW_ALARM_IDENTITY = 'draft:new-alarm';
const alarmTypes = ['digital', 'high', 'highHigh', 'low', 'lowLow', 'communication', 'system'];
const alarmPriorities = ['low', 'medium', 'high', 'critical'];
const analogAlarmTypes = new Set(['high', 'highHigh', 'low', 'lowLow']);

type AlarmEditorProps = {
  model: EngineeringPackageView;
  locale: EngineeringLocale;
};

export function AlarmEditor({ model, locale }: AlarmEditorProps) {
  const text = useMemo(() => editorTranslator(locale), [locale]);
  const alarms = model.alarms;
  const [query, setQuery] = useState('');
  const [selectedIdentity, setSelectedIdentity] = useState<string | null>(() =>
    alarms[0] ? alarmIdentity(alarms[0]) : null);
  const isNew = selectedIdentity === NEW_ALARM_IDENTITY;
  const selected = !isNew && selectedIdentity
    ? alarms.find(alarm => alarmIdentity(alarm) === selectedIdentity) ?? null
    : null;
  const [draft, setDraft] = useState<AlarmEngineering | null>(() => selected ? clone(selected) : null);
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);

  useEffect(() => {
    if (selectedIdentity === NEW_ALARM_IDENTITY) {
      setDraft(newAlarmDraft());
      setPreview(null);
      setPreviewError(null);
      return;
    }

    const current = selectedIdentity
      ? alarms.find(alarm => alarmIdentity(alarm) === selectedIdentity) ?? null
      : null;

    if (current) {
      setDraft(clone(current));
      setPreview(null);
      setPreviewError(null);
      return;
    }

    if (alarms[0]) {
      setSelectedIdentity(alarmIdentity(alarms[0]));
      return;
    }

    setDraft(null);
    setPreview(null);
    setPreviewError(null);
  }, [selectedIdentity, alarms]);

  useEffect(() => {
    setPreview(null);
    setPreviewError(null);
  }, [draft]);

  const filtered = alarms.filter(alarm => {
    const haystack = `${alarm.name} ${alarm.tagPath ?? alarm.tagId ?? ''} ${alarm.type} ${alarm.priority} ${alarm.area ?? ''} ${alarm.alarmClass ?? ''}`.toLowerCase();
    return haystack.includes(query.trim().toLowerCase());
  });

  const changed = draft
    ? isNew
      ? JSON.stringify(draft) !== JSON.stringify(newAlarmDraft())
      : selected
        ? JSON.stringify(selected) !== JSON.stringify(draft)
        : false
    : false;

  useBeforeUnload(changed);

  const chooseIdentity = (identity: string) => {
    if (identity === selectedIdentity) return;
    if (changed && !window.confirm(text('editor.discardConfirm'))) return;
    setSelectedIdentity(identity);
  };

  const reset = () => {
    if (isNew) setDraft(newAlarmDraft());
    else if (selected) setDraft(clone(selected));
    setPreview(null);
    setPreviewError(null);
  };

  const runPreview = async () => {
    if (!draft) return;
    if (!isNew && !selected) return;

    setPreviewing(true);
    setPreview(null);
    setPreviewError(null);
    try {
      const candidate = clone(model);
      if (isNew) {
        candidate.alarms = [...candidate.alarms, clone(draft)];
      } else if (selected) {
        const identity = alarmIdentity(selected);
        candidate.alarms = candidate.alarms.map(alarm =>
          alarmIdentity(alarm) === identity ? clone(draft) : alarm);
      }
      setPreview(await previewEngineeringPackage(candidate));
    } catch (reason) {
      setPreviewError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setPreviewing(false);
    }
  };

  const setAlarmType = (type: string) => {
    updateAlarm(setDraft, alarm => ({
      ...alarm,
      type,
      setpoint: analogAlarmTypes.has(type) ? alarm.setpoint : null,
      digitalActiveValue: type === 'digital' ? (alarm.digitalActiveValue ?? true) : alarm.digitalActiveValue
    }));
  };

  return (
    <div className="eng-section eng-editor-section">
      <header className="eng-editor-header">
        <div>
          <span className="eng-editor-eyebrow">{text('editor.previewOnly')}</span>
          <h1>{text('editor.alarmsTitle')}</h1>
          <p>{text('editor.alarmsDescription')}</p>
        </div>
        <div className="eng-editor-safety-note">
          <strong>{text('editor.previewOnly')}</strong>
          <span>{text('editor.previewOnlyHint')}</span>
        </div>
      </header>

      <div className="eng-editor-layout">
        <aside className="eng-editor-picker">
          <header>
            <div className="eng-editor-picker-title">
              <strong>Alarmes</strong>
              <button
                type="button"
                className={isNew ? 'active' : ''}
                onClick={() => chooseIdentity(NEW_ALARM_IDENTITY)}
              >
                + {text('editor.newAlarm')}
              </button>
            </div>
            <input
              type="search"
              aria-label={text('editor.search')}
              placeholder={text('editor.search')}
              value={query}
              onChange={event => setQuery(event.target.value)}
            />
          </header>

          <div className="eng-editor-picker-list">
            {filtered.length === 0 ? (
              <span className="eng-editor-picker-empty">{text('editor.noResults')}</span>
            ) : filtered.map(alarm => {
              const identity = alarmIdentity(alarm);
              return (
                <button
                  type="button"
                  className={identity === selectedIdentity ? 'selected' : ''}
                  key={identity}
                  onClick={() => chooseIdentity(identity)}
                >
                  <strong>{alarm.name}</strong>
                  <code>{alarm.tagPath ?? alarm.tagId ?? '—'}</code>
                  <span>{alarm.type} · {alarm.priority}</span>
                </button>
              );
            })}
          </div>
        </aside>

        <section className="eng-editor-form-panel">
          {!draft || (!isNew && !selected) ? (
            <div className="eng-editor-empty">{text('editor.noSelection')}</div>
          ) : (
            <>
              <div className="eng-editor-status">
                <div>
                  <span>{text('editor.draft')}</span>
                  <strong>{isNew ? text('editor.new') : changed ? text('editor.changed') : text('editor.original')}</strong>
                </div>
                <small>{isNew ? '+' : changed && selected ? diffCount(selected, draft) : 0}</small>
              </div>

              <div className="eng-editor-form-grid">
                <TextField
                  label={text('editor.field.name')}
                  value={draft.name}
                  onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, name: value }))}
                />
                <TextField
                  label={text('editor.field.tagPath')}
                  value={draft.tagPath ?? ''}
                  mono
                  onChange={value => updateAlarm(setDraft, alarm => ({
                    ...alarm,
                    tagId: null,
                    tagPath: emptyToNull(value)
                  }))}
                />
                <SelectField
                  label={text('editor.field.alarmType')}
                  value={draft.type}
                  options={alarmTypes}
                  onChange={setAlarmType}
                />
                <SelectField
                  label={text('editor.field.priority')}
                  value={draft.priority}
                  options={alarmPriorities}
                  onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, priority: value }))}
                />

                {analogAlarmTypes.has(draft.type) && (
                  <NumberField
                    label={text('editor.field.setpoint')}
                    value={draft.setpoint}
                    onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, setpoint: value }))}
                  />
                )}

                {draft.type === 'digital' && (
                  <BooleanField
                    label={text('editor.field.digitalActiveValue')}
                    checked={draft.digitalActiveValue !== false}
                    onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, digitalActiveValue: value }))}
                  />
                )}

                <TextField
                  label={text('editor.field.alarmClass')}
                  value={draft.alarmClass ?? ''}
                  onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, alarmClass: emptyToNull(value) }))}
                />
                <TextField
                  label={text('editor.field.area')}
                  value={draft.area ?? ''}
                  onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, area: emptyToNull(value) }))}
                />
                <NumberField
                  label={text('editor.field.activationDelay')}
                  value={draft.activationDelayMilliseconds}
                  integer
                  onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, activationDelayMilliseconds: value }))}
                />
                <BooleanField
                  label={text('editor.field.enabled')}
                  checked={draft.enabled !== false}
                  onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, enabled: value }))}
                />
                <BooleanField
                  label={text('editor.field.requiresAcknowledgement')}
                  checked={draft.requiresAcknowledgement !== false}
                  onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, requiresAcknowledgement: value }))}
                />
                <BooleanField
                  label={text('editor.field.shelvingAllowed')}
                  checked={draft.shelvingAllowed !== false}
                  onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, shelvingAllowed: value }))}
                />
                <TextAreaField
                  label={text('editor.field.message')}
                  value={draft.message ?? ''}
                  onChange={value => updateAlarm(setDraft, alarm => ({ ...alarm, message: emptyToNull(value) }))}
                />
              </div>

              <div className="eng-editor-actions">
                <button type="button" className="secondary" onClick={reset} disabled={!changed || previewing}>
                  {text('editor.reset')}
                </button>
                <button type="button" className="primary" onClick={() => void runPreview()} disabled={previewing}>
                  {previewing ? text('editor.previewing') : text('editor.preview')}
                </button>
              </div>

              <AlarmPreviewPanel preview={preview} error={previewError} locale={locale} />
            </>
          )}
        </section>
      </div>
    </div>
  );
}

function AlarmPreviewPanel({
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
            <span data-testid="preview-create-count"><b>{preview.createCount}</b> {text('editor.creates')}</span>
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

function BooleanField({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="eng-editor-check">
      <input type="checkbox" checked={checked} onChange={event => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

function useBeforeUnload(changed: boolean) {
  useEffect(() => {
    if (!changed) return undefined;

    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [changed]);
}

function newAlarmDraft(): AlarmEngineering {
  return {
    name: '',
    tagPath: '',
    type: 'high',
    priority: 'medium',
    setpoint: null,
    digitalActiveValue: true,
    activationDelayMilliseconds: null,
    requiresAcknowledgement: true,
    shelvingAllowed: true,
    enabled: true
  };
}

function updateAlarm(
  setter: React.Dispatch<React.SetStateAction<AlarmEngineering | null>>,
  update: (current: AlarmEngineering) => AlarmEngineering
) {
  setter(current => current ? update(current) : current);
}

function alarmIdentity(alarm: AlarmEngineering) {
  if (alarm.id) return `id:${alarm.id}`;
  return `alarm:${alarm.name}|tag:${alarm.tagPath ?? alarm.tagId ?? ''}`;
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
