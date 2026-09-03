import React from 'react';
import type { EngineeringLocale } from '../i18n';
import type { EngineeringSnapshot } from '../types';
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
  return <DynamoAuthoringCatalogProvider
    definitions={snapshot.package.dynamos ?? []}
    tags={snapshot.package.tags ?? []}
  >
    <LegacyVisualEditorWorkspace snapshot={snapshot} locale={locale} onApplied={onApplied} />
  </DynamoAuthoringCatalogProvider>;
}
