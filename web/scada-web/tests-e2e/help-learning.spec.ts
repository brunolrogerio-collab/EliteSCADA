import { expect, test } from '@playwright/test';
import { helpArea, helpBodyBlocks } from '../src/help/helpNavigation';
import { installCurrentHelpCatalog } from './helpTestSupport';

test.use({ locale: 'pt-BR' });

test('protocol walkthroughs expose real fields and working examples in every language', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.goto('/help?topic=drivers.mqtt-setup');
  const article = page.locator('article');
  await expect(article.getByRole('heading', { name: 'MQTT: tópicos, JSON e atualidade dos valores', exact: true })).toBeVisible();
  await expect(article.getByRole('rowheader', { name: 'Campo do valor no JSON', exact: false })).toBeVisible();
  await expect(article.locator('pre')).toContainText('"current":12.5');
  await expect(article).toContainText('não reconstrói o envelope');
  await page.getByLabel('Idioma', { exact: true }).selectOption('en');
  await expect(article.getByRole('heading', { name: 'MQTT: topics, JSON and value freshness', exact: true })).toBeVisible();
  await expect(article.getByRole('rowheader', { name: 'JSON value field', exact: false })).toBeVisible();
  await page.getByLabel('Language', { exact: true }).selectOption('es');
  await expect(article.getByRole('heading', { name: 'MQTT: tópicos, JSON y vigencia de valores', exact: true })).toBeVisible();
  await expect(article.getByRole('rowheader', { name: 'Campo del valor en JSON', exact: false })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('help-mqtt-fields-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/help?topic=drivers.opcua-setup');
  await expect(page.locator('article h2')).toHaveText('OPC UA: endpoint, certificados e importación de nodos');
  await expect(article.getByRole('rowheader', { name: 'Confianza temporal en Ingeniería', exact: false })).toBeVisible();
  await expect(article.locator('pre')).toContainText('"namespaceUri":"urn:example:plant"');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await article.getByRole('rowheader', { name: 'Referencia del certificado cliente', exact: false }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: testInfo.outputPath('help-opcua-fields-mobile.png') });
});

