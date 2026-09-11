import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

test('Monaco editor provides Python editing, markers, Script Assistant insertion and canonical entry-point completion without runtime execution authority', async () => {
  const editor = await source('../src/engineering/python-editor/PythonMonacoEditor.tsx');

  expect(editor).toContain("createModel(sourceRef.current, 'python'");
  expect(editor).toContain("lineNumbers: 'on'");
  expect(editor).toContain('registerCompletionItemProvider');
  expect(editor).toContain('setModelMarkers');
  expect(editor).toContain('buildEntryPointCompletions');
  expect(editor).toContain('PythonScriptAssistant');
  expect(editor).toContain("executeEdits('elitescada-script-assistant'");
  expect(editor).toContain('pushUndoStop()');
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

test('normal Runtime provider owns mediated TAG write while Engineering handler preview explicitly disables process writes', async () => {
  const workspace = await source('../src/engineering/scripts/ScriptEngineeringWorkspace.tsx');
  const previewHost = await source('../src/python-runtime/engineeringPythonPreview.ts');
  const provider = await source('../src/python-runtime/createClientVisualPythonCapabilityProvider.ts');
  const tagWriteApi = await source('../src/runtime/runtimeTagWriteApi.ts');

  expect(workspace).toContain('runEngineeringClientVisualPythonHandler');
  expect(workspace).toContain('data-testid="python-sandbox-preview"');
  expect(previewHost).toContain('createClientVisualPythonCapabilityProvider');
  expect(previewHost).toContain("`engineering-preview:${request.handlerName}`");
  expect(previewHost).toContain('tagWriter: null');
  expect(provider).toContain('readTag(reference)');
  expect(provider).toContain('writeTag: tagWriter');
  expect(provider).toContain('writeRuntimeTagValue');
  expect(provider).toContain('options.tagWriter === undefined ? writeRuntimeTagValue : options.tagWriter');
  expect(provider).toContain('readClientMemory(reference)');
  expect(provider).toContain('writeClientMemory(reference, value)');
  expect(tagWriteApi).toContain("/api/tags/${encodeURIComponent(normalizedTagId)}/write");
  expect(tagWriteApi).toContain("credentials: 'same-origin'");
  expect(provider).not.toContain('serverMemory');
  expect(provider).not.toContain('requestBackendOperation');
});

test('editor API help and Script Assistant derive from the reserved bridge capability contract', async () => {
  const descriptors = await source('../src/engineering/python-editor/pythonEditorDescriptors.ts');
  const assistantModel = await source('../src/engineering/scripts/scriptAssistantModel.ts');
  const worker = await source('../src/python-runtime/clientVisualPythonWorker.ts');

  expect(descriptors).toContain('CLIENT_VISUAL_PYTHON_CAPABILITIES');
  expect(assistantModel).toContain('CLIENT_VISUAL_PYTHON_CAPABILITIES');
  expect(assistantModel).toContain("case 'tag.write': return 'elite_scada.tag_write'");
  expect(worker).toContain("tag_write: (reference: unknown, value: unknown) => requestCapability('tag.write', 'write'");
});
