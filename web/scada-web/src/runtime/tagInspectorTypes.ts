export type RuntimeTagInspectorLocale = 'pt-BR' | 'en' | 'es';

export type RuntimeTagQualityName =
  | 'good'
  | 'uncertain'
  | 'bad'
  | 'bad-communication'
  | 'bad-configuration'
  | 'bad-device'
  | 'stale'
  | 'disabled'
  | 'unknown';

export type RuntimeTagQualityBucket = 'good' | 'attention' | 'bad' | 'no-sample';
export type RuntimeTagQualityFilter = 'all' | RuntimeTagQualityBucket;
export type RuntimeTagAccessFilter = 'all' | 'read-only' | 'writable';

export type RuntimeTagCurrentValue = {
  tagId: string;
  value: unknown;
  timestamp: string;
  quality: string | number;
  source?: string | null;
  sourceTimestamp?: string | null;
  serverTimestamp?: string | null;
};

export type RuntimeTagListItem = {
  id: string;
  name: string;
  path: string;
  dataType: string;
  engineeringUnit?: string | null;
  description?: string | null;
  readOnly: boolean;
  current?: RuntimeTagCurrentValue | null;
};

export type RuntimeTagDefinitionDetail = {
  id: string;
  name: string;
  path: string;
  dataType: string | number;
  source?: string | null;
  engineeringUnit?: string | null;
  description?: string | null;
  readOnly: boolean;
  metadata?: Record<string, string> | null;
};

export type RuntimeTagDetailResponse = {
  tag: RuntimeTagDefinitionDetail;
  current?: RuntimeTagCurrentValue | null;
};

export type RuntimeTagHistorySample = RuntimeTagCurrentValue;

export type RuntimeTagRealtimeEvent = {
  type: string;
  tag: {
    id: string;
    name: string;
    path: string;
    engineeringUnit?: string | null;
  };
  value: unknown;
  quality: string | number;
  timestamp: string;
  source?: string | null;
};

export type RuntimeTagEndpointIssue = 'unauthenticated' | 'forbidden' | 'not-found' | 'unavailable';

export type RuntimeTagInspectorFilter = {
  query: string;
  quality: RuntimeTagQualityFilter;
  access: RuntimeTagAccessFilter;
};

export type RuntimeTagInspectorSummary = {
  total: number;
  good: number;
  attention: number;
  bad: number;
  noSample: number;
  readOnly: number;
  writable: number;
};
