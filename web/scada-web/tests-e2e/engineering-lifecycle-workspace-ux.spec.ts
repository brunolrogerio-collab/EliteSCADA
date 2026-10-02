import { expect, test, type Page, type Route } from '@playwright/test';

type State = ReturnType<typeof createState>;

function createState() {
  const lifecycle = {
    projectKey: 'lifecycle-ux',
    status: 2,
    workingRevision: 4,
    publishedRevision: 3 as number | null,
    publishedAtUtc: '2026-10-02T18:30:00Z' as string | null,
    runtimeStatus: 2,
    activeRevision: 3 as number | null,
    activatedAtUtc: '2026-10-02T18:35:00Z' as string | null
  };
  return {
    workspace: {
      projectKey: 'lifecycle-ux',
      projectName: 'Lifecycle UX',
      baseRevision: 4 as number | null,
      checkedOutAtUtc: '2026-10-02T18:40:00Z',
      lastSavedAtUtc: '2026-10-02T18:40:00Z',
      isDirty: false,
      changeVersion: 14
    },
    lifecycle,
    revisions: [4, 3].map((revision, index) => ({
      revision,
      projectKey: 'lifecycle-ux',
      projectName: 'Lifecycle UX',
      engineeringSchema: 'scada.engineering',
      engineeringSchemaVersion: 15,
      savedAtUtc: index === 0 ? '2026-10-02T18:40:00Z' : '2026-10-02T18:30:00Z',
      basedOnRevision: revision - 1
    })),
    runtime: {
      projectKey: 'lifecycle-ux',
      configuredProjectKey: 'lifecycle-ux',
      consistent: true,
      durable: lifecycle,
      live: { mode: 'engineering', projectKey: 'lifecycle-ux', revision: 3 as number | null, activatedAtUtc: '2026-10-02T18:35:00Z' as string | null }
    },
    counts: { save: 0, publish: 0, activate: 0, checkout: 0 },
    failNext: null as null | { action: 'save'; status: 409 | 422 | 503 }
  };
}

async function mockApp(page: Page, state: State) {
  const pkg = {
    schema: 'scada.engineering', schemaVersion: 15, exportedAt: '2026-10-02T18:40:00Z',
    tags: [], alarms: [], dataSources: [], templates: [], equipment: [], dynamos: [],
    screens: [], popups: [], securityRoles: [], gateways: [], visualAssets: []
  };

  await page.route('**/api/**', async (route: Route) => {
    const req = route.request();
    const url = new URL(req.url());
    const p = url.pathname;

    if (p === '/api/engineering/workspace') return route.fulfill({ json: state.workspace });
    if (p === '/api/engineering/export/json') return route.fulfill({ json: pkg });
    if (p === '/api/engineering/persistence/status') return route.fulfill({ json: { enabled: true, provider: 'postgresql', configuredProjectKey: 'lifecycle-ux' } });
    if (p === '/api/engineering/persistence/lifecycle-ux/lifecycle') return route.fulfill({ json: state.lifecycle });
    if (p === '/api/engineering/persistence/lifecycle-ux/revisions') return route.fulfill({ json: state.revisions });
    if (p === '/api/engineering/persistence/lifecycle-ux/runtime') {
      state.runtime.durable = state.lifecycle;
      return route.fulfill({ json: state.runtime });
    }
    if (p === '/api/engineering/lock/status') return route.fulfill({ json: { configured: false, locked: false } });

    if (p === '/api/engineering/persistence/lifecycle-ux/save' && req.method() === 'POST') {
      state.counts.save++;
      if (state.failNext) {
        const status = state.failNext.status;
        state.failNext = null;
        return route.fulfill({ status, json: { error: `save-${status}` } });
      }
      const revision = state.revisions[0].revision + 1;
      const saved = {
        revision, projectKey: 'lifecycle-ux', projectName: 'Lifecycle UX',
        engineeringSchema: 'scada.engineering', engineeringSchemaVersion: 15,
        savedAtUtc: '2026-10-02T18:50:00Z', basedOnRevision: state.workspace.baseRevision
      };
      state.revisions.unshift(saved);
      state.workspace.baseRevision = revision;
      state.workspace.lastSavedAtUtc = saved.savedAtUtc;
      state.workspace.isDirty = false;
      state.lifecycle.workingRevision = revision;
      return route.fulfill({ json: saved });
    }

    const publish = p.match(/^\/api\/engineering\/persistence\/lifecycle-ux\/revisions\/(\d+)\/publish$/);
    if (publish && req.method() === 'POST') {
      state.counts.publish++;
      state.lifecycle.publishedRevision = Number(publish[1]);
      state.lifecycle.publishedAtUtc = '2026-10-02T18:55:00Z';
      return route.fulfill({ json: {} });
    }

    const checkout = p.match(/^\/api\/engineering\/persistence\/lifecycle-ux\/revisions\/(\d+)\/checkout$/);
    if (checkout && req.method() === 'POST') {
      state.counts.checkout++;
      const revision = Number(checkout[1]);
      state.workspace.baseRevision = revision;
      state.workspace.isDirty = false;
      state.lifecycle.workingRevision = revision;
      return route.fulfill({ json: {} });
    }

    if (p === '/api/engineering/persistence/lifecycle-ux/published/activate' && req.method() === 'POST') {
      state.counts.activate++;
      state.lifecycle.activeRevision = state.lifecycle.publishedRevision;
      state.lifecycle.activatedAtUtc = '2026-10-02T19:00:00Z';
      state.runtime.consistent = true;
      state.runtime.live.revision = state.lifecycle.activeRevision;
      state.runtime.live.activatedAtUtc = state.lifecycle.activatedAtUtc;
      return route.fulfill({ json: {} });
    }

    return route.fulfill({ json: {} });
  });
}

