import { expect, test } from '@playwright/test';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  decodeVisualEngineeringProperties,
  encodeVisualEngineeringProperties,
  getBuiltinVisualObjectSchema,
  VISUAL_PROPERTY_KEYS
} from '../src/visual-runtime';
import { measurementBandPercentages, measurementQualityState, measurementScale } from '../src/engineering/visual-editor/measurementVisualModel';

const measurementTypes = [
  BUILTIN_VISUAL_OBJECT_TYPES.measurementDial,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementRing,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementBar,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementThermometer,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementBands,
  BUILTIN_VISUAL_OBJECT_TYPES.measurementTarget
] as const;

test('measurement scale math clamps values and validates target bands without producing non-finite geometry', () => {
  expect(measurementScale(125, 0, 100)).toMatchObject({ percent: 100, valid: true });
  expect(measurementScale(-5, 0, 100)).toMatchObject({ percent: 0, valid: true });
  expect(measurementScale(3, 4, 4)).toMatchObject({ percent: 0, valid: false });
  expect(measurementScale(Number.NaN, 0, 100).percent).toBe(0);
  expect(measurementBandPercentages(0, 100, 25, 75)).toEqual({ first: 25, second: 75, valid: true });
  expect(measurementBandPercentages(0, 100, 80, 20).valid).toBe(false);
});

test('measurement quality follows canonical TagQuality numeric and string values and keeps unknown values fail-closed', () => {
  const numericCases = [
    [0, 'good'], [1, 'uncertain'], [2, 'bad'], [3, 'bad-communication'],
    [4, 'bad-configuration'], [5, 'bad-device'], [6, 'stale'], [7, 'disabled']
  ] as const;
  for (const [quality, expected] of numericCases) {
    expect(measurementQualityState({ quality }, true)).toBe(expected);
  }

  const stringCases = [
    ['Good', 'good'], ['Uncertain', 'uncertain'], ['Bad', 'bad'],
    ['BadCommunication', 'bad-communication'], ['Bad Configuration', 'bad-configuration'],
    ['bad-device', 'bad-device'], ['Stale', 'stale'], ['Disabled', 'disabled']
  ] as const;
  for (const [quality, expected] of stringCases) {
    expect(measurementQualityState({ quality }, true)).toBe(expected);
  }

  for (const quality of [8, -1, 1.5, '2', 'future-quality', null, undefined]) {
    expect(measurementQualityState({ quality }, true)).toBe('unknown');
  }
  expect(measurementQualityState({ quality: 0, state: 'Unavailable' }, true)).toBe('unavailable');
  expect(measurementQualityState({ quality: 0, state: 'LocalSession' }, true)).toBe('good');
  expect(measurementQualityState({ quality: 0 }, false)).toBe('unknown');
});

test('measurement widget properties round-trip using the canonical typed schema', () => {
  const schema = getBuiltinVisualObjectSchema(BUILTIN_VISUAL_OBJECT_TYPES.measurementBands);
  const values = {
    [VISUAL_PROPERTY_KEYS.value]: 42.5,
    [VISUAL_PROPERTY_KEYS.measurementUnit]: 'bar',
    [VISUAL_PROPERTY_KEYS.minimum]: 0,
    [VISUAL_PROPERTY_KEYS.maximum]: 100,
    [VISUAL_PROPERTY_KEYS.measurementTheme]: 'high-contrast',
    [VISUAL_PROPERTY_KEYS.measurementBandOneEnd]: 30,
    [VISUAL_PROPERTY_KEYS.measurementBandTwoEnd]: 70,
    [VISUAL_PROPERTY_KEYS.measurementQualityVisible]: true,
    [VISUAL_PROPERTY_KEYS.measurementFreshnessVisible]: true,
    [VISUAL_PROPERTY_KEYS.measurementStaleAfterSeconds]: 30
  };
  expect(encodeVisualEngineeringProperties(decodeVisualEngineeringProperties(values, schema), schema)).toEqual(values);
});

test('all six read-only measurement displays use the same accessible rendering in editor preview and Runtime', async ({ page }) => {
  const renderedByMode: string[][] = [];
  for (const mode of ['preview', 'runtime'] as const) {
    await page.goto(`/tests-e2e/harness/measurement-visual-harness.html?mode=${mode}`);
    const widgets = page.locator('[data-widget-type^="core.measurement"]');
    await expect(widgets).toHaveCount(6);
    const markup: string[] = [];
    for (let index = 0; index < measurementTypes.length; index += 1) {
      const widget = widgets.nth(index);
      await expect(widget).toHaveAttribute('role', 'img');
      await expect(widget).toHaveAttribute('data-widget-type', measurementTypes[index]);
      await expect(widget).toContainText('42.5 bar');
      await expect(widget).toContainText('Good');
      await expect(widget).toContainText('Fresh');
      if (measurementTypes[index] === BUILTIN_VISUAL_OBJECT_TYPES.measurementBands) {
        await expect(widget).toContainText('Band thresholds: 30 bar · 70 bar');
        await expect(widget).toHaveAttribute('aria-label', /Band thresholds: 30 bar, 70 bar/);
      }
      markup.push(await widget.evaluate(element => element.outerHTML));
    }
    await expect(page.locator('button, input, select, textarea')).toHaveCount(0);
    renderedByMode.push(markup);
  }
  expect(renderedByMode[1]).toEqual(renderedByMode[0]);
});

