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
  licenseState: 'Demo' | 'Valid' | 'Invalid';
  haEntitled: boolean | null;
  operationMode?: 'completed' | 'rejected' | 'missing-after-restart';
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
    licenseState: 'Valid',
    haEntitled: true,
    operationMode: 'completed',
    operations: []
  };
}

function standaloneState(haEntitled = false): MockState {
  const state = healthyState();
  state.licenseState = haEntitled ? 'Valid' : 'Demo';
  state.haEntitled = haEntitled ? true : null;
  state.protectionStatus = 'disabled';
  state.blocked = false;
  state.topology = topology({
    enabled: false,
    clusterId: null,
    topologyVersion: 1,
    stateVersion: 1,
    authorityEpoch: 1,
    localNodeId: 'standalone',
    effectiveActiveNodeId: 'standalone',
    ambiguousAuthority: false,
    nodes: [{
      nodeId: 'standalone',
      role: 'Local',
      state: 'Standalone',
      ready: true,
      healthy: true,
      synchronizationComplete: true,
      haLicenseEntitled: false,
      runtime: { projectKey: 'plant', revision: 22, mode: 'engineering' },
      lastObservedAtUtc: '2026-10-02T19:39:59Z',
      fresh: true,
      readinessReason: null,
      endpoints: []
    }]
  });
  state.config = configuration(false);
  for (const key of ['running', 'desired'] as const) {
    state.config[key] = {
      ...state.config[key],
      enabled: false,
      clusterId: null,
      localNodeId: 'standalone',
      initialActiveNodeId: 'standalone',
      topologyVersion: 1,
      nodes: [{ nodeId: 'standalone', localEndpoint: null, remoteEndpoint: null }],
      peerTransport: { enabled: false, peerEndpoint: null, authenticationConfigured: false },
      protection: {
        ...state.config[key].protection,
        enabled: false,
        automaticFailoverEnabled: false,
        referencePath: null
      }
    };
  }
  state.peer = {
    ...peer('disabled'),
    authenticationConfigured: false,
    localNodeId: 'standalone',
    peerNodeId: null,
    peerEndpoint: null,
    mirror: {
      hasState: false,
      liveSynchronized: false,
      sourceNodeId: null,
      sourceTransportInstanceId: null,
      replicationSequence: null,
      authoritativeState: null,
      receivedAtUtc: null,
      reasonCode: 'ha-disabled'
    }
  };
  return state;
}

