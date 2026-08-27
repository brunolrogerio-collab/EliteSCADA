import { expect, test } from '@playwright/test';

const events = [
  {
    id: 'audit-denied-1',
    timestampUtc: '2026-08-27T17:20:00Z',
    subjectId: 'operator-1',
    displayName: 'Operator One',
    action: 'command.execute',
    outcome: 'Denied',
    targetKind: 'command',
    targetId: 'pump.start',
    details: { reason: 'policy', requestedValue: 'true' },
    correlationId: 'corr-denied',
    area: 'Area1',
    projectKey: 'demo',
    revision: 9,
    roles: ['operator'],
    source: 'api'
  },
  {
    id: 'audit-success-2',
    timestampUtc: '2026-08-27T17:19:00Z',
    subjectId: 'engineer-1',
    displayName: 'Engineer One',
    action: 'engineering.preview',
    outcome: 'Succeeded',
    targetKind: 'project',
    targetId: 'demo',
    details: { changes: '3' },
    correlationId: 'corr-success',
    area: 'Engineering',
    projectKey: 'demo',
    revision: 9,
    roles: ['system-admin'],
    source: 'engineering'
  }
];

const healthyDiagnostics = {
  store: {
    persistedCount: 42,
    appendFailureCount: 0,
    lastPersistedAtUtc: '2026-08-27T17:20:01Z',
    lastAppendFailureAtUtc: null,
    lastRetentionRunAtUtc: null,
    lastRetentionDeletedCount: 0
  },
  buffer: {
    queueDepth: 0,
    successfullyForwardedCount: 42,
    forwardFailureCount: 0,
    rejectedCount: 0,
    droppedOnShutdownCount: 0,
    lastForwardedAtUtc: '2026-08-27T17:20:01Z',
    lastFailureAtUtc: null
  },
  retention: {
    enabled: false,
    maximumAge: null,
    batchSize: 1000,
    interval: null,
    maximumBatchesPerRun: 1,
    finiteRetentionActive: false
  }
};

test.use({ locale: 'pt-BR' });

test('Audit workspace keeps filters compact and moves event context into a keyboard-accessible master-detail view', async ({ page }) => {
  const auditQueries: string[] = [];

  await page.route('**/api/audit/diagnostics', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(healthyDiagnostics) });
  });
  await page.route('**/api/audit?**', async route => {
    const url = new URL(route.request().url());
    auditQueries.push(url.search);
    const filtered = url.searchParams.get('outcome') === 'Denied' ? [events[0]] : events;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(filtered) });
  });

  await page.goto('/audit');

  const advanced = page.locator('.audit-advanced-filters');
  await expect(advanced).not.toHaveAttribute('open', '');
  await expect(page.getByText('Sem filtros adicionais. Exibindo os eventos mais recentes.')).toBeVisible();

  const list = page.getByRole('listbox', { name: 'Lista de eventos de auditoria' });
  const options = list.getByRole('option');
  await expect(options).toHaveCount(2);

  const detail = page.locator('.audit-event-detail');
  await expect(detail).toContainText('command.execute');
  await expect(detail).toContainText('policy');
  await expect(detail).toContainText('corr-denied');

  await options.nth(1).focus();
  await page.keyboard.press('Enter');
  await expect(options.nth(1)).toHaveAttribute('aria-selected', 'true');
  await expect(detail).toContainText('engineering.preview');
  await expect(detail).toContainText('Engineer One');

  await page.getByLabel('Resultado').selectOption('Denied');
  await page.getByRole('button', { name: 'Consultar' }).click();
  await expect(page.getByLabel('Filtros ativos')).toContainText('Negado');
  await expect(options).toHaveCount(1);
  await expect.poll(() => auditQueries.some(query => new URLSearchParams(query).get('outcome') === 'Denied')).toBeTruthy();

  const diagnostics = page.locator('.audit-diagnostics-disclosure');
  await expect(diagnostics).not.toHaveAttribute('open', '');
  await expect(diagnostics).toContainText('Saudável');
  await expect(page.getByText('Persistidos')).not.toBeVisible();
});
