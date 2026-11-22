import React from 'react';
import type { EngineeringLocale } from '../i18n';

export type VisualEditorRegion = 'screens' | 'palette' | 'properties';

export function VisualEditorRegionToggle({
  region,
  collapsed,
  locale,
  onToggle
}: {
  region: VisualEditorRegion;
  collapsed: boolean;
  locale: EngineeringLocale;
  onToggle: () => void;
}) {
  const label = regionLabel(locale, region, collapsed);
  return <button
    type="button"
    className="visual-editor-region-toggle"
    data-testid={`visual-editor-${region}-toggle`}
    aria-label={label}
    aria-expanded={!collapsed}
    title={label}
    onClick={onToggle}
  >
    <span aria-hidden="true">{collapsed ? '›' : '‹'}</span>
  </button>;
}

function regionLabel(locale: EngineeringLocale, region: VisualEditorRegion, collapsed: boolean): string {
  const action = collapsed
    ? (locale === 'en' ? 'Show' : locale === 'es' ? 'Mostrar' : 'Mostrar')
    : (locale === 'en' ? 'Hide' : locale === 'es' ? 'Ocultar' : 'Ocultar');
  const names = locale === 'en'
    ? { screens: 'screen/popup list', palette: 'object palette', properties: 'Properties' }
    : locale === 'es'
      ? { screens: 'lista de pantallas/popups', palette: 'paleta de objetos', properties: 'Propiedades' }
      : { screens: 'lista de telas/popups', palette: 'paleta de objetos', properties: 'Propriedades' };
  return `${action} ${names[region]}`;
}
