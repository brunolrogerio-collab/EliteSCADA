import React from 'react';
import type { EngineeringLocale } from '../i18n';
import type { EngineeringSnapshot } from '../types';
import { normalizeDynamoDefinitionParameterContract } from '../../runtime/visual-navigation/dynamoParameterWireContract';
import { C07VisualEditorI18nProvider } from './c07VisualEditorI18n';
import { DynamoAuthoringCatalogProvider } from './DynamoAuthoringCatalogContext';
import { HmiOperationalConfigurationPanel } from './HmiOperationalConfigurationPanel';
import { VisualEditorWorkspace as LegacyVisualEditorWorkspace } from './VisualEditorWorkspaceLegacy';
import './VisualEditorLayoutControls.css';

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
  const hmiLabel = locale === 'en'
    ? 'HMI operational configuration'
    : locale === 'es'
      ? 'Configuración operativa HMI'
      : 'Configuração operacional da HMI';

  return <div className="visual-editor-layout">
    <C07VisualEditorI18nProvider locale={locale}>
      <DynamoAuthoringCatalogProvider
        definitions={normalizedSnapshot.package.dynamos ?? []}
        tags={normalizedSnapshot.package.tags ?? []}
        visualAssets={normalizedSnapshot.package.visualAssets ?? []}
      >
        <LegacyVisualEditorWorkspace snapshot={normalizedSnapshot} locale={locale} onApplied={onApplied} />
      </DynamoAuthoringCatalogProvider>
    </C07VisualEditorI18nProvider>

    <details className="visual-editor-operational-disclosure">
      <summary>{hmiLabel}</summary>
      <HmiOperationalConfigurationPanel snapshot={normalizedSnapshot} locale={locale} onApplied={onApplied} />
    </details>
  </div>;
}
