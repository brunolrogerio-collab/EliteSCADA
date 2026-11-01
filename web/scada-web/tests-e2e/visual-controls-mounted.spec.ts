import { expect, test } from '@playwright/test';

test('CheckBox observes Boolean values from every compatible non-TAG binding kind', async ({ page }) => {
  for (const source of ['expression', 'clientMemory', 'property']) {
    await page.goto(`/tests-e2e/visual-controls-harness.html?source=${source}`);
    const checkbox = page.getByRole('checkbox', { name: 'Bomba disponível' });
    if (source === 'expression') await expect(checkbox).toBeChecked();
    else await expect(checkbox).not.toBeChecked();
    await expect(checkbox).toBeDisabled();
    await expect(checkbox).toHaveAttribute('data-quality', '0');
  }
});

test('CheckBox displays read-only Boolean TAG values and keeps them disabled', async ({ page }) => {
  await page.goto('/tests-e2e/visual-controls-harness.html?source=readOnly');
  const checkbox = page.getByRole('checkbox', { name: 'Bomba disponível' });
  await expect(checkbox).not.toBeChecked();
  await expect(checkbox).toBeDisabled();
});

test('CheckBox does not dispatch a TAG write when Runtime lacks ProcessValueWrite', async ({ page }) => {
  await page.goto('/tests-e2e/visual-controls-harness.html?source=tag&processWrite=false');
  const checkbox = page.getByRole('checkbox', { name: 'Bomba disponível' });
  await expect(checkbox).toBeDisabled();
  await expect.poll(() => page.evaluate(() => (window as typeof window & { __controlWrites?: unknown[] }).__controlWrites?.length ?? 0)).toBe(0);
});

test('optional alarm-suppression CheckBox does not dispatch without AlarmShelve', async ({ page }) => {
  let mutations = 0;
  await page.route('**/api/alarms/suppressions', async route => {
    if (route.request().method() !== 'GET') mutations++;
    await route.fulfill({ json: {
      suppressionRevision: 1,
      alarms: [{ definitionId: 'c76c7e0b-3235-4a8b-936d-0e23a3f8b62e', name: 'High pressure', isSuppressed: false, suppressionCauses: [] }],
      locations: []
    } });
  });
  await page.goto('/tests-e2e/visual-controls-harness.html?source=suppression&alarmShelve=false');
  const checkbox = page.getByRole('checkbox', { name: 'Bomba disponível' });
  await expect(checkbox).toBeDisabled();
  expect(mutations).toBe(0);
});

test('optional alarm-suppression CheckBox uses the admitted Runtime session and confirms the canonical result', async ({ page }) => {
  const alarmId = 'c76c7e0b-3235-4a8b-936d-0e23a3f8b62e';
  let suppressed = false;
  let putBody: unknown;
  let putPath = '';
  await page.route('**/api/runtime/sessions', async route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string };
    await route.fulfill({ json: {
      sessionId: 'runtime-session-control-e2e', clientInstanceId: request.clientInstanceId,
      requestedClass: 'interactive', grantedClass: 'interactive'
    } });
  });
  await page.route('**/api/alarms/suppressions', async route => {
    await route.fulfill({ json: {
      suppressionRevision: suppressed ? 1 : 0,
      alarms: [{
        definitionId: alarmId, name: 'High pressure', isSuppressed: suppressed,
        suppressionCauses: suppressed ? [{
          targetKind: 'alarm', targetId: alarmId, targetName: 'High pressure', targetPath: 'High pressure',
          actor: 'Operator', reasonCode: 'maintenance', appliedAtUtc: '2026-10-31T12:00:00Z', expiresAtUtc: null
        }] : []
      }],
      locations: []
    } });
  });
  await page.route('**/api/alarms/suppressions/**', async route => {
    putPath = new URL(route.request().url()).pathname;
    putBody = route.request().postDataJSON();
    suppressed = true;
    await route.fulfill({ json: { changed: true, affectedAlarmCount: 1 } });
  });

  await page.goto('/tests-e2e/visual-controls-harness.html?source=suppression');
  const checkbox = page.getByRole('checkbox', { name: 'Bomba disponível' });
  await expect(checkbox).toBeEnabled();
  await checkbox.click();
  await expect.poll(() => putPath).toBe(`/api/alarms/suppressions/alarm/${alarmId}`);
  expect(putBody).toMatchObject({ reasonCode: 'maintenance', expiresAtUtc: expect.any(String) });
  await expect(checkbox).toBeChecked();
  await expect(page.getByRole('status')).toContainText('Supressão aplicada.');
});

