import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import type { AuthorityPolicyDocument, AuthorityRole } from '../src/engineering/userAdministrationApi';

test('explicit global Intelligence grant can be assigned to a nonadministrator and enables its tools', async ({ request, browser }) => {
  test.setTimeout(60_000);
  const auth = await (await request.get('/api/auth/config', { headers: { Authorization: '' } })).json();
  if (auth.initialAdministratorRequired) {
    const bootstrap = await request.post('/api/auth/bootstrap', { headers: { Authorization: '' }, data: {
      username: 'intelligence-grant-admin', displayName: 'Grant Test Administrator', password: 'Grant-test-admin-123!'
    } });
    expect(bootstrap.status()).toBe(200);
  }
  const baseline = await (await request.get('/api/auth/authority-policy')).json() as AuthorityPolicyDocument;
  const isIntelligence = (grant: { capability: number | string }) =>
    grant.capability === 15 || String(grant.capability).toLowerCase() === 'assetprocessintelligencemanage';
  const developer = baseline.roles.find(role => role.key === 'developer')!;
  expect(developer.grants?.some(grant => isIntelligence(grant) && !grant.scope)).toBe(true);

  const suffix = randomUUID();
  const role: AuthorityRole = { id: randomUUID(), key: `process-engineer-${suffix}`, name: 'Process Engineer', grants: [
    { capability: 'EngineeringView' }, { capability: 'EngineeringModify' },
    { capability: 'AssetProcessIntelligenceManage', scope: null }
  ] };
  const apply = async (policy: AuthorityPolicyDocument, roles: AuthorityRole[]) => {
    const data = { schema: policy.schema, schemaVersion: policy.schemaVersion,
      expectedVersion: policy.version, roles, scopes: policy.scopes };
    const preview = await request.post('/api/auth/authority-policy/preview', { data });
    const previewBody = await preview.text();
    expect(previewBody).not.toContain('INTELLIGENCE_MANAGER_SCOPE_REQUIRED');
    expect(preview.status(), previewBody).toBe(200);
    const applied = await request.put('/api/auth/authority-policy', { data });
    expect(applied.status()).toBe(200);
    return await applied.json() as AuthorityPolicyDocument;
  };

  let current = await apply(baseline, [...baseline.roles, role]);
  let userId: string | undefined;
  const userContext = await browser.newContext({ baseURL: 'http://127.0.0.1:5173', extraHTTPHeaders: { Authorization: '' } });
  try {
    const username = `intelligence-${suffix}`;
    const password = 'Intelligence-grant-test-123!';
    const created = await request.post('/api/auth/users', { data: {
      username, displayName: 'Intelligence Grant Test', password, roles: [role.key], isEnabled: true
    } });
    expect(created.status()).toBe(201);
    userId = (await created.json()).id;
    expect((await userContext.request.post('/api/auth/login', { data: { username, password } })).status()).toBe(200);
    const effective = await (await userContext.request.get('/api/auth/effective-capabilities')).json();
    expect(effective.workspace).toContain('AssetProcessIntelligenceManage');
    expect(effective.workspace).not.toContain('SystemAdmin');

    const page = await userContext.newPage();
    // Only process metadata is a fixture. Policy preview/apply, local identity,
    // login and effective-capabilities above all use the real API.
    const equipmentId = randomUUID(), profileId = randomUUID(), assignmentId = randomUUID(), tagId = randomUUID();
    await page.route('**/api/engineering/asset-process-intelligence/semantic-contracts/v2/catalog', route => route.fulfill({ json: {
      supportedSemanticContractVersions: [1, 2],
      semanticCatalogs: [{ catalogVersion: 1, roles: [] }, { catalogVersion: 2, roles: [] }],
      controlledEngineeringUnitCatalogs: [{ catalogVersion: 1, units: [] }, { catalogVersion: 2, units: [] }]
    } }));
    await page.route('**/api/engineering/asset-process-intelligence/scopes', route => route.fulfill({ json: {
      configuration: { version: 3, scopes: [], profiles: [{ id: profileId, key: 'pump', name: 'Pump', signals: [
        { id: randomUUID(), key: 'current', signalRole: 'process', tagId, required: true }
      ] }], assignments: [{ id: assignmentId, equipmentId, profileId }], processStages: [], operatingContextProfiles: [] },
      resources: [{ kind: 'Equipment', scopeId: equipmentId, path: 'Plant.Pump01', name: 'Pump P01' }],
      tags: [{ id: tagId, name: 'Current', path: 'Plant.Pump.Current', dataType: 'Double', engineeringUnit: 'A' }],
      licensed: true, execution: { stage: 'Stage1', workerRegistered: false, queueDepth: 0,
        activePermitCount: 0, projectKey: 'test-project', activeRevision: 1, workspaceDirty: false }
    } }));
    await page.goto('/tests-e2e/harness/authoring-preview.html?intelligence&locale=en');
    const contexts = page.getByTestId('intelligence-context-profiles');
    await contexts.getByLabel('Equipment assignment').selectOption(assignmentId, { timeout: 10_000 });
    await expect(contexts.getByRole('button', { name: 'New context', exact: true })).toBeEnabled();
    await contexts.getByRole('button', { name: 'New context', exact: true }).click();
    await expect(contexts.getByLabel('Operator label', { exact: true })).toBeEditable();

    current = await apply(current, [...baseline.roles, { ...role, grants: role.grants!.filter(grant => !isIntelligence(grant)) }]);
    const revoked = await (await userContext.request.get('/api/auth/effective-capabilities')).json();
    expect(revoked.workspace).not.toContain('AssetProcessIntelligenceManage');
    await page.reload();
    await expect(contexts.getByLabel('Equipment assignment')).toBeDisabled();
    await expect(contexts.getByRole('button', { name: 'New context', exact: true })).toBeDisabled();
  } finally {
    await userContext.close();
    if (userId) {
      const disabled = await request.put(`/api/auth/users/${userId}`, { data: {
        displayName: 'Intelligence Grant Test', isEnabled: false, roles: []
      } });
      expect(disabled.status()).toBe(200);
    }
    await apply(current, baseline.roles);
  }
});
