import { randomUUID } from 'node:crypto';
import { expect, test, type Page, type Route, type TestInfo } from '@playwright/test';

const harness = '/tests-e2e/ha-admin-harness.html';

function configuration(pendingRestart = false) {
  const running = {
    enabled: true,
    clusterId: 'plant-ha',
    localNodeId: 'node-a',
    initialActiveNodeId: 'node-a',
    topologyVersion: 7,
    freshnessSeconds: 15,
    nodes: [
      { nodeId: 'node-a', localEndpoint: 'http://10.0.0.11:5080', remoteEndpoint: 'https://a.example.test' },
      { nodeId: 'node-b', localEndpoint: 'http://10.0.0.12:5080', remoteEndpoint: 'https://b.example.test' }
    ],
    peerTransport: { enabled: true, peerEndpoint: 'https://b.example.test/api/runtime/ha/peer/replicate', authenticationConfigured: true },
    protection: {
      enabled: true,
      automaticFailoverEnabled: true,
      referenceStoreMode: 'shared-external-file',
      referencePath: '/mnt/shared/ha/reference.json',
      leaseSeconds: 15,
      pollMilliseconds: 500,
      readyWitnessMaximumAgeSeconds: 60,
      clockSkewSafetyMarginSeconds: 2
    }
  };
  const desired = pendingRestart
    ? {
        ...running,
        topologyVersion: 8,
        nodes: running.nodes.map(node => node.nodeId === 'node-b'
          ? { ...node, remoteEndpoint: 'https://b2.example.test' }
          : node)
      }
    : structuredClone(running);
  return {
    schema: 'elitescada.runtime-ha-host-configuration',
    schemaVersion: 1,
    generation: pendingRestart ? 12 : 11,
    updatedAtUtc: '2026-10-02T19:40:00Z',
    pendingRestart,
    applyMode: pendingRestart ? 'restart-required' : 'running',
    industrialEffectsBlocked: pendingRestart,
    running,
    desired,
    referenceStoreRequirements: {
      mode: 'shared-external-file',
      failClosedWhenUnavailable: true,
      summary: 'External shared fencing authority.',
      requiredSemantics: ['shared storage', 'exclusive locking', 'atomic replace', 'durable read-after-write', 'fail closed']
    }
  };
}

function topology(overrides: Record<string, unknown> = {}) {
  return {
    schema: 'elitescada.runtime-ha-topology',
    schemaVersion: 1,
    enabled: true,
    clusterId: 'plant-ha',
    topologyVersion: 7,
    stateVersion: 42,
    authorityEpoch: 19,
    authorityInstanceId: '11111111-1111-1111-1111-111111111111',
    localNodeId: 'node-a',
    effectiveActiveNodeId: 'node-a',
    ambiguousAuthority: false,
    pendingTransfer: null,
    generatedAtUtc: '2026-10-02T19:40:00Z',
    nodes: [
      {
        nodeId: 'node-a', role: 'Local', state: 'Active', ready: true, healthy: true,
        synchronizationComplete: true, haLicenseEntitled: true,
        runtime: { projectKey: 'plant', revision: 22, mode: 'Active' },
        lastObservedAtUtc: '2026-10-02T19:39:59Z', fresh: true, readinessReason: 'ready',
        endpoints: [{ kind: 'Local', address: 'http://10.0.0.11:5080', priority: 0 }, { kind: 'Remote', address: 'https://a.example.test', priority: 1 }]
      },
      {
        nodeId: 'node-b', role: 'Peer', state: 'ReadyStandby', ready: true, healthy: true,
        synchronizationComplete: true, haLicenseEntitled: true,
        runtime: { projectKey: 'plant', revision: 22, mode: 'Standby' },
        lastObservedAtUtc: '2026-10-02T19:39:59Z', fresh: true, readinessReason: 'ready-standby',
        endpoints: [{ kind: 'Local', address: 'http://10.0.0.12:5080', priority: 0 }, { kind: 'Remote', address: 'https://b.example.test', priority: 1 }]
      }
    ],
    ...overrides
  };
}

function peer(connectionState = 'connected') {
  return {
    connectionState,
    authenticationConfigured: true,
    localNodeId: 'node-a',
    peerNodeId: 'node-b',
    peerEndpoint: 'https://b.example.test/api/runtime/ha/peer/replicate',
    transportInstanceId: '22222222-2222-2222-2222-222222222222',
    lastOutboundSequence: 88,
    lastInboundSequence: 87,
    lastOutboundSuccessAtUtc: '2026-10-02T19:39:59Z',
    lastInboundAtUtc: '2026-10-02T19:39:58Z',
    consecutiveFailures: connectionState === 'connected' ? 0 : 4,
    reasonCode: connectionState === 'connected' ? 'peer-send-accepted' : 'peer-timeout',
    mirror: {
      hasState: true,
      liveSynchronized: connectionState === 'connected',
      sourceNodeId: 'node-b',
      sourceTransportInstanceId: '33333333-3333-3333-3333-333333333333',
      replicationSequence: 87,
      authoritativeState: {},
      receivedAtUtc: '2026-10-02T19:39:58Z',
      reasonCode: connectionState === 'connected' ? 'peer-state-synchronized' : 'peer-stale'
    }
  };
}