test('optional alarm-suppression CheckBox reports a denied mutation and keeps the canonical state', async ({ page }) => {
  await page.route('**/api/runtime/sessions', async route => {
    const request = route.request().postDataJSON() as { clientInstanceId: string };
    await route.fulfill({ json: {
      sessionId: 'runtime-session-control-denied-e2e', clientInstanceId: request.clientInstanceId,
      requestedClass: 'interactive', grantedClass: 'interactive'
    } });
  });
  await page.route('**/api/alarms/suppressions', async route => {
    await route.fulfill({ json: {
      suppressionRevision: 0,
      alarms: [{ definitionId: 'c76c7e0b-3235-4a8b-936d-0e23a3f8b62e', name: 'High pressure', isSuppressed: false, suppressionCauses: [] }],
      locations: []
    } });
  });
  await page.route('**/api/alarms/suppressions/**', async route => {
    await route.fulfill({ status: 403, json: { error: 'Authority scope denied this alarm.' } });
  });

  await page.goto('/tests-e2e/visual-controls-harness.html?source=suppression');
  const checkbox = page.getByRole('checkbox', { name: 'Bomba disponível' });
  await checkbox.click();
  await expect(page.getByRole('alert')).toContainText('Authority scope denied this alarm.');
  await expect(checkbox).not.toBeChecked();
});

test('CheckBox writes a Boolean TAG through the shared writer and waits for process confirmation', async ({ page }) => {
  await page.goto('/tests-e2e/visual-controls-harness.html?source=tag');
  const checkbox = page.getByRole('checkbox', { name: 'Bomba disponível' });
  await expect(checkbox).toBeEnabled();
  await checkbox.check();
  await expect.poll(() => page.evaluate(() => (window as typeof window & { __controlWrites?: unknown[] }).__controlWrites?.length ?? 0)).toBe(1);
  await expect(checkbox).toBeChecked();
  await expect(page.getByRole('status')).toContainText('confirmado');
});

test('CheckBox with bad quality fails closed and announces unavailable state', async ({ page }) => {
  await page.goto('/tests-e2e/visual-controls-harness.html?source=bad');
  const checkbox = page.getByRole('checkbox', { name: 'Bomba disponível' });
  await expect(checkbox).toBeDisabled();
  await expect(checkbox).toHaveAttribute('aria-checked', 'mixed');
  await expect(page.getByRole('status')).toContainText('indisponível');
});

test('ComboBox writes a typed selection through the shared writer and waits for TAG confirmation', async ({ page }) => {
  await page.goto('/tests-e2e/visual-controls-harness.html?control=combo');
  const combo = page.getByRole('combobox', { name: 'pump-mode' });
  await expect(combo).toHaveValue('AUTO');
  await expect(combo).toBeEnabled();
  await combo.selectOption('MANUAL');
  await expect.poll(() => page.evaluate(() => (window as typeof window & { __controlWrites?: unknown[] }).__controlWrites?.length ?? 0)).toBe(1);
  await expect(combo).toHaveValue('MANUAL');
  await expect(page.getByRole('status')).toContainText('confirmado');
});

test('ComboBox preserves an unknown current value and fails closed', async ({ page }) => {
  await page.goto('/tests-e2e/visual-controls-harness.html?control=combo&source=missing');
  const combo = page.getByRole('combobox', { name: 'pump-mode' });
  await expect(combo).toBeDisabled();
  await expect(combo).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByRole('status')).toContainText('Valor atual sem opção: REMOTE');
});

