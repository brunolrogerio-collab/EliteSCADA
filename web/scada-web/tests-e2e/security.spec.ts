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
  expect(tags).toHaveLength(7);
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
  expect(alarmDefinitions).toHaveLength(2);
  const shelfableAlarm = alarmDefinitions.find(alarm => alarm.shelvingAllowed);
  expect(shelfableAlarm).toBeTruthy();

  const engineeringResponse = await request.get('/api/engineering/export/json');
  expect(engineeringResponse.ok()).toBeTruthy();
  const engineeringJson = await engineeringResponse.text();
  const engineering = JSON.parse(engineeringJson) as {
    commands: Array<{ id: string; key: string; targetTagPath?: string }>;
  };
  const startCommand = engineering.commands.find(command => command.key === 'demo.p01.start');
  expect(startCommand).toBeTruthy();
  expect(startCommand!.targetTagPath).toBe('Demo.P01.Running');

  const protectedEngineeringGetPaths = [
    '/api/diagnostics/runtime',
    '/api/drivers',
    '/api/engineering/workspace',
    '/api/engineering/data-sources',
    '/api/engineering/templates',
    '/api/engineering/equipment',
    '/api/engineering/dynamos',
    '/api/engineering/screens',
    '/api/engineering/popups',
    '/api/engineering/security-roles',
    '/api/engineering/commands',
    '/api/engineering/export/json',
    '/api/engineering/export/tags.csv',
    '/api/engineering/export/alarms.csv',
    '/api/engineering/export/datasources.csv',
    '/api/engineering/persistence/status'
  ];

  for (const path of protectedEngineeringGetPaths) {
    expect((await request.get(path)).ok()).toBeTruthy();
  }

  expect((await request.post('/api/engineering/import/json/preview', {
    data: engineeringJson,
    headers: { 'content-type': 'application/json; charset=utf-8' }
  })).ok()).toBeTruthy();

  const projectPackageExport = await request.get(
    '/api/project-package/export?projectKey=e2e-security&projectName=E2E%20Security');
  expect(projectPackageExport.ok()).toBeTruthy();
  const projectPackageBytes = await projectPackageExport.body();
  const packageHeaders = { 'content-type': 'application/vnd.elitescada.project-package' };
  expect((await request.post('/api/project-package/inspect', {
    data: projectPackageBytes,
    headers: packageHeaders
  })).ok()).toBeTruthy();
  expect((await request.post('/api/project-package/import/preview', {
    data: projectPackageBytes,
    headers: packageHeaders
  })).ok()).toBeTruthy();

  const anonymous = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: '' }
  });
  try {
    const publicHealth = await anonymous.get('/health');
    expect(publicHealth.ok()).toBeTruthy();
    expect(await publicHealth.json()).toEqual({ status: 'ok', service: 'scada-api' });

    expect((await anonymous.get('/api/auth/me')).status()).toBe(401);
    expect((await anonymous.get('/api/tags')).status()).toBe(401);
    expect((await anonymous.get('/api/tags/current')).status()).toBe(401);
    expect((await anonymous.get(`/api/history/${frequency!.id}?limit=5`)).status()).toBe(401);
    expect((await anonymous.get('/api/alarms')).status()).toBe(401);
    expect((await anonymous.get('/api/alarms/definitions')).status()).toBe(401);
    for (const path of protectedEngineeringGetPaths) {
      expect((await anonymous.get(path)).status()).toBe(401);
    }
    expect((await anonymous.post('/api/engineering/import/json/preview', {
      data: engineeringJson,
      headers: { 'content-type': 'application/json; charset=utf-8' }
    })).status()).toBe(401);
    expect((await anonymous.get('/api/project-package/export')).status()).toBe(401);
    expect((await anonymous.post('/api/project-package/inspect', {
      data: projectPackageBytes,
      headers: packageHeaders
    })).status()).toBe(401);
    expect((await anonymous.post('/api/project-package/import/preview', {
      data: projectPackageBytes,
      headers: packageHeaders
    })).status()).toBe(401);
    expect((await anonymous.post(`/api/tags/${frequency!.id}/write`, {
      data: { value: 51 }
    })).status()).toBe(401);
    expect((await anonymous.post(`/api/commands/${startCommand!.id}/execute`)).status()).toBe(401);
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
    expect((await invalid.get('/api/tags')).status()).toBe(401);
    expect((await invalid.get('/api/engineering/workspace')).status()).toBe(401);
    expect((await invalid.get('/api/project-package/export')).status()).toBe(401);
    expect((await invalid.post(`/api/tags/${frequency!.id}/write`, {
      data: { value: 53 }
    })).status()).toBe(401);
    expect((await invalid.post(`/api/commands/${startCommand!.id}/execute`)).status()).toBe(401);
  } finally {
    await invalid.dispose();
  }

  const noReadToken = createE2eJwt('e2e-no-read', ['role-with-no-grants'], 'E2E No Read');
  const noRead = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${noReadToken}` }
  });
  try {
    const noReadTagsResponse = await noRead.get('/api/tags');
    expect(noReadTagsResponse.ok()).toBeTruthy();
    expect(await noReadTagsResponse.json()).toEqual([]);

    const noReadCurrentResponse = await noRead.get('/api/tags/current');
    expect(noReadCurrentResponse.ok()).toBeTruthy();
    expect(await noReadCurrentResponse.json()).toEqual([]);

    expect((await noRead.get('/api/tags/by-path/Demo.P01.Frequency')).status()).toBe(403);
    expect((await noRead.get(`/api/history/${frequency!.id}?limit=5`)).status()).toBe(403);

    const noReadAlarmsResponse = await noRead.get('/api/alarms');
    expect(noReadAlarmsResponse.ok()).toBeTruthy();
    expect(await noReadAlarmsResponse.json()).toEqual([]);

    const noReadAlarmDefinitionsResponse = await noRead.get('/api/alarms/definitions');
    expect(noReadAlarmDefinitionsResponse.ok()).toBeTruthy();
    expect(await noReadAlarmDefinitionsResponse.json()).toEqual([]);

    expect((await noRead.get('/api/diagnostics/runtime')).status()).toBe(403);
    expect((await noRead.get('/api/drivers')).status()).toBe(403);
    expect((await noRead.get('/api/engineering/workspace')).status()).toBe(403);
    expect((await noRead.get('/api/project-package/export')).status()).toBe(403);
  } finally {
    await noRead.dispose();
  }

  const operatorToken = createE2eJwt('e2e-operator', ['operator'], 'E2E Operator');
  const operator = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${operatorToken}` }
  });
  try {
    const operatorMe = await operator.get('/api/auth/me');
    expect(operatorMe.ok()).toBeTruthy();

    const operatorTagsResponse = await operator.get('/api/tags');
    expect(operatorTagsResponse.ok()).toBeTruthy();
    expect((await operatorTagsResponse.json()) as Array<unknown>).toHaveLength(7);
    expect((await operator.get(`/api/history/${frequency!.id}?limit=5`)).ok()).toBeTruthy();
    expect((await operator.get('/api/alarms/definitions')).ok()).toBeTruthy();

    for (const path of protectedEngineeringGetPaths) {
      expect((await operator.get(path)).status()).toBe(403);
    }
    expect((await operator.post('/api/engineering/import/json/preview', {
      data: engineeringJson,
      headers: { 'content-type': 'application/json; charset=utf-8' }
    })).status()).toBe(403);
    expect((await operator.get('/api/project-package/export')).status()).toBe(403);
    expect((await operator.post('/api/project-package/inspect', {
      data: projectPackageBytes,
      headers: packageHeaders
    })).status()).toBe(403);
    expect((await operator.post('/api/project-package/import/preview', {
      data: projectPackageBytes,
      headers: packageHeaders
    })).status()).toBe(403);

    // The demo operator has CommandExecute, but ProcessValueWrite is deliberately absent.
    expect((await operator.post(`/api/commands/${startCommand!.id}/execute`)).status()).toBe(202);
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
  expect((await request.post(`/api/commands/${startCommand!.id}/execute`)).status()).toBe(202);

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

  const auditResponse = await request.get('/api/audit?limit=150');
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
    event.action === 'command.execute' &&
    event.outcome === 0 &&
    event.targetId === 'demo.p01.start' &&
    event.details?.commandId === startCommand!.id)).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'e2e-operator' &&
    event.action === 'command.execute' &&
    event.outcome === 0 &&
    event.targetId === 'demo.p01.start')).toBeTruthy();
  expect(events.some(event =>
    event.subjectId === 'anonymous' &&
    event.action === 'command.execute' &&
    event.outcome === 1 &&
    event.targetId === 'demo.p01.start')).toBeTruthy();

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

