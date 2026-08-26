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

export type TagEngineering = {
  id?: string;
  name: string;
  path: string;
  dataType: string;
  source?: string;
  address?: string;
  engineeringUnit?: string;
  description?: string;
  readOnly: boolean;
  historian?: {
    enabled?: boolean;
    strategy?: string;
    deadband?: number;
    periodMilliseconds?: number;
    maximumPeriodMilliseconds?: number;
  };
};

export type AlarmEngineering = {
  id?: string;
  name: string;
  tagId?: string;
  tagPath?: string;
  type: string;
  priority: string;
  area?: string;
  enabled?: boolean;
};

export type DataSourceEngineering = {
  id?: string;
  key: string;
  name: string;
  driver: string;
  enabled?: boolean;
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
};

export type EngineeringSnapshot = {
  workspace: EngineeringWorkspaceDescriptor;
  package: EngineeringPackageView;
};
