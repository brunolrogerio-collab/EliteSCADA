import { expect, test, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const bottomObjectId = '26000000-0000-0000-0000-000000000902';
const runtimeProjection = {
  mode: 'engineering',
  projectKey: 'c26-runtime-viewport',
  projectName: 'C26 Runtime Viewport',
  revision: 26,
  activatedAtUtc: '2026-09-07T18:00:00Z',
  package: {
    schema: 'scada.engineering',
    schemaVersion: 16,
    startupScreenId: '26000000-0000-0000-0000-000000000901',
    screens: [{
      id: '26000000-0000-0000-0000-000000000901',
      key: 'viewport.home',
      name: 'Viewport Home',
      properties: { backgroundColor: '#15212b' },
      elements: [{
        id: bottomObjectId,
        key: 'bottom-visible-content',
        type: 'core.text',
        properties: {
          x: 120,
          y: 900,
          width: 420,
          height: 72,
          text: 'Conteúdo próximo ao rodapé lógico',
          textColor: '#ffffff'
        }
      }]
    }],
    popups: [],
    dynamos: [],
    scripts: [],
    scriptVisualEventReferences: [],
    visualAssets: []
  }
} as const;

async function installRuntimeContract(page: Page) {
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
      subjectId: 'c26-viewport-po-id',
      username: 'c26-viewport-po',
      displayName: 'C26 Viewport Product Owner',
      roles: ['developer'],
      identityProvider: 'local'
    }
  }));
  await page.route('**/api/auth/local-session', route => route.fulfill({
    json: { authenticated: true, username: 'c26-viewport-po' }
  }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({
    json: {
      authenticationEnabled: true,
      runtime: ['View', 'TrendUse', 'SystemAdmin'],
      workspace: ['EngineeringModify', 'UserRoleAdmin', 'SystemAdmin']
    }
  }));
  await page.route('**/api/engineering/persistence/status', route => route.fulfill({
    json: { enabled: true, hasProjects: true }
  }));
  await page.route('**/api/runtime/application', route => route.fulfill({ json: runtimeProjection }));
}

type LayoutMetrics = {
  appHeight: number;
  canvasHeight: number;
  viewportHeight: number;
  viewportWidth: number;
  viewportScrollHeight: number;
  viewportScrollWidth: number;
  stageHeight: number;
  stageWidth: number;
  rendererClientHeight: number;
  rendererClientWidth: number;
  rendererScrollHeight: number;
  rendererScrollWidth: number;
  rendererOverflow: string;
  rendererMargin: string;
  rendererBackgroundImage: string;
  bottomTop: number;
  bottomBottom: number;
  viewportTop: number;
  viewportBottom: number;
};

async function readLayout(page: Page): Promise<LayoutMetrics> {
  return await page.evaluate((objectId) => {
    const app = document.querySelector<HTMLElement>('[data-testid="runtime-engineering-application"]')!;
    const canvas = document.querySelector<HTMLElement>('[data-testid="runtime-engineering-canvas"]')!;
    const viewport = document.querySelector<HTMLElement>('[data-testid="runtime-logical-viewport"]')!;
    const stage = document.querySelector<HTMLElement>('[data-testid="runtime-logical-stage"]')!;
    const renderer = document.querySelector<HTMLElement>('.runtime-visual-definition > .visual-editor-renderer-stage')!;
    const bottom = document.querySelector<HTMLElement>(`[data-object-id="${objectId}"]`)!;
    const appRect = app.getBoundingClientRect();
    const canvasRect = canvas.getBoundingClientRect();
    const viewportRect = viewport.getBoundingClientRect();
    const stageRect = stage.getBoundingClientRect();
    const bottomRect = bottom.getBoundingClientRect();
    const rendererStyle = getComputedStyle(renderer);
    return {
      appHeight: appRect.height,
      canvasHeight: canvasRect.height,
      viewportHeight: viewportRect.height,
      viewportWidth: viewportRect.width,
      viewportScrollHeight: viewport.scrollHeight,
      viewportScrollWidth: viewport.scrollWidth,
      stageHeight: stageRect.height,
      stageWidth: stageRect.width,
      rendererClientHeight: renderer.clientHeight,
      rendererClientWidth: renderer.clientWidth,
      rendererScrollHeight: renderer.scrollHeight,
      rendererScrollWidth: renderer.scrollWidth,
      rendererOverflow: rendererStyle.overflow,
      rendererMargin: rendererStyle.margin,
      rendererBackgroundImage: rendererStyle.backgroundImage,
      bottomTop: bottomRect.top,
      bottomBottom: bottomRect.bottom,
      viewportTop: viewportRect.top,
      viewportBottom: viewportRect.bottom
    };
  }, bottomObjectId);
}

