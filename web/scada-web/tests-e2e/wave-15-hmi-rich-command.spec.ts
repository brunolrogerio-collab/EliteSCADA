import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {
  executeRuntimeRichCommand,
  loadRuntimeRichCommandDefinition,
  type RuntimeCommandFetch
} from '../src/runtime/visual-navigation/runtimeCommandApi';

const SESSION_ID = '11111111-2222-3333-4444-555555555555';
const COMMAND_ID = '8f1fd44e-c3ea-4a19-8e2f-776b3eecc19a';

function sessionResponse(init?: RequestInit) {
  return new Response(JSON.stringify({
    sessionId: SESSION_ID,
    clientInstanceId: JSON.parse(String(init?.body)).clientInstanceId
  }), { status: 201, headers: { 'content-type': 'application/json' } });
}

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

test('HMI Rich Command definition reads only the Active display schema', async () => {
  const calls: Array<{ input: string; init?: RequestInit }> = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    const url = String(input);
    calls.push({ input: url, init });
    if (url === '/api/runtime/sessions') return sessionResponse(init);
    return new Response(JSON.stringify({
      commandId: COMMAND_ID,
      semanticKey: 'cover.move',
      description: 'Move cover',
      parameters: [{
        key: 'position',
        required: true,
        schema: { kind: 'Percentage', minimum: 0, maximum: 100, unit: '%' }
      }]
    }), { status: 200, headers: { 'content-type': 'application/json' } });
  };

  const definition = await loadRuntimeRichCommandDefinition(COMMAND_ID, fetcher);

  expect(definition.semanticKey).toBe('cover.move');
  expect(definition.parameters[0].schema.kind).toBe('Percentage');
  expect(calls.map(call => call.input)).toEqual([
    '/api/runtime/rich-commands/' + COMMAND_ID + '/definition'
  ]);
  expect(calls[0].init?.method).toBe('GET');
  expect(calls[0].init?.credentials).toBe('same-origin');
  expect(calls[0].init?.headers).toMatchObject({ accept: 'application/json' });
});

test('HMI Rich Command invocation sends one canonical ID and invocation-time typed values', async () => {
  const calls: Array<{ input: string; init?: RequestInit }> = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    const url = String(input);
    calls.push({ input: url, init });
    if (url === '/api/runtime/sessions') return sessionResponse(init);
    return new Response(JSON.stringify({
      invocationId: 'e2d7b934-662a-4fdc-9cdf-df98b2a4b901',
      commandId: COMMAND_ID,
      outcome: 'Completed',
      observedAt: '2026-10-08T00:00:00Z'
    }), { status: 200, headers: { 'content-type': 'application/json' } });
  };

  const result = await executeRuntimeRichCommand(COMMAND_ID, { position: '37.5' }, fetcher);

  expect(result.outcome).toBe('Completed');
  expect(calls).toHaveLength(2);
  expect(calls[1].input).toBe('/api/runtime/rich-commands/' + COMMAND_ID + '/execute');
  expect(calls[1].init?.method).toBe('POST');
  expect(JSON.parse(String(calls[1].init?.body))).toEqual({
    commandId: COMMAND_ID,
    parameters: { position: '37.5' }
  });
  expect(calls[1].init?.headers).toMatchObject({
    accept: 'application/json',
    'content-type': 'application/json; charset=utf-8',
    'X-EliteSCADA-Runtime-Session': SESSION_ID
  });
});

test('HMI parameterless Rich Command submits an empty invocation payload', async () => {
  const calls: Array<{ input: string; init?: RequestInit }> = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    const url = String(input);
    calls.push({ input: url, init });
    if (url === '/api/runtime/sessions') return sessionResponse(init);
    return new Response(JSON.stringify({
      commandId: COMMAND_ID,
      outcome: 'Accepted',
      observedAt: '2026-10-08T00:00:00Z'
    }), { status: 200, headers: { 'content-type': 'application/json' } });
  };

  const result = await executeRuntimeRichCommand(COMMAND_ID, {}, fetcher);

  expect(result.outcome).toBe('Accepted');
  expect(JSON.parse(String(calls[1].init?.body))).toEqual({
    commandId: COMMAND_ID,
    parameters: {}
  });
});

test('HMI Rich Command transport ambiguity becomes Unknown and never retries', async () => {
  const calls: string[] = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    const url = String(input);
    calls.push(url);
    if (url === '/api/runtime/sessions') return sessionResponse(init);
    throw new TypeError('connection closed after request send');
  };

  const result = await executeRuntimeRichCommand(COMMAND_ID, {}, fetcher);

  expect(result.outcome).toBe('Unknown');
  expect(calls.filter(url => url.endsWith('/execute'))).toHaveLength(1);
});

test('Rich Command authoring is Version 2, limited to Screen and Popup, with invocation-time values', async () => {
  const contracts = await source('../../../src/Scada.Engineering/Contracts/VisualCompositionEngineeringContracts.cs');
  const validation = await source('../../../src/Scada.Engineering/Validation/VisualCompositionEngineeringValidation.cs');
  const exchange = await source('../../../src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs');
  const panel = await source('../src/engineering/visual-editor/HmiOperationalConfigurationPanel.tsx');
  const navigator = await source('../src/runtime/visual-navigation/RuntimeVisualNavigator.tsx');
  const service = await source('../../../src/Scada.Api/Runtime/HmiRichCommandInvocationService.cs');

  expect(contracts).toContain('ExecuteRichCommand');
  expect(contracts).toContain('public const int RichCommand = 2');
  expect(validation).toContain('VISUAL_RICH_COMMAND_ACTION_VERSION_UNSUPPORTED');
  expect(validation).toContain('VISUAL_RICH_COMMAND_PARAMETERS_NOT_ALLOWED');
  expect(exchange).toContain('VISUAL_RICH_COMMAND_NOT_FOUND');
  expect(exchange).toContain('ExecuteRichCommand actions are supported only by Screen and Popup');
  expect(panel).toContain("kind: 'ExecuteRichCommand'");
  expect(panel).toContain('version: 2');
  expect(panel).toContain("definitionKind === 'dynamo'");
  expect(navigator).toContain("action.kind === 'ExecuteRichCommand'");
  expect(navigator).toContain('data-testid="runtime-rich-command-parameters"');
  expect(navigator).toContain('definition.parameters.length === 0');
  expect(navigator).toContain('setRichCommandPrompt');
  expect(service).toContain('InteractionOrigin.Hmi');
  expect(service).toContain('SecurityCapability.CommandExecute');
  expect(service).toContain('AuditActions.CommandExecute');
  expect(service).toContain('IRichCommandDefinitionResolver');
  expect(service).toContain('IRichCommandBindingResolver');
});
