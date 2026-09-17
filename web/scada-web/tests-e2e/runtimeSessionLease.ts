import type { APIRequestContext } from '@playwright/test';

const runtimeSessionHeader = 'X-EliteSCADA-Runtime-Session';
const runtimeClientInstanceHeader = 'X-EliteSCADA-Runtime-Client-Instance';

let serial = 0;

/** Uses the public Runtime admission endpoint; it never synthesizes a lease or quota state. */
export async function admitInteractiveRuntimeSession(request: APIRequestContext): Promise<Record<string, string>> {
  const clientInstanceId = `e2e-runtime-client-${++serial}`;
  const response = await request.post('/api/runtime/sessions', {
    data: { clientInstanceId, connectionClass: 'interactive' }
  });

  if (response.status() !== 201) {
    throw new Error(`Runtime session admission expected 201, received ${response.status()}: ${await response.text()}`);
  }

  const body = await response.json() as { sessionId?: unknown; clientInstanceId?: unknown };
  if (typeof body.sessionId !== 'string' || body.clientInstanceId !== clientInstanceId) {
    throw new Error('Runtime session admission did not return a bound logical lease.');
  }

  return {
    [runtimeSessionHeader]: body.sessionId,
    [runtimeClientInstanceHeader]: clientInstanceId
  };
}