test('ComboBox does not dispatch a TAG write when Runtime lacks ProcessValueWrite', async ({ page }) => {
  await page.goto('/tests-e2e/visual-controls-harness.html?control=combo&processWrite=false');
  const combo = page.getByRole('combobox', { name: 'pump-mode' });
  await expect(combo).toBeDisabled();
  await expect.poll(() => page.evaluate(() => (window as typeof window & { __controlWrites?: unknown[] }).__controlWrites?.length ?? 0)).toBe(0);
});

test('Engineering inspector keeps generic Boolean use primary and exposes alarm suppression as an optional mode', async ({ page }) => {
  const alarmId = 'c76c7e0b-3235-4a8b-936d-0e23a3f8b62e';
  await page.goto('/tests-e2e/visual-controls-harness.html?surface=inspector&source=tag');
  const section = page.getByTestId('visual-checkbox-configuration');
  const use = page.getByLabel('Uso');
  await expect(section).toContainText('Vincule uma fonte Boolean para indicar qualquer estado verdadeiro/falso.');
  await expect(use).toHaveValue('boolean');
  await expect(use.locator('option[value="boolean"]')).toHaveText('Estado Booleano');
  await expect(section.getByRole('combobox', { name: 'Alarme', exact: true })).toHaveCount(0);

  await use.selectOption('alarmSuppression');
  const alarm = section.getByRole('combobox', { name: 'Alarme', exact: true });
  await expect(alarm).toBeVisible();
  await expect(alarm).toContainText('Motor · Pressão alta');
  await expect(page.locator('body')).not.toContainText(alarmId);
  await alarm.selectOption(alarmId);
  await page.getByRole('button', { name: 'Aplicar configuração' }).click();

  const mutations = await page.evaluate(() => (window as typeof window & { __controlMutations?: unknown[] }).__controlMutations ?? []);
  expect(mutations).toHaveLength(2);
  expect(mutations[0]).toMatchObject({ kind: 'property.set', propertyKey: 'checkBoxConfig', value: { mode: 'alarmSuppression', alarmDefinitionId: alarmId } });
  expect(mutations[1]).toMatchObject({ kind: 'binding.remove', propertyKey: 'checked' });
});

test('generic CheckBox Boolean mode keeps the same broad meaning in all editor locales', async ({ page }) => {
  const expected = {
    'pt-BR': { field: 'Uso', option: 'Estado Booleano' },
    en: { field: 'Use', option: 'Boolean state' },
    es: { field: 'Uso', option: 'Estado Booleano' }
  } as const;

  for (const locale of ['pt-BR', 'en', 'es'] as const) {
    await page.goto(`/tests-e2e/visual-controls-harness.html?surface=inspector&locale=${locale}`);
    const use = page.getByLabel(expected[locale].field);
    await expect(use).toHaveValue('boolean');
    await expect(use.locator('option[value="boolean"]')).toHaveText(expected[locale].option);
  }
});

test('Engineering inspector edits typed ComboBox labels and values without exposing technical IDs', async ({ page }) => {
  await page.goto('/tests-e2e/visual-controls-harness.html?surface=inspector&control=combo');
  await expect(page.getByTestId('visual-combobox-configuration')).toContainText('Cada opção tem o valor gravado no TAG');
  await page.getByLabel('Valor do TAG 1').fill('AUTO');
  await page.getByLabel('Texto exibido 1').fill('Automático');
  await page.getByRole('button', { name: 'Adicionar opção' }).click();
  await page.getByLabel('Valor do TAG 3').fill('OFF');
  await page.getByLabel('Texto exibido 3').fill('Desligado');
  await page.getByRole('button', { name: 'Aplicar configuração' }).click();

  const mutations = await page.evaluate(() => (window as typeof window & { __controlMutations?: unknown[] }).__controlMutations ?? []);
  expect(mutations).toHaveLength(1);
  expect(mutations[0]).toMatchObject({ kind: 'property.set', propertyKey: 'comboBoxOptions', value: [
    { value: 'AUTO', label: 'Automático' },
    { value: 'MANUAL', label: 'Manual' },
    { value: 'OFF', label: 'Desligado' }
  ] });
});
