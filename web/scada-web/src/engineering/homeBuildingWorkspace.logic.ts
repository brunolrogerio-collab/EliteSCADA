import { tagBindingSchemaIdentity, type DataSourceTypeDefinition } from './DataSourceCatalogEditor.logic';
import type {
  CommandEngineering,
  DataSourceEngineering,
  EngineeringPackageView,
  EquipmentCapabilityEngineering,
  EquipmentEngineering,
  EquipmentSourceBindingEngineering,
  TagEngineering
} from './types';
import type { DriverDiscoveryCandidateView, DriverMaterializationTagCandidateView } from './driverEngineeringApi';

type TagBindingField = NonNullable<DataSourceTypeDefinition['configurationSchema']>['tagBindingFields'][number];

export type MaterializationBuildOptions = Readonly<{
  locationId?: string | null;
  equipmentName?: string;
  equipmentPath?: string;
  idFactory?: () => string;
}>;

export function buildMaterializationPackage(
  sourcePackage: EngineeringPackageView,
  source: DataSourceEngineering,
  definition: DataSourceTypeDefinition,
  candidate: DriverDiscoveryCandidateView,
  options: MaterializationBuildOptions = {}
): EngineeringPackageView {
  if (!source.id) throw new Error('Salve a integração antes de importar dispositivos.');
  const materialization = candidate.materialization;
  if (!materialization) throw new Error('Este resultado não contém uma proposta de dispositivo para aplicar.');
  const schema = definition.configurationSchema;
  const bindingSchema = tagBindingSchemaIdentity(definition);
  if (!schema || !bindingSchema) throw new Error('O driver não expõe um contrato de binding de TAG compatível.');
  const stableDeviceIdentity = materialization.equipment.stableDeviceIdentity.trim();
  if (!stableDeviceIdentity) throw new Error('O dispositivo descoberto não tem uma identidade estável.');

  const idFactory = options.idFactory ?? createGuid;
  const packageCandidate = structuredClone(sourcePackage);
  const tags = [...packageCandidate.tags];
  const commands = [...(packageCandidate.commands ?? [])];
  const equipmentItems = [...(packageCandidate.equipment ?? [])];
  const existingEquipment = equipmentItems.find(item =>
    item.sourceBindings?.some(binding =>
      binding.dataSourceId.toLowerCase() === source.id!.toLowerCase() &&
      binding.stableDeviceIdentity === stableDeviceIdentity));

  const canonicalTags = new Map<string, TagEngineering>();
  for (const tagCandidate of materialization.tags ?? []) {
    if (!tagCandidate.candidateId.trim()) throw new Error('O driver retornou uma TAG sem identidade estável de candidato.');
    const portableAddress = tagCandidate.portableAddress?.trim();
    if (!portableAddress) throw new Error(`O dispositivo '${materialization.equipment.name}' tem a TAG '${tagCandidate.name}' sem endereço portável.`);
    if (!tagCandidate.path.trim() || !tagCandidate.name.trim()) throw new Error('O driver retornou uma TAG sem nome ou caminho canônico.');

    const previous = tags.find(tag =>
      tag.dataSourceId?.toLowerCase() === source.id!.toLowerCase() &&
      tag.metadata?.['homeBuilding.dataSourceId']?.toLowerCase() === source.id!.toLowerCase() &&
      tag.metadata?.['homeBuilding.stableDeviceIdentity'] === stableDeviceIdentity &&
      tag.metadata?.['homeBuilding.candidateId'] === tagCandidate.candidateId);
    const id = previous?.id ?? idFactory();
    const settings = bindingSettings(tagCandidate, schema.tagBindingFields, previous, portableAddress, bindingSchema.schemaId);
    const tag: TagEngineering = {
      ...(previous ?? {}),
      id,
      name: tagCandidate.name,
      path: tagCandidate.path,
      dataType: normalizeDataType(tagCandidate.dataType),
      source: source.key,
      dataSourceId: source.id,
      address: portableAddress,
      communicationBinding: {
        contractVersion: 1,
        schemaId: bindingSchema.schemaId,
        schemaVersion: bindingSchema.schemaVersion,
        portableAddress,
        settings
      },
      engineeringUnit: tagCandidate.engineeringUnit ?? previous?.engineeringUnit ?? null,
      readOnly: tagCandidate.readOnly,
      metadata: {
        ...(previous?.metadata ?? {}),
        ...(tagCandidate.metadata ?? {}),
        'homeBuilding.candidateId': tagCandidate.candidateId,
        'homeBuilding.dataSourceId': source.id,
        'homeBuilding.stableDeviceIdentity': stableDeviceIdentity
      }
    };
    const previousIndex = tags.findIndex(item => item.id === previous?.id);
    if (previousIndex >= 0) tags[previousIndex] = tag;
    else tags.push(tag);
    canonicalTags.set(tagCandidate.candidateId, tag);
  }

  const canonicalCommands = new Map<string, CommandEngineering>();
  for (const commandCandidate of materialization.commands ?? []) {
    if (!commandCandidate.candidateId.trim() || !commandCandidate.key.trim() || !commandCandidate.name.trim()) throw new Error('O driver retornou um comando sem identidade, identificador ou nome.');
    const targetTag = commandCandidate.targetTagCandidateId
      ? canonicalTags.get(commandCandidate.targetTagCandidateId)
      : undefined;
    if (commandCandidate.targetTagCandidateId && !targetTag)
      throw new Error(`O comando '${commandCandidate.name}' referencia uma TAG que não veio no candidato.`);
    const previous = commands.find(command =>
      command.metadata?.['homeBuilding.dataSourceId']?.toLowerCase() === source.id!.toLowerCase() &&
      command.metadata?.['homeBuilding.stableDeviceIdentity'] === stableDeviceIdentity &&
      command.metadata?.['homeBuilding.candidateId'] === commandCandidate.candidateId);
    const key = uniqueMaterializedCommandKey(
      commandCandidate.key,
      commands,
      previous?.id,
      `${source.id}\u0000${stableDeviceIdentity}\u0000${commandCandidate.candidateId}`
    );
    const id = previous?.id ?? idFactory();
    const command: CommandEngineering = {
      ...(previous ?? {}),
      id,
      key,
      name: commandCandidate.name,
      kind: 'WriteTagValue',
      value: commandCandidate.value,
      targetTagId: targetTag?.id ?? null,
      targetTagPath: targetTag?.path ?? null,
      equipmentPath: options.equipmentPath?.trim() || materialization.equipment.path,
      enabled: true,
      metadata: {
        ...(previous?.metadata ?? {}),
        ...(commandCandidate.metadata ?? {}),
        'homeBuilding.candidateId': commandCandidate.candidateId,
        'homeBuilding.dataSourceId': source.id,
        'homeBuilding.stableDeviceIdentity': stableDeviceIdentity
      }
    };
    const previousIndex = commands.findIndex(item => item.id === previous?.id);
    if (previousIndex >= 0) commands[previousIndex] = command;
    else commands.push(command);
    canonicalCommands.set(commandCandidate.candidateId, command);
  }

  const capabilities: EquipmentCapabilityEngineering[] = (materialization.equipment.capabilities ?? []).map(capability => ({
    id: capability.capabilityId,
    kind: capability.kind,
    version: capability.version || 1,
    metadata: { ...(capability.metadata ?? {}) },
    bindings: (capability.bindings ?? []).map(binding => {
      const tag = binding.tagCandidateId ? canonicalTags.get(binding.tagCandidateId) : undefined;
      const command = binding.commandCandidateId ? canonicalCommands.get(binding.commandCandidateId) : undefined;
      if (binding.tagCandidateId && !tag) throw new Error(`A capability '${capability.kind}' referencia uma TAG ausente no candidato.`);
      if (binding.commandCandidateId && !command) throw new Error(`A capability '${capability.kind}' referencia um comando ausente no candidato.`);
      return {
        role: binding.role,
        ...(tag?.id ? { tagId: tag.id } : {}),
        ...(command?.id ? { commandId: command.id } : {})
      };
    })
  }));

  const existingBindings: EquipmentSourceBindingEngineering[] = (existingEquipment?.sourceBindings ?? [])
    .filter(binding => !(binding.dataSourceId.toLowerCase() === source.id!.toLowerCase() &&
      binding.stableDeviceIdentity === materialization.equipment.stableDeviceIdentity));
  const equipment: EquipmentEngineering = {
    ...(existingEquipment ?? {}),
    id: existingEquipment?.id ?? idFactory(),
    path: options.equipmentPath?.trim() || materialization.equipment.path,
    name: options.equipmentName?.trim() || materialization.equipment.name,
    locationId: Object.prototype.hasOwnProperty.call(options, 'locationId')
      ? options.locationId ?? null
      : existingEquipment?.locationId ?? null,
    sourceBindings: [
      ...existingBindings,
      {
        dataSourceId: source.id,
        stableDeviceIdentity,
        role: materialization.equipment.sourceRole ?? 'primary'
      }
    ],
    capabilities: mergeCapabilities(existingEquipment?.capabilities ?? [], capabilities),
    metadata: {
      ...(existingEquipment?.metadata ?? {}),
      ...(materialization.equipment.metadata ?? {}),
      'homeBuilding.driverCandidateId': materialization.equipment.candidateId
    }
  };
  const equipmentIndex = equipmentItems.findIndex(item => item.id === existingEquipment?.id);
  if (equipmentIndex >= 0) equipmentItems[equipmentIndex] = equipment;
  else equipmentItems.push(equipment);

  packageCandidate.tags = tags;
  packageCandidate.commands = commands;
  packageCandidate.equipment = equipmentItems;
  return packageCandidate;
}

