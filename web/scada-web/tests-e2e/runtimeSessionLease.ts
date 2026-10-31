import type { APIRequestContext } from '@playwright/test';

const runtimeSessionHeader = 'X-EliteSCADA-Runtime-Session';
const runtimeClientInstanceHeader = 'X-EliteSCADA-Runtime-Client-Instance';
export const E2E_RUNTIME_CLIENT_INSTANCE_ID = 'e2e-runtime-client';
export const E2E_RUNTIME_VIEW_ONLY_CLIENT_INSTANCE_ID = 'e2e-runtime-view-only-client';

/** Uses the public Runtime admission endpoint; it never synthesizes a lease or quota state. */
export async function admitInteractiveRuntimeSession(request: APIRequestContext): Promise<Record<string, string>> {
  return admitRuntimeSessionForTest(request, 'interactive', E2E_RUNTIME_CLIENT_INSTANCE_ID);
}

/** Uses the public Runtime admission endpoint for read-only windows and media checks. */
export async function admitViewOnlyRuntimeSession(request: APIRequestContext): Promise<Record<string, string>> {
  return admitRuntimeSessionForTest(request, 'viewOnly', E2E_RUNTIME_VIEW_ONLY_CLIENT_INSTANCE_ID);
}

async function admitRuntimeSessionForTest(
  request: APIRequestContext,
  requestedClass: 'interactive' | 'viewOnly',
  clientInstanceId: string
): Promise<Record<string, string>> {
  // Stable identities avoid making a new remote client on every request while still
  // keeping interactive and explicitly read-only windows in their licensed seat pools.
  const response = await request.post('/api/runtime/sessions', {
    data: { clientInstanceId, connectionClass: requestedClass }
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
  if (body.grantedClass !== requestedClass) {
    throw new Error(`Runtime session admission requested ${requestedClass} but granted ${String(body.grantedClass)} (${String(body.admissionReasonCode)}).`);
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
