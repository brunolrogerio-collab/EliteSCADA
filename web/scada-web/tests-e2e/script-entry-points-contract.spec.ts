import { expect, test } from '@playwright/test';
import {
  isScriptEventAllowedForScope,
  retargetScriptEntryPoint,
  validateScriptDraft
} from '../src/engineering/scripts/ScriptEngineeringWorkspace.logic';

const entry = {
  eventKind: 'tagChanged' as const,
  handlerName: 'on_change',
  targetReference: null,
  tagReference: { tagId: 'tag-stable', selector: { kind: 'bit', index: 3 } },
  timerIntervalMs: null
};

test('event retargeting clears stale hidden targets and gives Timer its canonical default', () => {
  expect(retargetScriptEntryPoint(entry, 'timer')).toEqual({
    eventKind: 'timer', handlerName: 'on_change', targetReference: null,
    tagReference: null, timerIntervalMs: 1000
  });
  expect(retargetScriptEntryPoint({ ...entry, targetReference: 'memory-stable' }, 'clientMemoryChanged'))
    .toEqual({ eventKind: 'clientMemoryChanged', handlerName: 'on_change', targetReference: null, tagReference: null, timerIntervalMs: null });
});

test('entry validation keeps scope, Timer and stable event identities fail-closed', () => {
  const base = { id: 'script', name: 'Script', path: 'scripts/script.py', source: 'pass', languageVersion: '3', dependencies: [], description: null, metadata: {}, enabled: true, language: 'python' };
  expect(isScriptEventAllowedForScope('server', 'objectInteraction')).toBeFalsy();
  expect(validateScriptDraft({ ...base, scope: 'server', entryPoints: [{ ...entry, eventKind: 'objectInteraction' }] })).toContain('entryPointScope');
  expect(validateScriptDraft({ ...base, scope: 'clientVisual', entryPoints: [{ ...entry, eventKind: 'timer', tagReference: null, timerIntervalMs: 49 }] })).toContain('timerIntervalMs');
  expect(validateScriptDraft({ ...base, scope: 'clientVisual', entryPoints: [{ ...entry, eventKind: 'tagChanged', targetReference: 'legacy-path' }] })).toContain('targetReferenceUnexpected');
  expect(validateScriptDraft({ ...base, scope: 'clientVisual', entryPoints: [{ ...entry, eventKind: 'clientMemoryChanged', tagReference: null, targetReference: null }] })).toContain('targetReference');
});
