import type { PythonSourceDiagnostic } from '../../python-runtime/pythonRuntimeContracts';

export type PythonEditorMarkerSeverity = 'error' | 'warning' | 'info';

export type PythonEditorMarker = {
  severity: PythonEditorMarkerSeverity;
  code: string;
  message: string;
  startLineNumber: number;
  startColumn: number;
  endLineNumber: number;
  endColumn: number;
};

export type PythonEditorDiagnosticProjection = {
  markers: PythonEditorMarker[];
  rejectedCount: number;
};

export type PythonEditorDiagnosticSnapshot = {
  source: string;
  diagnostics: readonly PythonSourceDiagnostic[];
};

export type PythonEditorDiagnosticState =
  | { status: 'ready'; diagnostics: readonly PythonSourceDiagnostic[] }
  | { status: 'stale' }
  | { status: 'unavailable'; message?: string };

export function resolvePythonDiagnosticSnapshot(
  source: string,
  snapshot: PythonEditorDiagnosticSnapshot | null | undefined
): PythonEditorDiagnosticState {
  if (!snapshot) return { status: 'unavailable' };
  if (snapshot.source !== source) return { status: 'stale' };
  return { status: 'ready', diagnostics: snapshot.diagnostics };
}

export function projectPythonDiagnostics(
  diagnostics: readonly PythonSourceDiagnostic[] | null | undefined
): PythonEditorDiagnosticProjection {
  if (!diagnostics) return { markers: [], rejectedCount: 0 };

  const markers: PythonEditorMarker[] = [];
  let rejectedCount = 0;

  for (const diagnostic of diagnostics) {
    if (!isPositiveInteger(diagnostic.line) || !isPositiveInteger(diagnostic.column)) {
      rejectedCount++;
      continue;
    }

    const endLine = isPositiveInteger(diagnostic.endLine) ? diagnostic.endLine : diagnostic.line;
    let endColumn = isPositiveInteger(diagnostic.endColumn) ? diagnostic.endColumn : diagnostic.column + 1;
    if (endLine === diagnostic.line && endColumn <= diagnostic.column) endColumn = diagnostic.column + 1;

    markers.push({
      severity: diagnostic.severity,
      code: diagnostic.code,
      message: diagnostic.message,
      startLineNumber: diagnostic.line,
      startColumn: diagnostic.column,
      endLineNumber: endLine,
      endColumn
    });
  }

  return { markers, rejectedCount };
}

export function hasBlockingPythonDiagnostics(
  diagnostics: readonly PythonSourceDiagnostic[] | null | undefined
): boolean {
  return diagnostics?.some(diagnostic => diagnostic.severity === 'error') ?? false;
}

function isPositiveInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 1;
}
