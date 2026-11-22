export type EngineeringSemanticQualifier = Readonly<{
  name: string;
  allowedValues: readonly string[];
}>;

export type EngineeringSemanticRole = Readonly<{
  code: string;
  dimension: string;
  qualifiers: readonly EngineeringSemanticQualifier[];
}>;

export type EngineeringControlledUnit = Readonly<{
  code: string;
  dimension: string;
  canonicalUnit: string;
  scaleToCanonical: number;
}>;

export type VersionedSemanticCatalog = Readonly<{
  catalogVersion: number;
  roles: readonly EngineeringSemanticRole[];
}>;

export type VersionedControlledUnitCatalog = Readonly<{
  catalogVersion: number;
  units: readonly EngineeringControlledUnit[];
}>;

export type EngineeringSemanticCatalogResponse = Readonly<{
  supportedSemanticContractVersions: readonly number[];
  semanticCatalogs: readonly VersionedSemanticCatalog[];
  controlledEngineeringUnitCatalogs: readonly VersionedControlledUnitCatalog[];
}>;

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const CATALOG_PATH = '/api/engineering/asset-process-intelligence/semantic-contracts/v2/catalog';

export async function loadEngineeringSemanticCatalog(): Promise<EngineeringSemanticCatalogResponse> {
  const response = await fetch(`${API}${CATALOG_PATH}`, {
    headers: { accept: 'application/json' },
    credentials: 'include'
  });
  if (!response.ok) throw new Error(`Semantic catalog request failed (${response.status}).`);

  const catalog = await response.json() as EngineeringSemanticCatalogResponse;
  if (!catalog.supportedSemanticContractVersions?.includes(1) ||
      !catalog.supportedSemanticContractVersions.includes(2) ||
      !catalog.semanticCatalogs?.some(item => item.catalogVersion === 1) ||
      !catalog.semanticCatalogs.some(item => item.catalogVersion === 2) ||
      !catalog.controlledEngineeringUnitCatalogs?.some(item => item.catalogVersion === 1) ||
      !catalog.controlledEngineeringUnitCatalogs.some(item => item.catalogVersion === 2)) {
    throw new Error('Semantic catalog response is incomplete or unsupported.');
  }
  return catalog;
}
