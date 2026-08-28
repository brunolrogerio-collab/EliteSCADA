import { expect, test } from '@playwright/test';
import {
  CLIENT_VISUAL_PYTHON_BRIDGE_VERSION,
  CLIENT_VISUAL_PYTHON_CAPABILITIES,
  CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES,
  CLIENT_VISUAL_PYTHON_POLICY,
  hasMatchingPythonRuntimeIdentity
} from '../src/python-runtime/pythonRuntimeContracts';

test('Wave 06 development host is cross-origin isolated for bounded Pyodide interruption', async ({ page }) => {
  const response = await page.goto('/');
  expect(response).not.toBeNull();

  const headers = response!.headers();
  expect(headers['cross-origin-opener-policy']).toBe('same-origin');
  expect(headers['cross-origin-embedder-policy']).toBe('require-corp');

  const isolation = await page.evaluate(() => ({
    crossOriginIsolated: globalThis.crossOriginIsolated,
    sharedArrayBufferType: typeof SharedArrayBuffer,
    interruptSignal: (() => {
      const buffer = new SharedArrayBuffer(Int32Array.BYTES_PER_ELEMENT);
      const view = new Int32Array(buffer);
      Atomics.store(view, 0, 2);
      return Atomics.load(view, 0);
    })()
  }));

  expect(isolation.crossOriginIsolated).toBe(true);
  expect(isolation.sharedArrayBufferType).toBe('function');
  expect(isolation.interruptSignal).toBe(2);

  const health = await page.evaluate(async () => {
    const result = await fetch('/health');
    return { ok: result.ok, status: result.status };
  });

  expect(health.ok).toBe(true);
  expect(health.status).toBe(200);
});

test('Client Visual bridge v1 keeps the safe execution policy and capability surface fail-closed', () => {
  expect(CLIENT_VISUAL_PYTHON_BRIDGE_VERSION).toBe(1);
  expect(CLIENT_VISUAL_PYTHON_POLICY).toEqual({
    handlerTimeoutMs: 250,
    hardStopGraceMs: 50,
    maxQueuedEvents: 128,
    minimumTimerIntervalMs: 50,
    maxConsecutiveFailuresBeforeThrottle: 5,
    maxBridgeDepth: 32,
    maxBridgeNodes: 4096,
    maxBridgeStringLength: 65536,
    queueOverflowStrategy: 'coalesce-by-event-key',
    faultIsolationScope: 'script-runtime-instance'
  });

  expect([...CLIENT_VISUAL_PYTHON_CAPABILITIES]).toEqual([
    'tag.read',
    'clientMemory.read',
    'clientMemory.write',
    'visualProperty.read',
    'visualProperty.write',
    'visualTween.request',
    'backendOperation.request'
  ]);
  expect(new Set(CLIENT_VISUAL_PYTHON_CAPABILITIES).size).toBe(CLIENT_VISUAL_PYTHON_CAPABILITIES.length);

  const forbiddenCapabilities = [
    'serverMemory.read',
    'serverMemory.write',
    'sharedTag.write',
    'driver.access',
    'database.access',
    'filesystem.access',
    'network.fetch',
    'browser.dom',
    'browser.storage',
    'credential.read'
  ];
  for (const capability of forbiddenCapabilities) {
    expect(CLIENT_VISUAL_PYTHON_CAPABILITIES).not.toContain(capability);
  }

  expect([...CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES]).toEqual([
    'filesystem',
    'operating-system',
    'shell-process',
    'arbitrary-network',
    'database',
    'industrial-driver',
    'secret-credential',
    'browser-dom',
    'browser-storage',
    'server-memory-write',
    'shared-tag-write-direct'
  ]);
  expect(new Set(CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES).size).toBe(CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES.length);
});

test('stale or replacement runtime identities never match the active Script runtime instance', () => {
  const active = {
    scriptId: '72000000-0000-0000-0000-000000000002',
    runtimeInstanceId: 'runtime-active',
    visualRuntimeInstanceId: 'visual-active'
  };

  expect(hasMatchingPythonRuntimeIdentity(active, { ...active })).toBe(true);
  expect(hasMatchingPythonRuntimeIdentity(active, { ...active, runtimeInstanceId: 'runtime-replacement' })).toBe(false);
  expect(hasMatchingPythonRuntimeIdentity(active, { ...active, visualRuntimeInstanceId: 'visual-replacement' })).toBe(false);
  expect(hasMatchingPythonRuntimeIdentity(active, { ...active, scriptId: '72000000-0000-0000-0000-000000000003' })).toBe(false);
});
