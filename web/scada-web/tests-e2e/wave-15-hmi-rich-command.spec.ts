import { expect, test, type Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {
  executeRuntimeRichCommand,
  loadRuntimeRichCommandDefinition,
  type RuntimeCommandFetch,
  type RuntimeRichCommandDefinition
} from '../src/runtime/visual-navigation/runtimeCommandApi';

const SESSION_ID = '11111111-2222-3333-4444-555555555555';
const COMMAND_ID = '8f1fd44e-c3ea-4a19-8e2f-776b3eecc19a';

function sessionResponse(init?: RequestInit) {
  return new Response(JSON.stringify({
    sessionId: SESSION_ID,
    clientInstanceId: JSON.parse(String(init?.body)).clientInstanceId
  }), { status: 201, headers: { 'content-type': 'application/json' } });
}

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

test('HMI Rich Command definition reads only the Active display schema', async () => {
  const calls: Array<{ input: string; init?: RequestInit }> = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    const url = String(input);
    calls.push({ input: url, init });
    if (url === '/api/runtime/sessions') return sessionResponse(init);
    return new Response(JSON.stringify({
      commandId: COMMAND_ID,
      semanticKey: 'cover.move',
      description: 'Move cover',
      parameters: [{
        key: 'position',
        required: true,
        schema: { kind: 'Percentage', minimum: 0, maximum: 100, unit: '%' }
      }]
    }), { status: 200, headers: { 'content-type': 'application/json' } });
  };

  const definition = await loadRuntimeRichCommandDefinition(COMMAND_ID, fetcher);

  expect(definition.semanticKey).toBe('cover.move');
  expect(definition.parameters[0].schema.kind).toBe('Percentage');
  expect(calls.map(call => call.input)).toEqual([
    '/api/runtime/rich-commands/' + COMMAND_ID + '/definition'
  ]);
  expect(calls[0].init?.method).toBe('GET');
  expect(calls[0].init?.credentials).toBe('same-origin');
  expect(calls[0].init?.headers).toMatchObject({ accept: 'application/json' });
});

test('HMI Rich Command invocation sends one canonical ID and invocation-time typed values', async () => {
  const calls: Array<{ input: string; init?: RequestInit }> = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    const url = String(input);
    calls.push({ input: url, init });
    if (url === '/api/runtime/sessions') return sessionResponse(init);
    return new Response(JSON.stringify({
      invocationId: 'e2d7b934-662a-4fdc-9cdf-df98b2a4b901',
      commandId: COMMAND_ID,
      outcome: 'Completed',
      observedAt: '2026-10-08T00:00:00Z'
    }), { status: 200, headers: { 'content-type': 'application/json' } });
  };

  const result = await executeRuntimeRichCommand(COMMAND_ID, { position: '37.5' }, fetcher);

  expect(result.outcome).toBe('Completed');
  expect(calls).toHaveLength(2);
  expect(calls[1].input).toBe('/api/runtime/rich-commands/' + COMMAND_ID + '/execute');
  expect(calls[1].init?.method).toBe('POST');
  expect(JSON.parse(String(calls[1].init?.body))).toEqual({
    commandId: COMMAND_ID,
    parameters: { position: '37.5' }
  });
  expect(calls[1].init?.headers).toMatchObject({
    accept: 'application/json',
    'content-type': 'application/json; charset=utf-8',
    'X-EliteSCADA-Runtime-Session': SESSION_ID
  });
});

test('HMI parameterless Rich Command submits an empty invocation payload', async () => {
  const calls: Array<{ input: string; init?: RequestInit }> = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    const url = String(input);
    calls.push({ input: url, init });
    if (url === '/api/runtime/sessions') return sessionResponse(init);
    return new Response(JSON.stringify({
      commandId: COMMAND_ID,
      outcome: 'Accepted',
      observedAt: '2026-10-08T00:00:00Z'
    }), { status: 200, headers: { 'content-type': 'application/json' } });
  };

  const result = await executeRuntimeRichCommand(COMMAND_ID, {}, fetcher);

  expect(result.outcome).toBe('Accepted');
  expect(JSON.parse(String(calls[1].init?.body))).toEqual({
    commandId: COMMAND_ID,
    parameters: {}
  });
});

