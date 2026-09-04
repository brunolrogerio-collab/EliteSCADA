import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {
  calculateRuntimeLogicalTransform,
  viewportPointToLogical
} from '../src/runtime/visual-navigation/runtimeLogicalCanvas';

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

test('C16 Engineering exposes canonical Startup Screen, Popup X/Y and ExecuteCommand identity', async () => {
  const contracts = await source('../../../src/Scada.Engineering/Contracts/EngineeringContracts.cs');
  const visualContracts = await source('../../../src/Scada.Engineering/Contracts/VisualCompositionEngineeringContracts.cs');
  const exchange = await source('../../../src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs');
  const registry = await source('../../../src/Scada.Engineering/Views/EngineeringViewRegistry.cs');

  expect(contracts).toContain('Guid? StartupScreenId = null');
  expect(contracts).toContain('double X = 0');
  expect(contracts).toContain('double Y = 0');
  expect(visualContracts).toContain('ExecuteCommand');
  expect(visualContracts).toContain('Guid? CommandId = null');

  expect(exchange).toContain('STARTUP_SCREEN_NOT_FOUND');
  expect(exchange).toContain('VISUAL_ACTION_COMMAND_REQUIRED');
  expect(exchange).toContain('VISUAL_ACTION_COMMAND_NOT_FOUND');
  expect(exchange).toContain('VISUAL_ACTION_COMMAND_TARGET_NOT_ALLOWED');
  expect(exchange).toContain('VISUAL_ACTION_COMMAND_PARAMETERS_NOT_ALLOWED');
  expect(registry).toContain('Popup X/Y must be finite logical HMI coordinates.');
});

test('C16 Runtime resolves Startup Screen by persisted identity and never by lexical key convention', async () => {
  const mount = await source('../src/runtime/application/RuntimeApplicationMount.tsx');

  expect(mount).toContain('startupScreenId');
  expect(mount).toContain('HMI_RUNTIME_STARTUP_SCREEN_REQUIRED');
  expect(mount).toContain('HMI_RUNTIME_STARTUP_SCREEN_UNRESOLVED');
  expect(mount).toContain('screen.id === startupScreenId');
  expect(mount).not.toContain("startsWith('00_')");
  expect(mount).not.toContain('localeCompare');
});

test('C16 visual ExecuteCommand dispatch delegates to the canonical backend authority', async () => {
  const navigator = await source('../src/runtime/visual-navigation/RuntimeVisualNavigator.tsx');
  const commandApi = await source('../src/runtime/visual-navigation/runtimeCommandApi.ts');
  const exchange = await source('../../../src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs');

  expect(navigator).toContain("action.kind === 'ExecuteCommand'");
  expect(navigator).toContain('VISUAL_RUNTIME_COMMAND_REFERENCE_REQUIRED');
  expect(navigator).toContain('await executeRuntimeCommand(commandId)');
  expect(commandApi).toContain('/api/commands/');
  expect(commandApi).toContain('/execute');
  expect(commandApi).toContain("method: 'POST'");
  expect(commandApi).not.toContain('/api/tags/');
  expect(exchange).toContain('CommandId');
});

test('C16 Popup coordinates remain logical across 720p, 1080p, 1440p and 4K viewports', () => {
  const logicalPoint = { x: 640, y: 360 };
  const viewports = [
    { width: 1280, height: 720 },
    { width: 1920, height: 1080 },
    { width: 2560, height: 1440 },
    { width: 3840, height: 2160 }
  ];

  for (const viewport of viewports) {
    const transform = calculateRuntimeLogicalTransform(
      viewport.width,
      viewport.height,
      1920,
      1080
    );
    const clientX = transform.offsetX + logicalPoint.x * transform.scale;
    const clientY = transform.offsetY + logicalPoint.y * transform.scale;
    const restored = viewportPointToLogical(clientX, clientY, 0, 0, transform);

    expect(restored).not.toBeNull();
    expect(restored!.x).toBeCloseTo(logicalPoint.x, 6);
    expect(restored!.y).toBeCloseTo(logicalPoint.y, 6);
  }
});

test('C16 Popup runtime stays inside the C09 logical stage and preserves stacking plus operator alarm overlay', async () => {
  const navigator = await source('../src/runtime/visual-navigation/RuntimeVisualNavigator.tsx');
  const mount = await source('../src/runtime/application/RuntimeApplicationMount.tsx');

  expect(navigator).toContain('resolvePopupLogicalPosition');
  expect(navigator).toContain('data-popup-logical-x={position.x}');
  expect(navigator).toContain('data-popup-logical-y={position.y}');
  expect(navigator).toContain('data-popup-stack-index={index}');
  expect(navigator).toContain('zIndex: index + 1');
  expect(navigator).toContain("pointerEvents: 'auto'");
  expect(mount).toContain('runtime-operator-overlay');
  expect(mount).toContain('<RuntimeAlarmCenter');
});