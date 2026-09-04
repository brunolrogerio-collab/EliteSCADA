import React from 'react';
import { createRoot } from 'react-dom/client';
import type { EngineeringLocale } from '../src/engineering/i18n';
import type { VisualElementEngineering } from '../src/engineering/types';
import { CanonicalVisualRenderer } from '../src/engineering/visual-editor/CanonicalVisualRenderer';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  TREND_PENS_PROPERTY,
  createTrendPen,
  trendPensEngineeringValue,
  VISUAL_PROPERTY_KEYS
} from '../src/visual-runtime';

const tagOne = '11111111-1111-1111-1111-111111111111';
const tagTwo = '22222222-2222-2222-2222-222222222222';
const params = new URLSearchParams(window.location.search);
const mode = params.get('mode') === 'live' ? 'live' : 'history';
const requestedLocale = params.get('locale');
const locale: EngineeringLocale = requestedLocale === 'en' || requestedLocale === 'es' ? requestedLocale : 'pt-BR';
const count = params.get('count') === '2' ? 2 : 1;
const showQuality = params.get('quality') !== 'off';

const pressure = createTrendPen({
  id: tagOne,
  path: 'Area/Pump/Pressure',
  label: 'Pressure',
  unit: 'bar'
}, 0);
const flow = createTrendPen({
  id: tagTwo,
  path: 'Area/Pump/Flow',
  label: 'Flow',
  unit: 'm3/h'
}, 1);

const elements: VisualElementEngineering[] = [trendElement(
  'trend-harness-1',
  'trend-harness-primary',
  10,
  [pressure, flow]
)];
if (count === 2) {
  elements.push(trendElement(
    'trend-harness-2',
    'trend-harness-secondary',
    270,
    [{ ...flow, id: 'pen-secondary-flow', label: 'Flow secondary', axis: 'left' }]
  ));
}

function trendElement(
  id: string,
  key: string,
  y: number,
  pens: Parameters<typeof trendPensEngineeringValue>[0]
): VisualElementEngineering {
  return {
    id,
    key,
    type: BUILTIN_VISUAL_OBJECT_TYPES.trend,
    properties: {
      [VISUAL_PROPERTY_KEYS.x]: 10,
      [VISUAL_PROPERTY_KEYS.y]: y,
      [VISUAL_PROPERTY_KEYS.width]: 760,
      [VISUAL_PROPERTY_KEYS.height]: 240,
      [VISUAL_PROPERTY_KEYS.zIndex]: y,
      [VISUAL_PROPERTY_KEYS.trendMode]: mode,
      [VISUAL_PROPERTY_KEYS.trendWindowSeconds]: 3600,
      [VISUAL_PROPERTY_KEYS.trendRefreshSeconds]: 1,
      [VISUAL_PROPERTY_KEYS.trendLegendVisible]: true,
      [VISUAL_PROPERTY_KEYS.trendGridVisible]: true,
      [VISUAL_PROPERTY_KEYS.trendAxesVisible]: true,
      [VISUAL_PROPERTY_KEYS.trendQualityVisible]: showQuality,
      [TREND_PENS_PROPERTY]: trendPensEngineeringValue(pens)
    }
  };
}

const root = document.getElementById('root');
if (!root) throw new Error('C15 Trend harness root not found.');

createRoot(root).render(
  <div style={{ position: 'relative', width: 800, height: count === 2 ? 540 : 260 }}>
    <CanonicalVisualRenderer
      elements={elements}
      emptyLabel="No visual elements"
      locale={locale}
    />
  </div>
);
