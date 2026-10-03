import React, { createContext, useContext } from 'react';
import type { CommandEngineering, DynamoEngineering, TagEngineering, VisualAssetEngineering } from '../types';
import { normalizeDynamoDefinitionParameterContract } from '../../runtime/visual-navigation/dynamoParameterWireContract';

export type DynamoAuthoringCatalog = Readonly<{
  definitions: readonly DynamoEngineering[];
  tags: readonly TagEngineering[];
  commands: readonly CommandEngineering[];
  visualAssets: readonly VisualAssetEngineering[];
}>;

const EMPTY_CATALOG: DynamoAuthoringCatalog = Object.freeze({
  definitions: Object.freeze([]),
  tags: Object.freeze([]),
  commands: Object.freeze([]),
  visualAssets: Object.freeze([])
});

const DynamoAuthoringCatalogContext = createContext<DynamoAuthoringCatalog>(EMPTY_CATALOG);

export function DynamoAuthoringCatalogProvider({
  definitions,
  tags,
  commands,
  visualAssets,
  children
}: DynamoAuthoringCatalog & Readonly<{ children: React.ReactNode }>) {
  const value = React.useMemo<DynamoAuthoringCatalog>(() => Object.freeze({
    definitions: Object.freeze(definitions.map(normalizeDynamoDefinitionParameterContract)),
    tags: Object.freeze([...tags]),
    commands: Object.freeze([...commands]),
    visualAssets: Object.freeze([...visualAssets])
  }), [definitions, tags, commands, visualAssets]);
  return <DynamoAuthoringCatalogContext.Provider value={value}>{children}</DynamoAuthoringCatalogContext.Provider>;
}

export function useDynamoAuthoringCatalog(): DynamoAuthoringCatalog {
  return useContext(DynamoAuthoringCatalogContext);
}
