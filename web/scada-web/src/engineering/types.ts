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

export type HistorianCaptureStrategy =
  | 'periodic'
  | 'onChange'
  | 'onChangeDeadband'
  | 'onChangeDeadbandMaxInterval';

export type HistorianCaptureProfileEngineering = Readonly<{
  id?: string | null;
  key: string;
  name: string;
  strategy: HistorianCaptureStrategy;
  periodMilliseconds?: number | null;
  deadband?: number | null;
  maximumIntervalMilliseconds?: number | null;
  description?: string | null;
  metadata?: Record<string, string> | null;
  version?: number;
}>;

export type HistorianRetrievalMode =
  | 'raw'
  | 'last'
  | 'atOrBefore'
  | 'atOrAfter'
  | 'exact'
  | 'interpolated'
  | 'sampledFixedStep'
  | 'aggregate';

export type DataQueryAggregateFunction =
  | 'count'
  | 'sum'
  | 'average'
  | 'minimum'
  | 'maximum'
  | 'first'
  | 'last';

export type DataQueryParameterType =
  | 'string'
  | 'boolean'
  | 'number'
  | 'int64'
  | 'dateTime'
  | 'durationSeconds'
  | 'guid'
  | 'enum';

export type DataQueryParameterTarget =
  | 'absoluteFromUtc'
  | 'absoluteToUtc'
  | 'relativeDurationSeconds'
  | 'historianTargetUtc'
  | 'search'
  | 'filterValue';

export type DataQueryParameterValueEngineering = Readonly<{
  type: DataQueryParameterType;
  value: string;
}>;

export type DataQueryParameterEngineering = Readonly<{
  key: string;
  name: string;
  type: DataQueryParameterType;
  defaultValue?: DataQueryParameterValueEngineering | null;
  description?: string | null;
  allowedValues?: readonly DataQueryParameterValueEngineering[] | null;
}>;

export type DataQueryParameterBindingEngineering = Readonly<{
  parameterKey: string;
  target: DataQueryParameterTarget;
  filterIndex?: number | null;
  valueIndex?: number | null;
}>;

export type DataQueryGroupEngineering = Readonly<{ field: string }>;

export type DataQueryAggregateEngineering = Readonly<{
  key: string;
  field: string;
  function: DataQueryAggregateFunction;
}>;

export type HistorianRetrievalEngineering = Readonly<{
  mode: HistorianRetrievalMode;
  stepMilliseconds?: number | null;
  bucketMilliseconds?: number | null;
  maximumGapMilliseconds?: number | null;
  aggregateFunction?: DataQueryAggregateFunction | null;
  targetUtc?: string | null;
}>;

export type DataQueryEngineering = Readonly<{
  id?: string | null;
  key: string;
  name: string;
  providerKey: string;
  query: import('../runtime/historical-browser/historicalQueryApi').HistoricalQueryRequest;
  selectedFields?: readonly string[] | null;
  groups?: readonly DataQueryGroupEngineering[] | null;
  aggregates?: readonly DataQueryAggregateEngineering[] | null;
  parameters?: readonly DataQueryParameterEngineering[] | null;
  parameterBindings?: readonly DataQueryParameterBindingEngineering[] | null;
  historianRetrieval?: HistorianRetrievalEngineering | null;
  description?: string | null;
  metadata?: Record<string, string> | null;
  version?: number;
}>;

export type AlarmViewMatchState = 'any' | 'yes' | 'no';

export type AlarmViewFilterEngineering = Readonly<{
  areas?: readonly string[] | null;
  priorities?: readonly string[] | null;
  types?: readonly string[] | null;
  alarmClasses?: readonly string[] | null;
  categories?: readonly string[] | null;
  subconditions?: readonly string[] | null;
  sources?: readonly string[] | null;
  alarmIds?: readonly string[] | null;
  tagIds?: readonly string[] | null;
  equipmentIds?: readonly string[] | null;
  active?: AlarmViewMatchState;
  acknowledged?: AlarmViewMatchState;
  shelved?: AlarmViewMatchState;
  search?: string | null;
}>;

export type AlarmViewEngineering = Readonly<{
  id?: string | null;
  key: string;
  name: string;
  filter: AlarmViewFilterEngineering;
  description?: string | null;
  metadata?: Record<string, string> | null;
  version?: number;
}>;

export const ENGINEERING_FRAGMENT_SCHEMA = 'scada.engineering.fragment' as const;
export const ENGINEERING_FRAGMENT_SCHEMA_VERSION = 1 as const;
export const ENGINEERING_FRAGMENT_EXTENSION = '.escadafrag' as const;

export type EngineeringFragmentPlanOperation =
  | 'create'
  | 'reuseIdentical'
  | 'update'
  | 'remap'
  | 'skip'
  | 'conflict'
  | 'unsupported';

export type EngineeringFragmentEntityReference = Readonly<{
  entityKind: string;
  entityKey: string;
  entityId?: string | null;
}>;

export type EngineeringFragmentManifest = Readonly<{
  roots: readonly EngineeringFragmentEntityReference[];
  dependencies?: readonly EngineeringFragmentEntityReference[] | null;
  version?: number;
}>;

