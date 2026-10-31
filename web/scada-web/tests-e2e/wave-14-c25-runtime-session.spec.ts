import { expect, test, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });

type SessionProfile = {
  subjectId: string;
  username: string;
  displayName?: string;
  roles: string[];
  identityProvider: string;
};

type LogoutMode = 'success' | 'failure';
type RuntimeSessionObservation = { clientInstanceId: string; connectionClass: string; url: string };

const administrator: SessionProfile = {
  subjectId: 'local-admin-id',
  username: 'local-admin',
  displayName: 'Administrador Local',
  roles: ['developer'],
  identityProvider: 'local'
};

const runtimeOperator: SessionProfile = {
  subjectId: 'runtime-operator-id',
  username: 'runtime-operator',
  displayName: '   ',
  roles: ['runtime-only'],
  identityProvider: 'local'
};

const externalDeveloper: SessionProfile = {
  subjectId: 'external-developer-id',
  username: 'external-developer',
  displayName: 'External Developer',
  roles: ['developer'],
  identityProvider: 'jwt'
};

const authConfiguration = {
  authenticationEnabled: true,
  localLoginEnabled: true,
  initialAdministratorRequired: false,
  initialAdministratorSetupAvailable: false,
  initialAdministratorBlockedReason: null,
  passwordPolicy: {
    minimumLength: 8,
    maximumLength: 1024
  }
};

async function installSessionContract(
  page: Page,
  initialProfile: SessionProfile | null,
  initialLogoutMode: LogoutMode = 'success',
  authenticationEnabled = true,
  onRuntimeSessionAdmission?: (admission: RuntimeSessionObservation) => void
) {
  let currentProfile: SessionProfile | null = initialProfile;
  let logoutMode = initialLogoutMode;
  let logoutRequests = 0;
  let loginRequests = 0;
  let runtimeCapabilityRequests = 0;
  let runtimeSessionAdmissions = 0;
  let runtimeSessionSequence = 0;
  const runtimeSessions = new Map<string, { clientInstanceId: string; requestedClass: string; grantedClass: string }>();

  await page.context().route('**/api/auth/config', route => route.fulfill({ json: {
    ...authConfiguration,
    authenticationEnabled,
    localLoginEnabled: authenticationEnabled && authConfiguration.localLoginEnabled
  }}));
  await page.context().route('**/api/auth/me', route => {
    if (!currentProfile) return route.fulfill({ status: 401, body: '' });
    return route.fulfill({ json: currentProfile });
  });
  await page.context().route('**/api/auth/local-session', route => route.fulfill({
    json: {
      authenticated: currentProfile?.identityProvider === 'local',
      username: currentProfile?.identityProvider === 'local' ? currentProfile.username : null
    }
  }));
  await page.context().route('**/api/engineering/persistence/status', route => route.fulfill({
    json: { enabled: true, hasProjects: true }
  }));
  await page.context().route('**/api/auth/effective-capabilities', route => {
    const runtimeOnly = currentProfile?.username === runtimeOperator.username;
    if (runtimeOnly) runtimeCapabilityRequests++;
    return route.fulfill({
      json: runtimeOnly
        ? {
            authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
            authenticationEnabled,
            runtime: ['View'],
            workspace: []
          }
        : {
            authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
            authenticationEnabled,
            runtime: ['View', 'TrendUse', 'SystemAdmin'],
            workspace: ['EngineeringView', 'EngineeringModify', 'UserRoleAdmin', 'SystemAdmin']
          }
    });
  });
  await page.context().route('**/api/auth/logout', route => {
    logoutRequests++;
    if (logoutMode === 'failure') {
      return route.fulfill({ status: 503, json: { error: 'session invalidation unavailable' } });
    }

    currentProfile = null;
    return route.fulfill({ status: 204, body: '' });
  });
  await page.context().route('**/api/auth/login', async route => {
    loginRequests++;
    const request = route.request().postDataJSON() as { username?: string; password?: string };
    if (request.username !== runtimeOperator.username || request.password !== 'RuntimePass8') {
      return route.fulfill({ status: 401, body: '' });
    }

    currentProfile = runtimeOperator;
    return route.fulfill({ json: runtimeOperator });
  });
  await page.context().route('**/api/runtime/sessions', route => {
    if (route.request().method() !== 'POST') return route.continue();
    runtimeSessionAdmissions++;
    const request = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    const sessionId = `00000000-0000-0000-0000-${String(++runtimeSessionSequence).padStart(12, '0')}`;
    runtimeSessions.set(sessionId, {
      clientInstanceId: request.clientInstanceId,
      requestedClass: request.connectionClass,
      grantedClass: request.connectionClass
    });
    onRuntimeSessionAdmission?.({
      clientInstanceId: request.clientInstanceId,
      connectionClass: request.connectionClass,
      url: route.request().frame().page().url()
    });
    return route.fulfill({ status: 201, json: {
      sessionId,
      clientInstanceId: request.clientInstanceId,
      requestedClass: request.connectionClass,
      grantedClass: request.connectionClass,
      admissionReasonCode: null,
      capacityReasonCode: null
    }});
  });
  await page.context().route('**/api/runtime/sessions/*/heartbeat', route => {
    const sessionId = new URL(route.request().url()).pathname.split('/').at(-2) ?? '';
    const lease = runtimeSessions.get(sessionId);
    if (!lease) return route.fulfill({ status: 404, json: { error: 'session-not-found' } });
    return route.fulfill({ json: {
      sessionId,
      clientInstanceId: lease.clientInstanceId,
      requestedClass: lease.requestedClass,
      grantedClass: lease.grantedClass,
      admissionReasonCode: null,
      capacityReasonCode: null
    }});
  });
  await page.context().route('**/api/runtime/sessions/*/terminate', route => {
    const sessionId = new URL(route.request().url()).pathname.split('/').at(-2) ?? '';
    runtimeSessions.delete(sessionId);
    return route.fulfill({ status: 204, body: '' });
  });

  return {
    setLogoutMode(next: LogoutMode) {
      logoutMode = next;
    },
    get logoutRequests() {
      return logoutRequests;
    },
    get loginRequests() {
      return loginRequests;
    },
    get runtimeCapabilityRequests() {
      return runtimeCapabilityRequests;
    },
    get runtimeSessionAdmissions() {
      return runtimeSessionAdmissions;
    }
  };
}

