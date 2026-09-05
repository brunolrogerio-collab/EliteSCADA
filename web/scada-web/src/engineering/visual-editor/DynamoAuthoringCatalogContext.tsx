import React, { createContext, useContext } from 'react';
import type { DynamoEngineering, TagEngineering, VisualAssetEngineering } from '../types';
import { normalizeDynamoDefinitionParameterContract } from '../../runtime/visual-navigation/dynamoParameterWireContract';

export type DynamoAuthoringCatalog = Readonly<{
  definitions: readonly DynamoEngineering[];
  tags: readonly TagEngineering[];
  visualAssets: readonly VisualAssetEngineering[];
}>;

const EMPTY_CATALOG: DynamoAuthoringCatalog = Object.freeze({
  definitions: Object.freeze([]),
  tags: Object.freeze([]),
  visualAssets: Object.freeze([])
});

const DynamoAuthoringCatalogContext = createContext<DynamoAuthoringCatalog>(EMPTY_CATALOG);

export function DynamoAuthoringCatalogProvider({
  definitions,
  tags,
  visualAssets,
  children
}: DynamoAuthoringCatalog & Readonly<{ children: React.ReactNode }>) {
  const value = React.useMemo<DynamoAuthoringCatalog>(() => Object.freeze({
    definitions: Object.freeze(definitions.map(normalizeDynamoDefinitionParameterContract)),
    tags: Object.freeze([...tags]),
    visualAssets: Object.freeze([...visualAssets])
  }), [definitions, tags, visualAssets]);
  return <DynamoAuthoringCatalogContext.Provider value={value}>{children}</DynamoAuthoringCatalogContext.Provider>;
}

export function useDynamoAuthoringCatalog(): DynamoAuthoringCatalog {
  return useContext(DynamoAuthoringCatalogContext);
}
