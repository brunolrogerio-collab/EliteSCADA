import type { TagEngineering } from './types';
import type { TagSourceAwareEngineering } from './TagSourceSelector.logic';
import { parseCanonicalModbusAddress } from './TagAddressAssistant.logic';

export const MAX_TAG_SEQUENCE_COUNT = 200;

export type TagDuplicationDraft = TagSourceAwareEngineering;

export type TagSequenceOptions = Readonly<{
  count: number;
  namePattern: string;
  pathPattern: string;
  suffixStart: number;
  suffixStep: number;
  addressStep: number;
}>;

export type TagDuplicationCollision = Readonly<{
  code: 'TAG_ID_COLLISION' | 'TAG_PATH_COLLISION' | 'TAG_NAME_COLLISION' | 'TAG_ADDRESS_COLLISION';
  field: 'id' | 'path' | 'name' | 'address';
  index: number;
  message: string;
}>;

export type TagDuplicationValidation = Readonly<{
  canPreview: boolean;
  collisions: readonly TagDuplicationCollision[];
}>;

export function copyTagConfiguration(
  source: TagSourceAwareEngineering,
  newStableId: string
): TagDuplicationDraft {
  return {
    id: newStableId,
    name: source.name,
    path: source.path,
    dataType: source.dataType,
    source: source.source,
    address: source.address,
    engineeringUnit: source.engineeringUnit,
    description: source.description,
    readOnly: source.readOnly,
    scaleMinimum: source.scaleMinimum,
    scaleMaximum: source.scaleMaximum,
    historian: cloneValue(source.historian),
    metadata: cloneValue(source.metadata),
    accessPolicy: cloneValue(source.accessPolicy),
    initialValue: cloneValue(source.initialValue),
    addressSelector: cloneValue(source.addressSelector),
    historianCaptureProfileId: source.historianCaptureProfileId,
    dataSourceId: source.dataSourceId,
    communicationBinding: cloneValue(source.communicationBinding)
  };
}

export function createDuplicateTagDrafts(
  sources: readonly TagSourceAwareEngineering[],
  existingTags: readonly TagEngineering[],
  idFactory: () => string
): TagDuplicationDraft[] {
  const reservedPaths = new Set(existingTags.map(tag => normalize(tag.path)));
  const reservedNames = new Set(existingTags.map(tag => scopedNameKey(tag.path, tag.name)));

  return sources.map(source => {
    const draft = copyTagConfiguration(source, idFactory());
    let ordinal = 1;
    let path = duplicatePath(source.path, ordinal);
    let name = duplicateName(source.name, ordinal);

    while (reservedPaths.has(normalize(path)) || reservedNames.has(scopedNameKey(path, name))) {
      ordinal += 1;
      path = duplicatePath(source.path, ordinal);
      name = duplicateName(source.name, ordinal);
    }

    draft.path = path;
    draft.name = name;
    reservedPaths.add(normalize(path));
    reservedNames.add(scopedNameKey(path, name));
    return draft;
  });
}

export function createSequentialTagDrafts(
  source: TagSourceAwareEngineering,
  existingTags: readonly TagEngineering[],
  options: TagSequenceOptions,
  idFactory: () => string
): TagDuplicationDraft[] {
  validateSequenceOptions(options);
  const drafts: TagDuplicationDraft[] = [];

  for (let index = 0; index < options.count; index += 1) {
    const suffix = options.suffixStart + (index * options.suffixStep);
    const draft = copyTagConfiguration(source, idFactory());
    draft.name = expandPattern(options.namePattern, source, suffix);
    draft.path = expandPattern(options.pathPattern, source, suffix);

    if (!draft.name.trim()) throw new Error('Generated TAG name cannot be empty.');
    if (!draft.path.trim()) throw new Error('Generated TAG path cannot be empty.');

    drafts.push(draft);
  }

  return drafts;
}

export function sequentialModbusReference(
  sourceAddress: string | null | undefined,
  addressStep: number,
  generatedIndex: number
): Readonly<{ area: 'coil' | 'discrete' | 'holding' | 'input'; reference: number }> {
  if (!Number.isInteger(addressStep)) throw new Error('Modbus address step must be an integer.');
  if (generatedIndex < 0 || !Number.isInteger(generatedIndex)) throw new Error('Generated index must be a non-negative integer.');

  const parsed = parseCanonicalModbusAddress(sourceAddress);
  if (!parsed) {
    throw new Error('Sequential address generation requires a canonical Modbus address (coil/discrete/holding/input).');
  }

  const baseReference = Number(parsed.reference);
  const reference = baseReference + (addressStep * (generatedIndex + 1));
  if (!Number.isSafeInteger(reference) || reference < 0 || reference > 65535) {
    throw new Error(`Generated Modbus reference ${reference} is outside the canonical 0..65535 range.`);
  }

  return { area: parsed.area, reference };
}