async function installEngineeringRuntimeProjection(
  page: Page,
  branding?: unknown,
  visualAssets: unknown[] = [],
  runtimePresentation?: unknown,
  onRequest?: (headers: Record<string, string>) => void
) {
  await page.context().route('**/api/runtime/application', route => {
    onRequest?.(route.request().headers());
    return route.fulfill({ json: {
      mode: 'engineering',
      projectKey: 'c25-session-runtime',
      projectName: 'C25 Session Runtime',
      revision: 7,
      activatedAtUtc: '2026-09-06T12:00:00Z',
      package: {
        schema: 'elitescada.hmi',
        schemaVersion: 1,
        startupScreenId: 'screen-home-id',
        screens: [{
          id: 'screen-home-id',
          key: 'home',
          name: 'Home',
          elements: []
        }],
        popups: [],
        dynamos: [],
        scripts: [],
        scriptVisualEventReferences: [],
        visualAssets,
        branding,
        runtimePresentation
      }
    }});
  });
}

test('each Runtime window auto-admits with a distinct logical client identity', async ({ page }) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);
  const clientIds: string[] = [];
  await page.context().route('**/api/runtime/sessions', route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    clientIds.push(request.clientInstanceId);
    return route.fulfill({ status: 201, json: {
      sessionId: `00000000-0000-0000-0000-${String(clientIds.length).padStart(12, '0')}`,
      clientInstanceId: request.clientInstanceId,
      requestedClass: request.connectionClass,
      grantedClass: request.connectionClass,
      admissionReasonCode: null,
      capacityReasonCode: null
    }});
  });

  await page.goto('/');
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  const popupPromise = page.context().waitForEvent('page');
  await page.getByRole('link', { name: 'Abrir esta tela em outra janela' }).click();
  const secondWindow = await popupPromise;
  await expect(secondWindow).toHaveURL(/\?screen=home$/);
  await expect(secondWindow.getByTestId('runtime-engineering-application')).toBeVisible();
  await expect.poll(() => clientIds.length).toBe(2);
  expect(clientIds[0]).not.toBe(clientIds[1]);

  const duplicatePromise = page.context().waitForEvent('page');
  await page.evaluate(() => window.open('/?screen=home', 'runtime-duplicated-tab'));
  const duplicatedTab = await duplicatePromise;
  await expect(duplicatedTab.getByTestId('runtime-engineering-application')).toBeVisible();
  await expect.poll(() => clientIds.length).toBe(3);
  expect(new Set(clientIds).size).toBe(3);
});

test('Runtime without Authority authentication preserves its existing public projection without a session lease', async ({ page }) => {
  const contract = await installSessionContract(page, null, 'success', false);
  await installEngineeringRuntimeProjection(page);

  await page.goto('/');

  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  expect(contract.runtimeSessionAdmissions).toBe(0);
  await expect(page.getByTestId('runtime-session-required')).toHaveCount(0);
});

test('a fixed station window requests View Only before showing its active Runtime projection', async ({ page }) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);
  const admissions: Array<{ clientInstanceId: string; connectionClass: string }> = [];
  await page.context().route('**/api/runtime/sessions', route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    admissions.push(request);
    return route.fulfill({ status: 201, json: {
      sessionId: '00000000-0000-0000-0000-000000000781',
      clientInstanceId: request.clientInstanceId,
      requestedClass: request.connectionClass,
      grantedClass: request.connectionClass,
      admissionReasonCode: null,
      capacityReasonCode: null
    }});
  });

  await page.goto('/?screen=home&station=eee-control-room&slot=1&stationMode=fixed');
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  await expect.poll(() => admissions.length).toBe(1);
  expect(admissions[0].connectionClass).toBe('viewOnly');
});

test('starting a four-window station opens isolated Runtime sessions with the configured modes', async ({ page }) => {
  test.setTimeout(90_000);
  const admissions: RuntimeSessionObservation[] = [];
  await installSessionContract(page, administrator, 'success', true, admission => admissions.push(admission));
  await installEngineeringRuntimeProjection(page, undefined, [], {
    historicalPlaybackEnabled: false,
    version: 1,
    stationProfiles: [{
      id: 'eee-control-room',
      name: 'Sala de controle',
      windows: [
        { screenKey: 'home', mode: 'navigation' },
        { screenKey: 'home', mode: 'fixed' },
        { screenKey: 'home', mode: 'fixed' },
        { screenKey: 'home', mode: 'fixed' }
      ]
    }]
  });

  await page.goto('/');
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  const stationWindows: Page[] = [];
  const allWindowsOpened = new Promise<void>(resolve => {
    const onPage = (openedPage: Page) => {
      stationWindows.push(openedPage);
      if (stationWindows.length === 4) {
        page.context().off('page', onPage);
        resolve();
      }
    };
    page.context().on('page', onPage);
  });
  await page.getByRole('button', { name: 'Iniciar estação' }).click();
  await allWindowsOpened;

  await Promise.all(stationWindows.map(window =>
    expect(window.getByTestId('runtime-engineering-application')).toBeVisible({ timeout: 45_000 })
  ));
  expect(stationWindows).toHaveLength(4);
  const observedSlots = new Set<number>();
  for (const stationWindow of stationWindows) {
    const query = new URL(stationWindow.url()).searchParams;
    expect(query.get('screen')).toBe('home');
    expect(query.get('station')).toBe('eee-control-room');
    expect(query.get('stationRun')).toBeTruthy();
    const slot = Number(query.get('slot'));
    expect(Number.isInteger(slot) && slot >= 0 && slot < 4).toBe(true);
    observedSlots.add(slot);
    expect(query.get('stationMode')).toBe(slot === 0 ? 'navigation' : 'fixed');
    expect(await stationWindow.evaluate(() => window.opener === null)).toBe(true);
  }
  expect(observedSlots).toEqual(new Set([0, 1, 2, 3]));

  await expect.poll(() => admissions.length).toBe(5);
  expect(new Set(admissions.map(admission => admission.clientInstanceId)).size).toBe(5);
  const parentAdmission = admissions.find(admission => new URL(admission.url).searchParams.get('slot') === null);
  expect(parentAdmission?.connectionClass).toBe('interactive');
  for (let index = 0; index < stationWindows.length; index++) {
    const admission = admissions.find(candidate =>
      new URL(candidate.url).searchParams.get('slot') === String(index)
    );
    expect(admission?.connectionClass).toBe(index === 0 ? 'interactive' : 'viewOnly');
    await expect(page.getByTestId(`runtime-station-window-${index + 1}-status`)).toContainText(
      index === 0 ? 'sessão Interativa confirmada' : 'sessão Somente visualização confirmada'
    );
  }
});

