import { expect, test } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/installation/native-network-profile', route => route.fulfill({ status: 404, body: '{}' }));
  await page.route('**/api/installation/service-endpoints', route => route.fulfill({ contentType: 'application/json', body: '[]' }));
});

const harnessPath = '/tests-e2e/installation-network-harness.html';

function endpoint(transport: string, bindAddress: string, internalPort: number, publishedPort: number, publishedToHost = true) {
  return { transport, bindAddress, publishedPort, internalPort, publishedToHost };
}

const profile = {
  profile: 'stable-docker',
  instanceId: 'scadivo-stable',
  web: endpoint('TCP', '127.0.0.1', 80, 18080),
  api: endpoint('TCP', '127.0.0.1', 5080, 15080),
  database: endpoint('TCP', '127.0.0.1', 5432, 15432),
  mediaControl: endpoint('TCP', 'Docker network only', 9997, 9997, false),
  mediaPlayback: endpoint('TCP', 'Docker network only', 8888, 8888, false),
  applyThroughUi: false,
  hostMappingIndependentlyVerified: false
};

test('installation network UI prepares module and private media port settings', async ({ page }) => {
  let reads = 0;
  let mutations = 0;
  await page.route('**/api/installation/network-profile', async route => {
    if (route.request().method() !== 'GET') {
      mutations++;
      await route.fulfill({ status: 405, contentType: 'application/json', body: '{"error":"Read only"}' });
      return;
    }
    reads++;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(profile) });
  });

  await page.goto(`${harnessPath}?locale=pt-BR`);
  const app = page.getByTestId('installation-network');
  await expect(app.getByRole('heading', { name: 'Rede e serviços' })).toBeVisible();
  await expect(app.getByText('scadivo-stable', { exact: true })).toBeVisible();
  await expect(app.getByRole('row', { name: /Media control.*TCP.*9997.*—.*Somente rede Docker/ })).toBeVisible();
  await expect(app.getByRole('row', { name: /HLS.*TCP.*8888.*—.*Somente rede Docker/ })).toBeVisible();

  await expect(app.getByLabel('Porta Web no host', { exact: true })).toHaveValue('18080');
  await expect(app.getByText(/A instalação usa as portas padrão automaticamente/)).toBeVisible();
  await app.getByLabel('Porta Web no host', { exact: true }).fill('28080');
  await expect(app.locator('.installation-network__preview')).toContainText('http://localhost:28080');
  await app.getByLabel('Porta interna do controle de mídia').fill('19997');
  await app.getByLabel('Porta interna do HLS').fill('18888');
  const command = app.locator('.installation-network__apply textarea');
  await expect(command).toHaveValue(/-MediaControlPort 19997 -MediaPlaybackPort 18888/);
  await expect(app.getByText(/Não publique o MediaMTX diretamente no host/)).toBeVisible();

  await app.locator('.installation-network__apply select').selectOption('bash');
  await expect(command).toHaveValue(/--media-control-port 19997 --media-playback-port 18888/);
  expect(reads).toBeGreaterThan(0);
  expect(mutations).toBe(0);
});


test('native installation shows inventory without Docker apply controls', async ({ page }) => {
  await page.route('**/api/installation/network-profile', route => route.fulfill({ status: 404, body: '{}' }));
  await page.route('**/api/installation/service-endpoints', route => route.fulfill({
    contentType: 'application/json', body: JSON.stringify([
      { service: 'API', host: '127.0.0.1', port: 25080 },
      { service: 'MediaPlayback', host: null, port: null }
    ])
  }));
  await page.goto(`${harnessPath}?locale=pt-BR`);
  const inventory = page.getByTestId('installation-service-inventory');
  await expect(inventory.getByRole('row', { name: /API.*127.0.0.1.*25080/ })).toBeVisible();
  await expect(inventory.getByRole('row', { name: /Vídeo HLS.*Não informado.*Não informado/ })).toBeVisible();
  await expect(page.locator('.installation-network__apply')).toHaveCount(0);
});


test('database readiness distinguishes compatible connection, unready and failed session', async ({ page }) => {
  await page.route('**/api/installation/network-profile', route => route.fulfill({ status: 404, body: '{}' }));
  let status = 200;
  await page.route('**/api/installation/database-readiness', route => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ ready: status === 200 }) }));
  await page.goto(`${harnessPath}?locale=pt-BR`);
  const panel = page.getByTestId('installation-database-readiness');
  await panel.getByRole('button', { name: 'Verificar banco' }).click();
  await expect(panel.getByRole('status')).toHaveText('Banco disponível e compatível.');
  status = 503;
  await panel.getByRole('button', { name: 'Verificar banco' }).click();
  await expect(panel.getByRole('alert')).toContainText('O banco ainda não está pronto');
  status = 401;
  await panel.getByRole('button', { name: 'Verificar banco' }).click();
  await expect(panel.getByRole('alert')).toContainText('Confira sua sessão');
});


test('native access form loads current distinct ports and validates its plan', async ({ page }) => {
  await page.route('**/api/installation/network-profile', route => route.fulfill({ status: 404, body: '{}' }));
  await page.route('**/api/installation/native-network-profile', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ platform:'linux', webPort:18080, apiPort:15120 }) }));
  await page.goto(`${harnessPath}?locale=pt-BR`);
  const panel = page.getByTestId('native-access-ports');
  await expect(panel.getByLabel('Porta Web', { exact:true })).toHaveValue('18080');
  await expect(panel.getByLabel('Porta API', { exact:true })).toHaveValue('15120');
  await panel.getByLabel('Porta Web', { exact:true }).fill('28080');
  await expect(panel.locator('textarea')).toHaveValue(/--web-port 28080 --api-port 15120/);
  await panel.getByLabel('Porta API', { exact:true }).fill('28080');
  await expect(panel.getByRole('alert')).toContainText('válidas e diferentes');
  await expect(panel.locator('textarea')).toHaveValue('');
});
