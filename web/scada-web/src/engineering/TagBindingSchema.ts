import { loadDataSourceTypeCatalog } from './DataSourceCatalogEditor';
import {
  tagBindingSchemaIdentity,
  type DataSourceTypeDefinition,
  type TagBindingSchemaIdentity
} from './DataSourceCatalogEditor.logic';

let catalogPromise: Promise<DataSourceTypeDefinition[]> | null = null;

export async function loadTagBindingSchema(
  driverType: string
): Promise<TagBindingSchemaIdentity> {
  const types = await loadCatalog();
  const type = types.find(candidate =>
    candidate.typeKey.toLowerCase() === driverType.trim().toLowerCase());
  const identity = tagBindingSchemaIdentity(type);
  if (!type || !identity)
    throw new Error(`TAG binding schema is unavailable for Driver '${driverType}'.`);
  return identity;
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