export type EngineeringFragmentPreviewItem = Readonly<{
  source: EngineeringFragmentEntityReference;
  operation: EngineeringFragmentPlanOperation;
  target?: EngineeringFragmentEntityReference | null;
  reason?: string | null;
}>;

export type EngineeringFragmentEnvelope = Readonly<{
  schema: typeof ENGINEERING_FRAGMENT_SCHEMA;
  schemaVersion: typeof ENGINEERING_FRAGMENT_SCHEMA_VERSION;
  exportedAt: string;
  manifest: EngineeringFragmentManifest;
  engineering: EngineeringPackageView;
}>;

export type ReusableLibraryUpdateState =
  | 'upToDate'
  | 'updateAvailable'
  | 'locallyModified'
  | 'sourceMissing'
  | 'incompatible';

export type ReusableLibrarySourceProvenanceEngineering = Readonly<{
  sourceLibraryId: string;
  sourceResourceId: string;
  sourceVersion: string;
  sourceContentHash: string;
  version?: number;
}>;

export type ReusableLibraryResourceUpdateEngineering = Readonly<{
  source: ReusableLibrarySourceProvenanceEngineering;
  state: ReusableLibraryUpdateState;
  currentContentHash?: string | null;
}>;

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

export type TagPhysicalValueTransformEngineering = Readonly<{
  contractVersion?: number;
  byteSwap?: boolean;
  wordSwap?: boolean;
}>;

export type CommunicationTagBindingEngineering = Readonly<{
  contractVersion: number;
  schemaId: string;
  schemaVersion: number;
  portableAddress: string;
  settings?: Record<string, string> | null;
  valueTransform?: TagPhysicalValueTransformEngineering | null;
}>;