test('C26 Runtime uses the complete 1920x1080 logical Screen without Engineering editor scroll chrome', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await installRuntimeContract(page);

  await page.goto('/');
  await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
  await expect(page.getByTestId('runtime-logical-viewport')).toHaveAttribute('data-design-width', '1920');
  await expect(page.getByTestId('runtime-logical-viewport')).toHaveAttribute('data-design-height', '1080');
  await expect(page.getByText('Conteúdo próximo ao rodapé lógico')).toBeVisible();

  const layout = await readLayout(page);
  expect(layout.appHeight).toBeCloseTo(800, 0);
  expect(layout.canvasHeight).toBeCloseTo(762, 0);
  expect(layout.viewportHeight).toBeCloseTo(layout.canvasHeight, 0);
  expect(layout.viewportWidth).toBeCloseTo(1440, 0);
  expect(layout.viewportScrollHeight).toBeLessThanOrEqual(Math.ceil(layout.viewportHeight));
  expect(layout.viewportScrollWidth).toBeLessThanOrEqual(Math.ceil(layout.viewportWidth));

  expect(layout.rendererClientWidth).toBe(1920);
  expect(layout.rendererClientHeight).toBe(1080);
  expect(layout.rendererScrollWidth).toBe(layout.rendererClientWidth);
  expect(layout.rendererScrollHeight).toBe(layout.rendererClientHeight);
  expect(layout.rendererOverflow).toBe('hidden');
  expect(layout.rendererMargin).toBe('0px');
  expect(layout.rendererBackgroundImage).toBe('none');

  expect(layout.stageHeight).toBeCloseTo(layout.viewportHeight, 1);
  expect(layout.stageWidth).toBeLessThanOrEqual(layout.viewportWidth + 1);
  expect(layout.bottomTop).toBeGreaterThanOrEqual(layout.viewportTop);
  expect(layout.bottomBottom).toBeLessThanOrEqual(layout.viewportBottom + 1);
});

test('C26 Runtime keeps the same full logical Screen contract in fullscreen', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await installRuntimeContract(page);

  await page.goto('/');
  const runtime = page.getByTestId('runtime-engineering-application');
  await expect(runtime).toBeVisible();
  await runtime.getByRole('button', { name: 'Tela cheia' }).click();
  await expect(runtime).toHaveAttribute('data-runtime-fullscreen', 'true');

  const layout = await readLayout(page);
  expect(layout.appHeight).toBeCloseTo(900, 0);
  expect(layout.canvasHeight).toBeCloseTo(862, 0);
  expect(layout.viewportHeight).toBeCloseTo(layout.canvasHeight, 0);
  expect(layout.rendererClientWidth).toBe(1920);
  expect(layout.rendererClientHeight).toBe(1080);
  expect(layout.rendererScrollWidth).toBe(layout.rendererClientWidth);
  expect(layout.rendererScrollHeight).toBe(layout.rendererClientHeight);
  expect(layout.stageWidth).toBeCloseTo(layout.viewportWidth, 1);
  expect(layout.stageHeight).toBeLessThanOrEqual(layout.viewportHeight + 1);
  expect(layout.bottomTop).toBeGreaterThanOrEqual(layout.viewportTop);
  expect(layout.bottomBottom).toBeLessThanOrEqual(layout.viewportBottom + 1);
});