function administration(state: MockState) {
  return {
    schema: 'elitescada.runtime-ha-administration',
    schemaVersion: 1,
    protection: {
      enabled: state.config.running.protection.enabled,
      automaticFailoverEnabled: state.config.running.protection.automaticFailoverEnabled,
      industrialEffectsPermitted: !state.blocked,
      status: state.protectionStatus,
      reasonCode: state.blocked ? 'reference-unavailable' : 'local-takeover-committed',
      reference: state.topology.enabled ? {
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
      } : null,
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

function operation(kind: string, state: 'completed' | 'rejected' | 'running', target: string) {
  return {
    operationId: randomUUID(),
    kind,
    state,
    sourceNodeId: 'node-a',
    targetNodeId: target,
    epoch: state === 'completed' ? 20 : 19,
    startedAtUtc: '2026-10-02T19:40:00Z',
    completedAtUtc: state === 'running' ? null : '2026-10-02T19:40:01Z',
    reasonCode: state === 'completed' ? kind + '-completed' : state === 'running' ? 'started' : 'target-not-ready-standby'
  };
}

async function mockHa(page: Page, state: MockState) {
  await page.route('**/api/licensing/status', async route => {
    await route.fulfill({
      json: {
        license: {
          state: state.licenseState,
          tier: state.licenseState === 'Valid' ? 'Tags500' : null,
          schemaVersion: state.licenseState === 'Valid' ? 2 : null,
          haRuntime: state.haEntitled,
          diagnostic: null
        },
        runtime: {
          state: state.licenseState === 'Demo' ? 'DemoRunning' : 'Idle',
          activeLicenseState: state.licenseState,
          activeTier: state.licenseState === 'Valid' ? 'Tags500' : null,
          lastDiagnostic: null
        }
      }
    });
  });

  await page.route('**/api/runtime/ha/**', async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path === '/api/runtime/ha/configuration' && request.method() === 'PUT') {
      state.lastConfigBody = request.postDataJSON();
      const previous = state.config;
      state.config = {
        ...previous,
        generation: previous.generation + 1,
        updatedAtUtc: '2026-10-02T19:41:00Z',
        pendingRestart: true,
        applyMode: 'restart-required',
        industrialEffectsBlocked: true,
        desired: {
          ...previous.desired,
          enabled: state.lastConfigBody.enabled,
          clusterId: state.lastConfigBody.clusterId,
          localNodeId: state.lastConfigBody.localNodeId,
          initialActiveNodeId: state.lastConfigBody.initialActiveNodeId,
          topologyVersion: state.lastConfigBody.topologyVersion,
          freshnessSeconds: state.lastConfigBody.freshnessSeconds,
          nodes: state.lastConfigBody.nodes,
          peerTransport: {
            enabled: state.lastConfigBody.peerTransport.enabled,
            peerEndpoint: state.lastConfigBody.peerTransport.peerEndpoint,
            authenticationConfigured: Boolean(
              state.lastConfigBody.peerTransport.peerSharedSecret ||
              previous.desired.peerTransport.authenticationConfigured
            )
          },
          protection: state.lastConfigBody.protection
        }
      };
      await route.fulfill({ status: 202, json: { accepted: true, reasonCode: 'host-configuration-persisted-restart-required', snapshot: state.config, errors: [] } });
      return;
    }

    if (path.startsWith('/api/runtime/ha/actions/') && request.method() === 'POST') {
      const kind = path.split('/').at(-1)!;
      const body = request.postDataJSON() as { targetNodeId?: string | null };
      const disappeared = state.operationMode === 'missing-after-restart';
      const result = operation(kind, disappeared ? 'running' : state.operationMode ?? 'completed', body.targetNodeId || 'node-a');
      if (!disappeared) state.operations.unshift(result);
      await route.fulfill({ status: result.state === 'completed' || result.state === 'running' ? 202 : 409, json: result });
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
      schema: 'elitescada.runtime-ha-authority', schemaVersion: 1, enabled: state.topology.enabled, clusterId: state.topology.clusterId,
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

test('Demo keeps the HA topology and settings visible in read-only mode', async ({ page }, testInfo) => {
  const state = standaloneState(false);
  await mockHa(page, state);
  await open(page);

  await expect(page.getByTestId('ha-running-mode')).toContainText('Standalone');
  await expect(page.getByRole('button', { name: /^High Availability/ })).toBeEnabled();
  await expect(page.getByTestId('ha-topology-choice')).toContainText('Licença HA necessária');
  await page.getByTestId('ha-advanced-settings').locator('summary').first().click();
  await page.getByTestId('ha-safety-tuning').locator('summary').click();
  await expect(page.getByLabel('Lease (s)')).toBeVisible();
  await expect(page.getByLabel('Lease (s)')).toBeDisabled();
  await page.getByTestId('ha-peer-transport-override').locator('summary').click();
  await expect(page.getByTestId('ha-peer-endpoint-mode')).toBeVisible();
  await expect(page.getByLabel('Este servidor · Endereço principal')).toBeDisabled();
  await expect(page.getByLabel('Servidor parceiro · Endereço principal')).toBeDisabled();
  await expect(page.getByText('Visualização dos campos HA').first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Salvar preparação HA' })).toBeDisabled();
  await expect(page.getByRole('button', { name: /^Switchover/ })).toHaveCount(0);
  await evidence(page, testInfo, 'ha-standalone-demo-default');
});

test('an existing HA cluster remains observable but cannot be edited or operated without entitlement', async ({ page }) => {
  const state = healthyState();
  state.licenseState = 'Invalid';
  state.haEntitled = false;
  await mockHa(page, state);
  await open(page);

  await expect(page.getByTestId('ha-running-mode')).toContainText('High Availability');
  await expect(page.getByLabel('Este servidor · Endereço principal')).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Salvar configuração desejada' })).toBeDisabled();
  await expect(page.getByTestId('ha-license-actions-required')).toBeVisible();
  await expect(page.getByRole('button', { name: /^Switchover/ })).toBeDisabled();
  await expect(page.getByRole('button', { name: /^Failback/ })).toBeDisabled();
  await expect(page.getByRole('button', { name: /^Recovery/ })).toBeDisabled();
});

test('403 HA authorization is explained with the exact required capabilities', async ({ page }) => {
  await mockHa(page, healthyState());
  await page.route('**/api/runtime/ha/topology', route => route.fulfill({
    status: 403,
    contentType: 'application/json',
    body: JSON.stringify({ error: 'Forbidden' })
  }));

  await page.goto(harness);
  const denied = page.getByTestId('ha-admin-permission-required');
  await expect(denied).toBeVisible();
  await expect(denied).toContainText('HighAvailabilityObserve');
  await expect(denied).toContainText('HighAvailabilityTransfer');
  await expect(page.getByRole('link', { name: 'Abrir Segurança na Engenharia' })).toHaveAttribute('href', '/engineering/security');
});

test('licensed Standalone can prepare HA but still reports Standalone running until cold start', async ({ page }, testInfo) => {
  const state = standaloneState(true);
  await mockHa(page, state);
  await open(page);

  await expect(page.getByTestId('ha-running-mode')).toContainText('Standalone');
  await page.getByRole('button', { name: /^High Availability/ }).click();

  await expect(page.getByTestId('ha-preparing-mode')).toContainText('continua rodando em Standalone');
  await expect(page.getByTestId('ha-readiness')).toContainText('Configuração HA incompleta');
  await expect(page.getByTestId('ha-readiness')).toContainText('Ativação HA requer implantação/cold start');
  await expect(page.getByLabel('Este servidor · Endereço principal')).toBeEnabled();
  await expect(page.getByLabel('Este servidor · Endereço principal')).not.toHaveValue('');
  await expect(page.getByLabel('Servidor parceiro · Endereço principal')).toBeEnabled();
  await expect(page.getByText('Cluster ID', { exact: true })).toHaveCount(0);
  await expect(page.getByText('Node ID', { exact: true })).toHaveCount(0);
  await expect(page.getByTestId('ha-running-mode')).toContainText('Standalone');
  await evidence(page, testInfo, 'ha-standalone-licensed-preparation');
});

test('licensed Standalone auto-resolves technical HA identifiers when preparation is complete', async ({ page }, testInfo) => {
  const state = standaloneState(true);
  await mockHa(page, state);
  await open(page);

  await page.getByRole('button', { name: /^High Availability/ }).click();
  await page.getByLabel('Servidor parceiro · Endereço principal').fill('https://peer.example.test');
  await page.getByRole('button', { name: 'Configurar autenticação' }).click();

  const secret = 'standalone-preparation-secret-with-more-than-32-bytes';
  await page.getByLabel('Novo segredo compartilhado').fill(secret);

  await expect(page.getByTestId('ha-readiness')).toContainText('Configuração HA pronta para implantação');
  await page.getByRole('button', { name: 'Salvar preparação HA' }).click();

  expect(state.lastConfigBody.enabled).toBe(false);
  expect(state.lastConfigBody.clusterId).toMatch(/^ha-/);
  expect(state.lastConfigBody.nodes).toHaveLength(2);
  expect(state.lastConfigBody.nodes[0].nodeId).toMatch(/^ha-node-/);
  expect(state.lastConfigBody.nodes[1].nodeId).toMatch(/^ha-node-/);
  expect(state.lastConfigBody.nodes[0].nodeId).not.toBe(state.lastConfigBody.nodes[1].nodeId);
  expect(state.lastConfigBody.localNodeId).toBe(state.lastConfigBody.nodes[0].nodeId);
  expect(state.lastConfigBody.initialActiveNodeId).toBe(state.lastConfigBody.nodes[0].nodeId);
  expect(state.lastConfigBody.nodes[0].remoteEndpoint).toBe(state.lastConfigBody.nodes[0].localEndpoint);
  expect(state.lastConfigBody.nodes[1].remoteEndpoint).toBe(state.lastConfigBody.nodes[1].localEndpoint);
  expect(state.lastConfigBody.peerTransport.enabled).toBe(true);
  expect(state.lastConfigBody.peerTransport.peerEndpoint).toBeNull();
  expect(state.lastConfigBody.peerTransport.peerSharedSecret).toBe(secret);
  expect(state.lastConfigBody.topologyVersion).toBe(2);

  await expect(page.getByTestId('ha-running-mode')).toContainText('Standalone');
  await evidence(page, testInfo, 'ha-standalone-automatic-internal-identities');
});

test('mounted HA admin shows healthy Active/Ready Standby authority and peer freshness', async ({ page }, testInfo) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page);

  await expect(page.getByTestId('ha-peer-summary')).toContainText('Este servidor');
  await expect(page.getByTestId('ha-peer-summary')).toContainText('Servidor parceiro');
  await expect(page.getByText('node-a', { exact: true })).toHaveCount(0);
  await expect(page.getByText('node-b', { exact: true })).toHaveCount(0);
  await expect(page.getByText(/Ready Standby/)).toBeVisible();
  await expect(page.getByTestId('ha-peer-summary')).toContainText('connected');
  await expect(page.getByText('active', { exact: true })).toBeVisible();
  await expect(page.locator('.ha-summary-card')).toHaveCount(4);
  await expect(page.getByTestId('ha-advanced-settings')).not.toHaveAttribute('open', '');
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

  await expect(page.getByText('Bloqueado', { exact: true })).toBeVisible();
  await expect(page.getByText(/reference-unavailable/)).toBeVisible();
  await expect(page.getByRole('button', { name: /force active/i })).toHaveCount(0);
  await evidence(page, testInfo, 'ha-degraded-blocked');
});

test('mounted HA admin surfaces ambiguous authority without inventing an Active node', async ({ page }) => {
  const state = healthyState();
  state.protectionStatus = 'ambiguous';
  state.topology = topology({ ambiguousAuthority: true, effectiveActiveNodeId: null });
  await mockHa(page, state);
  await open(page);

  await expect(page.getByText('Autoridade ambígua', { exact: true })).toBeVisible();
  await expect(page.locator('.ha-summary-grid .ha-summary-card').filter({ hasText: 'Active efetivo' }).first()).toContainText('—');
  await expect(page.getByRole('button', { name: /force active/i })).toHaveCount(0);
});

test('advanced HA tuning stays out of the primary configuration flow until requested', async ({ page }) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page);

  await expect(page.getByTestId('ha-internal-automation')).toContainText('Identidades e versionamento automáticos');
  await expect(page.getByLabel('Servidor preferencial ao iniciar')).toHaveValue('local');
  await expect(page.getByText('Cluster ID', { exact: true })).toHaveCount(0);
  await expect(page.getByText('Node ID', { exact: true })).toHaveCount(0);

  const advanced = page.getByTestId('ha-advanced-settings');
  const safety = page.getByTestId('ha-safety-tuning');
  await expect(advanced).not.toHaveAttribute('open', '');
  await expect(page.getByLabel('Freshness (s)')).toBeHidden();

  await advanced.getByText('Configuração avançada', { exact: true }).click();
  await expect(page.getByLabel('Proteção HA habilitada')).toBeVisible();
  await expect(safety).not.toHaveAttribute('open', '');
  await safety.getByText('Ajustes técnicos de segurança', { exact: true }).click();
  await expect(page.getByLabel('Freshness (s)')).toBeVisible();

  await page.getByText('Diagnóstico técnico', { exact: true }).click();
  await expect(page.getByText('Versão da topologia', { exact: true })).toBeVisible();
});

test('peer endpoint stays automatic until an expert explicitly opens the transport override', async ({ page }) => {
  const state = healthyState();
  state.config.running.peerTransport.peerEndpoint = null;
  state.config.desired.peerTransport.peerEndpoint = null;
  await mockHa(page, state);
  await open(page);

  await expect(page.getByText('Conexão HA entre servidores', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Peer endpoint')).toHaveCount(0);

  const advanced = page.getByTestId('ha-advanced-settings');
  await advanced.getByText('Configuração avançada', { exact: true }).click();

  const transport = page.getByTestId('ha-peer-transport-override');
  await expect(transport).not.toHaveAttribute('open', '');
  await transport.getByText('Sobrescrever transporte do peer', { exact: true }).click();

  const mode = page.getByTestId('ha-peer-endpoint-mode');
  await expect(mode).toContainText('Endpoint automático');
  await page.getByRole('button', { name: 'Sobrescrever endpoint' }).click();
  await expect(page.getByLabel('Peer endpoint')).toBeVisible();
  await page.getByRole('button', { name: 'Usar endpoint automático' }).click();
  await expect(page.getByLabel('Peer endpoint')).toHaveCount(0);
});

test('configuration editing keeps peer secret write-only and truthfully shows restart-required', async ({ page }, testInfo) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page);

  const secret = 'this-is-a-new-peer-shared-secret-with-40-bytes';
  await expect(page.getByTestId('ha-peer-secret')).toHaveCount(0);
  await page.getByRole('button', { name: 'Trocar segredo' }).click();
  await page.getByLabel('Novo segredo compartilhado').fill(secret);
  await page.getByLabel('Servidor parceiro · Endereço externo (opcional)').fill('https://b2.example.test');
  await page.getByRole('button', { name: 'Salvar configuração desejada' }).click();

  await expect(page.getByTestId('ha-restart-required')).toBeVisible();
  await expect(page.getByTestId('ha-peer-secret')).toHaveCount(0);
  await expect(page.getByTestId('ha-authentication-state')).toContainText('Authentication configured');
  expect(state.lastConfigBody.peerTransport.peerSharedSecret).toBe(secret);
  expect(state.lastConfigBody.topologyVersion).toBe(8);
  expect(JSON.stringify(state.config)).not.toContain(secret);
  expect(await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }))).not.toContain(secret);
  await evidence(page, testInfo, 'ha-config-restart-required-secret-write-only');
});