function uniqueMaterializedCommandKey(
  baseKey: string,
  commands: readonly CommandEngineering[],
  currentId: string | null | undefined,
  identity: string
): string {
  const isTaken = (key: string) => commands.some(command =>
    command.id !== currentId && command.key.localeCompare(key, undefined, { sensitivity: 'accent' }) === 0);
  if (!isTaken(baseKey)) return baseKey;

  const stem = `${baseKey}~hb-${stableFingerprint(identity)}`;
  if (!isTaken(stem)) return stem;
  let suffix = 2;
  while (isTaken(`${stem}-${suffix}`)) suffix++;
  return `${stem}-${suffix}`;
}

function stableFingerprint(value: string): string {
  let hash = 2166136261;
  for (const character of value) {
    hash ^= character.codePointAt(0) ?? 0;
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0).toString(16).padStart(8, '0');
}

export function buildLocationPackage(
  sourcePackage: EngineeringPackageView,
  location: { id?: string | null; name: string; kind: string; parentLocationId?: string | null },
  idFactory: () => string = createGuid
): EngineeringPackageView {
  const name = location.name.trim();
  if (!name) throw new Error('Informe o nome do local.');
  const items = [...(sourcePackage.locations ?? [])];
  const current = location.id ? items.find(item => item.id === location.id) : undefined;
  const next = {
    ...(current ?? {}),
    id: current?.id ?? location.id ?? idFactory(),
    name,
    kind: location.kind,
    parentLocationId: location.parentLocationId || null
  };
  const index = items.findIndex(item => item.id === current?.id);
  if (index >= 0) items[index] = next;
  else items.push(next);
  return { ...structuredClone(sourcePackage), locations: items };
}