test('measurement theme colors reach each chart while inherit preserves configured colors', async ({ page }) => {
  const cases = [
    { theme: 'dark', track: '#475569', fill: '#38BDF8' },
    { theme: 'light', track: '#6B7280', fill: '#0E7490' },
    { theme: 'high-contrast', track: '#111111', fill: '#003B8E' },
    { theme: 'inherit', track: '#123456', fill: '#654321', configured: true }
  ];
  for (const item of cases) {
    const configured = item.configured ? '&trackColor=%23123456&fillColor=%23654321' : '';
    await page.goto(`/tests-e2e/harness/measurement-visual-harness.html?mode=preview&theme=${item.theme}${configured}`);
    const actual = await page.locator('[data-widget-type^="core.measurement"]').evaluateAll(elements => {
      const readChartColors = (widget: Element) => {
        const root = widget as HTMLElement;
        const svg = root.querySelector('svg');
        const firstSegment = root.querySelector('.visual-measurement__track, .visual-measurement__thermometer-track, .visual-measurement__target-track');
        const segmentFill = firstSegment?.querySelector('span');
        return {
          svgTrack: svg?.querySelector('path, circle')?.getAttribute('stroke') ?? null,
          svgFill: svg?.querySelectorAll('path, circle')[1]?.getAttribute('stroke') ?? null,
          track: firstSegment ? getComputedStyle(firstSegment).backgroundColor : null,
          fill: segmentFill ? getComputedStyle(segmentFill).backgroundColor : null
        };
      };
      return elements.map(readChartColors);
    });
    const rgb = (hex: string) => {
      const channels = hex.replace('#', '').match(/.{2}/g)!.map(channel => parseInt(channel, 16));
      return `rgb(${channels.join(', ')})`;
    };
    expect(actual[0]).toMatchObject({ svgTrack: item.track, svgFill: item.fill });
    expect(actual[1]).toMatchObject({ svgTrack: item.track, svgFill: item.fill });
    expect(actual[2]).toMatchObject({ track: rgb(item.track), fill: rgb(item.fill) });
    expect(actual[3]).toMatchObject({ track: rgb(item.track), fill: rgb(item.fill) });
    expect(actual[5]).toMatchObject({ track: rgb(item.track), fill: rgb(item.fill) });
  }
});

test('measurement bands expose localized thresholds and a localized invalid-limits message', async ({ page }) => {
  await page.goto('/tests-e2e/harness/measurement-visual-harness.html?mode=preview&bandOneEnd=80&bandTwoEnd=20');
  const bands = page.locator('[data-widget-type="core.measurementBands"]');
  await expect(bands).toContainText('Band thresholds: 80 bar · 20 bar');
  await expect(bands).toContainText('Invalid band limits');
  await expect(bands).toHaveAttribute('aria-label', /Band thresholds: 80 bar, 20 bar/);
});

test('measurement target preserves complete unit labels that contain spaces', async ({ page }) => {
  await page.goto('/tests-e2e/harness/measurement-visual-harness.html?mode=preview&unit=m%C2%B3%20%2F%20h');
  const target = page.locator('[data-widget-type="core.measurementTarget"]');
  await expect(target).toHaveAttribute('aria-label', /Target: 65 m³ \/ h/);
  await expect(target.locator('.visual-measurement__limits')).toContainText('Target: 65 m³ / h');
});

test('an unavailable projection is labeled Unavailable instead of implying Unknown or Fresh', async ({ page }) => {
  await page.goto('/tests-e2e/harness/measurement-visual-harness.html?mode=runtime&quality=0&state=Unavailable');
  const widget = page.locator('[data-widget-type="core.measurementDial"]');
  await expect(widget).toHaveAttribute('data-dynamic-state', 'unavailable');
  await expect(widget).toHaveAttribute('data-quality-state', 'unavailable');
  await expect(widget).toHaveAttribute('data-freshness-state', 'unavailable');
  await expect(widget).toHaveAttribute('aria-label', /Unavailable/);
  await expect(widget).not.toHaveAttribute('aria-label', /Unknown|Fresh|Good/);
  await expect(widget).toContainText('Unavailable');
});
