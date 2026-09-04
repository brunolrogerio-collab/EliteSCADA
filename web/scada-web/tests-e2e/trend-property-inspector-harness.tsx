import React from 'react';
import { createRoot } from 'react-dom/client';
import type { EngineeringLocale } from '../src/engineering/i18n';
import type { VisualElementEngineering } from '../src/engineering/types';
import { C07VisualEditorI18nProvider } from '../src/engineering/visual-editor/c07VisualEditorI18n';
import { PropertyInspector } from '../src/engineering/visual-editor/property-inspector/PropertyInspector';

const params = new URLSearchParams(window.location.search);
const requested = params.get('locale');
const locale: EngineeringLocale = requested === 'en' || requested === 'es' ? requested : 'pt-BR';

const trend: VisualElementEngineering = {
  key: 'c15-property-inspector-trend',
  type: 'core.trend',
  properties: {
    trendMode: 'history',
    trendWindowSeconds: 3600,
    trendRefreshSeconds: 5,
    trendLegendVisible: true,
    trendGridVisible: true,
    trendAxesVisible: true,
    trendQualityVisible: true
  }
};

const root = document.getElementById('root');
if (!root) throw new Error('Trend Property Inspector harness root not found.');

createRoot(root).render(
  <C07VisualEditorI18nProvider locale={locale}>
    <PropertyInspector
      selectedElements={[trend]}
      onMutationIntent={() => undefined}
    />
  </C07VisualEditorI18nProvider>
);