async function open(page: Page, state: State) {
  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'pt-BR'));
  await mockApp(page, state);
  await page.goto('/engineering');
  await expect(page.locator('.eng-lifecycle-workspace')).toBeVisible();
}

test('mounted lifecycle is one four-step surface and Save/Publish/Activate are one click', async ({ page }) => {
  const state = createState();
  state.workspace.isDirty = true;
  await open(page, state);
  const lifecycle = page.locator('.eng-lifecycle-workspace');

  await expect(lifecycle.locator('.eng-lifecycle-workspace__step')).toHaveCount(4);
  await expect(lifecycle).toContainText('Alterações pendentes');
  await expect(lifecycle).not.toContainText('Ciclo autoritativo do projeto');
  await expect(lifecycle).not.toContainText('Ciclo do Engineering');
  await expect(lifecycle).not.toContainText('Autoridade do backend preservada');
  await expect(lifecycle).not.toContainText('Versão de mudança');
  await expect(lifecycle.locator('.eng-lifecycle-workspace__fact')).toHaveCount(0);
  await expect(page.getByText('Próximos fluxos do editor')).toHaveCount(0);
  await expect(page.getByText('parse')).toHaveCount(0);

  await lifecycle.getByRole('button', { name: 'Salvar revisão' }).click();
  expect(state.counts.save).toBe(1);
  await expect(lifecycle.getByRole('dialog')).toHaveCount(0);
  await expect(lifecycle).toContainText('r5');

  const row5 = lifecycle.locator('.eng-lifecycle-workspace__revision-row').filter({ hasText: 'r5' });
  await row5.getByRole('button', { name: 'Publicar' }).click();
  expect(state.counts.publish).toBe(1);
  await expect(lifecycle.getByRole('dialog')).toHaveCount(0);
  await expect(lifecycle).toContainText('Pronto para ativar');

  await lifecycle.getByRole('button', { name: 'Ativar no Runtime' }).click();
  expect(state.counts.activate).toBe(1);
  await expect(lifecycle.getByRole('dialog')).toHaveCount(0);
  await expect(lifecycle).toContainText('Já está Active');
  await expect(lifecycle).toContainText('Runtime ativo');
});

test('clean checkout is direct; dirty checkout has the single data-loss confirmation', async ({ page }) => {
  const state = createState();
  await open(page, state);
  const lifecycle = page.locator('.eng-lifecycle-workspace');

  await lifecycle.locator('.eng-lifecycle-workspace__revision-row').filter({ hasText: 'r3' })
    .getByRole('button', { name: 'Usar no Working' }).click();
  expect(state.counts.checkout).toBe(1);
  await expect(lifecycle.getByRole('dialog')).toHaveCount(0);

  state.workspace.isDirty = true;
  await lifecycle.getByRole('button', { name: 'Atualizar ciclo' }).click();
  await expect(lifecycle).toContainText('Alterações pendentes');

  await lifecycle.locator('.eng-lifecycle-workspace__revision-row').filter({ hasText: 'r4' })
    .getByRole('button', { name: 'Usar no Working' }).click();
  expect(state.counts.checkout).toBe(1);

  const dialog = lifecycle.getByRole('dialog');
  await expect(dialog).toContainText('Descartar alterações não salvas do Working?');
  await dialog.getByRole('button', { name: 'Descartar alterações e usar revisão' }).click();
  expect(state.counts.checkout).toBe(2);
  await expect(dialog).toHaveCount(0);
});

test('mounted 409/422/503 errors stay visible and fail closed', async ({ page }) => {
  const state = createState();
  state.workspace.isDirty = true;
  await open(page, state);
  const lifecycle = page.locator('.eng-lifecycle-workspace');

  for (const [status, text] of [[409, 'conflito'], [422, 'validada'], [503, 'indisponível']] as const) {
    state.failNext = { action: 'save', status };
    await lifecycle.getByRole('button', { name: 'Salvar revisão' }).click();
    await expect(lifecycle.getByRole('alert')).toContainText(text);
    expect(state.workspace.isDirty).toBe(true);
  }
});

test('pt-BR/en/es stay equivalent and 1366/1440/1920 desktop widths do not overflow', async ({ page }) => {
  const state = createState();
  await open(page, state);

  for (const width of [1366, 1440, 1920]) {
    await page.setViewportSize({ width, height: 900 });
    await expect(page.locator('.eng-lifecycle-workspace')).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
  }

  await expect(page.getByText('Revisão salva', { exact: true })).toBeVisible();
  await page.getByLabel('Idioma').selectOption('en');
  await expect(page.getByText('Saved revision', { exact: true })).toBeVisible();
  await expect(page.getByText('Runtime active', { exact: true })).toBeVisible();

  await page.getByLabel('Language').selectOption('es');
  await expect(page.getByText('Revisión guardada', { exact: true })).toBeVisible();
  await expect(page.getByText('Runtime activo', { exact: true })).toBeVisible();
});