test('station launcher reports when the server rejects a window for exhausted licensed seats', async ({ page }) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page, undefined, [], {
    historicalPlaybackEnabled: false,
    version: 1,
    stationProfiles: [{
      id: 'eee-control-room',
      name: 'Sala de controle',
      windows: [
        { screenKey: 'home', mode: 'navigation' },
        { screenKey: 'home', mode: 'fixed' }
      ]
    }]
  });

  let successfulAdmissions = 0;
  await page.context().route('**/api/runtime/sessions', route => {
    if (route.request().method() !== 'POST') return route.continue();
    const sessionRequest = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    const slot = new URL(route.request().frame().page().url()).searchParams.get('slot');
    if (slot === '1') {
      return route.fulfill({ status: 409, json: {
        error: 'Runtime session capacity is unavailable.',
        capacityReasonCode: 'ViewOnlyQuotaExhausted'
      }});
    }
    return route.fulfill({ status: 201, json: {
      sessionId: '00000000-0000-0000-0000-' + String(++successfulAdmissions).padStart(12, '0'),
      clientInstanceId: sessionRequest.clientInstanceId,
      requestedClass: sessionRequest.connectionClass,
      grantedClass: sessionRequest.connectionClass,
      admissionReasonCode: null,
      capacityReasonCode: null
    }});
  });

  await page.goto('/');
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  const stationWindows: Page[] = [];
  const allWindowsOpened = new Promise<void>(resolve => {
    const onPage = (openedPage: Page) => {
      stationWindows.push(openedPage);
      if (stationWindows.length === 2) {
        page.context().off('page', onPage);
        resolve();
      }
    };
    page.context().on('page', onPage);
  });
  await page.getByRole('button', { name: 'Iniciar estação' }).click();
  await allWindowsOpened;

  const deniedWindow = stationWindows.find(window => new URL(window.url()).searchParams.get('slot') === '1');
  expect(deniedWindow).toBeTruthy();
  await expect(deniedWindow!.getByTestId('runtime-session-required')).toBeVisible();
  await expect(page.getByTestId('runtime-station-window-1-status')).toContainText('sessão Interativa confirmada');
  await expect(page.getByTestId('runtime-station-window-2-status')).toContainText('sem vaga de visualização disponível na licença');
});

test('blocked station popups provide a manual link for every configured screen', async ({ page }) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page, undefined, [], {
    historicalPlaybackEnabled: false,
    version: 1,
    stationProfiles: [{
      id: 'eee-control-room',
      name: 'Sala de controle',
      windows: [
        { screenKey: 'home', mode: 'navigation' },
        { screenKey: 'home', mode: 'fixed' }
      ]
    }]
  });
  await page.addInitScript(() => {
    Object.defineProperty(window, 'open', { configurable: true, value: () => null });
  });

  await page.goto('/');
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  await page.getByRole('button', { name: 'Iniciar estação' }).click();

  const status = page.getByRole('status').filter({ hasText: 'O navegador bloqueou 2 de 2 janelas' });
  await expect(status).toBeVisible();
  const first = status.getByRole('link', { name: 'Abrir tela 1' });
  const second = status.getByRole('link', { name: 'Abrir tela 2' });
  const firstHref = new URL((await first.getAttribute('href'))!, 'http://localhost');
  const secondHref = new URL((await second.getAttribute('href'))!, 'http://localhost');
  expect(firstHref.pathname).toBe('/');
  expect(firstHref.searchParams.get('screen')).toBe('home');
  expect(firstHref.searchParams.get('station')).toBe('eee-control-room');
  expect(firstHref.searchParams.get('slot')).toBe('0');
  expect(firstHref.searchParams.get('stationMode')).toBe('navigation');
  expect(firstHref.searchParams.get('stationRun')).toBeTruthy();
  expect(secondHref.pathname).toBe('/');
  expect(secondHref.searchParams.get('screen')).toBe('home');
  expect(secondHref.searchParams.get('station')).toBe('eee-control-room');
  expect(secondHref.searchParams.get('slot')).toBe('1');
  expect(secondHref.searchParams.get('stationMode')).toBe('fixed');
  expect(secondHref.searchParams.get('stationRun')).toBe(firstHref.searchParams.get('stationRun'));
  await expect(first).toHaveAttribute('rel', 'noopener noreferrer');
  await expect(second).toHaveAttribute('target', '_blank');

  const fallbackWindowPromise = page.context().waitForEvent('page');
  await first.click();
  const fallbackWindow = await fallbackWindowPromise;
  await expect(fallbackWindow).toHaveURL(/screen=home.*station=eee-control-room.*slot=0.*stationMode=navigation/);
  await expect(fallbackWindow.getByTestId('runtime-engineering-application')).toBeVisible();
});

test('external identity does not expose local switch-user even when local login is enabled', async ({ page }) => {
  await installSessionContract(page, externalDeveloper);

  await page.goto('/');
  await expect(page.getByTestId('session-menu-toggle')).toContainText('External Developer');
  await page.getByTestId('session-menu-toggle').click();

  await expect(page.getByTestId('session-switch-user')).toHaveCount(0);
  await expect(page.getByTestId('session-logout')).toBeVisible();
  await expect(page.getByTestId('session-close-interface')).toBeVisible();
});

test('account menu closes with Escape and returns focus to its trigger', async ({ page }) => {
  await installSessionContract(page, administrator);

  await page.goto('/');
  const trigger = page.getByTestId('session-menu-toggle');
  await trigger.focus();
  await trigger.press('Enter');
  await expect(page.getByTestId('session-menu-popup')).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(page.getByTestId('session-menu-popup')).toBeHidden();
  await expect(trigger).toBeFocused();
});

