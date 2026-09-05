import React from 'react';
import type { EngineeringLocale } from '../i18n';
import type { EngineeringSnapshot } from '../types';
import { normalizeDynamoDefinitionParameterContract } from '../../runtime/visual-navigation/dynamoParameterWireContract';
import { C07VisualEditorI18nProvider } from './c07VisualEditorI18n';
import { PopupVisualEditorWorkspace as PopupVisualEditorWorkspaceImpl } from './PopupVisualEditorWorkspaceImpl';

export function PopupVisualEditorWorkspace({
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

  return <C07VisualEditorI18nProvider locale={locale}>
    <PopupVisualEditorWorkspaceImpl snapshot={normalizedSnapshot} locale={locale} onApplied={onApplied} />
  </C07VisualEditorI18nProvider>;
}
