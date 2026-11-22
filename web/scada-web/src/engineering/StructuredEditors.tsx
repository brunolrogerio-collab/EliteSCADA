import React from 'react';
import { DataSourceCatalogEditor } from './DataSourceCatalogEditor';
import { TagEditor as SecuredTagEditor } from './SecuredEngineeringEditors';
import { MemoryTagSettingsPanel } from './MemoryTagSettingsPanel';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView } from './types';

export function DataSourceEditor({ model, locale, projectKey }: { model: EngineeringPackageView; locale: EngineeringLocale; projectKey: string }) {
  return <DataSourceCatalogEditor model={model} locale={locale} projectKey={projectKey} />;
}

export function TagEditor({ model, locale, projectKey }: { model: EngineeringPackageView; locale: EngineeringLocale; projectKey: string }) {
  return (
    <>
      <SecuredTagEditor model={model} locale={locale} projectKey={projectKey} />
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
