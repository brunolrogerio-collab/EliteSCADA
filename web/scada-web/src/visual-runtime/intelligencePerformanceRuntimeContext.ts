import { createContext, useContext } from 'react';

export type IntelligencePerformanceRuntimeContextValue = Readonly<{
  enabled: boolean;
  projectKey: string | null;
  activeRevision: number | null;
  runtimeContextId: string;
}>;

const IntelligencePerformanceRuntimeContext = createContext<IntelligencePerformanceRuntimeContextValue | null>(null);

export function useIntelligencePerformanceRuntimeContext() {
  return useContext(IntelligencePerformanceRuntimeContext);
}

export const IntelligencePerformanceRuntimeProvider = IntelligencePerformanceRuntimeContext.Provider;
