export type ProjectPortabilityMergeMode = 'CreateOnly' | 'UpdateExisting' | 'CreateAndUpdate';

export type ProjectPortabilityWorkspace = {
  projectKey?: string | null;
  projectName?: string | null;
  baseRevision?: number | null;
  isDirty: boolean;
  changeVersion: number;
};

export type CanonicalEngineeringIdentity = {
  schema: string;
  schemaVersion: number;
  exportedAt?: string | null;
};

export type ProjectPortabilityContext = {
  workspace: ProjectPortabilityWorkspace;
  canonical: CanonicalEngineeringIdentity;
};

export type ProjectPortabilityIssue = {
  code: string;
  message: string;
  entityKind: string;
  entityKey: string;
  isError: boolean;
};

export type ProjectPortabilityPreview = {
  mode: string;
  createCount: number;
  updateCount: number;
  skipCount: number;
  errorCount: number;
  items: Array<{
    entityKind: string;
    entityKey: string;
    operation: string;
    issues: ProjectPortabilityIssue[];
  }>;
  canApply: boolean;
};

export type ProjectPortabilityApplyResult = {
  mode: string;
  created: number;
  updated: number;
  skipped: number;
  issues: ProjectPortabilityIssue[];
};

export type ProjectPackageManifestFile = {
  path: string;
  mediaType: string;
  length: number;
  sha256: string;
};

export type ProjectPackageManifest = {
  format: string;
  formatVersion: number;
  packageId: string;
  createdAtUtc: string;
  product: string;
  projectKey: string;
  projectName: string;
  engineeringSchema: string;
  engineeringSchemaVersion: number;
  files: ProjectPackageManifestFile[];
};

export type ProjectPackageEngineeringSummary = {
  schema: string;
  schemaVersion: number;
  tags: number;
  alarms: number;
  dataSources: number;
  templates: number;
  equipment: number;
  dynamos: number;
  screens: number;
  popups: number;
  securityRoles: number;
  commands: number;
};

export type ProjectPackageInspection = {
  manifest: ProjectPackageManifest;
  engineering: ProjectPackageEngineeringSummary;
};

export type PortabilityPreviewToken = {
  sourceFingerprint: string;
  mode: ProjectPortabilityMergeMode;
  expectedChangeVersion: number;
  preview: ProjectPortabilityPreview;
};

export type ProjectPortabilityDownload = {
  blob: Blob;
  filename: string;
};
