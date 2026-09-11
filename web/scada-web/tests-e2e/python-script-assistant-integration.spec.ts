import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';
import {
  CLIENT_VISUAL_PYTHON_CAPABILITIES,
  CLIENT_VISUAL_PYTHON_PROTOCOL_CAPABILITIES
} from '../src/python-runtime/pythonRuntimeContracts';
import { CLIENT_VISUAL_PYTHON_API_HELP } from '../src/engineering/python-editor/pythonEditorDescriptors';

test('Python API Help advertises only official product capabilities', () => {
  const advertised = CLIENT_VISUAL_PYTHON_API_HELP.map(item => item.capability);

  expect(advertised).toEqual([...CLIENT_VISUAL_PYTHON_CAPABILITIES]);
  expect(advertised).not.toContain('backendOperation.request');
  expect(CLIENT_VISUAL_PYTHON_PROTOCOL_CAPABILITIES).toContain('backendOperation.request');
});

test('Monaco integration validates live editable source instead of only persisted script text', () => {
  const editorPath = fileURLToPath(new URL('../src/engineering/python-editor/PythonMonacoEditor.tsx', import.meta.url));
  const editorSource = readFileSync(editorPath, 'utf8');

  expect(editorSource).toContain("import { PythonScriptReferenceDiagnostics } from '../scripts/PythonScriptReferenceDiagnostics';");
  expect(editorSource).toMatch(/<PythonScriptReferenceDiagnostics\s+[\s\S]*?locale=\{locale\}[\s\S]*?source=\{source\}[\s\S]*?\/>/);
  expect(editorSource).toContain("onSourceChangeRef.current(next);");
});
