import type { APIRequestContext } from '@playwright/test';

const runtimeSessionHeader = 'X-EliteSCADA-Runtime-Session';
const runtimeClientInstanceHeader = 'X-EliteSCADA-Runtime-Client-Instance';

/** Uses the public Runtime admission endpoint; it never synthesizes a lease or quota state. */
export async function admitInteractiveRuntimeSession(request: APIRequestContext): Promise<Record<string, string>> {
  // The test suite has stable authenticated subjects.  A stable logical client identity avoids
  // making a new remote client for every request while still letting the server distinguish
  // different users, which is essential now that the demo pool is intentionally finite.
  const clientInstanceId = 'e2e-runtime-client';
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
