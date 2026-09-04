import { expect, test, type APIRequestContext } from '@playwright/test';

test.describe.configure({ mode: 'serial' });

type VisualElement = {
  id?: string | null;
  key: string;
  actions?: Array<Record<string, unknown>> | null;
  children?: VisualElement[] | null;
};

type VisualDefinition = {
  id?: string | null;
  key: string;
  elements?: VisualElement[] | null;
};

type C16Package = {
  startupScreenId?: string | null;
  screens?: VisualDefinition[] | null;
  popups?: VisualDefinition[] | null;
  dynamos?: VisualDefinition[] | null;
  commands?: Array<{ id?: string | null; enabled?: boolean }> | null;
  [key: string]: unknown;
};

type Preview = {
  canApply: boolean;
  errorCount: number;
  items: Array<{ issues?: Array<{ code: string; message: string; isError: boolean }> | null }>;
};

const unresolvedId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

function issues(preview: Preview) {
  return preview.items.flatMap(item => item.issues ?? []);
}

function firstElement(definitions: VisualDefinition[] | null | undefined) {
  for (const definition of definitions ?? []) {
    const element = findElement(definition.elements ?? []);
    if (element) return { definition, element };
  }
  return null;
}

function findElement(elements: VisualElement[]): VisualElement | null {
  for (const element of elements) {
    if (element.id) return element;
    const nested = findElement(element.children ?? []);
    if (nested) return nested;
  }
  return null;
}

async function exportPackage(request: APIRequestContext): Promise<C16Package> {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok()).toBeTruthy();
  return await response.json() as C16Package;
}

async function previewPackage(request: APIRequestContext, candidate: C16Package): Promise<Preview> {
  const response = await request.post('/api/engineering/import/json/preview', {
    headers: { 'content-type': 'application/json; charset=utf-8' },
    data: candidate
  });
  expect(response.ok()).toBeTruthy();
  return await response.json() as Preview;
}

async function applyPackage(request: APIRequestContext, candidate: C16Package) {
  const workspaceResponse = await request.get('/api/engineering/workspace');
  expect(workspaceResponse.ok()).toBeTruthy();
  const workspace = await workspaceResponse.json() as { changeVersion: number };
  const response = await request.post('/api/engineering/import/json/apply', {
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'x-elitescada-workspace-version': String(workspace.changeVersion)
    },
    data: candidate
  });
  expect(response.ok()).toBeTruthy();
}

test('Engineering Preview rejects an unresolved persisted Startup/Home Screen identity', async ({ request }) => {
  const original = await exportPackage(request);
  const candidate = structuredClone(original);
  candidate.startupScreenId = unresolvedId;

  const preview = await previewPackage(request, candidate);
  expect(preview.canApply).toBeFalsy();
  expect(issues(preview).some(issue => issue.code === 'STARTUP_SCREEN_NOT_FOUND' && issue.isError)).toBeTruthy();
});

test('Engineering Preview resolves ExecuteCommand by stable Command ID for Screen, Popup and Dynamo', async ({ request }) => {
  const original = await exportPackage(request);
  const commandId = original.commands?.find(command => command.enabled !== false && command.id)?.id;
  expect(commandId).toBeTruthy();

  for (const section of ['screens', 'popups', 'dynamos'] as const) {
    const candidate = structuredClone(original);
    const target = firstElement(candidate[section]);
    expect(target, `${section} requires at least one stable visual object for C16 acceptance`).toBeTruthy();
    target!.element.actions = [
      ...(target!.element.actions ?? []),
      {
        eventKey: `c16-${section}-execute`,
        kind: 'executeCommand',
        targetKey: null,
        commandId,
        parameters: null,
        version: 1
      }
    ];

    const preview = await previewPackage(request, candidate);
    const commandIssues = issues(preview).filter(issue => issue.code.startsWith('VISUAL_ACTION_COMMAND'));
    expect(commandIssues, `${section} ExecuteCommand should resolve the canonical Command`).toEqual([]);
    expect(preview.canApply).toBeTruthy();
  }
});

test('Engineering Preview rejects ExecuteCommand when the stable Command ID is unresolved', async ({ request }) => {
  const original = await exportPackage(request);
  const candidate = structuredClone(original);
  const target = firstElement(candidate.screens) ?? firstElement(candidate.popups) ?? firstElement(candidate.dynamos);
  expect(target).toBeTruthy();
  target!.element.actions = [
    ...(target!.element.actions ?? []),
    {
      eventKey: 'c16-unresolved-command',
      kind: 'executeCommand',
      targetKey: null,
      commandId: unresolvedId,
      parameters: null,
      version: 1
    }
  ];

  const preview = await previewPackage(request, candidate);
  expect(preview.canApply).toBeFalsy();
  expect(issues(preview).some(issue => issue.code === 'VISUAL_ACTION_COMMAND_NOT_FOUND' && issue.isError)).toBeTruthy();
});

test('Startup/Home can be persisted and explicitly cleared without lexical fallback state leaking in registry', async ({ request }) => {
  const original = await exportPackage(request);
  const home = original.screens?.find(screen => screen.id);
  expect(home?.id).toBeTruthy();

  try {
    const configured = structuredClone(original);
    configured.startupScreenId = home!.id!;
    await applyPackage(request, configured);

    const afterConfigure = await exportPackage(request);
    expect(afterConfigure.startupScreenId?.toLowerCase()).toBe(home!.id!.toLowerCase());

    const cleared = structuredClone(afterConfigure);
    cleared.startupScreenId = null;
    await applyPackage(request, cleared);

    const afterClear = await exportPackage(request);
    expect(afterClear.startupScreenId ?? null).toBeNull();
  } finally {
    await applyPackage(request, original);
  }
});