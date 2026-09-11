import { expect, test, type Locator } from '@playwright/test';

test.use({ locale: 'pt-BR' });

type ControlStyle = {
  color: string;
  backgroundColor: string;
  borderColor: string;
};

const FIELD_STATE_TOKENS = [
  '--eng-control-editable-bg',
  '--eng-control-editable-fg',
  '--eng-control-editable-border',
  '--eng-control-placeholder-fg',
  '--eng-control-readonly-bg',
  '--eng-control-readonly-fg',
  '--eng-control-readonly-border',
  '--eng-control-disabled-bg',
  '--eng-control-disabled-fg',
  '--eng-control-disabled-border'
] as const;

async function controlStyle(locator: Locator): Promise<ControlStyle> {
  return locator.evaluate(element => {
    const style = getComputedStyle(element);
    return {
      color: style.color,
      backgroundColor: style.backgroundColor,
      borderColor: style.borderColor
    };
  });
}

async function placeholderColor(locator: Locator): Promise<string> {
  return locator.evaluate(element => getComputedStyle(element, '::placeholder').color);
}

function rgbChannels(value: string): [number, number, number] {
  const channels = value.match(/[\d.]+/g)?.slice(0, 3).map(Number);
  if (!channels || channels.length !== 3) throw new Error(`Expected rgb/rgba color, received ${value}`);
  return channels as [number, number, number];
}

function relativeLuminance(value: string): number {
  const [red, green, blue] = rgbChannels(value).map(channel => {
    const normalized = channel / 255;
    return normalized <= 0.04045
      ? normalized / 12.92
      : ((normalized + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

function contrastRatio(foreground: string, background: string): number {
  const first = relativeLuminance(foreground);
  const second = relativeLuminance(background);
  const lighter = Math.max(first, second);
  const darker = Math.min(first, second);
  return (lighter + 0.05) / (darker + 0.05);
}

function expectReadable(foreground: string, background: string): void {
  expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(4.5);
}

test('C26.7 Engineering fields expose readable editable, placeholder, readonly and disabled states', async ({ page }) => {
  await page.goto('/engineering');

  const shell = page.locator('.eng-shell');
  await expect(shell).toBeVisible();
  const tokens = await shell.evaluate((element, names) => {
    const style = getComputedStyle(element);
    return Object.fromEntries(names.map(name => [name, style.getPropertyValue(name).trim()]));
  }, FIELD_STATE_TOKENS);
  for (const name of FIELD_STATE_TOKENS) expect(tokens[name], `${name} should be explicit`).not.toBe('');

  await page.getByRole('button', { name: /Telas/ }).click();
  const screens = page.getByTestId('visual-editor-workspace');
  await expect(screens).toBeVisible();

  const route = screens.locator('.visual-editor-screen-form input[placeholder="/overview"]');
  await expect(route).toBeVisible();
  const screenEditable = await controlStyle(route);
  const screenPlaceholder = await placeholderColor(route);
  expectReadable(screenEditable.color, screenEditable.backgroundColor);
  expectReadable(screenPlaceholder, screenEditable.backgroundColor);

  await route.evaluate(element => element.setAttribute('readonly', ''));
  const screenReadonly = await controlStyle(route);
  expectReadable(screenReadonly.color, screenReadonly.backgroundColor);
  expect(screenReadonly.backgroundColor).not.toBe(screenEditable.backgroundColor);

  await route.evaluate(element => {
    element.removeAttribute('readonly');
    element.setAttribute('disabled', '');
  });
  const screenDisabled = await controlStyle(route);
  expectReadable(screenDisabled.color, screenDisabled.backgroundColor);
  expect(screenDisabled.backgroundColor).not.toBe(screenEditable.backgroundColor);
  expect(screenDisabled.backgroundColor).not.toBe(screenReadonly.backgroundColor);

  await page.getByRole('button', { name: /Scripts/ }).click();
  await expect(page.getByRole('heading', { name: 'Scripts de Engenharia' })).toBeVisible();
  await page.getByRole('button', { name: 'Novo Script' }).click();

  const scriptName = page.getByLabel('Nome', { exact: true });
  const scriptLanguage = page.getByLabel('Linguagem');
  const scriptSearch = page.getByLabel('Buscar por nome, path ou descrição');
  await expect(scriptName).toBeVisible();
  await expect(scriptLanguage).toHaveAttribute('readonly', '');

  const scriptEditable = await controlStyle(scriptName);
  const scriptReadonly = await controlStyle(scriptLanguage);
  const scriptPlaceholder = await placeholderColor(scriptSearch);
  expectReadable(scriptEditable.color, scriptEditable.backgroundColor);
  expectReadable(scriptReadonly.color, scriptReadonly.backgroundColor);
  expectReadable(scriptPlaceholder, (await controlStyle(scriptSearch)).backgroundColor);
  expect(scriptReadonly.backgroundColor).not.toBe(scriptEditable.backgroundColor);

  await scriptName.evaluate(element => element.setAttribute('disabled', ''));
  const scriptDisabled = await controlStyle(scriptName);
  expectReadable(scriptDisabled.color, scriptDisabled.backgroundColor);
  expect(scriptDisabled.backgroundColor).not.toBe(scriptEditable.backgroundColor);
  expect(scriptDisabled.backgroundColor).not.toBe(scriptReadonly.backgroundColor);
});
