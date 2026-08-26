import { expect, request as playwrightRequest, test } from '@playwright/test';
import { createE2eJwt } from './jwt';

const baseURL = 'http://127.0.0.1:5173';

test('Engineering Delete and Bulk require authority and emit structural audit events', async ({ request }) => {
  const originalResponse = await request.get('/api/engineering/export/json');
  expect(originalResponse.ok()).toBeTruthy();
  const originalPackage = await originalResponse.json() as {
    tags: Array<{ id?: string; path: string; readOnly: boolean }>;
    alarms: Array<{ id?: string; name: string }>;
    [key: string]: unknown;
  };

  const frequency = originalPackage.tags.find(tag => tag.path === 'Demo.P01.Frequency');
  const deletableAlarm = originalPackage.alarms.find(alarm => Boolean(alarm.id));
  expect(frequency?.id).toBeTruthy();
  expect(deletableAlarm?.id).toBeTruthy();

  const bulkRequest = {
    entityKind: 'tag',
    entityIds: [frequency!.id],
    tags: { readOnly: !frequency!.readOnly }
  };

  const anonymous = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: '' }
  });
  try {
    expect((await anonymous.delete(`/api/engineering/tags/${frequency!.id}`, {
      headers: { 'x-elitescada-workspace-version': '0' }
    })).status()).toBe(401);
    expect((await anonymous.post('/api/engineering/bulk/preview', {
      data: bulkRequest
    })).status()).toBe(401);
    expect((await anonymous.post('/api/engineering/bulk/apply', {
      headers: { 'x-elitescada-workspace-version': '0' },
      data: bulkRequest
    })).status()).toBe(401);
  } finally {
    await anonymous.dispose();
  }

  const operatorToken = createE2eJwt('e2e-operator', ['operator'], 'E2E Operator');
  const operator = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${operatorToken}` }
  });
  try {
    expect((await operator.delete(`/api/engineering/tags/${frequency!.id}`, {
      headers: { 'x-elitescada-workspace-version': '0' }
    })).status()).toBe(403);
    expect((await operator.post('/api/engineering/bulk/preview', {
      data: bulkRequest
    })).status()).toBe(403);
    expect((await operator.post('/api/engineering/bulk/apply', {
      headers: { 'x-elitescada-workspace-version': '0' },
      data: bulkRequest
    })).status()).toBe(403);
  } finally {
    await operator.dispose();
  }

  try {
    const workspaceBeforeResponse = await request.get('/api/engineering/workspace');
    expect(workspaceBeforeResponse.ok()).toBeTruthy();
    const workspaceBefore = await workspaceBeforeResponse.json() as { changeVersion: number };

    // The referenced TAG must fail closed. This is an authorized mutation attempt,
    // but dependency validation prevents any change to the official Workspace.
    const blockedDelete = await request.delete(`/api/engineering/tags/${frequency!.id}`, {
      headers: { 'x-elitescada-workspace-version': String(workspaceBefore.changeVersion) }
    });
    expect(blockedDelete.status()).toBe(409);
    const blocked = await blockedDelete.json() as {
      dependencies: Array<{ entityKind: string; entityKey: string; relation: string }>;
    };
    expect(blocked.dependencies.length).toBeGreaterThan(0);

    const workspaceAfterBlockedResponse = await request.get('/api/engineering/workspace');
    expect(workspaceAfterBlockedResponse.ok()).toBeTruthy();
    const workspaceAfterBlocked = await workspaceAfterBlockedResponse.json() as { changeVersion: number };
    expect(workspaceAfterBlocked.changeVersion).toBe(workspaceBefore.changeVersion);

    // Alarm Delete is explicit and has no cascade. Remove exactly one persisted Alarm.
    const alarmDelete = await request.delete(`/api/engineering/alarms/${deletableAlarm!.id}`, {
      headers: { 'x-elitescada-workspace-version': String(workspaceAfterBlocked.changeVersion) }
    });
    expect(alarmDelete.ok()).toBeTruthy();
    const alarmDeleteResult = await alarmDelete.json() as { changeVersion: number; entityId: string };
    expect(alarmDeleteResult.entityId).toBe(deletableAlarm!.id);
    expect(alarmDeleteResult.changeVersion).toBeGreaterThan(workspaceAfterBlocked.changeVersion);

    // Bulk preview is read-only. Apply must use the exact Workspace version returned by preview.
    const bulkPreviewResponse = await request.post('/api/engineering/bulk/preview', {
      data: bulkRequest
    });
    expect(bulkPreviewResponse.ok()).toBeTruthy();
    const bulkPreview = await bulkPreviewResponse.json() as {
      changeVersion: number;
      affectedCount: number;
      preview: { canApply: boolean; updateCount: number; errorCount: number };
    };
    expect(bulkPreview.affectedCount).toBe(1);
    expect(bulkPreview.preview.canApply).toBeTruthy();
    expect(bulkPreview.preview.updateCount).toBe(1);
    expect(bulkPreview.preview.errorCount).toBe(0);

    const bulkApplyResponse = await request.post('/api/engineering/bulk/apply', {
      headers: { 'x-elitescada-workspace-version': String(bulkPreview.changeVersion) },
      data: bulkRequest
    });
    expect(bulkApplyResponse.ok()).toBeTruthy();
    const bulkApply = await bulkApplyResponse.json() as {
      changeVersion: number;
      affectedCount: number;
      result: { updated: number };
    };
    expect(bulkApply.affectedCount).toBe(1);
    expect(bulkApply.result.updated).toBe(1);
    expect(bulkApply.changeVersion).toBeGreaterThan(bulkPreview.changeVersion);

    const auditResponse = await request.get('/api/audit?limit=300');
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
      event.subjectId === 'anonymous' &&
      event.action === 'engineering.delete' &&
      event.outcome === 1 &&
      event.targetId === frequency!.id)).toBeTruthy();
    expect(events.some(event =>
      event.subjectId === 'e2e-operator' &&
      event.action === 'engineering.delete' &&
      event.outcome === 1 &&
      event.targetId === frequency!.id)).toBeTruthy();
    expect(events.some(event =>
      event.subjectId === 'e2e-developer' &&
      event.action === 'engineering.delete' &&
      event.outcome === 2 &&
      event.targetId === frequency!.id &&
      event.details?.reason === 'dependencies')).toBeTruthy();
    expect(events.some(event =>
      event.subjectId === 'e2e-developer' &&
      event.action === 'engineering.delete' &&
      event.outcome === 0 &&
      event.targetId === deletableAlarm!.id)).toBeTruthy();

    expect(events.some(event =>
      event.subjectId === 'anonymous' &&
      event.action === 'engineering.bulk.apply' &&
      event.outcome === 1)).toBeTruthy();
    expect(events.some(event =>
      event.subjectId === 'e2e-operator' &&
      event.action === 'engineering.bulk.apply' &&
      event.outcome === 1)).toBeTruthy();
    expect(events.some(event =>
      event.subjectId === 'e2e-developer' &&
      event.action === 'engineering.bulk.apply' &&
      event.outcome === 0 &&
      event.targetId === 'bulk' &&
      event.details?.affectedCount === '1')).toBeTruthy();

    // Mutations may identify structure, versions and counts, never process values or credentials.
    const mutationEvents = events.filter(event =>
      event.action === 'engineering.delete' || event.action === 'engineering.bulk.apply');
    expect(mutationEvents.every(event => !event.details || !('value' in event.details))).toBeTruthy();
    expect(mutationEvents.every(event => !event.details || !('authorization' in event.details))).toBeTruthy();
  } finally {
    const restore = await request.post('/api/engineering/import/json/apply', {
      headers: { 'content-type': 'application/json; charset=utf-8' },
      data: originalPackage
    });
    expect(restore.ok()).toBeTruthy();
  }
});
