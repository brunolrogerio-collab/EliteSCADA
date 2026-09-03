import React, { createContext, useContext } from 'react';
import type { DynamoEngineering, TagEngineering } from '../types';

export type DynamoAuthoringCatalog = Readonly<{
  definitions: readonly DynamoEngineering[];
  tags: readonly TagEngineering[];
}>;

const EMPTY_CATALOG: DynamoAuthoringCatalog = Object.freeze({
  definitions: Object.freeze([]),
  tags: Object.freeze([])
});

const DynamoAuthoringCatalogContext = createContext<DynamoAuthoringCatalog>(EMPTY_CATALOG);

export function DynamoAuthoringCatalogProvider({
  definitions,
  tags,
  children
}: DynamoAuthoringCatalog & Readonly<{ children: React.ReactNode }>) {
  const value = React.useMemo<DynamoAuthoringCatalog>(() => Object.freeze({
    definitions: Object.freeze([...definitions]),
    tags: Object.freeze([...tags])
  }), [definitions, tags]);
  return <DynamoAuthoringCatalogContext.Provider value={value}>{children}</DynamoAuthoringCatalogContext.Provider>;
}

export function useDynamoAuthoringCatalog(): DynamoAuthoringCatalog {
  return useContext(DynamoAuthoringCatalogContext);
}
