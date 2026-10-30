import { expect, test } from '@playwright/test';

const harnessPath = '/tests-e2e/installation-network-harness.html';

function endpoint(transport: string, internalPort: number, publishedPort: number, publishedToHost = true) {
  return {
    transport,
    bindAddress: '127.0.0.1',
    publishedPort,
    internalPort,
    publishedToHost
  };
}

const profile = {
  profile: 'stable-docker',
  instanceId: 'scadivo-stable',
  web: endpoint('TCP', 80, 18080),
  api: endpoint('TCP', 5080, 15080),
  database: endpoint('TCP', 5432, 15432),
  mediaControl: endpoint('TCP', 9997, 0, false),
  mediaPlayback: endpoint('TCP', 8888, 0, false),
  driverPorts: [],
  applyThroughUi: false,
  hostMappingIndependentlyVerified: false
};

test('installation network UI validates Driver listeners and prepares platform-specific commands', async ({ page }) => {
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
  await expect(app.getByRole('cell', { name: 'Somente rede Docker' })).toHaveCount(2);

  await app.getByRole('button', { name: 'Adicionar listener' }).click();
  const listener = app.locator('fieldset.installation-network__driver-port').first();
  await listener.getByLabel('Protocolo').selectOption('udp');
  await listener.getByLabel('Porta no host').fill('47808');
  await listener.getByLabel('Porta de escuta no container').fill('502');
  await expect(app.getByRole('alert')).toContainText('não pode abrir portas internas abaixo de 1024');
  await expect(app.locator('.installation-network__apply textarea')).toHaveValue('');

  await listener.getByLabel('Porta de escuta no container').fill('1502');
  await listener.getByLabel('Publicar em').selectOption('0.0.0.0');
  await expect(app.getByRole('note')).toContainText('disponível em todas as interfaces');
  const command = app.locator('.installation-network__apply textarea');
  await expect(command).toHaveValue(/-DriverPorts 'udp:0\.0\.0\.0:47808:1502'/);

  await app.locator('.installation-network__apply select').selectOption('bash');
  await expect(command).toHaveValue(/--driver-ports 'udp:0\.0\.0\.0:47808:1502'/);
  await expect(command).toHaveValue(/--replace-driver-ports/);
  expect(reads).toBeGreaterThan(0);
  expect(mutations).toBe(0);
});