test('HMI Rich Command transport ambiguity becomes Unknown and never retries', async () => {
  const calls: string[] = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    const url = String(input);
    calls.push(url);
    if (url === '/api/runtime/sessions') return sessionResponse(init);
    throw new TypeError('connection closed after request send');
  };

  const result = await executeRuntimeRichCommand(COMMAND_ID, {}, fetcher);

  expect(result.outcome).toBe('Unknown');
  expect(calls.filter(url => url.endsWith('/execute'))).toHaveLength(1);
});

test('Rich Command authoring is Version 2, limited to Screen and Popup, with invocation-time values', async () => {
  const contracts = await source('../../../src/Scada.Engineering/Contracts/VisualCompositionEngineeringContracts.cs');
  const validation = await source('../../../src/Scada.Engineering/Validation/VisualCompositionEngineeringValidation.cs');
  const exchange = await source('../../../src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs');
  const panel = await source('../src/engineering/visual-editor/HmiOperationalConfigurationPanel.tsx');
  const navigator = await source('../src/runtime/visual-navigation/RuntimeVisualNavigator.tsx');
  const service = await source('../../../src/Scada.Api/Runtime/HmiRichCommandInvocationService.cs');

  expect(contracts).toContain('ExecuteRichCommand');
  expect(contracts).toContain('public const int RichCommand = 2');
  expect(validation).toContain('VISUAL_RICH_COMMAND_ACTION_VERSION_UNSUPPORTED');
  expect(validation).toContain('VISUAL_RICH_COMMAND_PARAMETERS_NOT_ALLOWED');
  expect(exchange).toContain('VISUAL_RICH_COMMAND_NOT_FOUND');
  expect(exchange).toContain('ExecuteRichCommand actions are supported only by Screen and Popup');
  expect(panel).toContain("kind: 'ExecuteRichCommand'");
  expect(panel).toContain('version: 2');
  expect(panel).toContain("definitionKind === 'dynamo'");
  expect(navigator).toContain("action.kind === 'ExecuteRichCommand'");
  expect(navigator).toContain('data-testid="runtime-rich-command-parameters"');
  expect(navigator).toContain('definition.parameters.length === 0');
  expect(navigator).toContain('setRichCommandPrompt');
  expect(service).toContain('InteractionOrigin.Hmi');
  expect(service).toContain('SecurityCapability.CommandExecute');
  expect(service).toContain('AuditActions.CommandExecute');
  expect(service).toContain('IRichCommandDefinitionResolver');
  expect(service).toContain('IRichCommandBindingResolver');
});
const BROWSER_SESSION_ID = '22222222-3333-4444-5555-666666666666';
const BROWSER_COMMAND_PARAMETERLESS = '11111111-aaaa-4aaa-8aaa-111111111111';
const BROWSER_COMMAND_TYPED = '22222222-aaaa-4aaa-8aaa-222222222222';
const BROWSER_COMMAND_UNKNOWN = '33333333-aaaa-4aaa-8aaa-333333333333';
const BROWSER_COMMAND_POPUP = '44444444-aaaa-4aaa-8aaa-444444444444';
const BROWSER_LEGACY_COMMAND = '55555555-aaaa-4aaa-8aaa-555555555555';
const BROWSER_TAG_ID = '66666666-aaaa-4aaa-8aaa-666666666666';

function browserButton(
  id: string,
  key: string,
  text: string,
  y: number,
  action: Readonly<Record<string, unknown>>
) {
  return {
    id,
    key,
    type: 'core.button',
    properties: { x: 32, y, width: 280, height: 56, text },
    actions: [action]
  };
}

