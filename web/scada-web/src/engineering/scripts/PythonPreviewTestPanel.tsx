import React, { useMemo, useRef, useState } from 'react';
import { runEngineeringClientVisualPythonHandler } from '../../python-runtime/engineeringPythonPreview';
import type { EngineeringLocale } from '../i18n';
import type { PythonEditorDiagnosticSnapshot } from '../python-editor/pythonEditorDiagnostics';
import { scriptPythonPreviewCopy } from './scriptPythonPreviewCopy';
import {
  firstBlockingPythonDiagnostic,
  parsePythonPreviewSample,
  projectPythonPreviewExecution,
  sourceLineForPreview,
  type PythonPreviewProjection,
  type PythonPreviewState
} from './pythonPreviewModel';
import './python-preview-test.css';

type PythonPreviewTestPanelProps = {
  locale: EngineeringLocale;
  scriptId: string;
  source: string;
  handlerNames: readonly string[];
  disabled?: boolean;
  onDiagnostics(snapshot: PythonEditorDiagnosticSnapshot): void;
};

export function PythonPreviewTestPanel({
  locale,
  scriptId,
  source,
  handlerNames,
  disabled = false,
  onDiagnostics
}: PythonPreviewTestPanelProps) {
  const copy = useMemo(() => scriptPythonPreviewCopy(locale), [locale]);
  const [handlerName, setHandlerName] = useState(handlerNames[0] ?? '');
  const [sample, setSample] = useState('{\n  "preview": true\n}');
  const [state, setState] = useState<PythonPreviewState>('idle');
  const [result, setResult] = useState<PythonPreviewProjection | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const selectedHandler = handlerNames.includes(handlerName) ? handlerName : handlerNames[0] ?? '';
  const running = state === 'running';

  async function run() {
    if (!selectedHandler || disabled || running) return;
    const parsed = parsePythonPreviewSample(sample);
    if (!parsed.ok) {
      setState('validation-error');
      setResult(null);
      setValidationMessage(copy.sampleErrors[parsed.error]);
      return;
    }

    const abort = new AbortController();
    abortRef.current = abort;
    setState('running');
    setResult(null);
    setValidationMessage(null);

    try {
      const execution = await runEngineeringClientVisualPythonHandler({
        scriptId,
        source,
        handlerNames,
        handlerName: selectedHandler,
        payload: parsed.value,
        signal: abort.signal
      });
      onDiagnostics(execution.diagnostics);

      const diagnostic = firstBlockingPythonDiagnostic(execution.diagnostics.diagnostics);
      if (diagnostic) {
        setState('validation-error');
        setValidationMessage(`${diagnostic.code}: ${diagnostic.message} (${copy.line} ${diagnostic.line})`);
        return;
      }

      const projected = projectPythonPreviewExecution(
        selectedHandler,
        execution.status,
        execution.durationMs,
        execution.sanitizedError
      );
      setResult(projected);
      setState(projected.state);
    } catch {
      setState('unavailable');
      setValidationMessage(copy.unavailable);
    } finally {
      if (abortRef.current === abort) abortRef.current = null;
    }
  }

  function cancel() {
    abortRef.current?.abort();
  }

  const failingSourceLine = result?.trace
    ? sourceLineForPreview(source, result.trace.failingLine)
    : undefined;

  return (
    <section className="python-preview-test" data-testid="python-preview-test" aria-label={copy.title}>
      <strong>{copy.title}</strong>
      <span>{copy.hint}</span>

      {handlerNames.length > 0 ? (
        <>
          <div className="python-preview-test__inputs">
            <label>{copy.handler}
              <select
                value={selectedHandler}
                onChange={event => {
                  setHandlerName(event.target.value);
                  setState('idle');
                  setResult(null);
                  setValidationMessage(null);
                }}
                disabled={disabled || running}
              >
                {handlerNames.map(handler => <option key={handler} value={handler}>{handler}</option>)}
              </select>
            </label>
            <label>{copy.samplePayload}
              <textarea
                rows={5}
                value={sample}
                onChange={event => {
                  setSample(event.target.value);
                  setState('idle');
                  setResult(null);
                  setValidationMessage(null);
                }}
                disabled={disabled || running}
                spellCheck={false}
                aria-label={copy.samplePayload}
              />
            </label>
          </div>

          <div className="python-preview-test__actions">
            <button type="button" className="secondary" onClick={() => void run()} disabled={disabled || running}>
              {running ? copy.running : copy.run}
            </button>
            {running && (
              <button type="button" className="secondary" onClick={cancel}>{copy.cancel}</button>
            )}
          </div>
        </>
      ) : <span className="python-preview-test__muted">{copy.noHandler}</span>}

      <div className="python-preview-test__result" data-testid="python-preview-result" data-state={state} aria-live="polite">
        <strong>{copy.states[state]}</strong>
        {validationMessage && <span>{validationMessage}</span>}
        {result?.durationMs !== undefined && <span>{copy.duration}: {result.durationMs.toFixed(3)} ms</span>}
        {result?.sanitizedError && <span>{copy.error}: {result.sanitizedError}</span>}
        {result?.trace && (
          <div>
            <strong>{copy.traceback}</strong>
            <ul>
              {result.trace.frames.map((frame, index) => (
                <li key={`${frame.functionName}-${frame.line}-${index}`}>
                  {frame.functionName} · {copy.line} {frame.line}
                </li>
              ))}
            </ul>
            {failingSourceLine !== undefined && (
              <code data-testid="python-preview-failing-line">{failingSourceLine || copy.blankLine}</code>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
