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
  displayNameResourceKey?: string | null;
  descriptionResourceKey?: string | null;
};

export type TagBindingSchemaIdentity = Readonly<{
  schemaId: string;
  schemaVersion: number;
}>;

export type DataSourceTypeDefinition = {
  typeKey: string;
  displayName: string;
  displayNameResourceKey?: string | null;
  kind: string;
  description?: string | null;
  descriptionResourceKey?: string | null;
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
  tagBindingSchemaId?: string | null;
  tagBindingSchemaVersion?: number | null;
};

export type DataSourceDraftIssue = {
  fieldKey: string;
  code: 'required' | 'integer' | 'number' | 'duration' | 'enum' | 'minimum' | 'maximum' | 'incompatible';
  expected?: string;
};

export type IncompatibleDataSourceConfiguration = {
  settings: string[];
  secretReferences: string[];
};

export function tagBindingSchemaIdentity(
  type: DataSourceTypeDefinition | null | undefined
): TagBindingSchemaIdentity | null {
  const configurationSchema = type?.configurationSchema;
  if (!configurationSchema) return null;

  const schemaId = type?.tagBindingSchemaId?.trim() || configurationSchema.schemaId;
  const schemaVersion = type?.tagBindingSchemaVersion ?? configurationSchema.schemaVersion;
  if (!schemaId || !Number.isInteger(schemaVersion) || schemaVersion <= 0) return null;
  return { schemaId, schemaVersion };
}

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

export function draftForDataSourceSelection(
  selectedIdentity: string | null,
  sources: readonly DataSourceEngineering[]
): DataSourceEngineering | null {
  if (selectedIdentity === NEW_DATA_SOURCE_IDENTITY) return newDataSourceDraft();
  if (!selectedIdentity) return null;
  const current = sources.find(source => dataSourceIdentity(source) === selectedIdentity) ?? null;
  return current ? cloneDataSourceValue(current) : null;
}

export function incompatibleDataSourceConfiguration(
  source: DataSourceEngineering,
  type: DataSourceTypeDefinition
): IncompatibleDataSourceConfiguration {
  const fields = new Map(
    (type.configurationSchema?.dataSourceFields ?? []).map(field => [field.key.toLowerCase(), field] as const));

  const settings = Object.keys(source.settings ?? {}).filter(key => {
    const field = fields.get(key.toLowerCase());
    return !field || isProtectedReference(field.valueKind);
  });
  const secretReferences = Object.keys(source.secretReferences ?? {}).filter(key => {
    const field = fields.get(key.toLowerCase());
    return !field || !isProtectedReference(field.valueKind);
  });

  return { settings, secretReferences };
}

export function removeIncompatibleDataSourceConfiguration(
  source: DataSourceEngineering,
  type: DataSourceTypeDefinition
): DataSourceEngineering {
  const fields = new Map(
    (type.configurationSchema?.dataSourceFields ?? []).map(field => [field.key.toLowerCase(), field] as const));
  const settings = Object.fromEntries(
    Object.entries(source.settings ?? {}).filter(([key]) => {
      const field = fields.get(key.toLowerCase());
      return Boolean(field && !isProtectedReference(field.valueKind));
    }));
  const secretReferences = Object.fromEntries(
    Object.entries(source.secretReferences ?? {}).filter(([key]) => {
      const field = fields.get(key.toLowerCase());
      return Boolean(field && isProtectedReference(field.valueKind));
    }));

  return { ...source, settings, secretReferences };
}

export function validateDataSourceDraft(
  source: DataSourceEngineering,
  type: DataSourceTypeDefinition | null
): DataSourceDraftIssue[] {
  const issues: DataSourceDraftIssue[] = [];
  if (!source.name.trim()) issues.push({ fieldKey: '$name', code: 'required' });
  if (!source.key.trim()) issues.push({ fieldKey: '$key', code: 'required' });
  if (!source.driver.trim() || !type) issues.push({ fieldKey: '$type', code: 'required' });
  if (!type) return issues;

  const incompatible = incompatibleDataSourceConfiguration(source, type);
  for (const key of incompatible.settings)
    issues.push({ fieldKey: key, code: 'incompatible' });
  for (const key of incompatible.secretReferences)
    issues.push({ fieldKey: key, code: 'incompatible' });

  for (const field of type.configurationSchema?.dataSourceFields ?? []) {
    const values: Record<string, string> = isProtectedReference(field.valueKind)
      ? (source.secretReferences ?? {})
      : (source.settings ?? {});
    const raw = values[field.key];
    const value = raw == null || raw.trim() === '' ? field.defaultValue ?? '' : raw.trim();

    if (!value) {
      if (field.required) issues.push({ fieldKey: field.key, code: 'required', expected: field.expectedFormat ?? undefined });
      continue;
    }

    if (field.valueKind === 'boolean') {
      if (!/^(true|false)$/i.test(value))
        issues.push({ fieldKey: field.key, code: 'enum', expected: field.expectedFormat ?? 'true | false' });
    } else if (field.valueKind === 'integer' || field.valueKind === 'port') {
      if (!/^[+-]?\d+$/.test(value)) {
        issues.push({ fieldKey: field.key, code: 'integer', expected: field.expectedFormat ?? undefined });
        continue;
      }
      validateRange(Number(value), field, issues);
    } else if (field.valueKind === 'number') {
      const parsed = Number(value);
      if (!Number.isFinite(parsed)) {
        issues.push({ fieldKey: field.key, code: 'number', expected: field.expectedFormat ?? undefined });
        continue;
      }
      validateRange(parsed, field, issues);
    } else if (field.valueKind === 'duration') {
      const milliseconds = parseDurationMilliseconds(value);
      if (milliseconds === null) {
        issues.push({ fieldKey: field.key, code: 'duration', expected: field.expectedFormat ?? undefined });
        continue;
      }
      validateRange(milliseconds, field, issues);
    } else if (field.valueKind === 'enum' && field.allowedValues.length > 0 && !field.allowedValues.some(option => option.toLowerCase() === value.toLowerCase())) {
      issues.push({ fieldKey: field.key, code: 'enum', expected: field.expectedFormat ?? field.allowedValues.join(' | ') });
    }
  }

  return issues;
}

function parseDurationMilliseconds(value: string): number | null {
  const match = /^(?:(\d+)\.)?(\d{1,2}):([0-5]\d):([0-5]\d)(?:\.(\d{1,7}))?$/.exec(value);
  if (!match) return null;

  const days = Number(match[1] ?? 0);
  const hours = Number(match[2]);
  const minutes = Number(match[3]);
  const seconds = Number(match[4]);
  const fraction = match[5] ? Number(`0.${match[5]}`) : 0;
  const total = ((((days * 24) + hours) * 60 + minutes) * 60 + seconds + fraction) * 1000;
  return Number.isFinite(total) ? total : null;
}

function validateRange(
  value: number,
  field: DataSourceConfigurationField,
  issues: DataSourceDraftIssue[]
): void {
  if (field.minimum != null && value < field.minimum)
    issues.push({ fieldKey: field.key, code: 'minimum', expected: field.expectedFormat ?? String(field.minimum) });
  else if (field.maximum != null && value > field.maximum)
    issues.push({ fieldKey: field.key, code: 'maximum', expected: field.expectedFormat ?? String(field.maximum) });
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
