import type { DataSourceEngineering, EngineeringPackageView } from './types';

export const NEW_DATA_SOURCE_IDENTITY = 'draft:new-datasource:catalog';

export type DataSourceConfigurationField = {
  key: string;
  valueKind: string;
  required: boolean;
  displayName: string;
  description?: string | null;
  defaultValue?: string | null;
  allowedValues: string[];
  minimum?: number | null;
  maximum?: number | null;
  advanced: boolean;
  expectedFormat?: string | null;
  exampleValue?: string | null;
};

export type DataSourceTypeDefinition = {
  typeKey: string;
  displayName: string;
  kind: string;
  description?: string | null;
  capabilities: {
    supportsConnectionTest: boolean;
    supportsDiscovery: boolean;
    supportsBrowse: boolean;
    supportsFileImport: boolean;
    supportsReconcile: boolean;
    supportsSharedTransportInfrastructure: boolean;
  };
  configurationSchema?: {
    schemaId: string;
    schemaVersion: number;
    dataSourceFields: DataSourceConfigurationField[];
    tagBindingFields: DataSourceConfigurationField[];
  } | null;
};

export function isProtectedReference(kind: string): boolean {
  return kind === 'secretReference' || kind === 'certificateReference';
}

export function settingsForType(type: DataSourceTypeDefinition): {
  settings: Record<string, string>;
  secretReferences: Record<string, string>;
} {
  const settings: Record<string, string> = {};
  const secretReferences: Record<string, string> = {};

  for (const field of type.configurationSchema?.dataSourceFields ?? []) {
    if (field.defaultValue == null || field.defaultValue === '') continue;
    if (isProtectedReference(field.valueKind)) secretReferences[field.key] = field.defaultValue;
    else settings[field.key] = field.defaultValue;
  }

  return { settings, secretReferences };
}

export function switchDataSourceType(
  source: DataSourceEngineering,
  type: DataSourceTypeDefinition
): DataSourceEngineering {
  const defaults = settingsForType(type);
  return {
    ...source,
    driver: type.typeKey,
    settings: defaults.settings,
    secretReferences: defaults.secretReferences
  };
}

export function newDataSourceDraft(type?: DataSourceTypeDefinition): DataSourceEngineering {
  const defaults = type ? settingsForType(type) : { settings: {}, secretReferences: {} };
  return {
    key: '',
    name: '',
    driver: type?.typeKey ?? '',
    enabled: true,
    settings: defaults.settings,
    secretReferences: defaults.secretReferences
  };
}

export function dataSourceIdentity(source: DataSourceEngineering): string {
  return source.id ?? `key:${source.key}`;
}

export function buildDataSourceCandidate(
  model: EngineeringPackageView,
  draft: DataSourceEngineering,
  selectedIdentity: string | null,
  isNew: boolean
): EngineeringPackageView {
  const next = clone(model);
  next.dataSources = isNew
    ? [...(next.dataSources ?? []), clone(draft)]
    : (next.dataSources ?? []).map(source =>
        dataSourceIdentity(source) === selectedIdentity ? clone(draft) : source);
  return next;
}

export function cloneDataSourceValue<T>(value: T): T {
  return clone(value);
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}