const richCommandBrowserProjection = {
  mode: 'engineering',
  projectKey: 's5-hmi-rich-command-browser',
  projectName: 'S5 HMI Rich Command browser proof',
  revision: 1,
  activatedAtUtc: '2026-10-09T00:00:00Z',
  package: {
    schema: 'scada.engineering',
    schemaVersion: 23,
    startupScreenId: '77777777-aaaa-4aaa-8aaa-777777777777',
    screens: [{
      id: '77777777-aaaa-4aaa-8aaa-777777777777',
      key: 'home',
      name: 'Home',
      elements: [
        browserButton('s5-rich-parameterless-screen', 'rich-parameterless-screen', 'Comando sem parâmetros', 32, {
          eventKey: 'click', kind: 'ExecuteRichCommand', commandId: BROWSER_COMMAND_PARAMETERLESS, version: 2
        }),
        browserButton('s5-rich-typed-screen', 'rich-typed-screen', 'Comando tipado', 100, {
          eventKey: 'click', kind: 'ExecuteRichCommand', commandId: BROWSER_COMMAND_TYPED, version: 2
        }),
        browserButton('s5-rich-cancel-screen', 'rich-cancel-screen', 'Comando para cancelar', 168, {
          eventKey: 'click', kind: 'ExecuteRichCommand', commandId: BROWSER_COMMAND_TYPED, version: 2
        }),
        browserButton('s5-rich-unknown-screen', 'rich-unknown-screen', 'Comando resultado incerto', 236, {
          eventKey: 'click', kind: 'ExecuteRichCommand', commandId: BROWSER_COMMAND_UNKNOWN, version: 2
        }),
        browserButton('s5-rich-open-popup-screen', 'rich-open-popup-screen', 'Abrir utilitários', 304, {
          eventKey: 'click', kind: 'OpenPopup', targetKey: 'rich-tools', version: 1
        }),
        browserButton('s5-rich-concurrent-a', 'rich-concurrent-a', 'Comando concorrente A', 372, {
          eventKey: 'click', kind: 'ExecuteRichCommand', commandId: '77777777-bbbb-4bbb-8bbb-777777777771', version: 2
        }),
        browserButton('s5-rich-concurrent-b', 'rich-concurrent-b', 'Comando concorrente B', 440, {
          eventKey: 'click', kind: 'ExecuteRichCommand', commandId: '77777777-bbbb-4bbb-8bbb-777777777772', version: 2
        })
      ]
    }],
    popups: [{
      id: '88888888-aaaa-4aaa-8aaa-888888888888',
      key: 'rich-tools',
      name: 'Rich Command tools',
      properties: { width: 520, height: 320 },
      elements: [
        browserButton('s5-rich-popup-command', 'rich-popup-command', 'Comando Rich no Popup', 20, {
          eventKey: 'click', kind: 'ExecuteRichCommand', commandId: BROWSER_COMMAND_POPUP, version: 2
        }),
        browserButton('s5-rich-popup-legacy', 'rich-popup-legacy', 'Comando legado no Popup', 88, {
          eventKey: 'click', kind: 'ExecuteCommand', commandId: BROWSER_LEGACY_COMMAND, version: 1
        }),
        browserButton('s5-rich-popup-tag', 'rich-popup-tag', 'Escrever TAG no Popup', 156, {
          eventKey: 'click', kind: 'SetTagValue', targetKey: BROWSER_TAG_ID, parameters: { value: 42 }, version: 1
        })
      ]
    }],
    dynamos: [],
    scripts: [],
    scriptVisualEventReferences: [],
    visualAssets: []
  }
} as const;

function browserTypedDefinition(commandId: string, semanticKey: string): RuntimeRichCommandDefinition {
  return {
    commandId,
    semanticKey,
    description: 'Defina a posição solicitada.',
    parameters: [{
      key: 'position',
      required: true,
      description: 'Posição',
      schema: { kind: 'Percentage', minimum: 0, maximum: 100, unit: '%' }
    }]
  };
}

async function installRichCommandBrowserShell(page: Page) {
  await page.route('**/api/auth/config', route => route.fulfill({
    json: {
      authenticationEnabled: true,
      localLoginEnabled: true,
      initialAdministratorRequired: false,
      initialAdministratorSetupAvailable: false,
      initialAdministratorBlockedReason: null,
      passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
    }
  }));
  await page.route('**/api/auth/me', route => route.fulfill({
    json: {
      subjectId: 's5-rich-command-user',
      username: 's5-rich-command-user',
      displayName: 'S5 Rich Command User',
      roles: ['developer'],
      identityProvider: 'local'
    }
  }));
  await page.route('**/api/auth/local-session', route => route.fulfill({
    json: { authenticated: true, username: 's5-rich-command-user' }
  }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({
    json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: true,
      runtime: ['View', 'TrendUse', 'SystemAdmin'],
      workspace: ['EngineeringView', 'EngineeringModify', 'UserRoleAdmin', 'SystemAdmin']
    }
  }));
  await page.route('**/api/engineering/persistence/status', route => route.fulfill({
    json: { enabled: true, hasProjects: true }
  }));
  await page.route('**/api/runtime/application', route => route.fulfill({
    json: richCommandBrowserProjection
  }));
  await page.route('**/api/runtime/sessions', async route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string };
    await route.fulfill({
      status: 201,
      json: {
        sessionId: BROWSER_SESSION_ID,
        clientInstanceId: request.clientInstanceId,
        requestedClass: 'interactive',
        grantedClass: 'interactive',
        admissionReasonCode: null,
        capacityReasonCode: null
      }
    });
  });
}

