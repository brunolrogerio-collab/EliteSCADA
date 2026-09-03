import { loadDataSourceTypeCatalog } from './DataSourceCatalogEditor';
import {
  tagBindingSchemaIdentity,
  type DataSourceConfigurationField,
  type DataSourceTypeDefinition,
  type TagBindingSchemaIdentity
} from './DataSourceCatalogEditor.logic';

export type TagBindingDefinition = Readonly<{
  type: DataSourceTypeDefinition;
  identity: TagBindingSchemaIdentity;
  fields: readonly DataSourceConfigurationField[];
}>;

let catalogPromise: Promise<DataSourceTypeDefinition[]> | null = null;

export async function loadTagBindingDefinition(
  driverType: string
): Promise<TagBindingDefinition> {
  const types = await loadCatalog();
  const type = types.find(candidate =>
    candidate.typeKey.toLowerCase() === driverType.trim().toLowerCase());
  const identity = tagBindingSchemaIdentity(type);
  const configurationSchema = type?.configurationSchema;
  if (!type || !identity || !configurationSchema)
    throw new Error(`TAG binding schema is unavailable for Driver '${driverType}'.`);

  return {
    type,
    identity,
    fields: configurationSchema.tagBindingFields
  };
}

export async function loadTagBindingSchema(
  driverType: string
): Promise<TagBindingSchemaIdentity> {
  return (await loadTagBindingDefinition(driverType)).identity;
}

export function requireTagBindingField(
  definition: TagBindingDefinition,
  key: string
): DataSourceConfigurationField {
  const field = definition.fields.find(candidate =>
    candidate.key.toLowerCase() === key.trim().toLowerCase());
  if (!field)
    throw new Error(`TAG binding field '${key}' is unavailable for Driver '${definition.type.typeKey}'.`);
  return field;
}

export function requireAllowedTagBindingValue(
  definition: TagBindingDefinition,
  key: string,
  value: string
): DataSourceConfigurationField {
  const field = requireTagBindingField(definition, key);
  if (field.allowedValues.length > 0 && !field.allowedValues.includes(value)) {
    throw new Error(
      `TAG binding value '${value}' is not allowed for field '${field.key}' of Driver '${definition.type.typeKey}'.`);
  }
  return field;
}

export function resetTagBindingSchemaCatalogForTests(): void {
  catalogPromise = null;
}

function loadCatalog(): Promise<DataSourceTypeDefinition[]> {
  catalogPromise ??= loadDataSourceTypeCatalog().catch(reason => {
    catalogPromise = null;
    throw reason;
  });
  return catalogPromise;
}
