export type EngineeringWorkspaceDescriptor = {
  projectKey?: string | null;
  projectName?: string | null;
  baseRevision?: number | null;
  checkedOutAtUtc?: string | null;
  lastSavedAtUtc?: string | null;
  isDirty: boolean;
  changeVersion: number;
  tagCount: number;
  alarmCount: number;
  dataSourceCount: number;
  templateCount: number;
  equipmentCount: number;
  dynamoCount: number;
  screenCount: number;
  popupCount: number;
  securityRoleCount?: number;
};

export type HistorianEngineering = {
  enabled?: boolean;
  strategy?: string;
  deadband?: number | null;
  periodMilliseconds?: number | null;
  maximumPeriodMilliseconds?: number | null;
};

export type TagAccessPolicyEngineering = {
  readRoles?: string[] | null;
  writeRoles?: string[] | null;
  configureRoles?: string[] | null;
};

export type MemoryInitialValueEngineering = {
  dataType: string;
  value: unknown;
};

export type TagEngineering = {
  id?: string;
  name: string;
  path: string;
  dataType: string;
  source?: string | null;
  address?: string | null;
  engineeringUnit?: string | null;
  description?: string | null;
  readOnly: boolean;
  scaleMinimum?: number | null;
  scaleMaximum?: number | null;
  historian?: HistorianEngineering | null;
  metadata?: Record<string, string> | null;
  accessPolicy?: TagAccessPolicyEngineering | null;
  initialValue?: MemoryInitialValueEngineering | null;
};

export type AlarmEngineering = {
  id?: string;
  name: string;
  tagId?: string | null;
  tagPath?: string | null;
  type: string;
  priority: string;
  setpoint?: number | null;
  digitalActiveValue?: boolean;
  alarmClass?: string | null;
  area?: string | null;
  message?: string | null;
  activationDelayMilliseconds?: number | null;
  requiresAcknowledgement?: boolean;
  shelvingAllowed?: boolean;
  enabled?: boolean;
  metadata?: Record<string, string> | null;
};

export type DataSourceEngineering = {
  id?: string;
  key: string;
  name: string;
  driver: string;
  enabled?: boolean;
  settings?: Record<string, string> | null;
  secretReferences?: Record<string, string> | null;
  metadata?: Record<string, string> | null;
};

export type BindingEngineering = {
  key: string;
  kind: string;
  target: string;
  direction?: string;
};

export type TemplateEngineering = {
  id?: string;
  key: string;
  name: string;
  bindings?: BindingEngineering[];
};

export type EquipmentEngineering = {
  id?: string;
  path: string;
  name: string;
  templateKey?: string;
  bindings?: BindingEngineering[];
};

export type DynamoEngineering = {
  id?: string;
  key: string;
  name: string;
  templateKey?: string;
  bindings?: BindingEngineering[];
};

export type ScreenEngineering = {
  id?: string;
  key: string;
  name: string;
  route?: string;
  elements?: unknown[];
};

export type PopupEngineering = {
  id?: string;
  key: string;
  name: string;
  templateKey?: string;
  elements?: unknown[];
};

export type SecurityRoleEngineering = {
  id?: string;
  key: string;
  name: string;
  description?: string;
  grants?: Array<{ capability: string; scope?: Record<string, string> }>;
};

export type EngineeringPackageView = {
  schema: string;
  schemaVersion: number;
  exportedAt: string;
  tags: TagEngineering[];
  alarms: AlarmEngineering[];
  dataSources?: DataSourceEngineering[];
  templates?: TemplateEngineering[];
  equipment?: EquipmentEngineering[];
  dynamos?: DynamoEngineering[];
  screens?: ScreenEngineering[];
  popups?: PopupEngineering[];
  securityRoles?: SecurityRoleEngineering[];
  [key: string]: unknown;
};

export type EngineeringSnapshot = {
  workspace: EngineeringWorkspaceDescriptor;
  package: EngineeringPackageView;
};

export type ImportIssueView = {
  code: string;
  message: string;
  entityKind: string;
  entityKey: string;
  isError: boolean;
};

export type ImportPreviewItemView = {
  entityKind: string;
  entityKey: string;
  operation: string;
  issues: ImportIssueView[];
};

export type ImportPreviewView = {
  mode: string;
  createCount: number;
  updateCount: number;
  skipCount: number;
  errorCount: number;
  items: ImportPreviewItemView[];
  canApply: boolean;
};

export type ImportResultView = {
  mode: string;
  created: number;
  updated: number;
  skipped: number;
  issues: ImportIssueView[];
};
