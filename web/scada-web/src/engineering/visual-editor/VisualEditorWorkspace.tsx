import React from 'react';
import type { EngineeringLocale } from '../i18n';
import type { EngineeringSnapshot } from '../types';
import { normalizeDynamoDefinitionParameterContract } from '../../runtime/visual-navigation/dynamoParameterWireContract';
import { C07VisualEditorI18nProvider } from './c07VisualEditorI18n';
import { DynamoAuthoringCatalogProvider } from './DynamoAuthoringCatalogContext';
import { HmiOperationalConfigurationPanel } from './HmiOperationalConfigurationPanel';
import { VisualEditorWorkspace as LegacyVisualEditorWorkspace } from './VisualEditorWorkspaceLegacy';
import './VisualEditorLayoutControls.css';

type LayoutControlCopy = {
  controls: string;
  hideNavigation: string;
  showNavigation: string;
  hideScreens: string;
  showScreens: string;
  hidePalette: string;
  showPalette: string;
  hideProperties: string;
  showProperties: string;
};

export function VisualEditorWorkspace({
  snapshot,
  locale,
  onApplied
}: {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  onApplied: () => Promise<void>;
}) {
  const normalizedSnapshot = React.useMemo<EngineeringSnapshot>(() => ({
    ...snapshot,
    package: {
      ...snapshot.package,
      dynamos: snapshot.package.dynamos?.map(normalizeDynamoDefinitionParameterContract)
    }
  }), [snapshot]);
  const copy = React.useMemo(() => layoutControlCopy(locale), [locale]);
  const rootRef = React.useRef<HTMLDivElement>(null);
  const [navigationCollapsed, setNavigationCollapsed] = React.useState(false);
  const [screensCollapsed, setScreensCollapsed] = React.useState(false);
  const [paletteCollapsed, setPaletteCollapsed] = React.useState(false);
  const [propertiesCollapsed, setPropertiesCollapsed] = React.useState(false);

  React.useEffect(() => {
    const engineeringBody = rootRef.current?.closest('.eng-body');
    if (!(engineeringBody instanceof HTMLElement)) return undefined;
    engineeringBody.classList.toggle('eng-body--navigation-collapsed', navigationCollapsed);
    return () => engineeringBody.classList.remove('eng-body--navigation-collapsed');
  }, [navigationCollapsed]);

  const layoutClassName = [
    'visual-editor-layout',
    screensCollapsed ? 'visual-editor-layout--screens-collapsed' : '',
    paletteCollapsed ? 'visual-editor-layout--palette-collapsed' : '',
    propertiesCollapsed ? 'visual-editor-layout--properties-collapsed' : ''
  ].filter(Boolean).join(' ');

  return <div className={layoutClassName} ref={rootRef}>
    <div className="visual-editor-layout-controls" role="group" aria-label={copy.controls}>
      <LayoutToggle
        testId="engineering-navigation-toggle"
        collapsed={navigationCollapsed}
        collapseLabel={copy.hideNavigation}
        expandLabel={copy.showNavigation}
        onToggle={() => setNavigationCollapsed(current => !current)}
      />
      <LayoutToggle
        testId="visual-editor-screens-toggle"
        collapsed={screensCollapsed}
        collapseLabel={copy.hideScreens}
        expandLabel={copy.showScreens}
        onToggle={() => setScreensCollapsed(current => !current)}
      />
      <LayoutToggle
        testId="visual-editor-palette-toggle"
        collapsed={paletteCollapsed}
        collapseLabel={copy.hidePalette}
        expandLabel={copy.showPalette}
        onToggle={() => setPaletteCollapsed(current => !current)}
      />
      <LayoutToggle
        testId="visual-editor-properties-toggle"
        collapsed={propertiesCollapsed}
        collapseLabel={copy.hideProperties}
        expandLabel={copy.showProperties}
        onToggle={() => setPropertiesCollapsed(current => !current)}
      />
    </div>

    <HmiOperationalConfigurationPanel snapshot={normalizedSnapshot} locale={locale} onApplied={onApplied} />
    <C07VisualEditorI18nProvider locale={locale}>
      <DynamoAuthoringCatalogProvider
        definitions={normalizedSnapshot.package.dynamos ?? []}
        tags={normalizedSnapshot.package.tags ?? []}
        visualAssets={normalizedSnapshot.package.visualAssets ?? []}
      >
        <LegacyVisualEditorWorkspace snapshot={normalizedSnapshot} locale={locale} onApplied={onApplied} />
      </DynamoAuthoringCatalogProvider>
    </C07VisualEditorI18nProvider>
  </div>;
}

function LayoutToggle({
  testId,
  collapsed,
  collapseLabel,
  expandLabel,
  onToggle
}: {
  testId: string;
  collapsed: boolean;
  collapseLabel: string;
  expandLabel: string;
  onToggle: () => void;
}) {
  const label = collapsed ? expandLabel : collapseLabel;
  return <button
    type="button"
    className={collapsed ? 'is-collapsed' : ''}
    data-testid={testId}
    aria-label={label}
    aria-pressed={collapsed}
    title={label}
    onClick={onToggle}
  >
    <span aria-hidden="true">{collapsed ? '＋' : '−'}</span>
    {label}
  </button>;
}

function layoutControlCopy(locale: EngineeringLocale): LayoutControlCopy {
  if (locale === 'en') return {
    controls: 'Engineering workspace layout',
    hideNavigation: 'Collapse Engineering navigation',
    showNavigation: 'Expand Engineering navigation',
    hideScreens: 'Collapse screen list',
    showScreens: 'Expand screen list',
    hidePalette: 'Collapse palette',
    showPalette: 'Expand palette',
    hideProperties: 'Collapse Properties',
    showProperties: 'Expand Properties'
  };
  if (locale === 'es') return {
    controls: 'Disposición del workspace de Engineering',
    hideNavigation: 'Contraer navegación de Engineering',
    showNavigation: 'Expandir navegación de Engineering',
    hideScreens: 'Contraer lista de Pantallas',
    showScreens: 'Expandir lista de Pantallas',
    hidePalette: 'Contraer paleta',
    showPalette: 'Expandir paleta',
    hideProperties: 'Contraer Propiedades',
    showProperties: 'Expandir Propiedades'
  };
  return {
    controls: 'Layout do workspace de Engineering',
    hideNavigation: 'Recolher navegação do Engineering',
    showNavigation: 'Expandir navegação do Engineering',
    hideScreens: 'Recolher lista de Telas',
    showScreens: 'Expandir lista de Telas',
    hidePalette: 'Recolher paleta',
    showPalette: 'Expandir paleta',
    hideProperties: 'Recolher Propriedades',
    showProperties: 'Expandir Propriedades'
  };
}