test('account menu keeps its localized accessible name and actions in every supported locale', async ({ page }) => {
  await installSessionContract(page, administrator);
  const expectations = [
    { locale: 'pt-BR', account: 'Conta', switchUser: 'Trocar usuário', logout: 'Sair' },
    { locale: 'en', account: 'Account', switchUser: 'Switch user', logout: 'Sign out' },
    { locale: 'es', account: 'Cuenta', switchUser: 'Cambiar usuario', logout: 'Salir' }
  ];

  for (const expected of expectations) {
    await page.addInitScript(locale => window.localStorage.setItem('elitescada.engineering.locale', locale), expected.locale);
    await page.goto('/');
    const trigger = page.getByTestId('session-menu-toggle');
    await expect(trigger).toHaveAttribute('aria-label', `${expected.account}: Administrador Local`);
    await trigger.click();
    await expect(page.getByTestId('session-menu-popup')).toHaveAttribute('aria-label', expected.account);
    await expect(page.getByTestId('session-switch-user')).toHaveText(expected.switchUser);
    await expect(page.getByTestId('session-logout')).toHaveText(expected.logout);
  }
});

test('Runtime toolbar keeps icon controls localized, labeled and keyboard discoverable', async ({ page }, testInfo) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);

  const expectations = [
    { locale: 'pt-BR', overview: 'Visão geral', history: 'Histórico', alarms: 'Alarmes', fullscreen: 'Tela cheia' },
    { locale: 'en', overview: 'Overview', history: 'History', alarms: 'Alarms', fullscreen: 'Fullscreen' },
    { locale: 'es', overview: 'Vista general', history: 'Histórico', alarms: 'Alarmas', fullscreen: 'Pantalla completa' }
  ];

  for (const expected of expectations) {
    await page.addInitScript(locale => window.localStorage.setItem('elitescada.engineering.locale', locale), expected.locale);
    await page.goto('/');

    const runtime = page.getByTestId('runtime-engineering-application');
    const toolbar = runtime.getByRole('toolbar', { name: 'Runtime' });
    await expect(toolbar).toBeVisible();

    for (const label of [expected.overview, expected.history, expected.alarms, expected.fullscreen]) {
      const control = toolbar.getByRole('button', { name: label });
      await expect(control).toBeVisible();
      await expect(control).toHaveAttribute('title', label);
      await expect(control).toHaveAttribute('data-tooltip', label);
    }

    const overview = toolbar.getByRole('button', { name: expected.overview });
    await expect(overview).toHaveAttribute('aria-pressed', 'true');
    await overview.focus();
    await expect(overview).toBeFocused();

    if (expected.locale === 'pt-BR') {
      await testInfo.attach('runtime-shell-phase-b-toolbar-pt-BR', {
        body: await runtime.locator('.runtime-operator-bar').screenshot(),
        contentType: 'image/png'
      });
    }
  }
});

test('Runtime session panel is compact, localized and reflects automatic session admission', async ({ page }, testInfo) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);

  const expectations = [
    { locale: 'pt-BR', viewOnly: 'Visualização', interactive: 'Interativa' },
    { locale: 'en', viewOnly: 'View only', interactive: 'Interactive' },
    { locale: 'es', viewOnly: 'Visualización', interactive: 'Interactiva' }
  ];

  for (const expected of expectations) {
    await page.addInitScript(locale => window.localStorage.setItem('elitescada.engineering.locale', locale), expected.locale);
    await page.goto('/');
    await page.getByTestId('session-menu-toggle').click();

    const popup = page.getByTestId('session-menu-popup');
    await expect(page.getByTestId('runtime-session-request-viewOnly')).toHaveText(expected.viewOnly);
    await expect(page.getByTestId('runtime-session-request-interactive')).toHaveText(expected.interactive);

    await expect(page.getByTestId('runtime-session-status').locator('dd')).toHaveText([
      expected.interactive,
      expected.interactive
    ]);

    const popupBox = await popup.boundingBox();
    const viewOnlyBox = await page.getByTestId('runtime-session-request-viewOnly').boundingBox();
    const interactiveBox = await page.getByTestId('runtime-session-request-interactive').boundingBox();
    expect(popupBox).not.toBeNull();
    expect(viewOnlyBox).not.toBeNull();
    expect(interactiveBox).not.toBeNull();
    expect(popupBox!.width).toBeLessThanOrEqual(280);
    expect(Math.abs(viewOnlyBox!.y - interactiveBox!.y)).toBeLessThanOrEqual(1);

    if (expected.locale === 'pt-BR') {
      await testInfo.attach('runtime-shell-phase-a-compact-pt-BR', {
        body: await popup.screenshot(),
        contentType: 'image/png'
      });
    }
  }
});

test('Runtime session keeps an Interactive grant visible without promoting technical reason codes', async ({ page }, testInfo) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);

  await page.route('**/api/runtime/sessions', route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    return route.fulfill({ status: 201, json: {
      sessionId: '00000000-0000-0000-0000-000000000789',
      clientInstanceId: request.clientInstanceId,
      requestedClass: request.connectionClass,
      grantedClass: 'interactive',
      admissionReasonCode: 'InteractiveGranted',
      capacityReasonCode: 'InteractiveSeatReserved'
    }});
  });
  await page.route('**/api/runtime/sessions/*/terminate', route => route.fulfill({ status: 204, body: '' }));

  await page.goto('/');
  await page.getByTestId('session-menu-toggle').click();
  await page.getByTestId('runtime-session-request-interactive').click();

  const status = page.getByTestId('runtime-session-status');
  await expect(status.locator('dd')).toHaveText(['Interativa', 'Interativa']);
  await expect(page.getByTestId('runtime-session-notice')).toHaveCount(0);

  const diagnostics = page.getByTestId('runtime-session-diagnostics');
  await expect(diagnostics).not.toHaveAttribute('open', '');
  await expect(diagnostics.getByText('InteractiveGranted')).toBeHidden();

  await testInfo.attach('runtime-shell-phase-a-interactive', {
    body: await page.getByTestId('session-menu-popup').screenshot(),
    contentType: 'image/png'
  });
});

