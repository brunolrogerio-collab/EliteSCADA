import React, { useEffect, useMemo, useRef, useState } from 'react';
import * as monaco from 'monaco-editor';
import EditorWorker from 'monaco-editor/editor/editor.worker?worker';
import type { EngineeringLocale } from '../i18n';
import type {
  ScriptEngineeringEntryPoint,
  ScriptEngineeringScope
} from '../scripts/scriptEngineeringTypes';
import {
  buildEntryPointCompletions,
  CLIENT_VISUAL_PYTHON_API_HELP
} from './pythonEditorDescriptors';
import {
  projectPythonDiagnostics,
  type PythonEditorDiagnosticState
} from './pythonEditorDiagnostics';
import { pythonEditorCopy } from './pythonEditorCopy';
import './python-editor.css';

type MonacoGlobal = typeof globalThis & {
  MonacoEnvironment?: {
    getWorker(moduleId: string, label: string): Worker;
  };
};

const monacoGlobal = globalThis as MonacoGlobal;
if (!monacoGlobal.MonacoEnvironment) {
  monacoGlobal.MonacoEnvironment = {
    getWorker: () => new EditorWorker()
  };
}

export type PythonMonacoEditorProps = {
  scriptId: string;
  path: string;
  source: string;
  scope: ScriptEngineeringScope;
  entryPoints: readonly ScriptEngineeringEntryPoint[];
  locale: EngineeringLocale;
  diagnostics: PythonEditorDiagnosticState;
  onSourceChange(source: string): void;
  readOnly?: boolean;
};

const MARKER_OWNER = 'elitescada-python';