test('a beginner can find a property without knowing an internal topic ID', async ({ page }) => {
  await installCurrentHelpCatalog(page);
  await page.goto('/help');
  await expect(page.getByRole('heading', { name: 'Encontre a orientação certa' })).toBeVisible();
  const nav = page.getByRole('navigation', { name: 'Tópicos', exact: true });
  await expect(nav.getByText('getting-started.startup-authentication', { exact: true })).toHaveCount(0);
  const search = page.getByRole('searchbox');
  await search.fill('casas decimais');
  await expect(nav.getByRole('link', { name: 'Entrada numérica', exact: true })).toBeVisible();
  await nav.getByRole('link', { name: 'Entrada numérica', exact: true }).click();
  const article = page.locator('[data-help-topic="visual.object.core.numericinput"]');
  await expect(article.getByRole('table')).toBeVisible();
  await expect(article.getByRole('rowheader', { name: /Mostrar Aplicar/ })).toBeVisible();
  await expect(article.getByText('showApplyButton', { exact: true })).not.toBeVisible();
  await article.getByRole('rowheader', { name: /Mostrar Aplicar/ }).getByText('Referência técnica').click();
  await expect(article.getByText('showApplyButton', { exact: true })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Neste artigo' })).toBeVisible();
});

test('real catalog renders separated steps, a real illustration and searchable code in all locales', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto('/help?topic=visual.first-screen');
  await expect(page.locator('article ol.help-page__steps > li')).toHaveCount(7);
  const figure = page.getByRole('img', { name: /Tela real da demonstração EEE/ });
  await expect(figure).toBeVisible();
  await expect.poll(() => figure.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  await page.screenshot({ path: testInfo.outputPath('help-first-screen-desktop.png'), fullPage: true });
  await page.getByRole('searchbox').fill('visual_property_clear');
  await expect(page.getByRole('navigation', { name: 'Tópicos', exact: true }).getByRole('link', { name: 'Client Visual: interação e aparência com exemplos', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Limpar busca' }).click();
  await page.getByLabel('Idioma', { exact: true }).selectOption('en');
  await expect(page.getByRole('heading', { name: 'Your first screen: from TAG to visible value' })).toBeVisible();
  await page.getByLabel('Language', { exact: true }).selectOption('es');
  await expect(page.getByRole('heading', { name: 'Su primera pantalla: del TAG al valor visible' })).toBeVisible();
});

test('property tables remain usable on a narrow viewport and empty search is recoverable', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/help?topic=visual.object.core.trend');
  await expect(page.getByRole('heading', { name: 'Gráfico de tendência', exact: true })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Campos deste objeto' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.getByRole('region', { name: 'Campos deste objeto' }).locator('tbody tr').first().scrollIntoViewIfNeeded();
  await page.screenshot({ path: testInfo.outputPath('help-object-mobile.png') });
  await page.getByRole('searchbox').fill('no-such-help-field-87123');
  await expect(page.getByText('Nenhum tópico corresponde à busca.', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: 'Limpar busca' }).click();
  await expect(page.getByText('Nenhum tópico corresponde à busca.', { exact: false })).toHaveCount(0);
});

test('numbered prose keeps decimal values inside the same instruction', () => {
  expect(helpArea('visual-assets')).toBe('visual');
  expect(helpBodyBlocks('1. Use 3.6 como fator. 2. Confira o valor.')).toEqual([
    { ordered: true, lines: ['Use 3.6 como fator.', 'Confira o valor.'] }
  ]);
  expect(helpBodyBlocks('1. Calcule 11 × 2 + 5. 2. Confira 27.')).toEqual([
    { ordered: true, lines: ['Calcule 11 × 2 + 5.', 'Confira 27.'] }
  ]);
});

test('system tasks are discoverable and their field references stay localized', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.goto('/help?topic=database.topology');
  await expect(page.locator('article h2')).toHaveText('Banco de dados: configurar, migrar e recuperar');
  await expect(page.locator('article ol.help-page__steps > li')).toHaveCount(7);
  await expect(page.locator('article').getByRole('rowheader', { name: 'Host / FQDN', exact: false })).toBeVisible();
  await expect(page.locator('article')).toContainText('não copia nem mescla');
  await page.getByRole('searchbox').fill('lease');
  const nav = page.locator('nav.help-page__topics');
  await nav.locator('a[href="/help?topic=ha.configuration-operation"]').click();
  await expect(page.locator('article')).toContainText('não é licença comercial');
  await expect(page.getByRole('searchbox')).toHaveValue('lease');
  await page.goBack();
  await expect(page.locator('article h2')).toHaveText('Banco de dados: configurar, migrar e recuperar');
  await page.goForward();
  await expect(page.locator('article h2')).toHaveText('HA: configurar dois servidores e acompanhar a operação');
  await page.getByRole('button', { name: 'Limpar busca' }).click();
  await page.getByLabel('Idioma', { exact: true }).selectOption('en');
  await expect(page.locator('article h2')).toHaveText('HA: configure two servers and monitor operation');
  await expect(page.locator('article')).toContainText('not a commercial license');
  await page.getByLabel('Language', { exact: true }).selectOption('es');
  await expect(page.locator('article h2')).toHaveText('HA: configurar dos servidores y seguir la operación');
  await expect(page.locator('article')).toContainText('no es licencia comercial');
  await page.getByLabel('Idioma', { exact: true }).selectOption('pt-BR');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/help?topic=equipment.overview');
  await expect(page.locator('article')).toContainText('Template Faceplate (opcional)');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('help-equipment-mobile.png') });
  await page.goto('/help?topic=intelligence.activation');
  await expect(page.locator('article')).toContainText('Restaurar o grant não rearma');
  await page.goto('/help?topic=intelligence.analysis-guide');
  await expect(page.locator('article')).toContainText('limite inferior ≤ valor < limite superior');
  await page.goto('/help?topic=templates.overview');
  await expect(page.locator('article ol.help-page__steps > li')).toHaveCount(7);
});

test('report and history tasks explain fields and distinguish retrieval from capture', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.goto('/help?topic=reports.data-designer');
  const article = page.locator('article');
  await expect(article.getByRole('rowheader', { name: /Bruto, intervalo fixo ou resumo/ })).toBeVisible();
  await expect(article).toContainText('Somar leituras de vazão não calcula automaticamente volume');
  await page.getByLabel('Idioma', { exact: true }).selectOption('en');
  await expect(article).toContainText('Summing flow readings does not automatically calculate production volume');
  await page.getByLabel('Language', { exact: true }).selectOption('es');
  await expect(article).toContainText('Sumar caudales no calcula automáticamente volumen');
  await page.getByLabel('Idioma', { exact: true }).selectOption('pt-BR');
  await page.goto('/help?topic=historian.capture-query');
  await expect(article).toContainText('onChangeDeadbandMaxInterval');
  await expect(article).toContainText('não aumenta o polling do Driver');
  await expect(article.getByRole('rowheader', { name: /Período máximo/ })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('help-historian-fields.png'), fullPage: true });
  await page.goto('/help?topic=reports.layout-variables');
  await expect(article).toContainText('escrever psi num valor em bar não faz a conversão');
  await page.goto('/help?topic=reports.runtime-output');
  await expect(article).toContainText('São exportações do resultado, não pacotes');
});