test('realtime WebSocket requires identity, filters TAGs and expires with JWT', async ({ browser }) => {
  const context = await browser.newContext({
    extraHTTPHeaders: { Authorization: '' }
  });
  const page = await context.newPage();
  try {
    const anonymousResult = await page.evaluate(async () => {
      return await new Promise<string>(resolve => {
        const socket = new WebSocket('ws://127.0.0.1:5173/ws/tags');
        const timeout = window.setTimeout(() => {
          socket.close();
          resolve('timeout');
        }, 1500);
        socket.onopen = () => {
          window.clearTimeout(timeout);
          socket.close();
          resolve('opened');
        };
        socket.onerror = () => {
          window.clearTimeout(timeout);
          resolve('rejected');
        };
      });
    });
    expect(anonymousResult).toBe('rejected');

    const developerToken = createE2eJwt('ws-developer', ['developer'], 'WS Developer');
    const developerMessage = await page.evaluate(async token => {
      return await new Promise<string>(resolve => {
        const socket = new WebSocket(`ws://127.0.0.1:5173/ws/tags?access_token=${encodeURIComponent(token)}`);
        const timeout = window.setTimeout(() => {
          socket.close();
          resolve('timeout');
        }, 3000);
        socket.onmessage = event => {
          window.clearTimeout(timeout);
          socket.close();
          resolve(event.data);
        };
        socket.onerror = () => {
          window.clearTimeout(timeout);
          resolve('rejected');
        };
      });
    }, developerToken);
    expect(developerMessage).not.toBe('timeout');
    expect(developerMessage).not.toBe('rejected');
    const parsed = JSON.parse(developerMessage) as { type: string; tag: { path: string } };
    expect(parsed.type).toBe('tagValueChanged');
    expect(parsed.tag.path.startsWith('Demo.')).toBeTruthy();

    const noReadToken = createE2eJwt('ws-no-read', ['role-with-no-grants'], 'WS No Read');
    const noReadResult = await page.evaluate(async token => {
      return await new Promise<string>(resolve => {
        const socket = new WebSocket(`ws://127.0.0.1:5173/ws/tags?access_token=${encodeURIComponent(token)}`);
        let opened = false;
        const timeout = window.setTimeout(() => {
          socket.close();
          resolve(opened ? 'opened-no-message' : 'timeout-before-open');
        }, 1800);
        socket.onopen = () => { opened = true; };
        socket.onmessage = () => {
          window.clearTimeout(timeout);
          socket.close();
          resolve('leaked-message');
        };
        socket.onerror = () => {
          window.clearTimeout(timeout);
          resolve('rejected');
        };
      });
    }, noReadToken);
    expect(noReadResult).toBe('opened-no-message');

    const expiringToken = createE2eJwt('ws-expiring', ['developer'], 'WS Expiring', 5);
    const expiryResult = await page.evaluate(async token => {
      return await new Promise<string>(resolve => {
        const socket = new WebSocket(`ws://127.0.0.1:5173/ws/tags?access_token=${encodeURIComponent(token)}`);
        let opened = false;
        let receivedMessage = false;
        const timeout = window.setTimeout(() => {
          socket.close();
          resolve('timeout');
        }, 9000);
        socket.onopen = () => { opened = true; };
        socket.onmessage = () => { receivedMessage = true; };
        socket.onclose = () => {
          window.clearTimeout(timeout);
          resolve(opened && receivedMessage ? 'expired-after-message' : 'closed-too-early');
        };
      });
    }, expiringToken);
    expect(expiryResult).toBe('expired-after-message');
  } finally {
    await context.close();
  }
});