function bindingSettings(
  candidate: DriverMaterializationTagCandidateView,
  fields: readonly TagBindingField[],
  existing: TagEngineering | undefined,
  portableAddress: string,
  schemaId: string
): Record<string, string> {
  const prior = existing?.communicationBinding?.schemaId === schemaId
    ? existing.communicationBinding.settings ?? {}
    : {};
  const settings = { ...prior };
  for (const field of fields) {
    const fromCandidate = Object.entries(candidate.metadata ?? {})
      .find(([key]) => key.toLocaleLowerCase() === field.key.toLocaleLowerCase())?.[1];
    const value = field.key.toLocaleLowerCase() === 'address'
      ? portableAddress
      : fromCandidate ?? field.defaultValue ?? settings[field.key];
    if (value !== undefined && value !== null && value !== '') settings[field.key] = value;
    if (field.required && !settings[field.key])
      throw new Error(`A TAG '${candidate.name}' exige a configuração '${field.key}', que o candidato não forneceu.`);
  }
  return settings;
}

function mergeCapabilities(
  previous: readonly EquipmentCapabilityEngineering[] | null | undefined,
  incoming: readonly EquipmentCapabilityEngineering[]
): EquipmentCapabilityEngineering[] {
  const merged = new Map((previous ?? []).map(item => [item.id.toLocaleLowerCase(), item]));
  for (const item of incoming) {
    const existing = merged.get(item.id.toLocaleLowerCase());
    merged.set(item.id.toLocaleLowerCase(), {
      ...existing,
      ...item,
      metadata: { ...(existing?.metadata ?? {}), ...(item.metadata ?? {}) }
    });
  }
  return [...merged.values()];
}

function normalizeDataType(value: string | number): string {
  const numeric = ['boolean', 'int16', 'int32', 'int64', 'float', 'double', 'string', 'dateTime', 'enum'];
  if (typeof value === 'number') return numeric[value] ?? 'string';
  const normalized = value.trim().toLowerCase();
  const values: Record<string, string> = {
    boolean: 'boolean', int16: 'int16', int32: 'int32', int64: 'int64',
    float: 'float', double: 'double', string: 'string', datetime: 'dateTime', enum: 'enum'
  };
  return values[normalized] ?? 'string';
}

function createGuid(): string {
  if (globalThis.crypto?.randomUUID) return globalThis.crypto.randomUUID();
  const bytes = new Uint8Array(16);
  globalThis.crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = [...bytes].map(value => value.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
