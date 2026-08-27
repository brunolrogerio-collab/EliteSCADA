import { expect, test } from '@playwright/test';
import { engineeringLifecycleRequestBody } from '../src/engineering/engineeringLifecycleContract';
import {
  buildLifecycleSteps,
  canActivatePublished,
  canSaveWorkspace,
  confirmationText,
  lifecycleErrorText,
  projectLifecycleName,
  runtimeLifecycleName
} from '../src/engineering/EngineeringLifecycleWorkspace.logic';
import type { EngineeringLifecycleState } from '../src/engineering/engineeringLifecycleTypes';

function state(overrides: Partial<EngineeringLifecycleState> = {}): EngineeringLifecycleState {
  const base: EngineeringLifecycleState = {
    workspace: {
      projectKey: 'demo',
      projectName: 'Demo Project',
      baseRevision: 4,
      checkedOutAtUtc: '2026-08-27T18:00:00Z',
      lastSavedAtUtc: '2026-08-27T18:05:00Z',
      isDirty: false,
      changeVersion: 12
    },
    persistence: {
      enabled: true,
      provider: 'postgresql',
      configuredProjectKey: 'demo'
    },
    projectKey: 'demo',
    lifecycle: {
      projectKey: 'demo',
      status: 2,
      workingRevision: 4,
      publishedRevision: 3,
      runtimeStatus: 2,
      activeRevision: 3
    },
    revisions: [
      {
        revision: 4,
        projectKey: 'demo',
        projectName: 'Demo Project',
        engineeringSchema: 'scada.engineering',
        engineeringSchemaVersion: 9,
        savedAtUtc: '2026-08-27T18:05:00Z',
        basedOnRevision: 3
      },
      {
        revision: 3,
        projectKey: 'demo',
        projectName: 'Demo Project',
        engineeringSchema: 'scada.engineering',
        engineeringSchemaVersion: 9,
        savedAtUtc: '2026-08-27T17:30:00Z',
        basedOnRevision: 2
      }
    ],
    runtime: {
      projectKey: 'demo',
      configuredProjectKey: 'demo',
      consistent: true,
      durable: {
        projectKey: 'demo',
        status: 2,
        workingRevision: 4,
        publishedRevision: 3,
        runtimeStatus: 2,
        activeRevision: 3
      },
      live: {
        mode: 'engineering',
        projectKey: 'demo',
        revision: 3,
        activatedAtUtc: '2026-08-27T17:45:00Z'
      }
    }
  };

  return { ...base, ...overrides };
}

test.describe('Engineering Lifecycle Workspace contract', () => {
  test('mutation request bodies never claim trusted operator identity', () => {
    expect(engineeringLifecycleRequestBody('save', 'Demo Project')).toEqual({ projectName: 'Demo Project' });
    expect(engineeringLifecycleRequestBody('publish')).toEqual({});
    expect(engineeringLifecycleRequestBody('activate')).toEqual({});
    expect(engineeringLifecycleRequestBody('checkout')).toBeUndefined();

    for (const body of [
      engineeringLifecycleRequestBody('save', 'Demo Project'),
      engineeringLifecycleRequestBody('publish'),
      engineeringLifecycleRequestBody('activate')
    ]) {
      expect(body).not.toHaveProperty('savedBy');
      expect(body).not.toHaveProperty('publishedBy');
      expect(body).not.toHaveProperty('activatedBy');
    }
  });

  test('normalizes backend enum strings and numeric values without inventing lifecycle semantics', () => {
    expect(projectLifecycleName(0)).toBe('Empty');
    expect(projectLifecycleName(3)).toBe('ChangesPending');
    expect(projectLifecycleName('Published')).toBe('Published');
    expect(runtimeLifecycleName(1)).toBe('ActivationPending');
    expect(runtimeLifecycleName('Active')).toBe('Active');
  });

  test('saves only when persistence, project identity and a meaningful Working change require a revision', () => {
    expect(canSaveWorkspace(state())).toBe(false);
    expect(canSaveWorkspace(state({ workspace: { ...state().workspace, isDirty: true } }))).toBe(true);
    expect(canSaveWorkspace(state({ workspace: { ...state().workspace, baseRevision: null } }))).toBe(true);
    expect(canSaveWorkspace(state({ persistence: { enabled: false, configuredProjectKey: 'demo' } }))).toBe(false);
  });

  test('activation requires a Published revision and exact configured Runtime project binding', () => {
    expect(canActivatePublished(state())).toBe(true);
    expect(canActivatePublished(state({ persistence: { enabled: true, configuredProjectKey: null } }))).toBe(false);
    expect(canActivatePublished(state({ persistence: { enabled: true, configuredProjectKey: 'other' } }))).toBe(false);
    expect(canActivatePublished(state({ lifecycle: { ...state().lifecycle!, publishedRevision: null } }))).toBe(false);
  });

  test('projects dirty Working and Runtime divergence as attention states', () => {
    const dirty = state({
      workspace: { ...state().workspace, isDirty: true },
      runtime: { ...state().runtime!, consistent: false }
    });

    const steps = buildLifecycleSteps(dirty);
    expect(steps.find(step => step.key === 'working')?.state).toBe('warning');
    expect(steps.find(step => step.key === 'active')?.state).toBe('warning');
    expect(steps.find(step => step.key === 'published')?.revision).toBe(3);
  });

  test('critical lifecycle operations provide explicit localized confirmation text', () => {
    expect(confirmationText('checkout', 'pt-BR', 4).description).toContain('4');
    expect(confirmationText('publish', 'en', 4).description).toContain('does not activate Runtime');
    expect(confirmationText('activate', 'es').description).toContain('Runtime Active');
  });

  test('maps authorization, conflict, validation and unavailable statuses to understandable messages', () => {
    const makeError = (status: number) => Object.assign(new Error(`server-${status}`), { status });
    expect(lifecycleErrorText(makeError(401), 'pt-BR')).toContain('autenticar');
    expect(lifecycleErrorText(makeError(403), 'en')).toContain('not authorized');
    expect(lifecycleErrorText(makeError(409), 'en')).toContain('conflicts');
    expect(lifecycleErrorText(makeError(422), 'es')).toContain('validada');
    expect(lifecycleErrorText(makeError(503), 'pt-BR')).toContain('indisponível');
  });
});
