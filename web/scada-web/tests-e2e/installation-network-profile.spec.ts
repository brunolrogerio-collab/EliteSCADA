import { expect, test } from '@playwright/test';

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
