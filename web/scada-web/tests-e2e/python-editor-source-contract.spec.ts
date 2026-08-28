import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

test('Monaco editor provides Python editing, markers and canonical entry-point completion without runtime execution authority', async () => {
  const editor = await source('../src/engineering/python-editor/PythonMonacoEditor.tsx');

  expect(editor).toContain("createModel(sourceRef.current, 'python'");
  expect(editor).toContain("lineNumbers: 'on'");
  expect(editor).toContain('registerCompletionItemProvider');
  expect(editor).toContain('setModelMarkers');
  expect(editor).toContain('buildEntryPointCompletions');
  expect(editor).not.toMatch(/from ['\"]pyodide['\"]/);
  expect(editor).not.toContain('dispatch-event');
  expect(editor).not.toContain('initialize-script');
  expect(editor).not.toContain('backendOperation.request(');
});

test('Script workspace compiles the exact Client Visual draft before canonical Preview Apply CAS', async () => {
  const workspace = await source('../src/engineering/scripts/ScriptEngineeringWorkspace.tsx');
  const previewHost = await source('../src/python-runtime/engineeringPythonPreview.ts');
  const api = await source('../src/engineering/scripts/scriptEngineeringApi.ts');

  expect(workspace).toContain('PythonMonacoEditor');
  expect(workspace).toContain('onSourceChange={source => patchDraft({ source })}');
  expect(workspace).toContain('compileEngineeringClientVisualPython');
  expect(workspace).toContain('hasBlockingPythonDiagnostics(compiled.diagnostics)');
  expect(workspace).toContain('previewScriptMutation');
  expect(workspace).toContain('applyScriptMutation');
  expect(workspace.indexOf('compileEngineeringClientVisualPython({')).toBeLessThan(workspace.indexOf('previewScriptMutation(draft'));
  expect(previewHost).toContain('source: request.source');
  expect(previewHost).toContain('await runtime.compileSource(request.source)');
  expect(api).toContain("'x-elitescada-workspace-version'");
  expect(api).toContain('/api/engineering/import/json/preview');
  expect(api).toContain('/api/engineering/import/json/apply');
});

test('controlled Engineering handler preview uses the sandbox host without gaining direct process authority', async () => {
  const workspace = await source('../src/engineering/scripts/ScriptEngineeringWorkspace.tsx');
  const previewHost = await source('../src/python-runtime/engineeringPythonPreview.ts');
  const provider = await source('../src/python-runtime/createClientVisualPythonCapabilityProvider.ts');

  expect(workspace).toContain('runEngineeringClientVisualPythonHandler');
  expect(workspace).toContain('data-testid="python-sandbox-preview"');
  expect(previewHost).toContain('createClientVisualPythonCapabilityProvider');
  expect(previewHost).toContain("`engineering-preview:${request.handlerName}`");
  expect(provider).toContain('readTag(reference)');
  expect(provider).toContain('readClientMemory(reference)');
  expect(provider).toContain('writeClientMemory(reference, value)');
  expect(provider).not.toContain('writeTag');
  expect(provider).not.toContain('serverMemory');
  expect(provider).not.toContain('requestBackendOperation');
});

test('editor API help derives from the reserved bridge capability contract instead of inventing a Python module authority', async () => {
  const descriptors = await source('../src/engineering/python-editor/pythonEditorDescriptors.ts');
  const editorCopy = await source('../src/engineering/python-editor/pythonEditorCopy.ts');

  expect(descriptors).toContain('CLIENT_VISUAL_PYTHON_CAPABILITIES');
  expect(editorCopy).toContain('não é inventado pelo editor');
  expect(editorCopy).toContain('does not invent the final Python module name');
  expect(editorCopy).toContain('no inventa el nombre final del módulo Python');
});