test('Runtime session keeps Interactive to View Only fallback explicit while diagnostics stay secondary', async ({ page }, testInfo) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);

  await page.route('**/api/runtime/sessions', route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    return route.fulfill({ status: 201, json: {
      sessionId: '00000000-0000-0000-0000-000000000790',
      clientInstanceId: request.clientInstanceId,
      requestedClass: request.connectionClass,
      grantedClass: 'viewOnly',
      admissionReasonCode: 'InteractiveDownscoped',
      capacityReasonCode: 'InteractivePoolsExhausted'
    }});
  });
  await page.route('**/api/runtime/sessions/*/terminate', route => route.fulfill({ status: 204, body: '' }));

  await page.goto('/');
  await page.getByTestId('session-menu-toggle').click();
  await page.getByTestId('runtime-session-request-interactive').click();

  await expect(page.getByTestId('runtime-session-status').locator('dd')).toHaveText(['Interativa', 'Visualização']);
  await expect(page.getByTestId('runtime-session-notice')).toContainText('As vagas interativas estão indisponíveis');

  const diagnostics = page.getByTestId('runtime-session-diagnostics');
  await expect(diagnostics).not.toHaveAttribute('open', '');
  await expect(diagnostics.getByText('InteractivePoolsExhausted')).toBeHidden();

  await testInfo.attach('runtime-shell-phase-a-interactive-fallback', {
    body: await page.getByTestId('session-menu-popup').screenshot(),
    contentType: 'image/png'
  });
});

test('Runtime session class shows requested/granted truth and explicitly ends its user-owned lease', async ({ page }, testInfo) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);
  let terminated = false;
  await page.route('**/api/runtime/sessions', route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    return route.fulfill({ status: 201, json: {
      sessionId: '00000000-0000-0000-0000-000000000123', clientInstanceId: request.clientInstanceId,
      requestedClass: request.connectionClass, grantedClass: 'viewOnly', admissionReasonCode: 'ExplicitViewOnly', capacityReasonCode: 'SeatReserved'
    }});
  });
  await page.route('**/api/runtime/sessions/00000000-0000-0000-0000-000000000123/terminate', route => {
    terminated = true;
    return route.fulfill({ status: 204, body: '' });
  });

  await page.goto('/');
  await page.getByTestId('session-menu-toggle').click();
  await page.getByTestId('runtime-session-request-viewOnly').click();
  const status = page.getByTestId('runtime-session-status');
  await expect(status.locator('dd')).toHaveText(['Visualização', 'Visualização']);

  const diagnostics = page.getByTestId('runtime-session-diagnostics');
  await expect(diagnostics).not.toHaveAttribute('open', '');
  await expect(diagnostics.getByText('ExplicitViewOnly')).toBeHidden();

  await testInfo.attach('runtime-shell-phase-a-view-only', {
    body: await page.getByTestId('session-menu-popup').screenshot(),
    contentType: 'image/png'
  });

  await diagnostics.locator('summary').click();
  await expect(diagnostics.getByText('ExplicitViewOnly')).toBeVisible();

  await page.getByTestId('runtime-session-end').click();
  await expect.poll(() => terminated).toBe(true);
  await expect(page.getByTestId('runtime-session-status')).toHaveCount(0);
  await expect(page.getByTestId('runtime-session-required')).toBeVisible();
});

test('Runtime renews each window lease and binds its application projection to that lease', async ({ page }) => {
  await page.clock.install();
  await installSessionContract(page, administrator);
  const sessionId = '00000000-0000-0000-0000-000000000782';
  let clientInstanceId = '';
  let heartbeatRequests = 0;
  const boundProjectionSessions: string[] = [];
  let tagReadSessionId: string | null = null;
  let runtimeSocketUrl: string | null = null;
  await installEngineeringRuntimeProjection(page, undefined, [], undefined, headers => {
    const value = headers['x-elitescada-runtime-session'];
    if (value) boundProjectionSessions.push(value);
  });
  await page.route('**/api/runtime/sessions', route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    clientInstanceId = request.clientInstanceId;
    return route.fulfill({ status: 201, json: {
      sessionId,
      clientInstanceId,
      requestedClass: request.connectionClass,
      grantedClass: request.connectionClass,
      admissionReasonCode: null,
      capacityReasonCode: null
    }});
  });
  await page.route('**/api/runtime/sessions/*/heartbeat', route => {
    heartbeatRequests++;
    const request = route.request().postDataJSON() as { clientInstanceId: string };
    expect(request.clientInstanceId).toBe(clientInstanceId);
    return route.fulfill({ json: {
      sessionId,
      clientInstanceId: request.clientInstanceId,
      requestedClass: 'interactive',
      grantedClass: 'interactive',
      admissionReasonCode: null,
      capacityReasonCode: null
    }});
  });
  await page.route('**/api/tags', route => {
    tagReadSessionId = route.request().headers()['x-elitescada-runtime-session'] ?? null;
    return route.fulfill({ json: [] });
  });
  await page.routeWebSocket('**/ws/tags*', socket => { runtimeSocketUrl = socket.url(); });

  await page.goto('/');
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  await expect.poll(() => boundProjectionSessions).toContain(sessionId);
  await page.evaluate(async () => {
    const moduleUrl = new URL('/src/runtime/liveTagTransport.ts', window.location.origin).toString();
    const transport = await import(/* @vite-ignore */ moduleUrl);
    await transport.loadReadableRuntimeTags();
    transport.openRuntimeTagSocket();
  });
  await expect.poll(() => tagReadSessionId).toBe(sessionId);
  await expect.poll(() => runtimeSocketUrl).not.toBeNull();
  expect(new URL(runtimeSocketUrl!).searchParams.get('runtimeSessionId')).toBe(sessionId);
  expect(new URL(runtimeSocketUrl!).searchParams.get('clientInstanceId')).toBe(clientInstanceId);
  await page.clock.fastForward(20_000);
  await expect.poll(() => heartbeatRequests).toBe(1);
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
});

