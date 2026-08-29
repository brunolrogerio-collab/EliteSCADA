import type {
  EngineeringPackageView,
  TagEngineering,
  VisualAssetEngineering
} from '../types';

export type ProjectReferenceFamily =
  | 'tag'
  | 'serverMemory'
  | 'clientMemory'
  | 'system'
  | 'driverDiagnostic'
  | 'asset';

export type ProjectReferenceDescriptor = Readonly<{
  reference: string;
  label: string;
  family: ProjectReferenceFamily;
  dataType: string;
  engineeringUnit?: string | null;
  providerIdentity?: string | null;
  writable?: boolean;
  bindingKind?: 'Tag' | 'ClientMemory';
  pathSegments: readonly string[];
}>;

export type ClientMemoryDefinitionView = Readonly<{
  name: string;
  dataType: string;
  initialValue?: unknown;
  readOnly?: boolean;
}>;

export type RuntimeReferenceFacts = Readonly<{
  driverKeys?: readonly string[];
}>;

export function buildProjectReferenceCatalog(
  model: EngineeringPackageView,
  clientMemoryDefinitions: readonly ClientMemoryDefinitionView[] = [],
  runtimeFacts: RuntimeReferenceFacts = {}
): readonly ProjectReferenceDescriptor[] {
  const result: ProjectReferenceDescriptor[] = [];

  for (const tag of model.tags ?? []) {
    if (!tag.path?.trim()) continue;
    const family = isServerMemoryTag(tag) ? 'serverMemory' : 'tag';
    result.push(Object.freeze({
      reference: tag.path.trim(),
      label: tag.name?.trim() || tag.path.trim(),
      family,
      dataType: tag.dataType,
      engineeringUnit: tag.engineeringUnit ?? null,
      providerIdentity: tag.source ?? null,
      writable: !tag.readOnly,
      bindingKind: 'Tag',
      pathSegments: Object.freeze(splitPath(tag.path))
    }));
  }

  for (const definition of clientMemoryDefinitions) {
    const name = definition.name?.trim();
    if (!name) continue;
    result.push(Object.freeze({
      reference: name,
      label: name,
      family: 'clientMemory',
      dataType: definition.dataType,
      providerIdentity: 'builtin.memory.client',
      writable: definition.readOnly !== true,
      bindingKind: 'ClientMemory',
      pathSegments: Object.freeze(splitPath(name))
    }));
  }

  for (const asset of model.visualAssets ?? []) {
    if (!asset.id?.trim()) continue;
    result.push(assetReference(asset));
  }

  result.push(Object.freeze({
    reference: 'system.runtime.tagCount',
    label: 'Runtime TAG count',
    family: 'system',
    dataType: 'Int32',
    providerIdentity: 'runtime',
    writable: false,
    pathSegments: Object.freeze(['Runtime', 'TAG count'])
  }));
  result.push(Object.freeze({
    reference: 'system.runtime.driverCount',
    label: 'Runtime driver count',
    family: 'system',
    dataType: 'Int32',
    providerIdentity: 'runtime',
    writable: false,
    pathSegments: Object.freeze(['Runtime', 'Driver count'])
  }));

  for (const driverKey of runtimeFacts.driverKeys ?? []) {
    const key = driverKey.trim();
    if (!key) continue;
    result.push(Object.freeze({
      reference: `driver:${key}:state`,
      label: `${key} · state`,
      family: 'driverDiagnostic',
      dataType: 'String',
      providerIdentity: key,
      writable: false,
      pathSegments: Object.freeze([key, 'State'])
    }));
    result.push(Object.freeze({
      reference: `driver:${key}:recentFailureRate`,
      label: `${key} · recent failure rate`,
      family: 'driverDiagnostic',
      dataType: 'Double',
      providerIdentity: key,
      writable: false,
      pathSegments: Object.freeze([key, 'Recent failure rate'])
    }));
    result.push(Object.freeze({
      reference: `driver:${key}:lastSuccessfulCommunicationAt`,
      label: `${key} · last successful communication`,
      family: 'driverDiagnostic',
      dataType: 'DateTime',
      providerIdentity: key,
      writable: false,
      pathSegments: Object.freeze([key, 'Last successful communication'])
    }));
  }

  return Object.freeze(deduplicate(result));
}

