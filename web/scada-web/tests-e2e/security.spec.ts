import { expect, request as playwrightRequest, test } from '@playwright/test';
import { createE2eJwt } from './jwt';

const baseURL = 'http://127.0.0.1:5173';

test('API distinguishes access levels and records protected-operation audit events', async ({ request }) => {
  const meResponse = await request.get('/api/auth/me');
  expect(meResponse.ok()).toBeTruthy();
  const me = await meResponse.json() as { subjectId: string; displayName: string; roles: string[] };
  expect(me.subjectId).toBe('e2e-developer');
  expect(me.displayName).toBe('E2E Developer');
  expect(me.roles).toContain('developer');

  const tagsResponse = await request.get('/api/tags');
  expect(tagsResponse.ok()).toBeTruthy();
  const tags = await tagsResponse.json() as Array<{ id: string; path: string; readOnly: boolean }>;
  const frequency = tags.find(tag => tag.path === 'Demo.P01.Frequency');
  expect(frequency).toBeTruthy();
  expect(frequency!.readOnly).toBeFalsy();

  const alarmDefinitionsResponse = await request.get('/api/alarms/definitions');
  expect(alarmDefinitionsResponse.ok()).toBeTruthy();
  const alarmDefinitions = await alarmDefinitionsResponse.json() as Array<{
    id: string;
    name: string;
    shelvingAllowed: boolean;
  }>;
  const shelfableAlarm = alarmDefinitions.find(alarm => alarm.shelvingAllowed);
  expect(shelfableAlarm).toBeTruthy();

  const engineeringResponse = await request.get('/api/engineering/export/json');
  expect(engineeringResponse.ok()).toBeTruthy();
  const engineeringJson = await engineeringResponse.text();

  const anonymous = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: '' }
  });
  try {
    expect((await anonymous.get('/api/auth/me')).status()).toBe(401);
    expect((await anonymous.post(`/api/tags/${frequency!.id}/write`, {
      data: { value: 51 }
    })).status()).toBe(401);
    expect((await anonymous.post(`/api/alarms/${shelfableAlarm!.id}/shelve`)).status()).toBe(401);
    expect((await anonymous.post('/api/engineering/import/json/apply', {
      data: engineeringJson,
      headers: { 'content-type': 'application/json; charset=utf-8' }
    })).status()).toBe(401);
    expect((await anonymous.post('/api/engineering/persistence/e2e-security/save', {
      data: { projectName: 'E2E Security', savedBy: 'spoofed-anonymous' }
    })).status()).toBe(401);
  } finally {
    await anonymous.dispose();
  }

  const invalid = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: 'Bearer definitely-not-a-valid-jwt' }
  });
  try {
    expect((await invalid.get('/api/auth/me')).status()).toBe(401);
    expect((await invalid.post(`/api/tags/${frequency!.id}/write`, {
      data: { value: 53 }
    })).status()).toBe(401);
  } finally {
    await invalid.dispose();
  }

  const operatorToken = createE2eJwt('e2e-operator', ['operator'], 'E2E Operator');
  const operator = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${operatorToken}` }
  });
  try {
    const operatorMe = await operator.get('/api/auth/me');
    expect(operatorMe.ok()).toBeTruthy();

    // The demo operator can execute operational commands, but ProcessValueWrite is deliberately absent.
    expect((await operator.post(`/api/tags/${frequency!.id}/write`, {
      data: { value: 52 }
    })).status()).toBe(403);

    // AlarmShelve is deliberately separate from acknowledgement and command authority.
    expect((await operator.post(`/api/alarms/${shelfableAlarm!.id}/shelve`)).status()).toBe(403);

    // Engineering mutations require EngineeringModify, which the operator also does not have.
    expect((await operator.post('/api/engineering/import/json/apply', {
      data: engineeringJson,
      headers: { 'content-type': 'application/json; charset=utf-8' }
    })).status()).toBe(403);
    expect((await operator.post('/api/engineering/persistence/e2e-security/save', {
      data: { projectName: 'E2E Security', savedBy: 'spoofed-operator' }
    })).status()).toBe(403);

    // Audit history itself is administrative information.
    expect((await operator.get('/api/audit')).status()).toBe(403);
  } finally {
    await operator.dispose();
  }

  // The developer role explicitly has the protected capabilities used below.
  expect((await request.post(`/api/tags/${frequency!.id}/write`, {
    data: { value: 54 }
  })).status()).toBe(202);

  expect((await request.post(`/api/alarms/${shelfableAlarm!.id}/shelve`)).status()).toBe(200);
  const shelvedAlarmsResponse = await request.get('/api/alarms');
  expect(shelvedAlarmsResponse.ok()).toBeTruthy();
  const shelvedAlarms = await shelvedAlarmsResponse.json() as Array<{
    definitionId: string;
    state: number;
    shelvedBy?: string;
  }>;
  const shelvedAlarm = shelvedAlarms.find(alarm => alarm.definitionId === shelfableAlarm!.id);
  expect(shelvedAlarm).toBeTruthy();
  expect(shelvedAlarm!.state).toBe(5);
  expect(shelvedAlarm!.shelvedBy).toBe('E2E Developer');
  expect((await request.post(`/api/alarms/${shelfableAlarm!.id}/unshelve`)).status()).toBe(200);

  const saveResponse = await request.post('/api/engineering/persistence/e2e-security/save', {
    data: { projectName: 'E2E Security', savedBy: 'spoofed-client' }
  });
  expect(saveResponse.ok()).toBeTruthy();
  const saved = await saveResponse.json() as {
    revision: number;
    projectKey: string;
    savedBy: string;
  };
  expect(saved.projectKey).toBe('e2e-security');
  expect(saved.savedBy).toBe('e2e-developer');

  const publishResponse = await request.post(
    `/api/engineering/persistence/e2e-security/revisions/${saved.revision}/publish`,
    { data: { publishedBy: 'spoofed-publisher' } });
  expect(publishResponse.ok()).toBeTruthy();
  const published = await publishResponse.json() as {
    revision: { revision: number };
    publication: { publishedRevision: number; publishedBy?: string };
  };
  expect(published.publication.publishedRevision).toBe(saved.revision);
  if (published.publication.publishedBy !== undefined) {
    expect(published.publication.publishedBy).toBe('e2e-developer');
  }

  const checkoutResponse = await request.post(
    `/api/engineering/persistence/e2e-security/revisions/${saved.revision}/checkout`);
  expect(checkoutResponse.ok()).toBeTruthy();

  const applyResponse = await request.post(
    `/api/engineering/persistence/e2e-security/revisions/${saved.revision}/apply`);
  expect(applyResponse.ok()).toBeTruthy();

  const auditResponse = await request.get('/api/audit?limit=100');
  expect(auditResponse.ok()).toBeTruthy();
  const events = await auditResponse.json() as Array<{
    subjectId: string;
    action: string;
    outcome: number;
    targetKind: string;
    targetId: string;
    details?: Record<string, string>;
  }>;

  expect(events.some(event =>
    event.subjectId === 'e2e-developer' &&
    event.action === 'tag.write' &&
    event.outcome === 0 &&
    event.targetId === 'Demo.P01.Frequency')).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'e2e-operator' &&
    event.action === 'tag.write' &&
    event.outcome === 1 &&
    event.targetId === 'Demo.P01.Frequency')).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'anonymous' &&
    event.action === 'tag.write' &&
    event.outcome === 1 &&
    event.targetId === 'Demo.P01.Frequency')).toBeTruthy();

  expect(events.some(event =>
    event.subjectId === 'e2e-developer' &&
    event.action === 'alarm.shelve' &&
    event.outcome === 0 &&
    event.targetId === shelfableAlarm!.id &&
    event.details?.operation === 'shelve')).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'e2e-developer' &&
    event.action === 'alarm.shelve' &&
    event.outcome === 0 &&
    event.targetId === shelfableAlarm!.id &&
    event.details?.operation === 'unshelve')).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'e2e-operator' &&
    event.action === 'alarm.shelve' &&
    event.outcome === 1 &&
    event.targetId === shelfableAlarm!.id)).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'anonymous' &&
    event.action === 'alarm.shelve' &&
    event.outcome === 1 &&
    event.targetId === shelfableAlarm!.id)).toBeTruthy();

  expect(events.some(event =>
    event.subjectId === 'e2e-developer' &&
    event.action === 'engineering.save' &&
    event.outcome === 0 &&
    event.targetId === 'e2e-security')).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'e2e-operator' &&
    event.action === 'engineering.save' &&
    event.outcome === 1 &&
    event.targetId === 'e2e-security')).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'anonymous' &&
    event.action === 'engineering.save' &&
    event.outcome === 1 &&
    event.targetId === 'e2e-security')).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'e2e-developer' &&
    event.action === 'engineering.publish' &&
    event.outcome === 0)).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'e2e-developer' &&
    event.action === 'engineering.checkout' &&
    event.outcome === 0)).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'e2e-developer' &&
    event.action === 'engineering.import.apply' &&
    event.outcome === 0)).toBeTruthy();

  // Audit metadata is intentionally structural; process values and credentials are not copied into it.
  expect(events.every(event => !event.details || !('value' in event.details))).toBeTruthy();
  expect(events.every(event => !event.details || !('authorization' in event.details))).toBeTruthy();
});