test('Runtime closes the projection after lease renewal fails and can reacquire a seat', async ({ page }) => {
  await page.clock.install();
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);
  let admissions = 0;
  let heartbeatRequests = 0;
  await page.route('**/api/runtime/sessions', route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    admissions++;
    const sessionId = `00000000-0000-0000-0000-${String(780 + admissions).padStart(12, '0')}`;
    return route.fulfill({ status: 201, json: {
      sessionId,
      clientInstanceId: request.clientInstanceId,
      requestedClass: request.connectionClass,
      grantedClass: request.connectionClass,
      admissionReasonCode: null,
      capacityReasonCode: null
    }});
  });
  await page.route('**/api/runtime/sessions/*/heartbeat', route => {
    heartbeatRequests++;
    return route.fulfill({ status: 410, json: { error: 'Runtime session lease expired.' } });
  });

  await page.goto('/');
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  await page.clock.fastForward(20_000);
  await expect.poll(() => heartbeatRequests).toBe(1);
  await expect(page.getByTestId('runtime-session-required')).toBeVisible();
  await expect(page.getByTestId('runtime-engineering-application')).toHaveCount(0);

  await page.getByRole('button', { name: 'Abrir sessão Runtime' }).click();
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  expect(admissions).toBe(2);
});

test('failed Runtime class replacement clears the terminated old session before showing the admission error', async ({ page }) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);

  let admissionRequests = 0;
  let terminated = false;
  await page.route('**/api/runtime/sessions', route => {
    admissionRequests++;
    const request = route.request().postDataJSON() as { clientInstanceId: string; connectionClass: string };
    if (admissionRequests === 1) {
      return route.fulfill({ status: 201, json: {
        sessionId: '00000000-0000-0000-0000-000000000456',
        clientInstanceId: request.clientInstanceId,
        requestedClass: request.connectionClass,
        grantedClass: 'interactive',
        admissionReasonCode: 'InteractiveGranted',
        capacityReasonCode: 'InteractiveSeatReserved'
      }});
    }

    return route.fulfill({ status: 409, json: {
      error: 'Runtime session capacity is unavailable.',
      capacityReasonCode: 'EligiblePoolsExhausted'
    }});
  });
  await page.route('**/api/runtime/sessions/00000000-0000-0000-0000-000000000456/terminate', route => {
    terminated = true;
    return route.fulfill({ status: 204, body: '' });
  });

  await page.goto('/');
  await page.getByTestId('session-menu-toggle').click();
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  await page.getByTestId('runtime-session-request-viewOnly').click();

  await expect.poll(() => terminated).toBe(true);
  await expect(page.getByTestId('runtime-session-status')).toHaveCount(0);
  await expect(page.getByTestId('runtime-session-end')).toHaveCount(0);
  const admissionError = page.getByTestId('runtime-session-class').getByRole('alert');
  await expect(admissionError).toContainText('EligiblePoolsExhausted');
  await expect(admissionError).toContainText('Runtime session capacity is unavailable.');
});

test('shell keeps navigation, account and theme reachable without horizontal overflow at compact desktop widths', async ({ page }) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page);

  for (const width of [1180, 1024, 901]) {
    await page.setViewportSize({ width, height: 720 });
    await page.goto('/');
    await expect(page.getByRole('navigation', { name: 'SCADIVO' })).toBeVisible();
    await expect(page.getByTestId('session-menu-toggle')).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Tema' })).toBeVisible();
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  }
});

test('failed server invalidation keeps the current identity and Runtime interactive', async ({ page }) => {
  const contract = await installSessionContract(page, administrator, 'failure');
  await installEngineeringRuntimeProjection(page);

  await page.goto('/');
  await expect(page.locator('.app-bar')).toBeVisible();
  await expect(page.getByTestId('session-menu-toggle')).toContainText('Administrador Local');
  await page.getByTestId('session-menu-toggle').click();

  await page.getByTestId('session-switch-user').click();
  const sessionMenuError = page.getByTestId('session-menu-popup').locator('.user-session-menu__error');
  await expect(sessionMenuError).toHaveText('Não foi possível invalidar a sessão atual para trocar de usuário.');
  await expect(page.getByTestId('session-menu-toggle')).toContainText('Administrador Local');
  await expect(page.locator('.app-bar')).toBeVisible();
  await expect(page.locator('input[name="username"]')).toHaveCount(0);

  await page.getByTestId('session-logout').click();
  await expect(sessionMenuError).toHaveText('Não foi possível encerrar a sessão.');
  await expect(page.getByTestId('session-menu-toggle')).toContainText('Administrador Local');
  await expect(page.locator('.app-bar')).toBeVisible();

  expect(contract.logoutRequests).toBe(2);
  expect(contract.loginRequests).toBe(0);
});

test('switch-user invalidates first, blocks the shell until login, then reloads backend capabilities', async ({ page }) => {
  const contract = await installSessionContract(page, administrator);

  await page.goto('/');
  await expect(page.locator('.app-navigation a[href="/engineering"]')).toBeVisible();
  await expect(page.locator('.app-navigation a[href="/audit"]')).toBeVisible();
  await expect(page.locator('.app-navigation a[href="/licensing"]')).toBeVisible();
  await expect(page.getByTestId('session-menu-toggle')).toContainText('Administrador Local');

  await page.getByTestId('session-menu-toggle').click();
  await page.getByTestId('session-switch-user').click();

  await expect(page.locator('.app-bar')).toHaveCount(0);
  await expect(page.getByTestId('session-menu-toggle')).toHaveCount(0);
  await expect(page.locator('input[name="username"]')).toBeVisible();
  await expect(page.getByText('Sessão anterior encerrada. Entre com o próximo usuário para continuar.')).toBeVisible();
  expect(contract.logoutRequests).toBe(1);
  expect(contract.loginRequests).toBe(0);

  await page.locator('input[name="username"]').fill(runtimeOperator.username);
  await page.locator('input[name="password"]').fill('RuntimePass8');
  await page.getByRole('button', { name: 'Entrar' }).click();

  await expect(page.locator('.app-bar')).toBeVisible();
  await expect(page.getByTestId('session-menu-toggle')).toContainText(runtimeOperator.username);
  await page.getByTestId('session-menu-toggle').click();
  await expect(page.getByTestId('session-close-interface')).toHaveCount(0);
  await expect(page.locator('.app-navigation a[href="/"]')).toBeVisible();
  await expect(page.locator('.app-navigation a[href="/engineering"]')).toHaveCount(0);
  await expect(page.locator('.app-navigation a[href="/audit"]')).toHaveCount(0);
  await expect(page.locator('.app-navigation a[href="/licensing"]')).toHaveCount(0);
  await expect(page.locator('.runtime-view-navigation')).toHaveCount(0);

  expect(contract.loginRequests).toBe(1);
  expect(contract.runtimeCapabilityRequests).toBeGreaterThanOrEqual(2);
});

