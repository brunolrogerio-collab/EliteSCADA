import { expect, test } from '@playwright/test';
import {
  executeRuntimeCommand,
  RuntimeCommandExecutionError,
  type RuntimeCommandFetch
} from '../src/runtime/visual-navigation/runtimeCommandApi';

test('Runtime visual Command bridge POSTs only the canonical Command identity to backend authority', async () => {
  const calls: Array<{ input: string; init?: RequestInit }> = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    calls.push({ input: String(input), init });
    if (String(input) === '/api/runtime/sessions') {
      return new Response(JSON.stringify({
        sessionId: '11111111-2222-3333-4444-555555555555',
        clientInstanceId: JSON.parse(String(init?.body)).clientInstanceId
      }), { status: 201, headers: { 'content-type': 'application/json' } });
    }
    return new Response(null, { status: 202 });
  };

  await executeRuntimeCommand(' 11111111-2222-3333-4444-555555555555 ', fetcher);

  expect(calls).toHaveLength(2);
  expect(calls[0].input).toBe('/api/runtime/sessions');
  expect(calls[0].init?.method).toBe('POST');
  expect(calls[1].input).toBe('/api/commands/11111111-2222-3333-4444-555555555555/execute');
  expect(calls[1].init?.method).toBe('POST');
  expect(calls[1].init?.body).toBeUndefined();
  expect(calls[1].init?.headers).toMatchObject({
    accept: 'application/json',
    'X-EliteSCADA-Runtime-Session': '11111111-2222-3333-4444-555555555555'
  });
});

test('Runtime visual Command bridge preserves backend denial/failure instead of falling back to client TAG writes', async () => {
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    if (String(input) === '/api/runtime/sessions') {
      return new Response(JSON.stringify({
        sessionId: '11111111-2222-3333-4444-555555555555',
        clientInstanceId: JSON.parse(String(init?.body)).clientInstanceId
      }), { status: 201, headers: { 'content-type': 'application/json' } });
    }

    return new Response('Command execution denied.', { status: 403 });
  };

  await expect(executeRuntimeCommand('command-id', fetcher)).rejects.toMatchObject({
    name: 'RuntimeCommandExecutionError',
    status: 403,
    message: 'Command execution denied.'
  });
});

test('Runtime visual Command bridge rejects an empty stable identity before issuing a request', async () => {
  let called = false;
  const fetcher: RuntimeCommandFetch = async () => {
    called = true;
    return new Response(null, { status: 202 });
  };

  await expect(executeRuntimeCommand('   ', fetcher)).rejects.toBeInstanceOf(RuntimeCommandExecutionError);
  expect(called).toBe(false);
});