export function validateGeneratedTagDrafts(
  existingTags: readonly TagSourceAwareEngineering[],
  drafts: readonly TagDuplicationDraft[],
  options: Readonly<{ checkAddressCollisions: boolean }>
): TagDuplicationValidation {
  const collisions: TagDuplicationCollision[] = [];
  const existingIds = new Set(existingTags.map(tag => normalize(tag.id)).filter(Boolean));
  const seenIds = new Set<string>();
  const existingPaths = new Set(existingTags.map(tag => normalize(tag.path)));
  const seenPaths = new Set<string>();
  const existingNames = new Set(existingTags.map(tag => scopedNameKey(tag.path, tag.name)));
  const seenNames = new Set<string>();
  const existingAddresses = new Set(
    options.checkAddressCollisions
      ? existingTags.map(addressKey).filter((value): value is string => Boolean(value))
      : []
  );
  const seenAddresses = new Set<string>();

  drafts.forEach((draft, index) => {
    const id = normalize(draft.id);
    if (!id || existingIds.has(id) || seenIds.has(id)) {
      collisions.push({
        code: 'TAG_ID_COLLISION',
        field: 'id',
        index,
        message: `Generated TAG ${index + 1} must have a new unique stable ID.`
      });
    } else {
      seenIds.add(id);
    }

    const path = normalize(draft.path);
    if (!path || existingPaths.has(path) || seenPaths.has(path)) {
      collisions.push({
        code: 'TAG_PATH_COLLISION',
        field: 'path',
        index,
        message: `Generated path '${draft.path}' already exists or is duplicated in this Preview.`
      });
    } else {
      seenPaths.add(path);
    }

    const nameKey = scopedNameKey(draft.path, draft.name);
    if (!draft.name.trim() || existingNames.has(nameKey) || seenNames.has(nameKey)) {
      collisions.push({
        code: 'TAG_NAME_COLLISION',
        field: 'name',
        index,
        message: `Generated name '${draft.name}' collides in the same TAG path scope.`
      });
    } else {
      seenNames.add(nameKey);
    }

    if (options.checkAddressCollisions) {
      const key = addressKey(draft);
      if (key && (existingAddresses.has(key) || seenAddresses.has(key))) {
        collisions.push({
          code: 'TAG_ADDRESS_COLLISION',
          field: 'address',
          index,
          message: `Generated address '${draft.address ?? ''}' collides for the same Data Source.`
        });
      } else if (key) {
        seenAddresses.add(key);
      }
    }
  });

  return { canPreview: collisions.length === 0, collisions };
}

export function sourceIdentity(tag: TagSourceAwareEngineering): string {
  const stable = tag.dataSourceId?.trim();
  if (stable) return `id:${stable.toLowerCase()}`;
  const legacy = tag.source?.trim();
  return legacy ? `key:${legacy.toLowerCase()}` : 'none';
}

function validateSequenceOptions(options: TagSequenceOptions) {
  if (!Number.isInteger(options.count) || options.count < 1 || options.count > MAX_TAG_SEQUENCE_COUNT) {
    throw new Error(`Sequential TAG count must be an integer from 1 to ${MAX_TAG_SEQUENCE_COUNT}.`);
  }
  if (!Number.isInteger(options.suffixStart)) throw new Error('Numeric suffix start must be an integer.');
  if (!Number.isInteger(options.suffixStep) || options.suffixStep === 0) throw new Error('Numeric suffix step must be a non-zero integer.');
  if (!Number.isInteger(options.addressStep) || options.addressStep === 0) throw new Error('Modbus address step must be a non-zero integer.');
  if (!options.namePattern.includes('{n}')) throw new Error("Name pattern must include '{n}'.");
  if (!options.pathPattern.includes('{n}')) throw new Error("Path pattern must include '{n}'.");
}

function expandPattern(pattern: string, source: TagSourceAwareEngineering, suffix: number): string {
  return pattern
    .replaceAll('{name}', source.name)
    .replaceAll('{path}', source.path)
    .replaceAll('{n}', String(suffix));
}

function duplicatePath(path: string, ordinal: number): string {
  return ordinal === 1 ? `${path}_copy` : `${path}_copy_${ordinal}`;
}

function duplicateName(name: string, ordinal: number): string {
  return ordinal === 1 ? `${name} copy` : `${name} copy ${ordinal}`;
}

function scopedNameKey(path: string, name: string): string {
  return `${normalize(parentPath(path))}::${normalize(name)}`;
}

function parentPath(path: string): string {
  const slash = path.lastIndexOf('/');
  const dot = path.lastIndexOf('.');
  const separator = Math.max(slash, dot);
  return separator >= 0 ? path.slice(0, separator) : '';
}

function addressKey(tag: TagSourceAwareEngineering): string | null {
  const address = tag.address?.trim();
  if (!address) return null;
  return `${sourceIdentity(tag)}::${address.toLowerCase()}`;
}

function normalize(value: string | null | undefined): string {
  return value?.trim().toLowerCase() ?? '';
}

function cloneValue<T>(value: T): T {
  if (value === undefined || value === null) return value;
  return JSON.parse(JSON.stringify(value)) as T;
}