test('Runtime-only effective surface access does not expose a redundant fullscreen transition', async ({ page }) => {
  await installSessionContract(page, runtimeOperator);
  await installEngineeringRuntimeProjection(page);

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();

  const toolbar = runtime.getByRole('toolbar', { name: 'Runtime' });
  await expect(toolbar.getByRole('button', { name: 'Visão geral' })).toBeVisible();
  await expect(toolbar.getByRole('button', { name: 'Alarmes' })).toBeVisible();
  await expect(toolbar.getByRole('button', { name: 'Histórico' })).toHaveCount(0);
  await expect(toolbar.getByRole('button', { name: 'Tela cheia' })).toHaveCount(0);
});

test('fullscreen Runtime uses compact branded shell while preserving History, Alarm and session behavior', async ({ page }, testInfo) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page, {
    mode: 'text',
    text: 'RUNTIME BRAND',
    subtitle: 'Operations'
  });

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();

  const toolbar = runtime.getByRole('toolbar', { name: 'Runtime' });
  const overview = toolbar.getByRole('button', { name: 'Visão geral' });
  const history = toolbar.getByRole('button', { name: 'Histórico' });
  const alarms = toolbar.getByRole('button', { name: 'Alarmes' });
  const fullscreen = toolbar.getByRole('button', { name: 'Tela cheia' });

  await expect(overview).toHaveAttribute('aria-pressed', 'true');
  await history.click();
  await expect(runtime.getByTestId('runtime-history-overlay')).toBeVisible();
  await expect(history).toHaveAttribute('aria-pressed', 'true');
  await expect(overview).toHaveAttribute('aria-pressed', 'false');

  await alarms.click();
  await expect(runtime.getByTestId('runtime-history-overlay')).toHaveCount(0);
  await expect(runtime.locator('.runtime-operator-overlay:not(.runtime-operator-overlay--history)')).toBeVisible();
  await expect(alarms).toHaveAttribute('aria-pressed', 'true');

  await overview.click();
  await expect(runtime.locator('#runtime-alarm-overlay')).toBeHidden();
  await expect(runtime.getByTestId('runtime-history-overlay')).toHaveCount(0);
  await expect(overview).toHaveAttribute('aria-pressed', 'true');

  const toolbarBox = await toolbar.boundingBox();
  expect(toolbarBox).not.toBeNull();
  expect(toolbarBox!.width).toBeLessThanOrEqual(150);

  await fullscreen.click();
  await expect(runtime).toHaveAttribute('data-runtime-fullscreen', 'true');

  const fullscreenHeader = runtime.locator('.runtime-operator-bar--fullscreen');
  await expect(fullscreenHeader).toBeVisible();
  const fullscreenBrand = fullscreenHeader.locator('.runtime-operator-brand .app-brand');
  await expect(fullscreenBrand).toHaveAttribute('data-branding-mode', 'text');
  await expect(fullscreenBrand).toContainText('RUNTIME BRAND');

  const projectContext = fullscreenHeader.locator('.runtime-operator-context');
  await expect(projectContext).toHaveText('C25 Session Runtime');
  await expect(fullscreenHeader).not.toContainText('rev 7');

  const headerBox = await fullscreenHeader.boundingBox();
  const contextBox = await projectContext.boundingBox();
  expect(headerBox).not.toBeNull();
  expect(contextBox).not.toBeNull();
  const headerCenter = headerBox!.x + headerBox!.width / 2;
  const contextCenter = contextBox!.x + contextBox!.width / 2;
  expect(Math.abs(contextCenter - headerCenter)).toBeLessThanOrEqual(2);

  await expect(fullscreenHeader.getByRole('button', { name: 'Sair da tela cheia' })).toHaveCount(0);

  const runtimeSession = runtime.getByTestId('session-menu-toggle');
  await expect(runtimeSession).toBeVisible();
  await expect(runtimeSession).toContainText('Administrador Local');
  const sessionBox = await runtimeSession.boundingBox();
  expect(sessionBox).not.toBeNull();
  expect(sessionBox!.height).toBeLessThanOrEqual(38);

  await runtimeSession.click();
  await expect(runtime.getByTestId('session-switch-user')).toBeVisible();
  await expect(runtime.getByTestId('session-logout')).toBeVisible();
  await expect(runtime.getByTestId('runtime-session-class')).toBeVisible();
  await expect(runtime.getByTestId('session-close-interface')).toBeVisible();
  const exitFullscreen = runtime.getByTestId('session-exit-fullscreen');
  await expect(exitFullscreen).toHaveText('Sair da tela cheia');

  await testInfo.attach('runtime-shell-phase-b-fullscreen', {
    body: await runtime.screenshot(),
    contentType: 'image/png'
  });

  await exitFullscreen.click();
  await expect.poll(() => page.evaluate(() => document.fullscreenElement === null)).toBe(true);
});

test('fullscreen Runtime scales text branding with the configured header height', async ({ page }) => {
  await installSessionContract(page, administrator);
  await installEngineeringRuntimeProjection(page, {
    mode: 'text',
    text: 'RUNTIME BRAND',
    subtitle: 'Operations'
  }, [], { header: { height: 112 } });

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();
  await runtime.getByRole('toolbar', { name: 'Runtime' }).getByRole('button', { name: 'Tela cheia' }).click();

  const fullscreenHeader = runtime.locator('.runtime-operator-bar--fullscreen');
  await expect(fullscreenHeader).toHaveAttribute('style', /min-height:\s*112px/);
  await expect(fullscreenHeader).toHaveClass(/runtime-operator-bar--brand-expanded/);
  await expect(fullscreenHeader).toHaveCSS('--runtime-header-brand-scale', '2');
  const brand = fullscreenHeader.locator('.runtime-operator-brand .app-brand');
  await expect(brand).toHaveAttribute('data-branding-mode', 'text');
  await expect(brand.locator('.app-brand-copy strong')).toHaveCSS('font-size', '24px');
  await expect(brand.locator('.app-brand-copy small')).toHaveCSS('font-size', '18px');
});

