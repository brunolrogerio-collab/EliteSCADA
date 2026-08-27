import React from 'react';
import {
  DataSourceEditor,
  TagEditor as BaseTagEditor
} from './EngineeringMutationPanels';
import { MemoryTagSettingsPanel } from './MemoryTagSettingsPanel';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView } from './types';

export { DataSourceEditor };

export function TagEditor({ model, locale }: { model: EngineeringPackageView; locale: EngineeringLocale }) {
  return (
    <>
      <BaseTagEditor model={model} locale={locale} />
      <MemoryTagSettingsPanel model={model} locale={locale} />
    </>
  );
}