export function PythonMonacoEditor({
  scriptId,
  path,
  source,
  scope,
  entryPoints,
  locale,
  diagnostics,
  onSourceChange,
  readOnly = false
}: PythonMonacoEditorProps) {
  const copy = useMemo(() => pythonEditorCopy(locale), [locale]);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const editorRef = useRef<monaco.editor.IStandaloneCodeEditor | null>(null);
  const modelRef = useRef<monaco.editor.ITextModel | null>(null);
  const onSourceChangeRef = useRef(onSourceChange);
  const entryPointsRef = useRef(entryPoints);
  const sourceRef = useRef(source);
  const [cursor, setCursor] = useState({ line: 1, column: 1 });
  const [editorError, setEditorError] = useState<string | null>(null);

  onSourceChangeRef.current = onSourceChange;
  entryPointsRef.current = entryPoints;
  sourceRef.current = source;

  const projection = useMemo(
    () => diagnostics.status === 'ready'
      ? projectPythonDiagnostics(diagnostics.diagnostics)
      : { markers: [], rejectedCount: 0 },
    [diagnostics]
  );

  const errorCount = diagnostics.status === 'ready'
    ? diagnostics.diagnostics.filter(item => item.severity === 'error').length
    : 0;
  const warningCount = diagnostics.status === 'ready'
    ? diagnostics.diagnostics.filter(item => item.severity === 'warning').length
    : 0;

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    setEditorError(null);
    const safeScriptId = scriptId.replace(/[^a-zA-Z0-9-]/g, '-') || 'draft';
    const uri = monaco.Uri.parse(`inmemory://elitescada/python/${safeScriptId}.py`);

    let model: monaco.editor.ITextModel | null = null;
    let editor: monaco.editor.IStandaloneCodeEditor | null = null;
    try {
      monaco.editor.getModel(uri)?.dispose();
      model = monaco.editor.createModel(sourceRef.current, 'python', uri);
      editor = monaco.editor.create(container, {
        model,
        automaticLayout: true,
        readOnly,
        lineNumbers: 'on',
        glyphMargin: true,
        folding: true,
        minimap: { enabled: false },
        scrollBeyondLastLine: false,
        tabSize: 4,
        insertSpaces: true,
        detectIndentation: false,
        renderWhitespace: 'selection',
        wordWrap: 'off',
        quickSuggestions: true,
        suggestOnTriggerCharacters: true,
        padding: { top: 10, bottom: 10 },
        fontSize: 13,
        lineHeight: 20
      });

      editorRef.current = editor;
      modelRef.current = model;

      const contentDisposable = editor.onDidChangeModelContent(() => {
        const next = model?.getValue() ?? '';
        if (next !== sourceRef.current) onSourceChangeRef.current(next);
      });
      const cursorDisposable = editor.onDidChangeCursorPosition(event => {
        setCursor({ line: event.position.lineNumber, column: event.position.column });
      });
      const completionDisposable = monaco.languages.registerCompletionItemProvider('python', {
        provideCompletionItems(currentModel, position) {
          if (currentModel.uri.toString() !== uri.toString()) return { suggestions: [] };
          const word = currentModel.getWordUntilPosition(position);
          const range: monaco.IRange = {
            startLineNumber: position.lineNumber,
            endLineNumber: position.lineNumber,
            startColumn: word.startColumn,
            endColumn: word.endColumn
          };
          return {
            suggestions: buildEntryPointCompletions(entryPointsRef.current).map(item => ({
              label: item.label,
              kind: monaco.languages.CompletionItemKind.Function,
              detail: item.detail,
              documentation: item.documentation,
              insertText: item.insertText,
              insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
              range
            }))
          };
        }
      });

      return () => {
        completionDisposable.dispose();
        cursorDisposable.dispose();
        contentDisposable.dispose();
        monaco.editor.setModelMarkers(model!, MARKER_OWNER, []);
        editor?.dispose();
        model?.dispose();
        editorRef.current = null;
        modelRef.current = null;
      };
    } catch (cause) {
      editor?.dispose();
      model?.dispose();
      editorRef.current = null;
      modelRef.current = null;
      setEditorError(cause instanceof Error ? cause.message : copy.editorUnavailable);
    }
  }, [scriptId]);

  useEffect(() => {
    const model = modelRef.current;
    if (!model || model.getValue() === source) return;
    model.setValue(source);
  }, [source]);

  useEffect(() => {
    editorRef.current?.updateOptions({ readOnly });
  }, [readOnly]);

  useEffect(() => {
    const model = modelRef.current;
    if (!model) return;
    monaco.editor.setModelMarkers(
      model,
      MARKER_OWNER,
      projection.markers.map(marker => ({
        severity: markerSeverity(marker.severity),
        code: marker.code,
        message: marker.message,
        startLineNumber: marker.startLineNumber,
        startColumn: marker.startColumn,
        endLineNumber: marker.endLineNumber,
        endColumn: marker.endColumn
      }))
    );
  }, [projection]);

  return (
    <section className="python-editor" aria-label={copy.editorLabel} data-testid="python-monaco-editor">
      <header className="python-editor__toolbar">
        <div>
          <strong>{copy.editorLabel}</strong>
          <span className="python-editor__path">{path}</span>
        </div>
        <div className="python-editor__position" aria-label={`Line ${cursor.line}, column ${cursor.column}`}>
          Ln {cursor.line} · Col {cursor.column}
        </div>
      </header>

      <div className="python-editor__context">
        <div className="python-editor__context-block">
          <strong>{copy.entryPointContext}</strong>
          <div className="python-editor__chips">
            {entryPoints.length === 0
              ? <span className="python-editor__muted">{copy.noEntryPoints}</span>
              : entryPoints.map((entryPoint, index) => (
                <code key={`${entryPoint.eventKind}-${entryPoint.handlerName}-${index}`}>
                  {entryPoint.eventKind}: {entryPoint.handlerName}
                </code>
              ))}
          </div>
        </div>
        <details className="python-editor__api-help">
          <summary>{copy.apiHelp}</summary>
          {scope === 'clientVisual' ? (
            <>
              <p>{copy.apiHelpHint}</p>
              <ul>
                {CLIENT_VISUAL_PYTHON_API_HELP.map(item => (
                  <li key={item.capability}>
                    <code>{item.capability}</code>
                    <span><strong>{item.title}</strong> · {item.summary}</span>
                  </li>
                ))}
              </ul>
            </>
          ) : <p>{copy.serverScopeHint}</p>}
        </details>
      </div>

      <div ref={containerRef} className="python-editor__monaco" />
      {editorError && <div className="python-editor__error" role="alert">{copy.editorUnavailable}: {editorError}</div>}

      <footer className="python-editor__status">
        <span>{copy.sourceAuthority}</span>
        {diagnostics.status === 'ready' ? (
          <strong className={errorCount > 0 ? 'python-editor__diagnostic-error' : ''}>
            {copy.diagnosticsReady}: {errorCount} {copy.errors}, {warningCount} {copy.warnings}
            {projection.rejectedCount > 0 ? ` · ${projection.rejectedCount} ${copy.diagnosticsRejected}` : ''}
          </strong>
        ) : diagnostics.status === 'stale' ? (
          <strong className="python-editor__muted">{copy.diagnosticsStale}</strong>
        ) : (
          <strong className="python-editor__muted">
            {diagnostics.message ?? copy.diagnosticsUnavailable}
          </strong>
        )}
      </footer>
    </section>
  );
}

function markerSeverity(severity: 'error' | 'warning' | 'info'): monaco.MarkerSeverity {
  if (severity === 'error') return monaco.MarkerSeverity.Error;
  if (severity === 'warning') return monaco.MarkerSeverity.Warning;
  return monaco.MarkerSeverity.Info;
}