test('controlled switchover requires confirmation and shows completed operation result', async ({ page }, testInfo) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page);

  await page.getByRole('button', { name: /^Switchover/ }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('Este servidor');
  await expect(dialog).toContainText('Servidor parceiro');
  await expect(dialog).not.toContainText('node-a');
  await expect(dialog).not.toContainText('node-b');
  await page.getByTestId('ha-confirm-action').click();

  await expect(page.getByTestId('ha-operation-list')).toContainText('switchover-completed');
  await expect(page.getByTestId('ha-operation-list')).toContainText('completed');
  await evidence(page, testInfo, 'ha-switchover-completed');
});

test('standby node cannot request a switchover to the already-active peer', async ({ page }) => {
  const state = healthyState();
  state.topology = topology({
    localNodeId: 'node-b',
    effectiveActiveNodeId: 'node-a',
    nodes: [
      { ...state.topology.nodes[0], nodeId: 'node-a', role: 'Peer', state: 'Active', ready: true },
      { ...state.topology.nodes[1], nodeId: 'node-b', role: 'Local', state: 'ReadyStandby', ready: true }
    ]
  });
  state.config.running.localNodeId = 'node-b';
  state.config.desired.localNodeId = 'node-b';
  state.peer.localNodeId = 'node-b';
  state.peer.peerNodeId = 'node-a';
  await mockHa(page, state);
  await open(page);

  const switchover = page.getByTestId('ha-switchover-action');
  await expect(switchover).toBeDisabled();
  await expect(switchover).toContainText('Disponível somente no nó que está como Active efetivo.');
  await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('backend rejection remains visible as fail-closed operation result', async ({ page }, testInfo) => {
  const state = healthyState();
  state.operationMode = 'rejected';
  await mockHa(page, state);
  await open(page);

  await page.getByRole('button', { name: /^Switchover/ }).click();
  await page.getByTestId('ha-confirm-action').click();

  await expect(page.getByTestId('ha-operation-list')).toContainText('rejected');
  await expect(page.getByTestId('ha-operation-list')).toContainText('target-not-ready-standby');
  await evidence(page, testInfo, 'ha-operation-rejected');
});

test('a lost operation record after API restart is not left falsely running', async ({ page }) => {
  const state = healthyState();
  state.operationMode = 'missing-after-restart';
  await mockHa(page, state);
  await open(page);

  await page.getByRole('button', { name: /^Switchover/ }).click();
  await page.getByTestId('ha-confirm-action').click();

  const operationList = page.getByTestId('ha-operation-list');
  await expect(operationList).toContainText('Interrompida · resultado não disponível após reinício da API');
  await expect(operationList).toContainText('operation-status-no-longer-available');
  await expect(operationList).not.toContainText('running');
});

test('failback and recovery are explicit confirmed backend operations', async ({ page }) => {
  const state = healthyState();
  await mockHa(page, state);
  await open(page);

  for (const label of ['Failback', 'Recovery']) {
    await page.getByRole('button', { name: new RegExp('^' + label) }).click();
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
