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
  await expect(admitInteractiveRuntimeSession(admissionResponse({
    requestedClass: 'interactive',
    grantedClass: 'viewOnly',
    admissionReasonCode: 'AuthorityReadOnly'
  }), clientInstanceId)).rejects.toMatchObject<Partial<RuntimeSessionAdmissionError>>({
    name: 'RuntimeSessionAdmissionError',
    message: 'Interactive Runtime access was requested, but the server granted viewOnly access (AuthorityReadOnly).',
    outcome: { requestedClass: 'interactive', grantedClass: 'viewOnly' }
  });
});
