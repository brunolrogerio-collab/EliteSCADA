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
  commandCount?: number;
  visualAssetCount?: number;
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

export type TagValueSelectorEngineering = Readonly<{
  kind: 'bit' | string;
  index: number;
}>;

export type TagValueReferenceEngineering = Readonly<{
  tagId: string;
  selector?: TagValueSelectorEngineering | null;
}>;

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
  addressSelector?: TagValueSelectorEngineering | null;
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

export type GatewayEngineering = {
  id?: string;
  key: string;
  name: string;
  sourceTagId?: string | null;
  sourceTagPath?: string | null;
  destinationTagId?: string | null;
  destinationTagPath?: string | null;
  transferMode?: 'OnChange' | 'Periodic' | string;
  qualityPolicy?: 'GoodOnly' | string;
  conversionPolicy?: 'Exact' | 'CheckedNumeric' | string;
  initialTransferPolicy?: 'WaitForNextAcceptableValue' | 'SynchronizeFirstAcceptableValue' | string;
  gain?: number | null;
  offset?: number | null;
  deadband?: number | null;
  minimumIntervalMilliseconds?: number | null;
  periodMilliseconds?: number | null;
  description?: string | null;
  enabled?: boolean;
  metadata?: Record<string, string> | null;
};

export type GatewayRuntimeDiagnostic = {
  routeId: string;
  key: string;
  name: string;
  enabled: boolean;
  state: string;
  sourceTagId: string;
  sourceTagPath: string;
  sourceDataSource?: string | null;
  destinationTagId: string;
  destinationTagPath: string;
  destinationDataSource?: string | null;
  lastSourceUpdateAtUtc?: string | null;
  lastSuccessfulTransferAtUtc?: string | null;
  lastFailedTransferAtUtc?: string | null;
  transferCount: number;
  skippedTransferCount: number;
  coalescedUpdateCount: number;
  writeFailureCount: number;
  consecutiveFailures: number;
  lastError?: string | null;
  hasPendingValue: boolean;
  transferMode: string;
  effectiveIntervalMilliseconds?: number | null;
};

export type CommunicationDriverCounters = {
  cycles: number;
  requests: number;
  successfulOperations: number;
  failedOperations: number;
  consecutiveFailures: number;
  timeouts: number;
  connections: number;
  disconnections: number;
  reconnects: number;
  readOperations: number;
  writeOperations: number;
  updatesPublished: number;
};

export type CommunicationTagQualitySummary = {
  good: number;
  badCommunication: number;
  uncertain: number;
  bad: number;
  badConfiguration: number;
  badDevice: number;
  stale: number;
  disabled: number;
  noCurrentSample: number;
  total: number;
};

export type CommunicationDriverDiagnostic = {
  dataSourceKey: string;
  dataSourceName: string;
  driverType: string;
  runtimeInstanceId: string;
  endpoint?: string | null;
  state: string | number;
  stateChangedAt: string;
  capturedAt: string;
  lastSuccessfulCommunicationAt?: string | null;
  lastFailedCommunicationAt?: string | null;
  lastError?: string | null;
  dataAge?: string | null;
  configuredScanInterval?: string | null;
  lastOperationDuration?: string | null;
  averageOperationDuration?: string | null;
  lastScanDuration?: string | null;
  recentFailureRate: number;
  associatedTagCount: number;
  tagQuality: CommunicationTagQualitySummary;
  counters: CommunicationDriverCounters;
  protocolDetails?: Record<string, string> | null;
};

export type RuntimeDiagnosticsView = {
  runtime?: {
    communicationDrivers?: CommunicationDriverDiagnostic[];
  };
};

/**
 * In a visual element, key is the destination visual-property/slot key. Target
 * remains friendly/portable authoring text; concrete TAG bindings may also carry
 * tagReference as the canonical stable identity, including a typed bit selector.
 */
export type BindingEngineering = {
  key: string;
  kind: string;
  target: string;
  direction?: string | null;
  metadata?: Record<string, string> | null;
  tagReference?: TagValueReferenceEngineering | null;
};

export type VisualExpressionValueTypeEngineering = 'Boolean' | 'Number';
export type VisualExpressionDependencyKindEngineering = 'Tag' | 'ClientMemory';
export type VisualValueSourceKindEngineering = 'Tag' | 'ClientMemory' | 'Expression';

