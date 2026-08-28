import { expect, test } from '@playwright/test';
import {
  CLIENT_VISUAL_PYTHON_CAPABILITIES,
  type PythonSourceDiagnostic
} from '../src/python-runtime/pythonRuntimeContracts';
import {
  buildEntryPointCompletions,
  CLIENT_VISUAL_PYTHON_API_HELP
} from '../src/engineering/python-editor/pythonEditorDescriptors';
import {
  hasBlockingPythonDiagnostics,
  projectPythonDiagnostics,
  resolvePythonDiagnosticSnapshot
} from '../src/engineering/python-editor/pythonEditorDiagnostics';

test('Python diagnostics preserve valid 1-based line/column markers and reject invalid positions', () => {
  const diagnostics: PythonSourceDiagnostic[] = [
    {
      severity: 'error',
      code: 'PY_SYNTAX',
      message: 'invalid syntax',
      line: 3,
      column: 7,
      endLine: 3,
      endColumn: 11
    },
    {
      severity: 'warning',
      code: 'PY_WARNING',
      message: 'warning',
      line: 5,
      column: 2
    },
    {
      severity: 'info',
      code: 'INVALID_POSITION',
      message: 'must not reach Monaco',
      line: 0,
      column: 1
    }
  ];

  const projection = projectPythonDiagnostics(diagnostics);
  expect(projection.rejectedCount).toBe(1);
  expect(projection.markers).toEqual([
    expect.objectContaining({
      severity: 'error',
      code: 'PY_SYNTAX',
      startLineNumber: 3,
      startColumn: 7,
      endLineNumber: 3,
      endColumn: 11
    }),
    expect.objectContaining({
      severity: 'warning',
      code: 'PY_WARNING',
      startLineNumber: 5,
      startColumn: 2,
      endLineNumber: 5,
      endColumn: 3
    })
  ]);
  expect(hasBlockingPythonDiagnostics(diagnostics)).toBeTruthy();
  expect(hasBlockingPythonDiagnostics([{ ...diagnostics[1]!, severity: 'warning' }])).toBeFalsy();
});

test('compile diagnostics are accepted only for the exact source snapshot', () => {
  const diagnostics: PythonSourceDiagnostic[] = [{
    severity: 'error',
    code: 'PY_SYNTAX',
    message: 'invalid syntax',
    line: 1,
    column: 1
  }];

  expect(resolvePythonDiagnosticSnapshot('print(1)\n', undefined)).toEqual({ status: 'unavailable' });
  expect(resolvePythonDiagnosticSnapshot('print(2)\n', {
    source: 'print(1)\n',
    diagnostics
  })).toEqual({ status: 'stale' });
  expect(resolvePythonDiagnosticSnapshot('print(1)\n', {
    source: 'print(1)\n',
    diagnostics
  })).toEqual({ status: 'ready', diagnostics });
});

test('Client Visual editor help is a projection of every stable bridge v1 capability', () => {
  expect(CLIENT_VISUAL_PYTHON_API_HELP.map(item => item.capability)).toEqual([
    ...CLIENT_VISUAL_PYTHON_CAPABILITIES
  ]);
  expect(new Set(CLIENT_VISUAL_PYTHON_API_HELP.map(item => item.capability)).size)
    .toBe(CLIENT_VISUAL_PYTHON_CAPABILITIES.length);
});

test('entry-point completion uses only canonical valid handler names and de-duplicates them', () => {
  const completions = buildEntryPointCompletions([
    { eventKind: 'initialize', handlerName: 'initialize', targetReference: null },
    { eventKind: 'timer', handlerName: 'tick', targetReference: 'timer:1' },
    { eventKind: 'tagChanged', handlerName: 'tick', targetReference: 'tag:2' },
    { eventKind: 'dispose', handlerName: 'not valid', targetReference: null }
  ]);

  expect(completions.map(item => item.label)).toEqual(['initialize', 'tick']);
  expect(completions[0]?.insertText).toContain('def initialize():');
  expect(completions[0]?.insertText).toContain('${1:pass}');
  expect(completions[1]?.detail).toContain('timer');
});
