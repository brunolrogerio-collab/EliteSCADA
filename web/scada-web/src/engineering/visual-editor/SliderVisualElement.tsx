import React, { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import type { VisualElementEngineering } from '../types';
import type { VisualPropertyValue } from '../../visual-runtime';
import type { VisualDynamicDiagnostic, VisualDynamicSample } from './visualDynamicRuntime';
import { quantizeAndClamp, resolveSliderConfiguration } from './sliderVisualModel';
import './SliderVisualElement.css';

export type SliderTagWrite = (tagId: string, value: number) => Promise<void>;

export type SliderVisualElementProps = Readonly<{
  element: VisualElementEngineering;
  values: Readonly<Record<string, VisualPropertyValue>>;
  diagnostics: readonly VisualDynamicDiagnostic[];
  liveSamples: ReadonlyMap<string, VisualDynamicSample>;
  style: CSSProperties;
  runtimeObjectId?: string;
  title?: string;
  onTagWrite?: SliderTagWrite;
}>;

export function SliderVisualElement({
  element,
  values,
  diagnostics,
  liveSamples,
  style,
  runtimeObjectId,
  title,
  onTagWrite
}: SliderVisualElementProps) {
  const config = useMemo(
    () => resolveSliderConfiguration(element, values, diagnostics, liveSamples),
    [element, values, diagnostics, liveSamples]
  );
  const [draftValue, setDraftValue] = useState(config.value);
  const [pending, setPending] = useState(false);
  const [writeError, setWriteError] = useState<string | null>(null);
  const lastRequested = useRef<number | null>(null);

  useEffect(() => {
    if (!pending) setDraftValue(config.value);
  }, [config.value, pending]);

  const canWrite = config.interactionEnabled &&
    Boolean(onTagWrite) &&
    Boolean(config.tagId) &&
    config.writeDirection &&
    config.sourceAvailable &&
    !config.sourceReadOnly &&
    !pending;

  const commit = async (candidate: number) => {
    if (!canWrite || !config.tagId || !onTagWrite) return;
    const next = quantizeAndClamp(candidate, config.minimum, config.maximum, config.step);
    if (lastRequested.current === next && next === config.value) return;
    lastRequested.current = next;
    setDraftValue(next);
    setPending(true);
    setWriteError(null);
    try {
      await onTagWrite(config.tagId, next);
    } catch (reason) {
      setWriteError(reason instanceof Error ? reason.message : String(reason));
      setDraftValue(config.value);
    } finally {
      setPending(false);
    }
  };

  const unavailable = !config.sourceAvailable;
  const effectiveTitle = [title, writeError].filter(Boolean).join('\n') || undefined;
  const cssVariables = {
    '--slider-track-color': config.trackColor,
    '--slider-thumb-color': config.thumbColor
  } as CSSProperties;
  const inputStyle: CSSProperties = config.orientation === 'vertical'
    ? {
        writingMode: 'vertical-lr',
        direction: config.reverseDirection ? 'ltr' : 'rtl'
      }
    : { direction: config.reverseDirection ? 'rtl' : 'ltr' };

  return <div
    className={`visual-editor-object visual-editor-slider visual-editor-slider--${config.orientation}${unavailable ? ' visual-editor-slider--unavailable' : ''}`}
    style={{ ...style, ...cssVariables }}
    data-object-id={element.id ?? undefined}
    data-runtime-object-id={runtimeObjectId}
    data-slider-mode={config.interactionEnabled ? 'adjust' : 'passive'}
    data-slider-write-state={writeError ? 'failed' : pending ? 'pending' : canWrite ? 'ready' : 'disabled'}
    data-dynamic-state={unavailable ? 'unavailable' : 'available'}
    title={effectiveTitle}
  >
    <input
      aria-label={element.key}
      type="range"
      min={config.minimum}
      max={config.maximum}
      step={config.step}
      value={draftValue}
      disabled={!canWrite}
      aria-readonly={!canWrite}
      aria-invalid={unavailable || Boolean(writeError)}
      style={inputStyle}
      onChange={event => setDraftValue(Number(event.currentTarget.value))}
      onPointerUp={event => void commit(Number(event.currentTarget.value))}
      onKeyUp={event => {
        if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown'].includes(event.key)) {
          void commit(Number(event.currentTarget.value));
        }
      }}
      onBlur={event => void commit(Number(event.currentTarget.value))}
    />
    <output aria-live="polite">{unavailable ? '—' : formatSliderValue(draftValue, config.step)}</output>
    {writeError ? <span className="visual-editor-slider__error" role="alert">!</span> : null}
  </div>;
}

function formatSliderValue(value: number, step: number): string {
  const decimals = Math.min(12, Math.max(0, (String(step).split('.')[1] ?? '').length));
  return value.toLocaleString(undefined, { maximumFractionDigits: decimals, useGrouping: false });
}