test('custom Runtime header controls resize to their labels and stay clear of the user menu', async ({ page }) => {
  await installSessionContract(page, administrator);
  const visualAsset = {
    id: 'header-pump-icon',
    key: 'header-pump-icon',
    name: 'Pump icon',
    originalFileName: 'pump.svg',
    mediaType: 'image/svg+xml',
    byteLength: 68,
    sha256: 'header-pump-icon-hash',
    pixelWidth: 24,
    pixelHeight: 24
  };
  await page.route('**/api/runtime/visual-assets/header-pump-icon/content', route => route.fulfill({
    contentType: 'image/svg+xml',
    body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9" fill="#20cbbb"/></svg>'
  }));
  await installEngineeringRuntimeProjection(page, undefined, [visualAsset], {
    header: {
      links: [
        { label: 'Tela 12345', screenKey: 'home' },
        { label: 'Bomba A', screenKey: 'home', visualAssetId: visualAsset.id }
      ]
    }
  });

  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  const customButtons = runtime.getByTestId('runtime-custom-screen-link');
  await expect(customButtons).toHaveCount(2);
  const textButton = customButtons.nth(0);
  await expect(textButton).toHaveText('Tela 12345');
  const textButtonBox = await textButton.boundingBox();
  expect(textButtonBox).not.toBeNull();
  expect(textButtonBox!.width).toBeGreaterThan(30);
  expect(textButtonBox!.width).toBeLessThan(160);
  const imageButton = customButtons.nth(1);
  await expect(imageButton.locator('img')).toBeVisible();
  await expect(imageButton).toHaveAttribute('aria-label', 'Bomba A');

  const fullscreen = runtime.getByRole('toolbar', { name: 'Runtime' }).getByRole('button', { name: 'Tela cheia' });
  await fullscreen.click();
  await expect(runtime).toHaveAttribute('data-runtime-fullscreen', 'true');
  const fullscreenHeader = runtime.locator('.runtime-operator-bar--fullscreen');
  const leftToolbar = fullscreenHeader.locator('.runtime-operator-side--left .runtime-operator-toolbar');
  await expect(leftToolbar).toBeVisible();
  await expect(fullscreenHeader.locator('.runtime-operator-side--right .runtime-operator-toolbar')).toHaveCount(0);

  const toolbarBox = await leftToolbar.boundingBox();
  const userBox = await runtime.getByTestId('session-menu-toggle').boundingBox();
  expect(toolbarBox).not.toBeNull();
  expect(userBox).not.toBeNull();
  const overlapX = Math.max(0, Math.min(toolbarBox!.x + toolbarBox!.width, userBox!.x + userBox!.width) - Math.max(toolbarBox!.x, userBox!.x));
  const overlapY = Math.max(0, Math.min(toolbarBox!.y + toolbarBox!.height, userBox!.y + userBox!.height) - Math.max(toolbarBox!.y, userBox!.y));
  expect(overlapX === 0 || overlapY === 0).toBeTruthy();

  await runtime.getByTestId('session-menu-toggle').click();
  await runtime.getByTestId('session-exit-fullscreen').click();
  await expect.poll(() => page.evaluate(() => document.fullscreenElement === null)).toBe(true);
});

test.describe('mobile Runtime custom header', () => {
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

  test('keeps custom navigation separate from the user menu', async ({ page }) => {
    await installSessionContract(page, administrator);
    const visualAsset = {
      id: 'header-pump-icon', key: 'header-pump-icon', name: 'Pump icon', originalFileName: 'pump.svg',
      mediaType: 'image/svg+xml', byteLength: 68, sha256: 'header-pump-icon-hash', pixelWidth: 24, pixelHeight: 24
    };
    await page.route('**/api/runtime/visual-assets/header-pump-icon/content', route => route.fulfill({
      contentType: 'image/svg+xml',
      body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9" fill="#20cbbb"/></svg>'
    }));
    await installEngineeringRuntimeProjection(page, undefined, [visualAsset], {
      header: { links: [
        { label: 'Tela 12345', screenKey: 'home' },
        { label: 'Bomba A', screenKey: 'home', visualAssetId: visualAsset.id }
      ] }
    });

    await page.goto('/');
    const runtime = page.getByTestId('runtime-engineering-application');
    const compactHeader = runtime.locator('.runtime-operator-bar--fullscreen');
    await expect(compactHeader).toBeVisible();
    const compactToolbar = compactHeader.locator('.runtime-operator-side--left .runtime-operator-toolbar');
    await expect(compactToolbar).toBeVisible();
    await expect(compactToolbar.getByTestId('runtime-custom-screen-link')).toHaveCount(2);
    const compactToolbarBox = await compactToolbar.boundingBox();
    const compactUserBox = await runtime.getByTestId('session-menu-toggle').boundingBox();
    expect(compactToolbarBox).not.toBeNull();
    expect(compactUserBox).not.toBeNull();
    const compactOverlapX = Math.max(0, Math.min(compactToolbarBox!.x + compactToolbarBox!.width, compactUserBox!.x + compactUserBox!.width) - Math.max(compactToolbarBox!.x, compactUserBox!.x));
    const compactOverlapY = Math.max(0, Math.min(compactToolbarBox!.y + compactToolbarBox!.height, compactUserBox!.y + compactUserBox!.height) - Math.max(compactToolbarBox!.y, compactUserBox!.y));
    expect(compactOverlapX === 0 || compactOverlapY === 0).toBeTruthy();
  });
});

test('successful sign-out removes the current client authority only after server invalidation succeeds', async ({ page }) => {
  const contract = await installSessionContract(page, administrator);

  await page.goto('/');
  await page.getByTestId('session-menu-toggle').click();
  await page.getByTestId('session-logout').click();

  await expect(page.locator('.app-bar')).toHaveCount(0);
  await expect(page.getByTestId('session-menu-toggle')).toHaveCount(0);
  await expect(page.locator('input[name="username"]')).toBeVisible();
  expect(contract.logoutRequests).toBe(1);
});