export function filterProjectReferences(
  catalog: readonly ProjectReferenceDescriptor[],
  query: string
): readonly ProjectReferenceDescriptor[] {
  const needle = query.trim().toLocaleLowerCase();
  if (!needle) return catalog;
  return Object.freeze(catalog.filter(item =>
    item.reference.toLocaleLowerCase().includes(needle) ||
    item.label.toLocaleLowerCase().includes(needle) ||
    item.family.toLocaleLowerCase().includes(needle) ||
    item.dataType.toLocaleLowerCase().includes(needle) ||
    (item.providerIdentity ?? '').toLocaleLowerCase().includes(needle)
  ));
}

export function isScalarProjectDataType(dataType: string): boolean {
  return ['boolean', 'int16', 'int32', 'int64', 'float', 'double', 'string', 'datetime', 'enum']
    .includes(dataType.trim().toLowerCase());
}

export function isReferenceCompatibleWithVisualProperty(
  destinationType: string,
  reference: ProjectReferenceDescriptor,
  options: Readonly<{ allowScalarText?: boolean }> = {}
): boolean {
  if (reference.family === 'asset') return destinationType === 'assetRef';
  if (!reference.bindingKind) return false;
  if (destinationType === 'string' && options.allowScalarText && isScalarProjectDataType(reference.dataType)) return true;

  const dataType = reference.dataType.trim().toLowerCase();
  if (destinationType === 'number') return ['int16', 'int32', 'int64', 'float', 'double'].includes(dataType);
  if (destinationType === 'boolean') return dataType === 'boolean';
  if (destinationType === 'string') return ['string', 'enum', 'datetime'].includes(dataType);
  if (destinationType === 'color') return dataType === 'string';
  if (destinationType === 'enum') return dataType === 'enum' || dataType === 'string';
  return false;
}

export function projectReferenceFamilyLabel(
  family: ProjectReferenceFamily,
  locale: 'pt-BR' | 'en' | 'es'
): string {
  const labels = {
    'pt-BR': {
      tag: 'TAGs', serverMemory: 'Memória do Servidor', clientMemory: 'Memória do Cliente',
      system: 'Sistema / Runtime', driverDiagnostic: 'Data Sources / Drivers', asset: 'Assets'
    },
    en: {
      tag: 'TAGs', serverMemory: 'Server Memory', clientMemory: 'Client Memory',
      system: 'System / Runtime', driverDiagnostic: 'Data Sources / Drivers', asset: 'Assets'
    },
    es: {
      tag: 'TAGs', serverMemory: 'Memoria del Servidor', clientMemory: 'Memoria del Cliente',
      system: 'Sistema / Runtime', driverDiagnostic: 'Data Sources / Drivers', asset: 'Assets'
    }
  } as const;
  return labels[locale][family];
}

function isServerMemoryTag(tag: TagEngineering): boolean {
  const source = tag.source?.trim().toLowerCase() ?? '';
  const provider = tag.metadata?.sourceProviderType?.trim().toLowerCase() ?? '';
  return source === 'builtin.memory.server' ||
    source.includes('memory.server') ||
    provider === 'builtin.memory.server' ||
    (tag.initialValue != null && source.includes('memory'));
}

function assetReference(asset: VisualAssetEngineering): ProjectReferenceDescriptor {
  return Object.freeze({
    reference: asset.id!,
    label: asset.name?.trim() || asset.key,
    family: 'asset',
    dataType: 'assetRef',
    providerIdentity: 'project.assets',
    writable: false,
    pathSegments: Object.freeze([asset.name?.trim() || asset.key])
  });
}

function splitPath(value: string): string[] {
  return value.split(/[/.\\]+/g).map(part => part.trim()).filter(Boolean);
}

function deduplicate(values: readonly ProjectReferenceDescriptor[]): ProjectReferenceDescriptor[] {
  const seen = new Set<string>();
  const result: ProjectReferenceDescriptor[] = [];
  for (const value of values) {
    const identity = `${value.family}\u0000${value.reference}`;
    if (seen.has(identity)) continue;
    seen.add(identity);
    result.push(value);
  }
  return result;
}