async function routeRichCommandDefinitions(
  page: Page,
  definitions: Readonly<Record<string, RuntimeRichCommandDefinition>>
) {
  await page.route('**/api/runtime/rich-commands/*/definition', async route => {
    const commandId = new URL(route.request().url()).pathname.split('/').slice(-2, -1)[0] ?? '';
    const definition = definitions[commandId];
    if (!definition) {
      await route.fulfill({ status: 404, json: { error: 'Unexpected Rich Command definition.' } });
      return;
    }
    await route.fulfill({ status: 200, json: definition });
  });
}

async function routeRichCommandExecution(
  page: Page,
  calls: Array<{ commandId: string; parameters: Record<string, unknown> }>,
  invalidSuccess = false
) {
  await page.route('**/api/runtime/rich-commands/*/execute', async route => {
    const body = route.request().postDataJSON() as { commandId: string; parameters: Record<string, unknown> };
    calls.push(body);
    if (invalidSuccess) {
      await route.fulfill({ status: 200, json: {} });
      return;
    }
    await route.fulfill({
      status: 200,
      json: {
        commandId: body.commandId,
        outcome: 'Completed',
        observedAt: '2026-10-09T00:00:00Z'
      }
    });
  });
}

test('Rendered Screen parameterless action invokes once and shows the server outcome', async ({ page }) => {
  await installRichCommandBrowserShell(page);
  await routeRichCommandDefinitions(page, {
    [BROWSER_COMMAND_PARAMETERLESS]: {
      commandId: BROWSER_COMMAND_PARAMETERLESS,
      semanticKey: 'cover.stop',
      parameters: []
    }
  });
  const calls: Array<{ commandId: string; parameters: Record<string, unknown> }> = [];
  await routeRichCommandExecution(page, calls);

  await page.goto('/');
  await page.getByRole('button', { name: 'Comando sem parâmetros' }).click();

  await expect.poll(() => calls.length).toBe(1);
  expect(calls).toEqual([{ commandId: BROWSER_COMMAND_PARAMETERLESS, parameters: {} }]);
  const feedback = page.getByTestId('runtime-action-feedback')
    .locator('[data-object-id="s5-rich-parameterless-screen"]');
  await expect(feedback).toBeVisible();
  await expect(feedback).toHaveText('Completed');
});

