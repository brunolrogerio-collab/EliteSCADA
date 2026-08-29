import type {
  EngineeringPackageView,
  TagEngineering,
  TagValueReferenceEngineering,
  VisualAssetEngineering
} from '../types';

export type ProjectReferenceFamily =
  | 'tag'
  | 'serverMemory'
  | 'clientMemory'
  | 'system'
  | 'driverDiagnostic'
  | 'asset';

export type ProjectReferenceSelectorCapability = Readonly<{
  kind: 'bit';
  minIndex: number;
  maxIndex: number;
}>;

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
  tagReference?: TagValueReferenceEngineering | null;
  selectorCapability?: ProjectReferenceSelectorCapability | null;
}>;

export type ProjectReferenceResolution = Readonly<{
  status: 'found' | 'ambiguous' | 'notFound';
  descriptor?: ProjectReferenceDescriptor;
}>;

export type ClientMemoryDefinitionView = Readonly<{
  id?: string;
  name: string;
  path: string;
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
    const tagId = tag.id?.trim();
    const selectorCapability = bitSelectorCapability(tag.dataType);
    result.push(Object.freeze({
      reference: tag.path.trim(),
      label: tag.name?.trim() || tag.path.trim(),
      family,
      dataType: tag.dataType,
      engineeringUnit: tag.engineeringUnit ?? null,
      providerIdentity: tag.source ?? null,
      writable: !tag.readOnly,
      bindingKind: 'Tag',
      pathSegments: Object.freeze(splitPath(tag.path)),
      tagReference: tagId ? Object.freeze({ tagId }) : null,
      selectorCapability
    }));
  }

  for (const definition of clientMemoryDefinitions) {
    const path = definition.path?.trim();
    if (!path) continue;
    const name = definition.name?.trim() || path;
    const tagId = definition.id?.trim();
    result.push(Object.freeze({
      reference: path,
      label: name,
      family: 'clientMemory',
      dataType: definition.dataType,
      providerIdentity: 'builtin.memory.client',
      writable: definition.readOnly !== true,
      bindingKind: 'ClientMemory',
      pathSegments: Object.freeze(splitPath(path)),
      tagReference: tagId ? Object.freeze({ tagId }) : null,
      selectorCapability: null
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

export function createTagBitProjectReference(
  base: ProjectReferenceDescriptor,
  bitIndex: number
): ProjectReferenceDescriptor | null {
  const capability = base.selectorCapability;
  const tagReference = base.tagReference;
  if (!capability || capability.kind !== 'bit' || !tagReference?.tagId) return null;
  if (!Number.isInteger(bitIndex) || bitIndex < capability.minIndex || bitIndex > capability.maxIndex) return null;

  const suffix = bitIndex.toString().padStart(2, '0');
  const reference = `${base.reference}.${suffix}`;
  const label = `${base.label}.${suffix}`;
  const pathSegments = base.pathSegments.length > 0
    ? [...base.pathSegments.slice(0, -1), `${base.pathSegments[base.pathSegments.length - 1]}.${suffix}`]
    : [reference];

  return Object.freeze({
    ...base,
    reference,
    label,
    dataType: 'Boolean',
    engineeringUnit: null,
    pathSegments: Object.freeze(pathSegments),
    tagReference: Object.freeze({
      tagId: tagReference.tagId,
      selector: Object.freeze({ kind: 'bit', index: bitIndex })
    }),
    selectorCapability: null
  });
}

export function resolveProjectReference(
  catalog: readonly ProjectReferenceDescriptor[],
  rawReference: string
): ProjectReferenceResolution {
  const candidate = rawReference.trim();
  if (!candidate) return Object.freeze({ status: 'notFound' });

  const exactReference = catalog.filter(item => item.reference === candidate);
  if (exactReference.length === 1) return Object.freeze({ status: 'found', descriptor: exactReference[0] });
  if (exactReference.length > 1) return Object.freeze({ status: 'ambiguous' });

  const exactLabel = catalog.filter(item => item.label === candidate);
  if (exactLabel.length === 1) return Object.freeze({ status: 'found', descriptor: exactLabel[0] });
  if (exactLabel.length > 1) return Object.freeze({ status: 'ambiguous' });

  const bitMatch = /^(.*)\.(\d{1,2})$/.exec(candidate);
  if (!bitMatch) return Object.freeze({ status: 'notFound' });
  const baseText = bitMatch[1];
  const bitIndex = Number(bitMatch[2]);
  const bases = catalog.filter(item => item.reference === baseText || item.label === baseText);
  const derived = bases
    .map(base => createTagBitProjectReference(base, bitIndex))
    .filter((item): item is ProjectReferenceDescriptor => item !== null);
  if (derived.length === 1) return Object.freeze({ status: 'found', descriptor: derived[0] });
  if (derived.length > 1) return Object.freeze({ status: 'ambiguous' });
  return Object.freeze({ status: 'notFound' });
}

export function projectReferenceIdentity(reference: ProjectReferenceDescriptor): string {
  const tagReference = reference.tagReference;
  if (tagReference?.tagId) {
    const selector = tagReference.selector;
    return selector
      ? `tag:${tagReference.tagId}:selector:${selector.kind}:${selector.index}`
      : `tag:${tagReference.tagId}`;
  }
  return `${reference.family}:${reference.reference}`;
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
    (item.providerIdentity ?? '').toLocaleLowerCase().includes(needle) ||
    (item.tagReference?.tagId ?? '').toLocaleLowerCase().includes(needle)
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

function bitSelectorCapability(dataType: string): ProjectReferenceSelectorCapability | null {
  const width = dataType.trim().toLowerCase() === 'int16' ? 16
    : dataType.trim().toLowerCase() === 'int32' ? 32
      : dataType.trim().toLowerCase() === 'int64' ? 64
        : null;
  return width === null ? null : Object.freeze({ kind: 'bit', minIndex: 0, maxIndex: width - 1 });
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
    const identity = projectReferenceIdentity(value);
    if (seen.has(identity)) continue;
    seen.add(identity);
    result.push(value);
  }
  return result;
}