export type VisualExpressionDependencyEngineering = Readonly<{
  symbol: string;
  kind: VisualExpressionDependencyKindEngineering;
  valueType: VisualExpressionValueTypeEngineering;
  tagReference: TagValueReferenceEngineering;
  target?: string | null;
  version?: number;
}>;

export type VisualExpressionEngineering = Readonly<{
  text: string;
  resultType: VisualExpressionValueTypeEngineering;
  dependencies?: readonly VisualExpressionDependencyEngineering[] | null;
  version?: number;
}>;

export type VisualValueSourceEngineering = Readonly<{
  kind: VisualValueSourceKindEngineering;
  valueType: VisualExpressionValueTypeEngineering;
  target?: string | null;
  tagReference?: TagValueReferenceEngineering | null;
  expression?: VisualExpressionEngineering | null;
  version?: number;
}>;

export type VisualPropertyExpressionEngineering = Readonly<{
  propertyKey: string;
  expression: VisualExpressionEngineering;
  version?: number;
}>;

export type VisualBooleanConditionKindEngineering = 'Direct' | 'NumericInterval';
export type VisualNumericIntervalModeEngineering = 'Inside' | 'Outside';

export type VisualBooleanConditionEngineering = Readonly<{
  propertyKey: string;
  kind: VisualBooleanConditionKindEngineering;
  source: VisualValueSourceEngineering;
  negate?: boolean;
  minimum?: number | null;
  minimumInclusive?: boolean;
  maximum?: number | null;
  maximumInclusive?: boolean;
  intervalMode?: VisualNumericIntervalModeEngineering;
  version?: number;
}>;

export type VisualAnalogFillDirectionEngineering =
  | 'BottomToTop'
  | 'TopToBottom'
  | 'LeftToRight'
  | 'RightToLeft';

export type VisualAnalogFillEngineering = Readonly<{
  source: VisualValueSourceEngineering;
  inputMinimum: number;
  inputMaximum: number;
  fillColor: string;
  clamp?: boolean;
  invertScale?: boolean;
  direction?: VisualAnalogFillDirectionEngineering;
  version?: number;
}>;

export type VisualEngineeringAssetReference = Readonly<{
  assetId: string;
}>;

export type VisualAssetEngineering = {
  id?: string | null;
  key: string;
  name: string;
  originalFileName: string;
  mediaType: 'image/png' | 'image/jpeg' | 'image/bmp' | string;
  byteLength: number;
  sha256: string;
  pixelWidth?: number | null;
  pixelHeight?: number | null;
  description?: string | null;
  metadata?: Record<string, string> | null;
};

/**
 * Canonical Engineering visual properties are JSON-native. The shared scalar
 * Visual Property Registry still validates its declared public properties, while
 * object-specific structural payloads such as core.polygon points remain typed
 * by the owning visual-object contract.
 */
export interface VisualEngineeringPropertyObject {
  readonly [key: string]: VisualEngineeringPropertyValue;
}

export type VisualEngineeringPropertyValue =
  | number
  | boolean
  | string
  | null
  | VisualEngineeringAssetReference
  | readonly VisualEngineeringPropertyValue[]
  | VisualEngineeringPropertyObject;

export type VisualEngineeringPropertyMap = Record<string, VisualEngineeringPropertyValue>;

export type VisualElementEngineering = {
  id?: string | null;
  key: string;
  type: string;
  dynamoKey?: string | null;
  equipmentPath?: string | null;
  bindings?: BindingEngineering[] | null;
  properties?: VisualEngineeringPropertyMap | null;
  context?: Record<string, string> | null;
  children?: VisualElementEngineering[] | null;
  metadata?: Record<string, string> | null;
  propertyExpressions?: readonly VisualPropertyExpressionEngineering[] | null;
  booleanConditions?: readonly VisualBooleanConditionEngineering[] | null;
  analogFill?: VisualAnalogFillEngineering | null;
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
  route?: string | null;
  elements?: VisualElementEngineering[] | null;
  properties?: Record<string, string> | null;
  context?: Record<string, string> | null;
  metadata?: Record<string, string> | null;
};

export type PopupEngineering = {
  id?: string;
  key: string;
  name: string;
  templateKey?: string | null;
  elements?: VisualElementEngineering[] | null;
  properties?: Record<string, string> | null;
  context?: Record<string, string> | null;
  metadata?: Record<string, string> | null;
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
  gateways?: GatewayEngineering[];
  visualAssets?: VisualAssetEngineering[];
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
