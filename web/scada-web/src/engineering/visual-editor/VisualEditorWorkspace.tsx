import React from 'react';
import type { EngineeringLocale } from '../i18n';
import type { EngineeringSnapshot } from '../types';
import { normalizeDynamoDefinitionParameterContract } from '../../runtime/visual-navigation/dynamoParameterWireContract';
import { DynamoAuthoringCatalogProvider } from './DynamoAuthoringCatalogContext';
import { VisualEditorWorkspace as LegacyVisualEditorWorkspace } from './VisualEditorWorkspaceLegacy';

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

  return <DynamoAuthoringCatalogProvider
    definitions={normalizedSnapshot.package.dynamos ?? []}
    tags={normalizedSnapshot.package.tags ?? []}
    visualAssets={normalizedSnapshot.package.visualAssets ?? []}
  >
    <LegacyVisualEditorWorkspace snapshot={normalizedSnapshot} locale={locale} onApplied={onApplied} />
  </DynamoAuthoringCatalogProvider>;
}
