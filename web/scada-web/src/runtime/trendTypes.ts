import type { RuntimeTagEndpointIssue, RuntimeTagHistorySample, RuntimeTagListItem } from './tagInspectorTypes';

export type BasicTrendLocale = 'pt-BR' | 'en' | 'es';
export type BasicTrendMode = 'live' | 'historical';
export type BasicTrendWindow = '15m' | '1h' | '6h' | '24h';
export type BasicTrendQualityTone = 'good' | 'attention' | 'bad' | 'unknown';

export type BasicTrendRange = {
  from: string;
  to: string;
  window: BasicTrendWindow;
};

export type BasicTrendSummary = {
  total: number;
  good: number;
  attention: number;
  bad: number;
  unknown: number;
  numericCount: number;
  minimum: number | null;
  maximum: number | null;
  latestValue: unknown;
  latestTimestamp: string | null;
};

export type BasicTrendPlotPoint = {
  x: number;
  y: number;
  value: number;
  timestamp: string;
  qualityTone: BasicTrendQualityTone;
};

export type BasicTrendPlot = {
  points: BasicTrendPlotPoint[];
  minimum: number;
  maximum: number;
};

export type BasicTrendState = {
  tag: RuntimeTagListItem | null;
  range: BasicTrendRange | null;
  samples: RuntimeTagHistorySample[];
  issue: RuntimeTagEndpointIssue | null;
};

export type { RuntimeTagEndpointIssue, RuntimeTagHistorySample, RuntimeTagListItem };