type MockState = {
  topology: any;
  config: any;
  peer: any;
  protectionStatus: string;
  blocked: boolean;
  operationMode?: 'completed' | 'rejected';
  lastConfigBody?: any;
  operations: any[];
};

function healthyState(): MockState {
  return {
    topology: topology(),
    config: configuration(false),
    peer: peer(),
    protectionStatus: 'active',
    blocked: false,
    operationMode: 'completed',
    operations: []
  };
}

function administration(state: MockState) {
  return {
    schema: 'elitescada.runtime-ha-administration',
    schemaVersion: 1,
    protection: {
      enabled: true,
      automaticFailoverEnabled: true,
      industrialEffectsPermitted: !state.blocked,
      status: state.protectionStatus,
      reasonCode: state.blocked ? 'reference-unavailable' : 'local-takeover-committed',
      reference: {
        schema: 'elitescada.runtime-ha-reference-authority',
        schemaVersion: 1,
        clusterId: 'plant-ha',
        topologyVersion: 7,
        epoch: 19,
        activeNodeId: state.blocked ? null : 'node-a',
        leaseId: '44444444-4444-4444-4444-444444444444',
        leaseUntilUtc: '2026-10-02T19:41:00Z',
        updatedAtUtc: '2026-10-02T19:40:00Z',
        previousAuthorityFenced: true,
        reasonCode: state.blocked ? 'reference-unavailable' : 'reference-local-active'
      },
      lastReadyStandbyWitness: {
        nodeId: 'node-b',
        readiness: { healthy: true, synchronizationComplete: true, haLicenseEntitled: true, observedAtUtc: '2026-10-02T19:39:59Z' },
        readyAtUtc: '2026-10-02T19:39:59Z'
      },
      topology: state.topology
    },
    peer: state.peer,
    configuration: state.config,
    operations: state.operations
  };
}

function operation(kind: string, state: 'completed' | 'rejected', target: string) {
  return {
    operationId: randomUUID(),
    kind,
    state,
    sourceNodeId: 'node-a',
    targetNodeId: target,
    epoch: state === 'completed' ? 20 : 19,
    startedAtUtc: '2026-10-02T19:40:00Z',
    completedAtUtc: '2026-10-02T19:40:01Z',
    reasonCode: state === 'completed' ? kind + '-completed' : 'target-not-ready-standby'
  };
}

async function mockHa(page: Page, state: MockState) {
  await page.route('**/api/runtime/ha/**', async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path === '/api/runtime/ha/configuration' && request.method() === 'PUT') {
      state.lastConfigBody = request.postDataJSON();
      state.config = configuration(true);
      state.config.desired = {
        ...state.config.desired,
        clusterId: state.lastConfigBody.clusterId,
        localNodeId: state.lastConfigBody.localNodeId,
        initialActiveNodeId: state.lastConfigBody.initialActiveNodeId,
        topologyVersion: state.lastConfigBody.topologyVersion,
        freshnessSeconds: state.lastConfigBody.freshnessSeconds,
        nodes: state.lastConfigBody.nodes,
        peerTransport: {
          enabled: state.lastConfigBody.peerTransport.enabled,
          peerEndpoint: state.lastConfigBody.peerTransport.peerEndpoint,
          authenticationConfigured: true
        },
        protection: state.lastConfigBody.protection
      };
      await route.fulfill({ status: 202, json: { accepted: true, reasonCode: 'host-configuration-persisted-restart-required', snapshot: state.config, errors: [] } });
      return;
    }

    if (path.startsWith('/api/runtime/ha/actions/') && request.method() === 'POST') {
      const kind = path.split('/').at(-1)!;
      const body = request.postDataJSON() as { targetNodeId?: string | null };
      const result = operation(kind, state.operationMode ?? 'completed', body.targetNodeId || 'node-a');
      state.operations.unshift(result);
      await route.fulfill({ status: result.state === 'completed' ? 202 : 409, json: result });
      return;
    }

    if (path.startsWith('/api/runtime/ha/operations/')) {
      const id = path.split('/').at(-1);
      const found = state.operations.find(item => item.operationId === id);
      await route.fulfill({ status: found ? 200 : 404, json: found ?? {} });
      return;
    }

    if (path === '/api/runtime/ha/topology') return route.fulfill({ json: state.topology });
    if (path === '/api/runtime/ha/authority') return route.fulfill({ json: {
      schema: 'elitescada.runtime-ha-authority', schemaVersion: 1, enabled: true, clusterId: 'plant-ha',
      topologyVersion: state.topology.topologyVersion, authorityEpoch: state.topology.authorityEpoch,
      effectiveActiveNodeId: state.topology.effectiveActiveNodeId, ambiguousAuthority: state.topology.ambiguousAuthority,
      blocked: state.blocked, activeEndpoints: state.blocked ? [] : state.topology.nodes[0].endpoints,
      protectionStatus: state.protectionStatus, generatedAtUtc: state.topology.generatedAtUtc
    } });
    if (path === '/api/runtime/ha/administration') return route.fulfill({ json: administration(state) });
    if (path === '/api/runtime/ha/configuration') return route.fulfill({ json: state.config });
    if (path === '/api/runtime/ha/peer/status') return route.fulfill({ json: state.peer });
    await route.fulfill({ status: 404, json: {} });
  });
}

