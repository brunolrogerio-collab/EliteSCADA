import React, { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import type { VisualElementEngineering } from '../types';
import type { EngineeringLocale } from '../i18n';
import { VISUAL_PROPERTY_KEYS, type VisualPropertyValue } from '../../visual-runtime';
import type { VisualDynamicDiagnostic, VisualDynamicSample } from './visualDynamicRuntime';
import {
  formatNumericInputValue,
  resolveNumericInputConfiguration,
  validateNumericInputCandidate
} from './numericInputVisualModel';
import type { SliderTagWrite } from './SliderVisualElement';
import './NumericInputVisualElement.css';

export type NumericInputVisualElementProps = Readonly<{
  element: VisualElementEngineering;
  values: Readonly<Record<string, VisualPropertyValue>>;
  diagnostics: readonly VisualDynamicDiagnostic[];
  liveSamples: ReadonlyMap<string, VisualDynamicSample>;
  style: CSSProperties;
  runtimeObjectId?: string;
  locale?: EngineeringLocale;
  title?: string;
  onTagWrite?: SliderTagWrite;
}>;

export function NumericInputVisualElement({
  element,
  values,
  diagnostics,
  liveSamples,
  style,
  runtimeObjectId,
  locale = 'pt-BR',
  title,
  onTagWrite
}: NumericInputVisualElementProps) {
  const config = useMemo(
    () => resolveNumericInputConfiguration(element, values, diagnostics, liveSamples),
    [element, values, diagnostics, liveSamples]
  );
  const enabled = values[VISUAL_PROPERTY_KEYS.enabled] !== false;
  const showApplyButton = values[VISUAL_PROPERTY_KEYS.showApplyButton] !== false;
  const showCancelButton = values[VISUAL_PROPERTY_KEYS.showCancelButton] !== false;
  const showSteppers = values[VISUAL_PROPERTY_KEYS.showSteppers] !== false;
  const [draft, setDraft] = useState(() => formatNumericInputValue(config.value, config.precision));
  const [editing, setEditing] = useState(false);
  const [pending, setPending] = useState(false);
  const [awaitingReadback, setAwaitingReadback] = useState(false);
  const [readbackStart, setReadbackStart] = useState<string | null>(null);
  const [writeError, setWriteError] = useState<string | null>(null);
  const [unauthorized, setUnauthorized] = useState(false);
  const authoritative = formatNumericInputValue(config.value, config.precision);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (awaitingReadback && config.sampleTimestamp !== readbackStart) {
      setAwaitingReadback(false);
      setReadbackStart(null);
      setDraft(authoritative);
      setEditing(false);
      return;
    }
    if (!editing && !pending && !awaitingReadback) setDraft(authoritative);
  }, [authoritative, awaitingReadback, config.sampleTimestamp, editing, pending, readbackStart]);

  const runtimeWriteAvailable = Boolean(onTagWrite);
  const canWrite = enabled &&
    config.interactionEnabled &&
    runtimeWriteAvailable &&
    Boolean(config.tagId) &&
    config.writeDirection &&
    config.sourceAvailable &&
    !config.sourceReadOnly &&
    !pending &&
    !awaitingReadback;

  const state = !runtimeWriteAvailable ? 'design'
    : unauthorized ? 'unauthorized'
    : writeError ? 'write-failed'
    : pending ? 'pending'
    : awaitingReadback ? 'readback-pending'
    : !config.sourceAvailable ? 'bad-quality'
    : config.sourceReadOnly || !config.writeDirection ? 'read-only'
    : editing ? 'editing'
    : canWrite ? 'ready'
    : 'disabled';

  const cancel = () => {
    if (pending || awaitingReadback) return;
    setDraft(authoritative);
    setEditing(false);
    setWriteError(null);
    setUnauthorized(false);
  };

  const commit = async () => {
    if (!canWrite || !config.tagId || !onTagWrite) return;
    let candidate: number;
    try {
      candidate = validateNumericInputCandidate(draft, config.minimum, config.maximum, config.step);
    } catch (reason) {
      setWriteError(reason instanceof Error ? reason.message : String(reason));
      return;
    }

    setPending(true);
    setWriteError(null);
    setUnauthorized(false);
    try {
      const startedAt = config.sampleTimestamp;
      await onTagWrite(config.tagId, candidate);
      setReadbackStart(startedAt);
      setAwaitingReadback(true);
      setDraft(authoritative);
    } catch (reason) {
      const status = reason && typeof reason === 'object' && 'status' in reason
        ? Number((reason as { status?: unknown }).status)
        : undefined;
      setUnauthorized(status === 401 || status === 403);
      setWriteError(reason instanceof Error ? reason.message : String(reason));
      setDraft(authoritative);
      setEditing(false);
    } finally {
      setPending(false);
    }
  };

  const effectiveTitle = [title, writeError].filter(Boolean).join('\n') || undefined;
  const statusText = stateLabel(state, config.unit);

  return <div
    className={`visual-editor-object visual-editor-numeric-input visual-editor-numeric-input--${state}`}
    style={style}
    data-object-id={element.id ?? undefined}
    data-runtime-object-id={runtimeObjectId}
    data-enabled={enabled}
    data-numeric-input-state={state}
    data-show-apply={showApplyButton}
    data-show-cancel={showCancelButton}
    data-show-steppers={showSteppers}
    data-dynamic-state={config.sourceAvailable ? 'available' : 'unavailable'}
    title={effectiveTitle}
    aria-label={`${element.key}: ${statusText}`}
  >
    <input
      ref={inputRef}
      aria-label={element.key}
      type="number"
      min={config.minimum}
      max={config.maximum}
      step={config.step}
      value={draft}
      readOnly={!canWrite}
      aria-readonly={!canWrite}
      aria-invalid={!config.sourceAvailable || Boolean(writeError)}
      onFocus={() => { if (canWrite) { setEditing(true); setWriteError(null); setUnauthorized(false); } }}
      onChange={event => { if (canWrite) { setEditing(true); setDraft(event.currentTarget.value); } }}
      onKeyDown={event => {
        if (event.key === 'Enter') {
          event.preventDefault();
          void commit();
        } else if (event.key === 'Escape') {
          event.preventDefault();
          cancel();
          inputRef.current?.blur();
        }
      }}
    />
    {config.unit ? <span className="visual-editor-numeric-input__unit">{' '}{config.unit}</span> : null}
    {showApplyButton ? <button
      type="button"
      className="visual-editor-numeric-input__apply"
      aria-label={applyLabel(locale)}
      title={applyLabel(locale)}
      aria-keyshortcuts="Enter"
      onClick={() => void commit()}
      disabled={!canWrite || !editing}
    >↵</button> : null}
    {showCancelButton ? <button
      type="button"
      className="visual-editor-numeric-input__cancel"
      data-testid="numeric-input-cancel"
      aria-label={cancelLabel(locale)}
      title={cancelLabel(locale)}
      onClick={cancel}
      disabled={!editing || pending || awaitingReadback}
    >×</button> : null}
    <span className="visual-editor-numeric-input__state" role={writeError ? 'alert' : 'status'} aria-live="polite">
      {statusText}
    </span>
  </div>;
}

function applyLabel(locale: EngineeringLocale): string {
  return locale === 'en' ? 'Apply value' : locale === 'es' ? 'Aplicar valor' : 'Aplicar valor';
}

function cancelLabel(locale: EngineeringLocale): string {
  return locale === 'en' ? 'Cancel editing' : locale === 'es' ? 'Cancelar edición' : 'Cancelar edição';
}

function stateLabel(state: string, unit: string): string {
  switch (state) {
    case 'design': return 'Design preview · no process write';
    case 'unauthorized': return 'Write not authorized';
    case 'write-failed': return 'Write failed';
    case 'pending': return 'Writing…';
    case 'readback-pending': return 'Waiting for authoritative readback…';
    case 'bad-quality': return 'Bad/unavailable quality';
    case 'read-only': return 'Read only';
    case 'editing': return unit ? `Editing · ${unit}` : 'Editing';
    case 'ready': return unit ? `Ready · ${unit}` : 'Ready';
    default: return 'Unavailable';
  }
}
