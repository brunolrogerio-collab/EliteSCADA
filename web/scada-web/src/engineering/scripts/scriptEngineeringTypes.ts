export type ScriptEngineeringScope = 'clientVisual' | 'server';

export type ScriptEngineeringEventKind =
  | 'initialize'
  | 'dispose'
  | 'objectInteraction'
  | 'tagChanged'
  | 'clientMemoryChanged'
  | 'timer'
  | 'propertyChanged'
  | 'frameTick'
  | 'serverRuntimeEvent';

export type ScriptEngineeringDependencyKind =
  | 'script'
  | 'visualDefinition'
  | 'visualObject'
  | 'tag'
  | 'clientMemoryTag'
  | 'serverMemoryTag'
  | 'resource';

export type ScriptEngineeringEntryPoint = {
  eventKind: ScriptEngineeringEventKind;
  handlerName: string;
  targetReference?: string | null;
};

export type ScriptEngineeringDependency = {
  kind: ScriptEngineeringDependencyKind;
  stableReference: string;
};

export type ScriptEngineeringDefinition = {
  id: string;
  path: string;
  name: string;
  scope: ScriptEngineeringScope;
  source: string;
  enabled: boolean;
  language: string;
  languageVersion: string;
  entryPoints: ScriptEngineeringEntryPoint[];
  dependencies: ScriptEngineeringDependency[];
  description?: string | null;
  metadata: Record<string, string>;
};

export type ScriptVisualEventReference = {
  visualDefinitionId: string;
  visualObjectId?: string | null;
  eventKind: ScriptEngineeringEventKind;
  scriptId: string;
  entryPoint: string;
  targetReference?: string | null;
};

export type ScriptEngineeringWorkspaceDescriptor = {
  projectKey?: string | null;
  projectName?: string | null;
  baseRevision?: number | null;
  isDirty: boolean;
  changeVersion: number;
};

export type ScriptImportMode = 'CreateOnly' | 'UpdateExisting';

export type ScriptImportIssue = {
  code: string;
  message: string;
  entityKind?: string | number;
  entityKey?: string;
  isError: boolean;
};

export type ScriptImportPreviewItem = {
  entityKind?: string | number;
  entityKey: string;
  operation: string | number;
  issues: ScriptImportIssue[];
};

export type ScriptImportPreview = {
  mode: string | number;
  createCount: number;
  updateCount: number;
  skipCount: number;
  errorCount: number;
  items: ScriptImportPreviewItem[];
  canApply: boolean;
};

export type ScriptImportResult = {
  mode: string | number;
  created: number;
  updated: number;
  skipped: number;
  issues: ScriptImportIssue[];
};

export type ScriptDeleteDependency = {
  entityKind: string;
  entityId: string;
  entityKey: string;
  relation: string;
};

export type ScriptDeleteResult = {
  deleted: boolean;
  entityKind: string;
  entityId: string;
  entityKey: string;
  changeVersion: number;
};

export type CanonicalScriptPackage = {
  schema: 'scada.engineering';
  schemaVersion: 10;
  exportedAt: string;
  tags: never[];
  alarms: never[];
  scripts: Array<{
    id: string;
    path: string;
    name: string;
    scope: ScriptEngineeringScope;
    source: string;
    enabled: boolean;
    language: string;
    languageVersion: string;
    entryPoints: ScriptEngineeringEntryPoint[];
    dependencies: ScriptEngineeringDependency[];
    description?: string | null;
    metadata: Record<string, string>;
  }>;
  scriptVisualEventReferences: ScriptVisualEventReference[];
};

export type ScriptMutationPreviewToken = {
  package: CanonicalScriptPackage;
  packageFingerprint: string;
  mode: ScriptImportMode;
  expectedChangeVersion: number;
  preview: ScriptImportPreview;
};

export type ScriptEngineeringContext = {
  workspace: ScriptEngineeringWorkspaceDescriptor;
  scripts: ScriptEngineeringDefinition[];
  visualEventReferences: ScriptVisualEventReference[];
};