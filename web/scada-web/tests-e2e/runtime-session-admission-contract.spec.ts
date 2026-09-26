import { expect, test } from '@playwright/test';
import {
  admitInteractiveRuntimeSession,
  admitRuntimeSession,
  RuntimeSessionAdmissionError,
  type RuntimeSessionFetch
} from '../src/runtime/runtimeSessionAdmissionApi';

const clientInstanceId = 'fc0a-runtime-client';

function admissionResponse(body: Record<string, unknown>): RuntimeSessionFetch {
  return async () => new Response(JSON.stringify({
    sessionId: '11111111-2222-3333-4444-555555555555',
    clientInstanceId,
    ...body
  }), { status: 201, headers: { 'content-type': 'application/json' } });
}

test('Runtime admission retains the authoritative requested, granted and reason fields', async () => {
  const outcome = await admitRuntimeSession('interactive', admissionResponse({
    requestedClass: 'interactive',
    grantedClass: 'viewOnly',
    admissionReasonCode: 'AuthorityReadOnly',
    capacityReasonCode: 'AuthorityDownscopeViewOnlyReserved'
  }), clientInstanceId);

  expect(outcome.requestedClass).toBe('interactive');
  expect(outcome.grantedClass).toBe('viewOnly');
  expect(outcome.admissionReasonCode).toBe('AuthorityReadOnly');
  expect(outcome.capacityReasonCode).toBe('AuthorityDownscopeViewOnlyReserved');
});

test('an interactive operation reports a server viewOnly fallback instead of using that lease', async () => {
  const calls: Array<{ input: string; init?: RequestInit }> = [];
  const fetcher: RuntimeSessionFetch = async (input, init) => {
    calls.push({ input: String(input), init });
    if (String(input).endsWith('/terminate')) return new Response(null, { status: 204 });
    return new Response(JSON.stringify({
      sessionId: '11111111-2222-3333-4444-555555555555', clientInstanceId,
      requestedClass: 'interactive', grantedClass: 'viewOnly', admissionReasonCode: 'AuthorityReadOnly'
    }), { status: 201, headers: { 'content-type': 'application/json' } });
  };
  await expect(admitInteractiveRuntimeSession(fetcher, clientInstanceId)).rejects.toMatchObject<Partial<RuntimeSessionAdmissionError>>({
    name: 'RuntimeSessionAdmissionError',
    message: 'Interactive Runtime access was requested, but the server granted viewOnly access (AuthorityReadOnly).',
    outcome: { requestedClass: 'interactive', grantedClass: 'viewOnly' }
  });
  expect(calls.map(call => call.input)).toEqual(['/api/runtime/sessions', '/api/runtime/sessions/11111111-2222-3333-4444-555555555555/terminate']);
  expect(calls[1]?.init?.body).toBe(JSON.stringify({ clientInstanceId }));
});

test('capacity rejection exposes the machine-readable reason without hiding the server message', async () => {
  const fetcher: RuntimeSessionFetch = async () => new Response(JSON.stringify({
    error: 'Runtime session capacity is unavailable.',
    capacityReasonCode: 'EligiblePoolsExhausted'
  }), { status: 409, headers: { 'content-type': 'application/json' } });

  await expect(admitRuntimeSession('interactive', fetcher, clientInstanceId)).rejects.toMatchObject<Partial<RuntimeSessionAdmissionError>>({
    name: 'RuntimeSessionAdmissionError',
    status: 409,
    message: 'Runtime session capacity is unavailable.',
    capacityReasonCode: 'EligiblePoolsExhausted'
  });
});