test('alarm and operational event guides teach fields, state paths and emission in all locales', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.goto('/help?topic=alarms.configuration');
  const article = page.locator('article');
  await expect(article.getByRole('rowheader', { name: 'Setpoint (limite)', exact: true })).toBeVisible();
  await expect(article).toContainText('A igualdade também ativa');
  await expect(article).toContainText('não retarda a ativação');
  await page.getByLabel('Idioma', { exact: true }).selectOption('en');
  await expect(article).toContainText('does not delay Runtime activation');
  await expect(article).toContainText('Send 10: it becomes Active');
  await page.getByLabel('Language', { exact: true }).selectOption('es');
  await expect(article).toContainText('no retrasa la activación');
  await expect(article).toContainText('La igualdad también activa');
  await page.getByLabel('Idioma', { exact: true }).selectOption('pt-BR');
  await page.goto('/help?topic=operational-events.configuration');
  await expect(article).toContainText('não cria gatilho de mudança');
  await expect(article).toContainText('não substitua pelo Identificador textual');
  await expect(article.locator('pre')).toContainText('def initialize(event):');
  await expect(article.locator('pre')).toContainText('emit_operational_event(');
  await expect(article.getByRole('rowheader', { name: 'TAG (opcional)', exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('help-operational-event-fields.png'), fullPage: true });
  await page.goto('/help?topic=alarms.runtime-operation');
  await expect(article).toContainText('o item deixa a lista ativa após o ACK');
  await expect(article).toContainText('não reconhece, desabilita ou coloca em shelving');
  await page.setViewportSize({ width: 390, height: 844 });
  await article.getByRole('rowheader', { name: 'Returned', exact: true }).scrollIntoViewIfNeeded();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('help-alarm-states-mobile.png') });
});

test('security and Audit guides distinguish account policy session and read-only diagnostics', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.goto('/help?topic=security.local-accounts');
  const article = page.locator('article');
  await expect(article.getByRole('rowheader', { name: 'Senha / Nova senha', exact: true })).toBeVisible();
  await expect(article).toContainText('nome de login é mostrado para identificação');
  await page.goto('/help?topic=security.policy-scopes');
  await expect(article).toContainText('não à pessoa selecionada');
  await expect(article.getByRole('rowheader', { name: 'CommandExecute', exact: true })).toBeVisible();
  await expect(article.getByRole('rowheader', { name: 'ProcessValueWrite', exact: true })).toBeVisible();
  await expect(article).toContainText('não oferece formulário para criar ou reorganizar essa árvore');
  await page.getByLabel('Idioma', { exact: true }).selectOption('en');
  await expect(article).toContainText('not the selected person');
  await expect(article).toContainText('Unscoped grants this capability globally');
  await page.getByLabel('Language', { exact: true }).selectOption('es');
  await expect(article).toContainText('no a la persona seleccionada');
  await expect(article).toContainText('Sin alcance concede esa capability globalmente');
  await page.getByLabel('Idioma', { exact: true }).selectOption('pt-BR');
  await page.goto('/help?topic=audit.query-diagnostics');
  await expect(article.getByRole('rowheader', { name: 'Eventos por página', exact: true })).toBeVisible();
  await expect(article).toContainText('não apaga eventos');
  await expect(article).toContainText('não há campos para editar política de retenção');
  await page.screenshot({ path: testInfo.outputPath('help-audit-fields-desktop.png'), fullPage: true });
  await page.goto('/help?topic=security.lock-workflow');
  await expect(article).toContainText('Salvar → Publicar → Ativar');
  await expect(article).toContainText('não concede permissões ausentes');
  await page.setViewportSize({ width: 390, height: 844 });
  await article.getByRole('rowheader', { name: 'Remover segredo', exact: true }).scrollIntoViewIfNeeded();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('help-lock-fields-mobile.png') });
});

test('library and Gateway file guides expose usable steps and the exact typed CSV examples', async ({ page }, testInfo) => {
  await installCurrentHelpCatalog(page);
  await page.goto('/help?topic=libraries.associate-incorporate');
  const article = page.locator('article');
  await expect(article).toContainText('Associar e Preview não alteram o Working');
  await expect(article).toContainText('não apaga recursos já incorporados');
  await expect(article.getByRole('rowheader', { name: 'Versão da nova biblioteca', exact: true })).toBeVisible();
  await page.getByLabel('Idioma', { exact: true }).selectOption('en');
  await expect(article).toContainText('Association and Preview do not change Working');
  await page.getByLabel('Language', { exact: true }).selectOption('es');
  await expect(article).toContainText('Asociar y Preview no cambian Working');
  await page.getByLabel('Idioma', { exact: true }).selectOption('pt-BR');
  await page.goto('/help?topic=gateway.configure-test');
  await expect(article.getByRole('rowheader', { name: 'Gain (ganho)', exact: true })).toBeVisible();
  await expect(article).toContainText('11 × 2 + 5');
  await expect(article.locator('pre')).toContainText('SourceTagPath,DestinationTagPath');
  await expect(article.locator('pre')).toContainText('CheckedNumeric,WaitForNextAcceptableValue,2,5,true');
  await page.screenshot({ path: testInfo.outputPath('help-gateway-fields-desktop.png'), fullPage: true });
  await page.goto('/help?topic=engineering.tabular-workflow');
  await expect(article).toContainText('Fórmulas XLSX são recusadas');
  await expect(article).toContainText('colunas omitidas podem assumir valores padrão');
  await expect(article.locator('pre')).toContainText('Server.Destination,Destination,int32,memory.server,false,int32,0');
  await page.setViewportSize({ width: 390, height: 844 });
  await article.getByRole('rowheader', { name: 'Modo de mesclagem', exact: true }).scrollIntoViewIfNeeded();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('help-tabular-fields-mobile.png') });
  await page.goto('/help?topic=assets.management');
  await expect(article).toContainText('preserva a identidade estável');
});
