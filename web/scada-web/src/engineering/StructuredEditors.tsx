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
      {hasMemoryTags(model) && <MemoryTagSettingsPanel model={model} locale={locale} />}
    </>
  );
}

function hasMemoryTags(model: EngineeringPackageView): boolean {
  const memorySources = new Set(
    (model.dataSources ?? [])
      .filter(source => {
        const driver = source.driver.toLowerCase();
        return driver === 'builtin.memory.client' || driver === 'builtin.memory.server';
      })
      .map(source => source.key.toLowerCase()));

  return model.tags.some(tag => tag.source && memorySources.has(tag.source.toLowerCase()));
}
