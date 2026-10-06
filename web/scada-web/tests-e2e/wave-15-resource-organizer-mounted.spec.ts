import { expect, test } from '@playwright/test';

const kinds = ['screens', 'templates', 'popups', 'tags', 'dataSources', 'mediaSources', 'alarms', 'operationalEvents', 'gatewayRoutes'];

for (const kind of kinds) {
  test(`${kind}: blank list, create, rename, copy, paste, drag and collapse`, async ({ page }) => {
    await page.goto('/tests-e2e/harness/resource-organizer.html');
    await page.getByLabel('Tipo', { exact: true }).selectOption(kind);
    const organizer = page.getByTestId(`engineering-resource-organizer-${kind}`);
    const sidebar = page.locator('aside');
    const box = (await sidebar.boundingBox())!;
    // The entire empty column, not only the existing item, is an interaction surface.
    await page.mouse.click(box.x + 100, box.y + box.height - 25, { button: 'right' });
    await page.getByRole('menuitem', { name: 'Nova pasta', exact: true }).click();
    await page.getByRole('dialog').getByRole('textbox').fill('Equipamentos');
    await page.getByRole('dialog').getByRole('button', { name: 'Salvar', exact: true }).click();
    const folder = organizer.locator('.engineering-resource-organizer__folder');
    await folder.getByRole('button').click({ button: 'right' });
    await page.getByRole('menuitem', { name: 'Renomear pasta' }).click();
    await page.getByRole('dialog').getByRole('textbox').fill('Área 1');
    await page.getByRole('dialog').getByRole('button', { name: 'Salvar', exact: true }).click();
    await expect(folder.getByRole('button')).toHaveAccessibleName('Recolher pasta: Área 1');

    const original = organizer.getByRole('button', { name: 'Original', exact: true });
    await original.click({ button: 'right' });
    await page.getByRole('menuitem', { name: 'Copiar', exact: true }).click();
    await organizer.click({ button: 'right', position: { x: 100, y: 400 } });
    await page.getByRole('menuitem', { name: 'Colar', exact: true }).click();
    await expect(organizer.getByRole('button', { name: 'Original (cópia)', exact: true })).toBeVisible();
    await original.dragTo(folder.getByRole('button', { name: 'Recolher pasta: Área 1' }));
    await expect(folder.getByRole('button', { name: 'Original', exact: true })).toBeVisible();
    await folder.getByRole('button', { name: 'Recolher pasta: Área 1' }).click();
    await expect(organizer.getByRole('button', { name: 'Original', exact: true })).toBeHidden();
    await folder.getByRole('button', { name: 'Expandir pasta: Área 1' }).click();
    await original.dragTo(organizer, { targetPosition: { x: 120, y: 430 } });
    await expect(folder.getByRole('button', { name: 'Original', exact: true })).toHaveCount(0);
    await expect(original).toBeVisible();
    await page.reload();
    await page.getByLabel('Tipo', { exact: true }).selectOption(kind);
    await expect(folder.getByRole('button', { name: 'Recolher pasta: Área 1' })).toBeVisible();
  });
}