test('Rendered Screen typed action validates required and bounded values, then shows completion', async ({ page }) => {
  await installRichCommandBrowserShell(page);
  await routeRichCommandDefinitions(page, {
    [BROWSER_COMMAND_TYPED]: browserTypedDefinition(BROWSER_COMMAND_TYPED, 'cover.move')
  });
  const calls: Array<{ commandId: string; parameters: Record<string, unknown> }> = [];
  await routeRichCommandExecution(page, calls);

  await page.goto('/');
  await page.getByRole('button', { name: 'Comando tipado' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('heading')).toHaveText('cover.move');
  const position = dialog.locator('input[type="text"]').first();

  await dialog.locator('button[type="submit"]').click();
  await expect(dialog.locator('[role="alert"]')).toBeVisible();
  await expect(position).toHaveAttribute('aria-invalid', 'true');
  expect(calls).toHaveLength(0);

  await position.fill('101');
  await dialog.locator('button[type="submit"]').click();
  await expect(dialog.locator('[role="alert"]')).toBeVisible();
  await expect(position).toHaveAttribute('aria-invalid', 'true');
  expect(calls).toHaveLength(0);

  await position.fill('37.5');
  await dialog.locator('button[type="submit"]').click();
  await expect(dialog).toHaveCount(0);
  await expect.poll(() => calls.length).toBe(1);
  expect(calls).toEqual([{ commandId: BROWSER_COMMAND_TYPED, parameters: { position: '37.5' } }]);
  const feedback = page.getByTestId('runtime-action-feedback')
    .locator('[data-object-id="s5-rich-typed-screen"]');
  await expect(feedback).toBeVisible();
  await expect(feedback).toHaveText('Completed');
});

test('Cancelling a rendered typed Screen action sends no execute POST', async ({ page }) => {
  await installRichCommandBrowserShell(page);
  await routeRichCommandDefinitions(page, {
    [BROWSER_COMMAND_TYPED]: browserTypedDefinition(BROWSER_COMMAND_TYPED, 'cover.move')
  });
  const calls: Array<{ commandId: string; parameters: Record<string, unknown> }> = [];
  await routeRichCommandExecution(page, calls);

  await page.goto('/');
  await page.getByRole('button', { name: 'Comando para cancelar' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await dialog.locator('button[type="button"]').click();
  await expect(dialog).toHaveCount(0);
  expect(calls).toHaveLength(0);
  await expect(page.getByTestId('runtime-action-feedback')
    .locator('[data-object-id="s5-rich-cancel-screen"]')).toHaveCount(0);
});

test('Rendered Screen shows Unknown for an invalid successful response and posts once', async ({ page }) => {
  await installRichCommandBrowserShell(page);
  await routeRichCommandDefinitions(page, {
    [BROWSER_COMMAND_UNKNOWN]: {
      commandId: BROWSER_COMMAND_UNKNOWN,
      semanticKey: 'cover.stop',
      parameters: []
    }
  });
  const calls: Array<{ commandId: string; parameters: Record<string, unknown> }> = [];
  await routeRichCommandExecution(page, calls, true);

  await page.goto('/');
  await page.getByRole('button', { name: 'Comando resultado incerto' }).click();

  await expect.poll(() => calls.length).toBe(1);
  expect(calls).toEqual([{ commandId: BROWSER_COMMAND_UNKNOWN, parameters: {} }]);
  const feedback = page.getByTestId('runtime-action-feedback')
    .locator('[data-object-id="s5-rich-unknown-screen"]');
  await expect(feedback).toBeVisible();
  await expect(feedback).toHaveText('Unknown');
});

test('Rendered Popup Rich Command coexists with legacy bodyless Command and TAG write actions', async ({ page }) => {
  await installRichCommandBrowserShell(page);
  await routeRichCommandDefinitions(page, {
    [BROWSER_COMMAND_POPUP]: {
      commandId: BROWSER_COMMAND_POPUP,
      semanticKey: 'popup.reset',
      parameters: []
    }
  });
  const richCalls: Array<{ commandId: string; parameters: Record<string, unknown> }> = [];
  await routeRichCommandExecution(page, richCalls);
  const legacyCalls: Array<{ method: string; body: string | null }> = [];
  const tagCalls: Array<{ method: string; body: unknown }> = [];
  await page.route('**/api/commands/' + BROWSER_LEGACY_COMMAND + '/execute', async route => {
    legacyCalls.push({ method: route.request().method(), body: route.request().postData() });
    await route.fulfill({ status: 204 });
  });
  await page.route('**/api/tags/' + BROWSER_TAG_ID + '/write', async route => {
    tagCalls.push({ method: route.request().method(), body: route.request().postDataJSON() });
    await route.fulfill({ status: 204 });
  });
  await page.route('**/api/tags', route => route.fulfill({ status: 200, json: [] }));

  await page.goto('/');
  await page.getByRole('button', { name: 'Abrir utilitários' }).click();
  const popup = page.locator('[data-popup-key="rich-tools"]');
  await expect(popup).toBeVisible();

  await popup.getByRole('button', { name: 'Comando Rich no Popup' }).click();
  await expect.poll(() => richCalls.length).toBe(1);
  expect(richCalls).toEqual([{ commandId: BROWSER_COMMAND_POPUP, parameters: {} }]);
  const feedback = page.getByTestId('runtime-action-feedback')
    .locator('[data-object-id="s5-rich-popup-command"]');
  await expect(feedback).toBeVisible();
  await expect(feedback).toHaveText('Completed');

  await popup.getByRole('button', { name: 'Comando legado no Popup' }).click();
  await expect.poll(() => legacyCalls.length).toBe(1);
  expect(legacyCalls).toEqual([{ method: 'POST', body: null }]);

  await popup.getByRole('button', { name: 'Escrever TAG no Popup' }).click();
  await expect.poll(() => tagCalls.length).toBe(1);
  expect(tagCalls).toEqual([{ method: 'POST', body: { value: 42 } }]);
});

test('Concurrent Screen definition replies keep one prompt owner and release the losing action', async ({ page }) => {
  await installRichCommandBrowserShell(page);
  const pendingDefinitions: Array<{
    commandId: string;
    respond: (definition: RuntimeRichCommandDefinition) => Promise<void>;
  }> = [];
  await page.route('**/api/runtime/rich-commands/*/definition', route => new Promise<void>(resolve => {
    const commandId = new URL(route.request().url()).pathname.split('/').slice(-2, -1)[0] ?? '';
    pendingDefinitions.push({
      commandId,
      respond: async definition => {
        await route.fulfill({ status: 200, json: definition });
        resolve();
      }
    });
  }));
  const executeCalls: Array<{ commandId: string; parameters: Record<string, unknown> }> = [];
  await routeRichCommandExecution(page, executeCalls);

  await page.goto('/');
  await page.getByRole('button', { name: 'Comando concorrente A' }).click();
  await page.getByRole('button', { name: 'Comando concorrente B' }).click();
  await expect.poll(() => pendingDefinitions.length).toBe(2);

  const commandB = pendingDefinitions.find(item => item.commandId.endsWith('7772'));
  const commandA = pendingDefinitions.find(item => item.commandId.endsWith('7771'));
  expect(commandA).toBeTruthy();
  expect(commandB).toBeTruthy();
  await commandB!.respond(browserTypedDefinition(commandB!.commandId, 'rich.command.b'));
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading')).toHaveText('rich.command.b');

  await commandA!.respond(browserTypedDefinition(commandA!.commandId, 'rich.command.a'));
  await expect(page.getByTestId('runtime-action-feedback')
    .locator('[data-object-id="s5-rich-concurrent-a"]')).toHaveCount(0);
  await expect(dialog.getByRole('heading')).toHaveText('rich.command.b');

  await dialog.locator('input[type="text"]').first().fill('24');
  await dialog.locator('button[type="submit"]').click();
  await expect(dialog).toHaveCount(0);
  await expect.poll(() => executeCalls.length).toBe(1);
  expect(executeCalls).toEqual([{ commandId: commandB!.commandId, parameters: { position: '24' } }]);

  await page.getByRole('button', { name: 'Comando concorrente A' }).click();
  await expect.poll(() => pendingDefinitions.length).toBe(3);
  await pendingDefinitions[2].respond(browserTypedDefinition(commandA!.commandId, 'rich.command.a'));
  const nextDialog = page.getByRole('dialog');
  await expect(nextDialog.getByRole('heading')).toHaveText('rich.command.a');
  await nextDialog.locator('button[type="button"]').click();
  expect(executeCalls).toHaveLength(1);
});

test('Rich Command 4xx server rejection remains authoritative and single-attempt', async () => {
  const executeCalls: string[] = [];
  const fetcher: RuntimeCommandFetch = async (input, init) => {
    const url = String(input);
    if (url === '/api/runtime/sessions') return sessionResponse(init);
    executeCalls.push(url);
    return new Response('not-json', { status: 409, headers: { 'content-type': 'application/json' } });
  };

  const result = await executeRuntimeRichCommand(COMMAND_ID, {}, fetcher);

  expect(result.outcome).toBe('Rejected');
  expect(executeCalls.filter(url => url.endsWith('/execute'))).toHaveLength(1);
});

const invalidExecuteSuccessResponses: readonly Readonly<{ name: string; status: number; body: string }>[] = [
  { name: 'parseable invalid 202 JSON', status: 202, body: '{}' },
  {
    name: 'mismatched command identity',
    status: 200,
    body: JSON.stringify({
      commandId: 'different-command-id',
      outcome: 'Completed',
      observedAt: '2026-10-09T00:00:00Z'
    })
  },
  {
    name: 'unrecognized outcome',
    status: 200,
    body: JSON.stringify({
      commandId: COMMAND_ID,
      outcome: 'Queued',
      observedAt: '2026-10-09T00:00:00Z'
    })
  },
  { name: 'malformed success body', status: 200, body: 'not-json' }
];

for (const scenario of invalidExecuteSuccessResponses) {
  test('Rich Command ' + scenario.name + ' remains Unknown after one execute POST', async () => {
    const executeCalls: string[] = [];
    const fetcher: RuntimeCommandFetch = async (input, init) => {
      const url = String(input);
      if (url === '/api/runtime/sessions') return sessionResponse(init);
      executeCalls.push(url);
      return new Response(scenario.body, {
        status: scenario.status,
        headers: { 'content-type': 'application/json' }
      });
    };

    const result = await executeRuntimeRichCommand(COMMAND_ID, {}, fetcher);

    expect(result.outcome).toBe('Unknown');
    expect(executeCalls.filter(url => url.endsWith('/execute'))).toHaveLength(1);
  });
}
