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

  const body = await response.json() as {
    sessionId?: unknown;
    clientInstanceId?: unknown;
    grantedClass?: unknown;
    admissionReasonCode?: unknown;
  };
  if (typeof body.sessionId !== 'string' || body.clientInstanceId !== clientInstanceId) {
    throw new Error('Runtime session admission did not return a bound logical lease.');
  }
  if (body.grantedClass !== 'interactive') {
    throw new Error(`Runtime session admission unexpectedly downscoped the test client: ${String(body.admissionReasonCode)}.`);
  }

  return {
    [runtimeSessionHeader]: body.sessionId,
    [runtimeClientInstanceHeader]: clientInstanceId
  };
}

/** Ends the public logical lease so independent E2E subjects do not consume a Demo seat. */
export async function terminateRuntimeSession(
  request: APIRequestContext,
  headers: Record<string, string>
): Promise<void> {
  const sessionId = headers[runtimeSessionHeader];
  const clientInstanceId = headers[runtimeClientInstanceHeader];
  if (!sessionId || !clientInstanceId) {
    throw new Error('Runtime session termination requires the admission headers.');
  }

  const response = await request.post(`/api/runtime/sessions/${sessionId}/terminate`, {
    data: { clientInstanceId },
    headers
  });
  if (response.status() !== 204) {
    throw new Error(`Runtime session termination expected 204, received ${response.status()}: ${await response.text()}`);
  }
}