test('menu is visible at the viewport edge and retains the dark theme; cancel is harmless', async ({ page }) => {
  await page.setViewportSize({ width: 420, height: 650 });
  await page.goto('/tests-e2e/harness/resource-organizer.html');
  await page.getByLabel('Escuro').check();
  const organizer = page.getByTestId('engineering-resource-organizer-tags');
  const listBox = (await organizer.boundingBox())!;
  await page.mouse.click(listBox.x + listBox.width - 10, listBox.y + listBox.height - 10, { button: 'right' });
  const menu = page.getByRole('menu');
  await expect(menu).toBeVisible();
  const box = (await menu.boundingBox())!;
  expect(box.x).toBeGreaterThanOrEqual(0);
  expect(box.x + box.width).toBeLessThanOrEqual(420);
  expect(box.y + box.height).toBeLessThanOrEqual(650);
  await expect(menu).toHaveCSS('background-color', 'rgb(20, 28, 37)');
  await page.getByRole('menuitem', { name: 'Nova pasta', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Cancelar', exact: true }).click();
  await expect(organizer.locator('.engineering-resource-organizer__folder')).toHaveCount(0);
});

test('keyboard clipboard stays scoped and deleting a folder preserves its resources', async ({ page }) => {
  await page.goto('/tests-e2e/harness/resource-organizer.html');
  const original = page.getByRole('button', { name: 'Original', exact: true });
  await original.press('Control+c');
  await original.press('Control+v');
  await expect(page.getByRole('button', { name: 'Original (cópia)', exact: true })).toBeVisible();
  await page.getByLabel('Tipo', { exact: true }).selectOption('alarms');
  await original.click({ button: 'right' });
  await expect(page.getByRole('menuitem', { name: 'Colar', exact: true })).toHaveCount(0);
  await page.getByRole('menuitem', { name: 'Nova pasta', exact: true }).click();
  await page.getByRole('dialog').getByRole('textbox').fill('Teste');
  await page.getByRole('dialog').getByRole('button', { name: 'Salvar', exact: true }).click();
  const folder = page.locator('.engineering-resource-organizer__folder');
  await original.dragTo(folder.getByRole('button', { name: 'Recolher pasta: Teste' }));
  await folder.getByRole('button', { name: 'Recolher pasta: Teste' }).click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Excluir pasta', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Excluir pasta', exact: true }).click();
  await expect(folder).toHaveCount(0);
  await expect(original).toBeVisible();
});

test('foreign drag payload cannot move a resource into this list', async ({ page }) => {
  await page.goto('/tests-e2e/harness/resource-organizer.html');
  const original = page.getByRole('button', { name: 'Original', exact: true });
  await original.click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Nova pasta', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Salvar', exact: true }).click();
  const folder = page.locator('.engineering-resource-organizer__folder');
  const payload = await page.evaluateHandle(() => {
    const data = new DataTransfer();
    data.setData('application/x-elitescada-engineering-resource', JSON.stringify({ scope: 'other-project:tags', identity: 'one' }));
    return data;
  });
  await folder.dispatchEvent('drop', { dataTransfer: payload });
  await expect(folder.getByRole('button', { name: 'Original', exact: true })).toHaveCount(0);
  await expect(original).toBeVisible();
});

test('real TAG editor shares toolbar and context clipboard including multi-selection and shortcuts', async ({ page }) => {
  await page.route('**/api/**', route => route.fulfill({ json: {} }));
  await page.goto('/tests-e2e/harness/resource-organizer.html?tag-editor');
  const organizer = page.getByTestId('engineering-resource-organizer-tags');
  const motor = organizer.getByRole('button', { name: /^Motor/ });
  await motor.click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Copiar', exact: true }).click();
  await expect(page.getByTestId('tag-clipboard-count')).toHaveText('1');
  await expect(page.getByTestId('tag-paste')).toBeEnabled();
  await page.getByTestId('tag-paste').click();
  await expect(page.getByTestId('tag-generated-row')).toHaveCount(1);
  await page.getByRole('button', { name: 'Selecionar vários' }).click();
  await organizer.getByRole('button', { name: /^Válvula/ }).click();
  await expect(page.getByTestId('tag-duplication-selected-count')).toHaveText('2');
  await motor.press('Control+c');
  await expect(page.getByTestId('tag-clipboard-count')).toHaveText('2');
  await motor.click({ button: 'right' });
  await page.getByRole('menuitem', { name: 'Colar', exact: true }).click();
  await expect(page.getByTestId('tag-generated-row')).toHaveCount(2);
  await page.getByRole('button', { name: 'Concluir seleção' }).click();
  await page.getByTestId('tag-copy-selected').click();
  await motor.press('Control+v');
  await expect(page.getByTestId('tag-generated-row')).toHaveCount(1);
  await motor.press('Control+d');
  await expect(page.getByTestId('tag-generated-row')).toHaveCount(1);
});