async function open(page: Page, locale = 'pt-BR') {
  await page.goto(harness + '?locale=' + encodeURIComponent(locale));
  await expect(page.getByTestId('ha-admin-workspace')).toBeVisible();
}

async function evidence(page: Page, testInfo: TestInfo, name: string) {
  await testInfo.attach(name, { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
}

test('mounted HA admin shows healthy Active/Ready Standby authority and peer freshness', async ({ page }, testInfo) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page);

  await expect(page.getByText('node-a', { exact: true }).first()).toBeVisible();
  await expect(page.getByText(/Ready Standby/)).toBeVisible();
  await expect(page.getByText(/connected/)).toBeVisible();
  await expect(page.getByText('active', { exact: true })).toBeVisible();
  await evidence(page, testInfo, 'ha-active-standby-healthy');
});

test('mounted HA admin exposes degraded/blocked fail-closed state without promotion controls', async ({ page }, testInfo) => {
  const state = healthyState();
  state.blocked = true;
  state.protectionStatus = 'degraded';
  state.peer = peer('stale');
  state.topology = topology({ effectiveActiveNodeId: null });
  await mockHa(page, state);
  await open(page);

  await expect(page.getByText('blocked', { exact: true })).toBeVisible();
  await expect(page.getByText(/reference-unavailable/)).toBeVisible();
  await expect(page.getByRole('button', { name: /force active/i })).toHaveCount(0);
  await evidence(page, testInfo, 'ha-degraded-blocked');
});

test('configuration editing keeps peer secret write-only and truthfully shows restart-required', async ({ page }, testInfo) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page);

  const secret = 'this-is-a-new-peer-shared-secret-with-40-bytes';
  await page.getByLabel('Novo peer shared secret').fill(secret);
  await page.getByLabel('Versão da topologia').fill('8');
  const nodeB = page.locator('fieldset').filter({ hasText: 'node-b' });
  await nodeB.getByLabel('Endpoint Remote').fill('https://b2.example.test');
  await page.getByRole('button', { name: 'Salvar configuração desejada' }).click();

  await expect(page.getByTestId('ha-restart-required')).toBeVisible();
  await expect(page.getByTestId('ha-peer-secret')).toHaveValue('');
  await expect(page.getByTestId('ha-authentication-state')).toHaveText('Authentication configured');
  expect(state.lastConfigBody.peerTransport.peerSharedSecret).toBe(secret);
  expect(JSON.stringify(state.config)).not.toContain(secret);
  expect(await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }))).not.toContain(secret);
  await evidence(page, testInfo, 'ha-config-restart-required-secret-write-only');
});

test('controlled switchover requires confirmation and shows completed operation result', async ({ page }, testInfo) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page);

  await page.getByRole('button', { name: 'Switchover', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('node-a');
  await expect(dialog).toContainText('node-b');
  await page.getByTestId('ha-confirm-action').click();

  await expect(page.getByTestId('ha-operation-list')).toContainText('switchover-completed');
  await expect(page.getByTestId('ha-operation-list')).toContainText('completed');
  await evidence(page, testInfo, 'ha-switchover-completed');
});

test('backend rejection remains visible as fail-closed operation result', async ({ page }, testInfo) => {
  const state = healthyState();
  state.operationMode = 'rejected';
  await mockHa(page, state);
  await open(page);

  await page.getByRole('button', { name: 'Switchover', exact: true }).click();
  await page.getByTestId('ha-confirm-action').click();

  await expect(page.getByTestId('ha-operation-list')).toContainText('rejected');
  await expect(page.getByTestId('ha-operation-list')).toContainText('target-not-ready-standby');
  await evidence(page, testInfo, 'ha-operation-rejected');
});

test('failback and recovery are explicit confirmed backend operations', async ({ page }) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page);

  for (const label of ['Failback', 'Recovery']) {
    await page.getByRole('button', { name: label, exact: true }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await page.getByTestId('ha-confirm-action').click();
    await expect(page.getByTestId('ha-operation-list')).toContainText(label.toLowerCase());
  }
});

test('HA admin surface is available in pt-BR, English and Spanish', async ({ page }) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page, 'pt-BR');
  await expect(page.getByRole('heading', { name: 'Alta Disponibilidade' })).toBeVisible();

  await page.goto(harness + '?locale=en');
  await expect(page.getByRole('heading', { name: 'High Availability' })).toBeVisible();

  await page.goto(harness + '?locale=es');
  await expect(page.getByRole('heading', { name: 'Alta Disponibilidad' })).toBeVisible();
});