export type TagEngineering = {
  id?: string;
  name: string;
  path: string;
  dataType: string;
  dataSourceId?: string | null;
  source?: string | null;
  address?: string | null;
  communicationBinding?: CommunicationTagBindingEngineering | null;
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
  historianCaptureProfileId?: string | null;
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

export type MediaSourceProtocolEngineering = 'http' | 'hls' | 'mjpeg' | 'rtsp';

export type MediaSourceEngineering = {
  id?: string | null;
  key: string;
  name: string;
  protocol: MediaSourceProtocolEngineering;
  endpoint: string;
  enabled?: boolean;
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

export type DriverHostHealth = {
  status: string;
  service: string;
  nodeIdentity?: string | null;
  observedAtUtc: string;
  freshForSeconds: number;
  uptime: string;
  activeRuntimeAvailable: boolean;
  activeRevision?: number | null;
};

export type NetworkProbeResult = {
  status: string;
  address?: string | null;
  elapsedMilliseconds?: number | null;
  detail?: string | null;
};

export type NetworkReachabilityProbeResponse = {
  authority: string;
  observedAtUtc: string;
  host: string;
  port: number;
  tcp: NetworkProbeResult;
  icmp: NetworkProbeResult;
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
  /**
   * Runtime-only Dynamo projection. Never authored or persisted: the instance
   * compositor materializes Boolean/Number public parameters here so the shared
   * dynamic resolver can consume them without fabricating a live TAG sample.
   */
  projectedValue?: boolean | number;
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

export type VisualPropertyMapRuleEngineering = Readonly<{
  value: VisualEngineeringPropertyValue;
  minimum?: number | null;
  minimumInclusive?: boolean;
  maximum?: number | null;
  maximumInclusive?: boolean;
}>;

export type VisualPropertyMapEngineering = Readonly<{
  propertyKey: string;
  source: VisualValueSourceEngineering;
  rules: readonly VisualPropertyMapRuleEngineering[];
  fallback?: VisualEngineeringPropertyValue | null;
  version?: number;
}>;

export type VisualEngineeringAssetReference = Readonly<{
  assetId: string;
}>;

export type ApplicationBrandingMode = 'default' | 'text' | 'image' | 'none';

export type ApplicationBrandingEngineering = Readonly<{
  mode: ApplicationBrandingMode;
  text?: string | null;
  subtitle?: string | null;
  visualAssetId?: string | null;
}>;

export type RuntimePresentationEngineering = Readonly<{
  historicalPlaybackEnabled: boolean;
  mobileOrientation?: 'landscape' | 'portrait';
  mobileScreens?: Readonly<Record<string, string>> | null;
  header?: RuntimeHeaderEngineering | null;
  version: number;
}>;

export type RuntimeHeaderTextStyleEngineering = Readonly<{
  fontFamily?: string | null;
  fontSize?: number;
  fontWeight?: 'normal' | 'bold' | 400 | 500 | 600 | 700;
  color?: string | null;
}>;

export type RuntimeHeaderDateTimeEngineering = Readonly<{
  mode?: 'off' | 'time' | 'date' | 'dateTime';
  position?: 'left' | 'right';
  order?: number;
  dateFormat?: 'dd/MM/yyyy' | 'MM/dd/yyyy' | 'yyyy-MM-dd';
  timeFormat?: '24h' | '12h';
}>;

export type RuntimeHeaderEngineering = Readonly<{
  enabled?: boolean;
  height?: number;
  backgroundColor?: string | null;
  titlePosition?: 'left' | 'center' | 'right';
  controlsPosition?: 'left' | 'right';
  controlsOrder?: number;
  showScreenName?: boolean;
  titleStyle?: RuntimeHeaderTextStyleEngineering | null;
  screenNameStyle?: RuntimeHeaderTextStyleEngineering | null;
  dateTime?: RuntimeHeaderDateTimeEngineering | null;
  overviewVisible?: boolean;
  historyVisible?: boolean;
  alarmsVisible?: boolean;
  playbackVisible?: boolean;
  links?: readonly { label: string; screenKey: string; visualAssetId?: string | null }[] | null;
}>;

export type VisualAssetEngineering = {
  id?: string | null;
  key: string;
  name: string;
  originalFileName: string;
    mediaType: 'image/png' | 'image/jpeg' | 'image/bmp' | 'image/svg+xml' | string;
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
  dynamoDefinitionId?: string | null;
  equipmentPath?: string | null;
  equipmentId?: string | null;
  bindings?: BindingEngineering[] | null;
  properties?: VisualEngineeringPropertyMap | null;
  context?: Record<string, string> | null;
  children?: VisualElementEngineering[] | null;
  metadata?: Record<string, string> | null;
  propertyExpressions?: readonly VisualPropertyExpressionEngineering[] | null;
  booleanConditions?: readonly VisualBooleanConditionEngineering[] | null;
  analogFill?: VisualAnalogFillEngineering | null;
  propertyMaps?: readonly VisualPropertyMapEngineering[] | null;
  dynamoParameters?: readonly import('../runtime/visual-navigation/runtimeVisualNavigationModel').DynamoParameterValueEngineering[] | null;
  actions?: readonly import('../runtime/visual-navigation/runtimeVisualNavigationModel').VisualNavigationActionEngineering[] | null;
};

export type TemplateEngineering = {
  id?: string;
  key: string;
  name: string;
  bindings?: BindingEngineering[];
  elements?: VisualElementEngineering[] | null;
  properties?: Record<string, string> | null;
  context?: Record<string, string> | null;
  metadata?: Record<string, string> | null;
};

export type EquipmentEngineering = {
  id?: string;
  path: string;
  name: string;
  templateKey?: string | null;
  templateId?: string | null;
  bindings?: BindingEngineering[];
};

export type DynamoEngineering = {
  id?: string;
  key: string;
  name: string;
  templateKey?: string;
  templateId?: string | null;
  bindings?: BindingEngineering[];
  properties?: Record<string, string> | null;
  context?: Record<string, string> | null;
  metadata?: Record<string, string> | null;
  parameters?: readonly import('../runtime/visual-navigation/runtimeVisualNavigationModel').DynamoParameterDefinitionEngineering[] | null;
  elements?: readonly VisualElementEngineering[] | null;
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

/** Popup X/Y live in the same fixed logical HMI coordinate space as Screens. */
export type PopupEngineering = {
  id?: string;
  key: string;
  name: string;
  templateKey?: string | null;
  templateId?: string | null;
  elements?: VisualElementEngineering[] | null;
  properties?: Record<string, string> | null;
  context?: Record<string, string> | null;
  metadata?: Record<string, string> | null;
  x?: number;
  y?: number;
};

export type SecurityRoleEngineering = {
  id?: string;
  key: string;
  name: string;
  description?: string;
  grants?: Array<{ capability: string; scope?: Record<string, string> }>;
};

export type CommandEngineering = {
  id?: string | null;
  key: string;
  name: string;
  kind: string;
  value: string;
  targetTagId?: string | null;
  targetTagPath?: string | null;
  description?: string | null;
  area?: string | null;
  equipmentPath?: string | null;
  enabled?: boolean;
  metadata?: Record<string, string> | null;
};

export type EngineeringPackageView = {
  schema: string;
  schemaVersion: number;
  exportedAt: string;
  tags: TagEngineering[];
  alarms: AlarmEngineering[];
  dataSources?: DataSourceEngineering[];
  mediaSources?: MediaSourceEngineering[];
  templates?: TemplateEngineering[];
  equipment?: EquipmentEngineering[];
  dynamos?: DynamoEngineering[];
  screens?: ScreenEngineering[];
  popups?: PopupEngineering[];
  securityRoles?: SecurityRoleEngineering[];
  commands?: CommandEngineering[];
  gateways?: GatewayEngineering[];
  visualAssets?: VisualAssetEngineering[];
  historianCaptureProfiles?: HistorianCaptureProfileEngineering[];
  dataQueries?: DataQueryEngineering[];
  alarmViews?: AlarmViewEngineering[];
  reports?: readonly import('./reports/reportContracts').ReportEngineeringDto[] | null;
  branding?: ApplicationBrandingEngineering | null;
  runtimePresentation?: RuntimePresentationEngineering | null;
  startupScreenId?: string | null;
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
